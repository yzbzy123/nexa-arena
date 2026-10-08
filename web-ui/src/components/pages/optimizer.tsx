import { useEffect, useRef, useState } from "react"
import { Check, ChevronDown, Download, ExternalLink, FolderSearch, Play, RefreshCcw, RotateCcw, Square, Upload } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Checkbox } from "@/components/ui/checkbox"
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog"
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible"
import { Separator } from "@/components/ui/separator"
import { invoke, isDesktop } from "@/lib/bridge"
import type { Game } from "@/lib/types"
import type { ActionRunner } from "./shared"
import { Choice, NumberField, PageHeading } from "./shared"
import { ManagementWorkbench } from "./management"
import { ProcessProfiles } from "./process-profiles"
import { GuidedTuning } from "./guided-tuning"
import { PreferenceSaveQueue } from "@/lib/preference-save-queue"

type Option = { id: string; title: string; category: string; description: string; scope: string;
  restart: boolean; effect: string; risk: string; source: string; currentValue: string | null; desiredValue: string | null;
  supported: boolean; optimized: boolean; applied: boolean; state: string; error?: string }
type Outcome = { id: string; ok: boolean; message: string }
type ProcessStatus = { running: boolean; executable: string; pid: number; priority: string | null; canApply: boolean; canRestore: boolean }
type MemoryState = {
  memory: { totalMb: number; availableMb: number; freeMb: number; standbyMb: number; supported: boolean;
    error?: string; minimumTimerMs: number; maximumTimerMs: number; currentTimerMs: number };
  automatic: boolean; onlyGame: boolean; listThreshold: number; freeThreshold: number;
  timerRequestedMs: number; timerTargetMs: number; cleanCount: number; lastMessage: string
}
type Snapshot = { process: ProcessStatus; path: string | null; pathSource?:string|null;pathMessage?:string|null;candidates?:string[];options: Option[]; memory: MemoryState }

