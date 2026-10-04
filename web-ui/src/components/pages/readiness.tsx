import { useState } from "react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Choice } from "./shared"
import type { ActionRunner } from "./shared"
import type { Game } from "@/lib/types"
type Report={hardware:{name:string;details:string}[];cpuLoad:string;memoryTotalMb:number;memoryAvailableMb:number;gameMode:string;memoryIntegrityRegistry:string;activePowerPlan:string;note:string;path:string|null;process:{running:boolean;pid:number;priority:string|null};sampledUtc:string}
export function ReadinessPanel({busy,run}:{busy:string|null;run:ActionRunner}){
  const [game,setGame]=useState<Game>("CS2"),[report,setReport]=useState<Report|null>(null)
  const [timing,setTiming]=useState<{samples:number;averageMs:number;minMs:number;maxMs:number;p95Ms:number;note:string}|null>(null)
  return <Card className="panel-card"><CardHeader><CardTitle>负载与游戏就绪检查</CardTitle><CardDescription>按需读取硬件、当前 CPU 负载、内存和游戏进程，不保存 FPS、不生成虚假的瓶颈分数，也不会修改设置。</CardDescription></CardHeader><CardContent className="flex flex-col gap-4">
    <div className="flex flex-wrap items-end gap-3"><div className="min-w-48 flex-1"><Choice label="检查游戏" value={game} onChange={value=>{setGame(value as Game);setReport(null)}} options={[{value:"CS2",label:"CS2"},{value:"VALORANT",label:"VALORANT"}]}/></div><Button disabled={!!busy} onClick={async()=>{const value=await run<Report>("readiness.inspect",{game});if(value)setReport(value)}}>只读检查</Button></div>
    {report&&<><div className="manager-list">{report.hardware.map((item,index)=><div className="list-row" key={index}><div><strong className="list-row-title">{item.name}</strong><span className="list-row-meta">{item.details}</span></div></div>)}</div>
      <div className="flex flex-col gap-2 text-sm"><div>CPU 当前负载：{report.cpuLoad}</div><div>可用内存：{report.memoryAvailableMb} / {report.memoryTotalMb} MB</div><div>游戏进程：{report.process.running?`PID ${report.process.pid} · ${report.process.priority}`:"未运行"}</div><div>游戏模式配置值：{report.gameMode}</div><div>内存完整性配置值：{report.memoryIntegrityRegistry}</div><div className="break-all">电源计划：{report.activePowerPlan}</div></div><p className="section-caption">{report.note}</p></>}
    <Button variant="outline" disabled={!!busy} onClick={async()=>{const value=await run<typeof timing>("timing.measure");if(value)setTiming(value)}}>测量线程唤醒耗时，不改计时器</Button>
    {timing&&<><p role="status" className="text-sm">{timing.samples} 个样本 · 平均 {timing.averageMs.toFixed(2)} ms · P95 {timing.p95Ms.toFixed(2)} ms · 最小/最大 {timing.minMs.toFixed(2)}/{timing.maxMs.toFixed(2)} ms</p><p className="section-caption">{timing.note}</p></>}
  </CardContent></Card>
}
