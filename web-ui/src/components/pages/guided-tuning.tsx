import { useState } from "react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Checkbox } from "@/components/ui/checkbox"
import { Field, FieldGroup, FieldLabel, FieldLegend, FieldSet } from "@/components/ui/field"
export function GuidedTuning({options,onSelect}:{options:{id:string;supported:boolean}[];onSelect:(ids:string[])=>void}){
  const [recording,setRecording]=useState(true),[controller,setController]=useState(true),[shortcuts,setShortcuts]=useState(false),[animations,setAnimations]=useState(false)
  function choose(){const ids=["game-mode",...(!recording?["game-capture"]:[]),...(!controller?["gamebar-controller"]:[]),...(!shortcuts?["sticky-shortcut","filter-shortcut","toggle-shortcut"]:[]),...(animations?["client-animation","menu-animation","combo-animation","tooltip-animation","tooltip-fade","menu-fade","selection-fade","listbox-smooth"]:[])];onSelect(ids.filter(id=>options.some(x=>x.id===id&&x.supported)))}
  return <Card className="panel-card"><CardHeader><CardTitle>按使用习惯选择基础方案</CardTitle><CardDescription>回答后只载入勾选清单，仍需查看影响并确认。不会自动关闭服务、网络、设备或安全防护。</CardDescription></CardHeader><CardContent className="flex flex-col gap-4"><FieldSet><FieldLegend variant="label">保留你使用的功能</FieldLegend><FieldGroup className="gap-3">
    <Field orientation="horizontal"><Checkbox id="guided-recording" checked={recording} onCheckedChange={value=>setRecording(value===true)}/><FieldLabel htmlFor="guided-recording">我需要 Windows 游戏录制或回溯片段</FieldLabel></Field>
    <Field orientation="horizontal"><Checkbox id="guided-controller" checked={controller} onCheckedChange={value=>setController(value===true)}/><FieldLabel htmlFor="guided-controller">我需要用手柄按键打开 Game Bar</FieldLabel></Field>
    <Field orientation="horizontal"><Checkbox id="guided-access" checked={shortcuts} onCheckedChange={value=>setShortcuts(value===true)}/><FieldLabel htmlFor="guided-access">我使用粘滞键、筛选键等辅助快捷键</FieldLabel></Field>
    <Field orientation="horizontal"><Checkbox id="guided-animations" checked={animations} onCheckedChange={value=>setAnimations(value===true)}/><FieldLabel htmlFor="guided-animations">我愿意减少传统 Windows 界面动画</FieldLabel></Field>
  </FieldGroup></FieldSet><Button variant="outline" onClick={choose}>生成勾选方案，不立即应用</Button></CardContent></Card>
}