export function OptimizerPage({ busy, preview, game, onGameChange, run }: { busy: string | null; preview: boolean; game:Game;onGameChange:(game:Game)=>void;run: ActionRunner }) {
  const setGame=onGameChange
  const [snapshot,setSnapshot] = useState<Snapshot | null>(null)
  const [error,setError] = useState<string | null>(null)
  const [selected,setSelected] = useState<string[]>([])
  const [category,setCategory] = useState("全部")
  const [query,setQuery] = useState("")
  const [status,setStatus] = useState("全部")
  const [confirmation,setConfirmation] = useState<"apply" | "restore" | null>(null)
  const [outcomes,setOutcomes] = useState<Outcome[]>([])
  const [tab,setTab]=useState("tuning")
  const snapshotRequest=useRef(0),currentGame=useRef(game)
  currentGame.current=game
  const locked = preview || !!busy
  useEffect(() => {
    if(!isDesktop)return
    let alive=true;const request=++snapshotRequest.current
    invoke<Snapshot>("optimizer.inspect",{game}).then(value=>{if(alive&&request===snapshotRequest.current&&currentGame.current===game){setSnapshot(value);setError(null)}}).catch(cause=>{if(alive&&request===snapshotRequest.current&&currentGame.current===game)setError(cause.message)})
    return()=>{alive=false}
  },[game])
  async function refresh() {
    const request=++snapshotRequest.current
    try { const value=await invoke<Snapshot>("optimizer.inspect",{game,refresh:true});if(request===snapshotRequest.current&&currentGame.current===game){setSnapshot(value);setError(null)} }
    catch(cause){if(request===snapshotRequest.current&&currentGame.current===game)setError(cause instanceof Error?cause.message:"读取失败")}
  }
  function toggle(id:string){setSelected(old=>old.includes(id)?old.filter(x=>x!==id):[...old,id])}
  async function change(restore:boolean) {
    setConfirmation(null)
    const result=await run<{outcomes:Outcome[];catalog:Option[]}>("optimizer.apply",{game,ids:selected,restore})
    if(result){setOutcomes(result.outcomes);await refresh();const failed=result.outcomes.filter(x=>!x.ok).length
      if(failed)toast.error(`${failed} 项未完成，请查看逐项结果`);else toast.success(restore?"已还原所选原值":"所选优化已处理")}
  }
  const options=snapshot?.options ?? []
  const shown=options.filter(x=>(category==="全部"||x.category===category)
    &&(status==="全部"||(status==="可配置"&&x.supported&&!x.optimized)||(status==="已保存原值"&&x.applied)||(status==="暂不可用"&&!x.supported))
    &&`${x.title} ${x.description} ${x.category}`.toLocaleLowerCase().includes(query.trim().toLocaleLowerCase()))
  const categories=["全部",...new Set(options.map(x=>x.category))]
  const selectedOptions=options.filter(x=>selected.includes(x.id))
  return <>
    <PageHeading title="游戏优化">按类别选择系统优化项，应用前保存原值；待机内存和定时器功能可单独控制。</PageHeading>
    <Card className="panel-card"><CardHeader className={tab==="tuning"?undefined:"sr-only"}><CardTitle>目标游戏</CardTitle>
      <CardDescription>游戏专属项目只作用于所选 EXE；全局项目会明确标注。原值保存在本机，重启工具后仍可还原。</CardDescription></CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-wrap items-end gap-3"><div className="min-w-48 flex-1"><Choice label="游戏" value={game} disabled={!!busy} onChange={value=>{setGame(value as Game);setSnapshot(null);setSelected([]);setOutcomes([]);if(isDesktop&&!preview)void run("optimizer.select",{game:value})}}
          options={[{value:"CS2",label:"COUNTER-STRIKE 2"},{value:"VALORANT",label:"VALORANT"}]} /></div>
          <Button variant="outline" disabled={locked} onClick={async()=>{const value=await run<Snapshot>("optimizer.pick",{game});if(value)setSnapshot(value)}}><FolderSearch data-icon="inline-start" />选择主程序</Button>
          <Button variant="outline" disabled={!isDesktop||!!busy} onClick={refresh}><RefreshCcw data-icon="inline-start" />刷新状态</Button></div>
        <p className="section-caption break-all">{snapshot?.path?<>{snapshot.pathSource} · {snapshot.path}</>:snapshot?.pathMessage??"正在核对保存路径和游戏安装记录…"}</p>
        {snapshot?.path&&snapshot.pathMessage&&<p className="section-caption">{snapshot.pathMessage}</p>}
        {!snapshot?.path&&(snapshot?.candidates?.length??0)>1&&<div className="flex flex-col gap-2">{snapshot?.candidates?.map(path=><Button key={path} variant="outline" disabled={locked} className="h-auto justify-start whitespace-normal break-all text-left" onClick={async()=>{const value=await run<Snapshot>("optimizer.usePath",{game,path});if(value)setSnapshot(value)}}>使用此安装：{path}</Button>)}</div>}
        {error && <p role="alert" className="text-destructive text-sm">{error}</p>}
      </CardContent>
    </Card>
    <Tabs value={tab} onValueChange={setTab}>
      <TabsList variant="line" className="w-full justify-start border-b"><TabsTrigger value="tuning">系统调优</TabsTrigger><TabsTrigger value="memory">内存与待机</TabsTrigger><TabsTrigger value="tools">系统管理</TabsTrigger></TabsList>
      <TabsContent value="tuning" className="pt-3">
        <GuidedTuning options={options} onSelect={setSelected}/>
        <div className="optimizer-filters">
          <FieldGroup className="flex-row flex-wrap items-end gap-3">
            <Field className="min-w-48 flex-1"><FieldLabel htmlFor="optimization-search">搜索优化项</FieldLabel><Input id="optimization-search" value={query} onChange={event=>setQuery(event.target.value)} placeholder="搜索服务、鼠标、后台或电源…" /></Field>
            <Field className="min-w-36 flex-1"><Choice label="类别" value={category} onChange={setCategory} options={categories.map(value=>({value,label:value=== "全部"?`全部类别 · ${options.length}`:`${value} · ${options.filter(x=>x.category===value).length}`}))} /></Field>
            <Field className="min-w-36 flex-1"><Choice label="状态" value={status} onChange={setStatus} options={["全部","可配置","已保存原值","暂不可用"].map(value=>({value,label:value}))} /></Field>
          </FieldGroup>
          <div className="flex flex-wrap items-center justify-between gap-3"><span className="section-caption" role="status">显示 {shown.length} / {options.length} 项 · 已保存原值 {options.filter(x=>x.applied).length} 项</span>
            <div className="form-actions"><Button variant="outline" disabled={!!busy} onClick={()=>setSelected(options.filter(x=>x.supported&&["game-mode","gamebar-controller","sticky-shortcut"].includes(x.id)).map(x=>x.id))}>选择基础项目</Button>
              <Button variant="outline" disabled={!!busy} onClick={()=>setSelected([])}>清空选择</Button></div></div>
        </div>
        <div className="tuning-list">
          {shown.map(option=>{const outcome=outcomes.find(x=>x.id===option.id);return <div className="tuning-row" key={option.id}>
            <Checkbox id={`tuning-${option.id}`} aria-describedby={`tuning-description-${option.id}`} checked={selected.includes(option.id)} disabled={(!option.supported&&!option.applied)||!!busy} onCheckedChange={()=>toggle(option.id)} />
            <div className="min-w-0"><label htmlFor={`tuning-${option.id}`}><strong>{option.title}</strong></label><p id={`tuning-description-${option.id}`}>{option.description}</p>
              <span className="tuning-scope">{option.scope} · {option.effect}</span>
              {option.currentValue!==null && <p className="tuning-values">当前：{option.currentValue || "空值"} → 目标：{option.desiredValue || "空值"}</p>}
              {option.error && <p className="tuning-error">{option.error}</p>}
              {outcome && <p className={outcome.ok?"tuning-result":"tuning-error"}>{outcome.message}</p>}
            </div><div className="flex flex-col items-end gap-2"><Badge variant={option.applied?"default":"outline"}>{option.applied?"已保存原值":option.state}</Badge>
              {option.risk!=="常规" && <Badge variant="secondary">{option.risk}</Badge>}</div>
          </div>})}
          {snapshot && shown.length===0 && <p className="p-5 text-muted-foreground">没有符合筛选条件的优化项。试试其他关键词或“全部”类别与状态。</p>}
          {!snapshot && !error && <p role="status" className="p-5 text-muted-foreground">正在读取系统设置…</p>}
        </div>
        <div className="tuning-toolbar"><span className="section-caption">已选 {selected.length} 项 · 按原值还原，不套用统一默认值</span>
          <div className="form-actions"><Button variant="outline" disabled={locked||!selected.some(id=>options.find(x=>x.id===id)?.applied)} onClick={()=>setConfirmation("restore")}><RotateCcw data-icon="inline-start" />还原选中</Button>
            <Button disabled={!isDesktop||!!busy||selected.length===0||!selectedOptions.some(x=>x.supported)} onClick={()=>setConfirmation("apply")}><Check data-icon="inline-start" />应用选中项</Button></div></div>
        <div className="flex flex-wrap items-center justify-between gap-3 py-4"><p className="section-caption">方案仅保存所选项目，不包含原值或游戏路径；导入后需要再次确认，不自动应用。</p><div className="form-actions">
          <Button variant="outline" disabled={locked||selected.length===0} onClick={async()=>{const result=await run<{saved:boolean}>("optimizer.profileExport",{game,ids:selected});if(result?.saved)toast.success("所选方案已导出")}}><Download data-icon="inline-start" />导出方案</Button>
          <Button variant="outline" disabled={locked} onClick={async()=>{const result=await run<{loaded:boolean;game:Game;ids:string[]}>("optimizer.profileImport");if(result?.loaded){setGame(result.game);setSelected(result.ids);setOutcomes([]);toast.success("方案已载入，请检查所选项目后再应用")}}}><Upload data-icon="inline-start" />导入方案</Button></div></div>
        <Card className="panel-card mt-5"><CardHeader><CardTitle>当前游戏进程</CardTitle>
          <CardDescription>{snapshot?.process.running?`PID ${snapshot.process.pid} · 当前优先级 ${snapshot.process.priority}`:"启动所选游戏后可调整当前进程的调度优先级。"}</CardDescription></CardHeader>
          <CardContent className="form-actions"><Button disabled={locked||!snapshot?.process.canApply} onClick={async()=>{const result=await run("optimizer.priority",{game,enabled:true});if(result)refresh()}}>应用高于普通</Button>
            <Button variant="outline" disabled={locked||!snapshot?.process.canRestore} onClick={async()=>{const result=await run("optimizer.priority",{game,enabled:false});if(result)refresh()}}>还原原优先级</Button></CardContent></Card>
        <ProcessProfiles game={game} busy={busy} preview={preview} run={run} />
      </TabsContent>
      <TabsContent value="memory" className="pt-3">{snapshot && <MemoryPanel initial={snapshot.memory} busy={busy} preview={preview} run={run} />}</TabsContent>
      <TabsContent value="tools" className="pt-3">
        <div className="system-tools-stack">
        <ManagementWorkbench busy={busy} preview={preview} run={run} />
        <Collapsible className="system-shortcuts" defaultOpen={false} data-system-shortcuts>
          <Separator />
          <div className="system-shortcuts-heading"><CollapsibleTrigger asChild><Button variant="ghost" className="system-shortcuts-toggle"><span>Windows 管理入口</span><ChevronDown data-icon="inline-end" /></Button></CollapsibleTrigger><p className="section-caption">图形、启动项、服务和网络的系统快捷入口</p></div>
          <CollapsibleContent>
            <p className="section-caption">查看驱动、启动项目、服务和网络配置；进入系统页面后由你选择具体改动。</p>
            <div className="system-shortcuts-grid">
            {[["graphics","游戏图形首选项","按 EXE 设置 GPU、窗口化优化"],["graphics-default","默认图形设置","查看硬件加速 GPU 计划等硬件相关开关"],["startup","启动应用","管理登录后自动运行的程序"],["services","系统服务","查看服务状态和依赖"],["network","网络状态","检查网络连接及适配器设置"]].map(([page,title,description])=><div className="system-shortcut-entry" key={page}><div><div className="list-row-title">{title}</div><span className="list-row-meta">{description}</span></div><Button variant="outline" size="sm" disabled={locked} aria-label={`打开${title}`} onClick={()=>run("optimizer.settings",{page})}><ExternalLink data-icon="inline-start" />打开</Button></div>)}
            </div>
          </CollapsibleContent>
        </Collapsible>
        </div>
      </TabsContent>
    </Tabs>
    <AlertDialog open={confirmation!==null} onOpenChange={open=>{if(!open)setConfirmation(null)}}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>{confirmation==="restore"?"还原所选项目的原值":"检查所选项目及影响"}</AlertDialogTitle>
      <AlertDialogDescription>{confirmation==="restore"?"按本机保存的原值还原；若设置已被其他程序改动，会保留备份并提示冲突。":"全局项目影响整个 Windows；服务可能影响搜索等功能，电源设置可能增加功耗。每项单独备份、写入及校验；失败项尝试回滚，成功项保留。"}</AlertDialogDescription></AlertDialogHeader>
      <div className="flex max-h-80 flex-col gap-3 overflow-auto text-sm">{selectedOptions.map(x=><div key={x.id} className="flex flex-col gap-1"><strong>{x.title} · {x.scope}</strong><span className="text-muted-foreground">{x.description}</span><span className="text-muted-foreground">{x.effect}</span></div>)}</div>
      <AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction disabled={locked} onClick={()=>change(confirmation==="restore")}>{confirmation==="restore"?"确认还原原值":"保存原值并应用"}</AlertDialogAction></AlertDialogFooter>
    </AlertDialogContent></AlertDialog>
  </>
}

