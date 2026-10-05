import { useEffect, useState } from "react"
import { PackagePlus, RefreshCcw, Trash2 } from "lucide-react"
import { toast } from "sonner"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { Field, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { invoke, isDesktop } from "@/lib/bridge"
import type { ActionRunner } from "./shared"
type App={name:string;package:string;location:string;framework:boolean;resource:boolean;nonRemovable:boolean;protectedPackage:boolean}
export function AppxPanel({busy,preview,run}:{busy:string|null;preview:boolean;run:ActionRunner}){
  const [apps,setApps]=useState<App[]>([]),[query,setQuery]=useState(""),[error,setError]=useState<string|null>(null),[loading,setLoading]=useState(false),[selected,setSelected]=useState<App|null>(null),[ack,setAck]=useState(false)
  const locked=preview||!!busy
  useEffect(()=>{let active=true;setLoading(true);if(isDesktop)invoke<App[]>("appx.list").then(value=>{if(active)setApps(value)}).catch(cause=>{if(active)setError(cause.message)}).finally(()=>{if(active)setLoading(false)});else setLoading(false);return()=>{active=false}},[])
  async function refresh(){setLoading(true);try{setApps(await invoke<App[]>("appx.list"));setError(null)}catch(cause){setError(cause instanceof Error?cause.message:"读取失败")}finally{setLoading(false)}}
  const shown=apps.filter(x=>x.name.toLocaleLowerCase().includes(query.trim().toLocaleLowerCase()))
  return <div className="stack"><Alert><AlertTitle>应用卸载不是普通可逆优化</AlertTitle><AlertDescription>只管理当前用户的 APPX/MSIX 应用；框架、系统核心、资源与不可移除的包受保护。卸载可能删除应用数据或安装文件，不能承诺一键还原；不会自动精简或批量勾选。</AlertDescription></Alert>
    <div className="flex flex-wrap items-end gap-3"><Field className="min-w-48 flex-1"><FieldLabel htmlFor="appx-search">搜索应用包</FieldLabel><Input id="appx-search" value={query} onChange={event=>setQuery(event.target.value)}/></Field><Button variant="outline" disabled={loading||!!busy} onClick={refresh}><RefreshCcw data-icon="inline-start"/>刷新</Button>
      <Button variant="outline" disabled={locked} onClick={async()=>{const result=await run<{ok:boolean;verified:boolean;message:string}>("appx.install");if(result?.ok){if(result.verified)toast.success(result.message);else toast.info(result.message);await refresh()}}}><PackagePlus data-icon="inline-start"/>选择可信安装包</Button></div>
    {error&&<p role="alert" className="text-destructive">{error}</p>}<p role="status" className="section-caption">{loading?"正在读取当前用户应用…":`显示 ${shown.length} / ${apps.length} 项`}</p>
    <div className="manager-list">{shown.map(app=><div key={app.package} className="list-row"><div className="min-w-0"><strong className="list-row-title">{app.name}</strong><span className="list-row-meta break-all">{app.package}</span></div>{app.protectedPackage?<Badge variant="outline">受保护</Badge>:<Button variant="destructive" size="sm" disabled={locked||loading} onClick={()=>{setSelected(app);setAck(false)}}><Trash2 data-icon="inline-start"/>检查卸载</Button>}</div>)}</div>
    <AlertDialog open={selected!==null} onOpenChange={open=>{if(!open)setSelected(null)}}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>卸载当前用户应用</AlertDialogTitle><AlertDialogDescription>{selected?.name}。这不是可逆调优，应用数据或安装文件可能丢失；恢复需要 Microsoft Store 或可信安装包，不会移除其他用户的应用。</AlertDialogDescription></AlertDialogHeader>
      <label className="flex items-start gap-2 text-sm"><Checkbox checked={ack} onCheckedChange={value=>setAck(value===true)}/>我已经备份需要的应用数据，理解不能承诺一键恢复。</label>
      <AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction variant="destructive" disabled={locked||!ack} onClick={async()=>{const packageName=selected?.package;setSelected(null);if(packageName){const result=await run<{ok:boolean;verified:boolean;message:string}>("appx.remove",{package:packageName,acknowledged:ack});if(result?.ok){if(result.verified)toast.success(result.message);else toast.info(result.message);await refresh()}}}}>确认卸载此应用</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
  </div>
}
