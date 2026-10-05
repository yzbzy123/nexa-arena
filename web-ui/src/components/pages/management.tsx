import { useEffect, useRef, useState } from "react"
import { Check, RefreshCcw, RotateCcw } from "lucide-react"
import { toast } from "sonner"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Checkbox } from "@/components/ui/checkbox"
import { invoke, isDesktop } from "@/lib/bridge"
import type { ActionRunner } from "./shared"
import { Choice } from "./shared"
import { AppxPanel } from "./appx"
import { ReadinessPanel } from "./readiness"
import { RepairPanel } from "./repair"

type Item={id:string;title:string;detail:string;current:string;currentChoice:string;effect:string;blocked:string|null;supported:boolean;applied:boolean;choices:{value:string;label:string}[]}
type Catalog={items:Item[]}
const modules=[{value:"startup",label:"登录启动项"},{value:"tasks",label:"计划任务"},{value:"services",label:"服务管理"},{value:"network",label:"网卡 DNS"},{value:"network-advanced",label:"网卡高级属性"},{value:"power",label:"电源会话还原"},{value:"nvidia-global",label:"NVIDIA 全局设置"},{value:"nvidia-CS2",label:"NVIDIA · CS2"},{value:"nvidia-VALORANT",label:"NVIDIA · VALORANT"},{value:"devices",label:"设备音频控制"},{value:"device-msi",label:"MSI 配置（实验性）"},{value:"interrupt-affinity",label:"中断路由（实验性）"},{value:"ifeo",label:"游戏 IFEO 重定向"},{value:"apps",label:"APPX / MSIX 应用"},{value:"readiness",label:"负载与就绪检查"},{value:"repair",label:"Windows 检查与修复"}]
const descriptions:Record<string,string>={startup:"管理当前用户及所有用户的 32/64 位 Run 登录启动项。移出前保存原始命令和值类型；不结束当前程序，不处理安装器 RunOnce。",tasks:"只改变将来的任务触发，不运行、删除或强停任务。Windows 关键任务、安全与反作弊相关任务受保护；仅修改你认识的可选任务。",services:"显示服务真实启动和运行状态。只修改明确允许的可选服务；音频、网络、安全、更新及未知依赖只读，不强停当前服务。",network:"仅调整所选网卡的 IPv4 DNS，不改 IP、网关、IPv6 或 Wi-Fi。公共 DNS 运营方会处理解析请求，不保证降低游戏延迟。","network-advanced":"使用驱动实际提供的合法枚举值；中断节流、节能、卸载等可能改善或降低表现。请求不自动重启适配器，更改后需手动重连或重启核对。",power:"电源会话原值独立保存；仅在现值仍匹配本次写入值时还原。外部手动变更不会被覆盖。",devices:"仅控制已识别的 NVIDIA 音频控制器/子设备，不禁用 GPU、监视器、输入或存储设备。更改会影响 HDMI/DP 音频和默认音频路由。","device-msi":"实验性：仅管理驱动已有的 PCI 网卡/显卡 MSI 参数，不创建未知中断键。配置值不代表运行时验证；重启后可能发生设备或显示异常。","interrupt-affinity":"实验性：仅管理已有策略键的 PCI 显卡、网卡或 USB 控制器；USB 控制器设置影响所有关联外设，不是只改鼠标。微软建议优先保留默认策略。",ifeo:"检查 CS2/VALORANT 的 IFEO 调试器重定向，仅移除已有 Debugger 值并保存原值。依赖调试器的环境勿修改，不设置任意调试器或注入游戏。"}

