import { useEffect, useState } from "react"
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { invoke, isDesktop } from "@/lib/bridge"
import type { ActionRunner } from "./shared"
type Status={running:boolean;current:string|null;message:string;exitCode:number|null;lines:string[]}
const tasks=[{id:"sfc-verify",title:"验证 Windows 系统文件",detail:"SFC /verifyonly：检查，不主动替换文件。可能耗时数分钟。"},{id:"sfc-repair",title:"修复 Windows 系统文件",detail:"SFC /scannow：按 Windows 组件源修复文件，会更改系统文件；不等同于游戏优化。"},{id:"dism-scan",title:"检查 Windows 组件存储",detail:"DISM ScanHealth：检查组件存储状态，不自动重启。"},{id:"dism-repair",title:"修复 Windows 组件存储",detail:"DISM RestoreHealth：可能使用 Windows Update 下载修复文件。"},{id:"dns-flush",title:"清空 DNS 解析缓存",detail:"仅清空系统 DNS 缓存，不改 DNS 服务器、IP 或网络驱动；不保证降低游戏延迟。"}]
export function RepairPanel({busy,preview,run}:{busy:string|null;preview:boolean;run:ActionRunner}){
  const [state,setState]=useState<Status|null>(null),[confirmation,setConfirmation]=useState<(typeof tasks)[number]|null>(null)
  useEffect(()=>{if(!isDesktop)return;let active=true;const update=()=>invoke<Status>("repair.status").then(value=>{if(active)setState(value)}).catch(()=>undefined);update();const timer=window.setInterval(update,2000);return()=>{active=false;window.clearInterval(timer)}},[])
  return <Card className="panel-card"><CardHeader><CardTitle>Windows 检查与修复</CardTitle><CardDescription>运行 Windows 自带工具并显示输出，不下载修复脚本，不关闭安全软件，不自动重启。开始后请等任务结束再关闭工具；这些操作不作为可逆调优项。</CardDescription></CardHeader><CardContent className="flex flex-col gap-4">
    {tasks.map(task=><div className="list-row" key={task.id}><div><strong className="list-row-title">{task.title}</strong><span className="list-row-meta">{task.detail}</span></div><Button variant="outline" disabled={preview||!!busy||state?.running} onClick={()=>setConfirmation(task)}>检查运行</Button></div>)}
    <p role="status" className="section-caption">{state?.message??"正在读取任务状态…"}</p>{!!state?.lines.length&&<pre className="code-output max-h-64 overflow-auto">{state.lines.join("\n")}</pre>}
    <AlertDialog open={confirmation!==null} onOpenChange={open=>{if(!open)setConfirmation(null)}}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>{confirmation?.title}</AlertDialogTitle><AlertDialogDescription>{confirmation?.detail} 请先保存工作；系统修复不是可逆设置切换，不能使用普通原值备份撤销系统文件修复。运行中请等待完成。</AlertDialogDescription></AlertDialogHeader>
      <AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction disabled={preview||!!busy} onClick={async()=>{const task=confirmation?.id;setConfirmation(null);if(task){const result=await run<Status>("repair.start",{task,confirmed:true});if(result)setState(result)}}}>确认运行 Windows 工具</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
  </CardContent></Card>
}