function MemoryPanel({initial,busy,preview,run}:{initial:MemoryState;busy:string|null;preview:boolean;run:ActionRunner}) {
  const [state,setState]=useState(initial)
  const [listMb,setListMb]=useState(initial.listThreshold)
  const [freeMb,setFreeMb]=useState(initial.freeThreshold)
  const [onlyGame,setOnlyGame]=useState(initial.onlyGame)
  const [timerMs,setTimerMs]=useState(initial.timerTargetMs||1)
  const [loading,setLoading]=useState(isDesktop)
  const [saveStatus,setSaveStatus]=useState("参数修改后自动保存；不启动监控或定时器请求。")
  const [saveError,setSaveError]=useState(false)
  const alive=useRef(true),edited=useRef(false)
  type Draft={listMb:number;freeMb:number;onlyGame:boolean;milliseconds:number}
  const draft=useRef<Draft>({listMb:initial.listThreshold,freeMb:initial.freeThreshold,onlyGame:initial.onlyGame,milliseconds:initial.timerTargetMs||1})
  const queue=useRef<PreferenceSaveQueue<Draft,MemoryState>|null>(null)
  if(!queue.current)queue.current=new PreferenceSaveQueue(
    value=>invoke<MemoryState>("memory.save",value),
    (value,result,latest)=>{if(!alive.current)return;setState(result);if(latest&&JSON.stringify(value)===JSON.stringify(draft.current)){const confirmed=result.listThreshold===value.listMb&&result.freeThreshold===value.freeMb&&result.onlyGame===value.onlyGame&&result.timerTargetMs===value.milliseconds;setSaveError(!confirmed);setSaveStatus(confirmed?"参数已保存到本机；监控和定时器请求不会随保存自动开启。":"保存返回值不一致，请重新保存并核对。")}},
    (value,error,latest)=>{if(alive.current&&latest&&JSON.stringify(value)===JSON.stringify(draft.current)){setSaveError(true);setSaveStatus("参数未保存："+(error instanceof Error?error.message:String(error))+"；可点击保存参数重试。")}},
  )
  const disabled=preview||!!busy||loading
  function valid(value:Draft){const m=state.memory;return Number.isInteger(value.listMb)&&Number.isInteger(value.freeMb)&&value.listMb>=128&&value.freeMb>=128&&value.listMb<=m.totalMb&&value.freeMb<=m.totalMb&&Number.isFinite(value.milliseconds)&&value.milliseconds>0&&(!m.maximumTimerMs||(value.milliseconds>=m.minimumTimerMs&&value.milliseconds<=m.maximumTimerMs))}
  function save(value:Draft){if(preview||!isDesktop){setSaveStatus("预览模式不会保存参数。");return}if(!valid(value)){setSaveError(true);setSaveStatus("输入尚未保存：请填写有效的内存阈值和定时器范围。");return}setSaveError(false);setSaveStatus("正在保存参数…");queue.current!.enqueue({...value})}
  function edit(value:Partial<Draft>){edited.current=true;draft.current={...draft.current,...value};setListMb(draft.current.listMb);setFreeMb(draft.current.freeMb);setOnlyGame(draft.current.onlyGame);setTimerMs(draft.current.milliseconds);save(draft.current)}
  useEffect(()=>{
    alive.current=true
    const update=()=>invoke<MemoryState>("memory.status").then(value=>{if(!alive.current)return;setState(value);if(!edited.current){draft.current={listMb:value.listThreshold,freeMb:value.freeThreshold,onlyGame:value.onlyGame,milliseconds:value.timerTargetMs||1};setListMb(value.listThreshold);setFreeMb(value.freeThreshold);setOnlyGame(value.onlyGame);setTimerMs(value.timerTargetMs||1)}setLoading(false)}).catch(error=>{if(alive.current){setLoading(false);setSaveError(true);setSaveStatus("参数读取失败，请重新进入此页："+(error instanceof Error?error.message:String(error)))}})
    if(!isDesktop){setLoading(false);return()=>{alive.current=false}}
    void update()
    const interval=window.setInterval(update,2000)
    return()=>{alive.current=false;window.clearInterval(interval)}
  },[])
  async function act(action:string,data?:Record<string,unknown>){const result=await run<MemoryState>(action,data);if(result)setState(result)}
  const m=state.memory
  return <div className="stack">
    <Card className="panel-card"><CardHeader><CardTitle>待机内存管理</CardTitle><CardDescription>待机缓存由 Windows 自动管理；可按两个阈值同时满足的条件触发清理。</CardDescription></CardHeader>
      <CardContent className="flex flex-col gap-5">
        <div className="memory-readouts"><div><span>总内存</span><strong>{(m.totalMb/1024).toFixed(1)} GB</strong></div><div><span>可用（含待机）</span><strong>{m.availableMb.toLocaleString()} MB</strong></div><div><span>待机列表</span><strong>{m.supported?m.standbyMb.toLocaleString():"—"} MB</strong></div><div><span>空闲（不含待机）</span><strong>{m.supported?m.freeMb.toLocaleString():"—"} MB</strong></div></div>
        {m.error && <p className="text-destructive text-sm" role="alert">{m.error}</p>}
        <div className="field-columns"><NumberField label="待机列表至少（MB）" value={listMb} onChange={value=>edit({listMb:value})} min={128} max={m.totalMb} disabled={state.automatic||loading||!!busy} />
          <NumberField label="且空闲内存低于（MB）" value={freeMb} onChange={value=>edit({freeMb:value})} min={128} max={m.totalMb} disabled={state.automatic||loading||!!busy} /></div>
        <label className="flex items-center gap-2 text-sm"><Checkbox checked={onlyGame} onCheckedChange={value=>edit({onlyGame:value===true})} disabled={state.automatic||loading||!!busy} />仅在 CS2 / VALORANT 运行时自动清理</label>
        <p role="status" className={saveError?"text-destructive text-sm":"section-caption"}>{loading?"正在读取本机已保存参数…":saveStatus}</p>
        {state.automatic && <p className="section-caption">停止监控后可修改阈值与游戏条件。</p>}
        <div className="form-actions"><Button variant="outline" disabled={disabled||!valid(draft.current)} onClick={()=>save(draft.current)}>保存参数</Button><Button variant="outline" disabled={disabled||!m.supported} onClick={()=>act("memory.purge")}>手动清理待机列表</Button>
          <Button disabled={disabled||(!state.automatic&&(!valid(draft.current)||saveStatus==="正在保存参数…"))||(!m.supported&&!state.automatic)} onClick={()=>act("memory.configure",{enabled:!state.automatic,listMb:state.automatic?state.listThreshold:listMb,freeMb:state.automatic?state.freeThreshold:freeMb,onlyGame:state.automatic?state.onlyGame:onlyGame})}>
            {state.automatic?<Square data-icon="inline-start" />:<Play data-icon="inline-start" />}{state.automatic?"停止监控":"开始监控"}</Button></div>
        <p className="section-caption" role="status">{state.lastMessage} · 本次清理 {state.cleanCount} 次。监控每 5 秒检查，两次清理至少间隔 30 秒。</p>
      </CardContent></Card>
    <Card className="panel-card"><CardHeader><CardTitle>定时器精度请求</CardTitle><CardDescription>独立可选。Windows 11 的请求效果受系统和进程策略影响；关闭工具会释放请求。</CardDescription></CardHeader>
      <CardContent className="flex flex-col gap-4"><div className="memory-readouts"><div><span>系统报告当前精度</span><strong>{m.currentTimerMs.toFixed(3)} ms</strong></div><div><span>系统支持最小值</span><strong>{m.minimumTimerMs.toFixed(3)} ms</strong></div><div><span>工具请求值</span><strong>{state.timerRequestedMs?state.timerRequestedMs.toFixed(3)+" ms":"未请求"}</strong></div></div>
        <div className="flex flex-wrap items-end gap-3"><div className="min-w-32 flex-1"><NumberField label="请求精度（ms）" value={timerMs} onChange={value=>edit({milliseconds:value})} min={m.minimumTimerMs} max={m.maximumTimerMs||15.625} step={.1} disabled={loading||!!busy} /></div>
          <Button disabled={disabled||!m.maximumTimerMs||!valid(draft.current)||saveStatus==="正在保存参数…"} onClick={()=>act("memory.timer",{enabled:true,milliseconds:timerMs})}>应用请求</Button>
          <Button variant="outline" disabled={disabled||!state.timerRequestedMs} onClick={()=>act("memory.timer",{enabled:false,milliseconds:timerMs})}>释放请求</Button></div>
      </CardContent></Card>
  </div>
}