export function ManagementWorkbench({busy,preview,run}:{busy:string|null;preview:boolean;run:ActionRunner}) {
  const [module,setModule]=useState("startup")
  const [items,setItems]=useState<Item[]>([])
  const [query,setQuery]=useState("")
  const [error,setError]=useState<string|null>(null)
  const [loading,setLoading]=useState(false)
  const [selected,setSelected]=useState<Item|null>(null)
  const [choice,setChoice]=useState("")
  const [confirmation,setConfirmation]=useState<"apply"|"restore"|null>(null)
  const [riskAcknowledged,setRiskAcknowledged]=useState(false)
  const [host,setHost]=useState("223.5.5.5")
  const [test,setTest]=useState<{sent:number;received:number;lost:number;averageMs:number;minMs:number;maxMs:number;jitterMs:number;note:string}|null>(null)
  const catalogRequest=useRef(0)
  const locked=preview||!!busy
  useEffect(()=>{
    let active=true;const request=++catalogRequest.current;setLoading(true);setItems([]);setSelected(null);setQuery("");setError(null)
    if(module==="apps"||module==="readiness"||module==="repair"){setLoading(false);return}
    if(!isDesktop){setLoading(false);return}
    invoke<Catalog>("manager.inspect",{module}).then(value=>{if(active&&request===catalogRequest.current)setItems(value.items)}).catch(cause=>{if(active&&request===catalogRequest.current)setError(cause.message)}).finally(()=>{if(active&&request===catalogRequest.current)setLoading(false)})
    return()=>{active=false;if(request===catalogRequest.current)catalogRequest.current++}
  },[module])
  async function refresh(){const request=++catalogRequest.current;setLoading(true);try{const value=await invoke<Catalog>("manager.inspect",{module});if(request!==catalogRequest.current)return;setItems(value.items);setError(null);setSelected(current=>current?value.items.find(x=>x.id===current.id)??null:null)}catch(cause){if(request===catalogRequest.current)setError(cause instanceof Error?cause.message:"读取失败")}finally{if(request===catalogRequest.current)setLoading(false)}}
  const shown=items.filter(x=>`${x.title} ${x.detail}`.toLocaleLowerCase().includes(query.trim().toLocaleLowerCase()))
  function pick(item:Item){setSelected(item);setChoice(item.currentChoice||"")}
  const experimental=module==="device-msi"||module==="interrupt-affinity"
  async function change(restore:boolean){setConfirmation(null);if(!selected)return;const result=await run<{ok:boolean;message:string}>("manager.change",{module,target:selected.id,choice,restore,riskAcknowledged});if(result?.ok){toast.success(result.message);await refresh()}}
  return <div className="stack" data-management-module={module}>
    <FieldGroup className="flex-row flex-wrap items-end gap-3"><Field className="min-w-48 flex-1"><Choice label="管理模块" value={module} onChange={setModule} options={modules}/></Field>
      {module!=="apps"&&module!=="readiness"&&module!=="repair"&&<><Field className="min-w-48 flex-1"><FieldLabel htmlFor="manager-search">搜索项目</FieldLabel><Input id="manager-search" value={query} onChange={event=>setQuery(event.target.value)} placeholder="输入名称、任务路径或网卡参数…" /></Field>
      <Button variant="outline" disabled={!isDesktop||loading||!!busy} onClick={refresh}><RefreshCcw data-icon="inline-start"/>刷新列表</Button></>}</FieldGroup>
    {module==="apps"?<AppxPanel busy={busy} preview={preview} run={run}/>:module==="readiness"?<ReadinessPanel busy={busy} run={run}/>:module==="repair"?<RepairPanel busy={busy} preview={preview} run={run}/>:<>
    <Alert><AlertTitle>只修改明确选中的项目</AlertTitle><AlertDescription>{module.startsWith("nvidia")?"通过 NVIDIA 官方 NVAPI 读写驱动配置。全局项影响全部程序和电池功耗；游戏项需先选择主程序，仅允许已确认属于该游戏的关联程序，未知或跨游戏关联只读。不改变分辨率或禁用设备。":descriptions[module]}</AlertDescription></Alert>
    {error&&<Alert variant="destructive"><AlertTitle>列表读取失败</AlertTitle><AlertDescription>{error} 请刷新或切换其他模块。</AlertDescription></Alert>}
    <div className="management-layout">
      <div><p className="section-caption mb-3" role="status">{loading?"正在读取…":`显示 ${shown.length} / ${items.length} 项 · 原值备份 ${items.filter(x=>x.applied).length} 项`}</p>
        <div className="manager-list">{shown.map(item=><button type="button" key={item.id} className="manager-list-row" aria-pressed={selected?.id===item.id} onClick={()=>pick(item)}><span><strong>{item.title}</strong><span className="list-row-meta">{item.detail}</span></span><Badge variant={item.applied?"default":"outline"}>{item.applied?"已保存原值":item.supported?"可管理":"只读"}</Badge></button>)}
          {!loading&&shown.length===0&&<p className="p-4 text-muted-foreground">{items.length?"没有匹配的项目。":"未发现项目，或该模块在此系统不可用。"}</p>}</div>
      </div>
      <Card className="panel-card manager-inspector"><CardHeader><CardTitle>{selected?.title??"选择一个项目"}</CardTitle><CardDescription>{selected?.detail??"在左侧选择后查看当前状态、影响和可用操作。不会自动应用。"}</CardDescription></CardHeader>
        <CardContent className="flex flex-col gap-4">{selected&&<><p className="text-sm">当前：{selected.current}</p><p className="section-caption">{selected.effect}</p>{selected.blocked&&<p className="text-destructive text-sm">{selected.blocked}</p>}
          {selected.supported&&selected.choices.length>0&&<Choice label="目标操作 / 值" value={choice} onChange={setChoice} options={selected.choices}/>}
          <div className="flex flex-wrap gap-2"><Button disabled={locked||loading||!selected.supported||!choice} onClick={()=>{setRiskAcknowledged(false);setConfirmation("apply")}}><Check data-icon="inline-start"/>检查并应用</Button><Button variant="outline" disabled={locked||loading||!selected.applied||!selected.supported} onClick={()=>{setRiskAcknowledged(false);setConfirmation("restore")}}><RotateCcw data-icon="inline-start"/>还原原值</Button></div>
          <p className="section-caption">原值备份受当前 Windows 用户保护。若现值或目标身份被外部修改，会保留备份并拒绝覆盖。</p></>}</CardContent></Card>
    </div>
    {(module==="network"||module==="network-advanced")&&<Card className="panel-card"><CardHeader><CardTitle>网络往返延迟检查</CardTitle><CardDescription>发送 8 次 ICMP 请求，显示丢包和往返抖动；不是游戏 UDP 测速，不自动切换 DNS。</CardDescription></CardHeader><CardContent className="flex flex-col gap-4">
      <FieldGroup className="flex-row flex-wrap items-end gap-3"><Field className="min-w-48 flex-1"><FieldLabel htmlFor="network-test-host">目标 IP 或主机名</FieldLabel><Input id="network-test-host" value={host} onChange={event=>setHost(event.target.value)}/></Field><Button disabled={locked||!host.trim()} onClick={async()=>{const value=await run<typeof test>("network.test",{host:host.trim()});if(value)setTest(value)}}>开始检查</Button></FieldGroup>
      {test&&<p role="status" className="text-sm">收到 {test.received}/{test.sent} · 丢包 {test.lost} · 平均 {test.averageMs.toFixed(1)} ms · 最小/最大 {test.minMs}/{test.maxMs} ms · 相邻往返抖动 {test.jitterMs.toFixed(1)} ms</p>}</CardContent></Card>}
    <AlertDialog open={confirmation!==null} onOpenChange={open=>{if(!open)setConfirmation(null)}}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>{confirmation==="restore"?"还原此项目的原值":"确认修改此项目"}</AlertDialogTitle><AlertDialogDescription>{selected?.title} · {selected?.effect}。{confirmation==="restore"?"只还原本机保存的原值，外部改动不会被覆盖。":"先保存原值，再修改并读回校验；失败时尝试回滚。此操作可能影响对应程序或网络功能。"}</AlertDialogDescription></AlertDialogHeader>
      <div className="flex flex-col gap-2 text-sm"><p>当前：{selected?.current}</p><p>目标：{confirmation==="restore"?"还原本机保存的原值":selected?.choices.find(x=>x.value===choice)?.label}</p><p className="break-all">{selected?.detail}</p></div>
      {experimental&&<label className="flex items-start gap-2 text-sm"><Checkbox checked={riskAcknowledged} onCheckedChange={value=>setRiskAcknowledged(value===true)}/>我理解这是实验性驱动参数，可能导致设备/显示异常；我有安全模式或系统备份恢复能力。</label>}
      <AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction disabled={locked||(experimental&&!riskAcknowledged)} onClick={()=>change(confirmation==="restore")}>{confirmation==="restore"?"确认还原":"保存原值并应用"}</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
    </>}
    </div>
}
