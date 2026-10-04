import { useEffect, useState } from "react"
import { Play, Save, Square } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Checkbox } from "@/components/ui/checkbox"
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog"
import { invoke, isDesktop } from "@/lib/bridge"
import type { Game } from "@/lib/types"
import type { ActionRunner } from "./shared"
import { Choice } from "./shared"
type Profile={Game:Game;Priority:string;Affinity:string;HighPower:boolean}
type Status={enabled:boolean;message:string;profile:Profile;saved:Profile[];logicalCpus:number;affinitySupported:boolean;highPowerAvailable:boolean;activeSessions:number}
export function ProcessProfiles({game,busy,preview,run}:{game:Game;busy:string|null;preview:boolean;run:ActionRunner}){
  const [state,setState]=useState<Status|null>(null),[priority,setPriority]=useState("AboveNormal"),[affinity,setAffinity]=useState(""),[highPower,setHighPower]=useState(false),[error,setError]=useState<string|null>(null)
  const [confirm,setConfirm]=useState(false)
  useEffect(()=>{
    let active=true;setState(null);setError(null)
    if(isDesktop)invoke<Status>("process.status",{game}).then(value=>{if(active){setState(value);setPriority(value.profile.Priority);setAffinity(value.profile.Affinity);setHighPower(value.profile.HighPower)}}).catch(cause=>{if(active)setError(cause.message)})
    return()=>{active=false}
  },[game])
  useEffect(()=>{if(!state?.enabled)return;const timer=window.setInterval(()=>invoke<Status>("process.status",{game}).then(setState).catch(()=>undefined),2500);return()=>window.clearInterval(timer)},[game,state?.enabled])
  const locked=preview||!!busy||!state
  return <Card className="panel-card mt-5"><CardHeader><CardTitle>按游戏进程方案</CardTitle><CardDescription>保存只记录配置；启动监控后才会处理已保存方案的游戏进程。重开工具不会自动启用。</CardDescription></CardHeader><CardContent className="flex flex-col gap-4">
    <FieldGroup className="flex-row flex-wrap gap-4"><Field className="min-w-48 flex-1"><Choice label={`${game} 优先级`} value={priority} onChange={setPriority} disabled={state?.enabled} options={[{value:"Normal",label:"普通"},{value:"AboveNormal",label:"高于普通"},{value:"High",label:"高（可能影响其他程序）"}]}/></Field>
      <Field className="min-w-48 flex-1"><FieldLabel htmlFor="process-affinity">CPU 亲和性（十六进制，留空不改）</FieldLabel><Input id="process-affinity" value={affinity} onChange={event=>setAffinity(event.target.value)} disabled={state?.enabled||!state?.affinitySupported} placeholder="例如 F 表示逻辑 CPU 0–3" /></Field></FieldGroup>
    <p className="section-caption">本机 {state?.logicalCpus??"—"} 个逻辑 CPU。亲和性不是通用加速项；X3D/混合架构不建议盲目限制核心，超过 64 个逻辑 CPU 暂不修改。</p>
    <label className="flex items-center gap-2 text-sm"><Checkbox checked={highPower} onCheckedChange={value=>setHighPower(value===true)} disabled={state?.enabled||!state?.highPowerAvailable}/>游戏运行时切换已有高性能电源计划，退出后按原值还原</label>
    {!state?.highPowerAvailable&&<p className="section-caption">未检测到已有高性能计划，不会自动创建或修改电池参数。</p>}
    {error&&<p role="alert" className="text-destructive text-sm">{error}</p>}
    <div className="form-actions"><Button variant="outline" disabled={locked||state?.enabled} onClick={async()=>{const result=await run<Status>("process.save",{game,priority,affinity,highPower});if(result){setState(result);toast.success("已保存，尚未应用")}}}><Save data-icon="inline-start"/>保存此游戏方案</Button>
      <Button disabled={locked||(!state?.enabled&&!state?.saved.length&&!state?.activeSessions)} onClick={async()=>{if(!state?.enabled&&!state?.activeSessions){setConfirm(true);return}const result=await run<Status>("process.configure",{game,enabled:false});if(result)setState(result)}}>{state?.enabled?<Square data-icon="inline-start"/>:<Play data-icon="inline-start"/>}{state?.enabled?"停止监控并还原":state?.activeSessions?"还原上次进程会话":"启动已保存方案监控"}</Button></div>
    <p role="status" className="section-caption">{state?.message??"正在读取方案…"} · 活动会话 {state?.activeSessions??0}</p>
    <p className="section-caption">普通关闭或停止监控时尝试还原本次写入的优先级和亲和性；外部修改不会被覆盖。访问被反作弊拒绝时停止，不注入游戏或绕过保护。异常退出后的电源原值可在“系统管理 → 电源会话还原”核对。</p>
    <AlertDialog open={confirm} onOpenChange={setConfirm}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>启动已保存游戏方案的监控</AlertDialogTitle><AlertDialogDescription>下面的方案仅在对应游戏运行时执行。优先级和核心限制可能改善或降低表现；电源切换影响整机功耗。停止监控或普通关闭工具时按本次原值还原。</AlertDialogDescription></AlertDialogHeader>
      <div className="flex max-h-64 flex-col gap-3 overflow-auto text-sm">{state?.saved.map(profile=><p key={profile.Game}>{profile.Game} · {profile.Priority} · CPU {profile.Affinity||"保持原设置"} · 电源 {profile.HighPower?"已有高性能计划":"不更改"}</p>)}</div>
      <AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction disabled={locked} onClick={async()=>{setConfirm(false);const result=await run<Status>("process.configure",{game,enabled:true});if(result)setState(result)}}>确认启动监控</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
  </CardContent></Card>
}
