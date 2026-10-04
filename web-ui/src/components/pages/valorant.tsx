import { useState } from "react"
import { AlertCircle, ArrowRight, Check, MonitorUp, RotateCcw } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Switch } from "@/components/ui/switch"
import { AspectSketch } from "@/components/aspect-sketch"
import type { Bootstrap, DisplayState } from "@/lib/types"
import type { ActionRunner } from "./shared"
import { PageHeading } from "./shared"

export function ValorantPage({ boot, display, busy, run }: {
  boot: Bootstrap; display: DisplayState; busy: string | null; run: ActionRunner
}) {
  const [customWidth, setCustomWidth] = useState(1440)
  const [customHeight, setCustomHeight] = useState(1080)
  const selected = display.selected ?? boot.modes[0] ?? { width: 1440, height: 1080 }
  const current = `${display.width} × ${display.height} @ ${display.refresh}Hz`
  const session = display.stage >= 2 ? "已应用目标模式" : display.stage === 1 ? "等待游戏画面确认" : display.saved ? "等待恢复" : "尚未开始"
  const canAct = !boot.preview && !busy && !display.restoring && !display.switching
  const restored = !display.saved && display.stage === 0 && /已验证恢复|已恢复到/.test(display.status)
  const currentStep = display.restoring || display.stage >= 2 ? 2 : display.configRepaired ? 1 : 0
  return <>
    <PageHeading title="VALORANT 画面控制">
      先选目标比例，再修复游戏配置；进入可见武器与 HUD 的画面后确认目标模式。
    </PageHeading>
    <div className="status-strip" role="status">
      <div><strong>{current}</strong><small>当前桌面显示模式</small></div>
      <Badge variant={display.stage >= 2 ? "default" : "outline"}>{session}</Badge>
    </div>
    <div className="split-layout">
      <div className="stack">
        <Card className="panel-card">
          <CardHeader><CardTitle>目标分辨率</CardTitle><CardDescription>点击预设只会选择模式，实际切换由右侧操作执行。</CardDescription></CardHeader>
          <CardContent className="flex flex-col gap-4">
            <div className="mode-grid" role="group" aria-label="目标分辨率">
              {boot.modes.filter((mode) => mode.width >= 1024 && mode.height >= 768 && mode.width <= 2560)
                .filter((mode) => ["1280x960", "1280x1024", "1440x1080", "1568x1080", "2088x1440"].includes(`${mode.width}x${mode.height}`)
                    || (display.selected?.width === mode.width && display.selected.height === mode.height))
                .slice(0, 8).map((mode) => {
                  const active = selected.width === mode.width && selected.height === mode.height
                  return <Button key={`${mode.width}x${mode.height}`} variant="outline" data-selected={active}
                    aria-pressed={active} disabled={!canAct} onClick={() => run("display.select", mode)}>
                    <strong>{mode.width} × {mode.height}</strong>
                    <span>{mode.width * 3 === mode.height * 4 ? "4:3" : mode.width * 4 === mode.height * 5 ? "5:4" : "自定义比例"}</span>
                  </Button>
                })}
            </div>
            <div className="flex flex-wrap items-end gap-3 border-t pt-4">
              <FieldGroup className="min-w-28 flex-1"><Field><FieldLabel htmlFor="custom-width">宽度</FieldLabel>
                <Input id="custom-width" type="number" min={640} max={10000} value={customWidth}
                  onChange={(event) => setCustomWidth(Number(event.target.value))} /></Field></FieldGroup>
              <FieldGroup className="min-w-28 flex-1"><Field><FieldLabel htmlFor="custom-height">高度</FieldLabel>
                <Input id="custom-height" type="number" min={480} max={10000} value={customHeight}
                  onChange={(event) => setCustomHeight(Number(event.target.value))} /></Field></FieldGroup>
              <Button variant="outline" disabled={!canAct} onClick={() => run("display.add", { width: customWidth, height: customHeight }, "已添加目标模式")}>添加模式</Button>
            </div>
          </CardContent>
        </Card>
        <Card className="panel-card">
          <CardHeader><CardTitle>画面比例</CardTitle><CardDescription>左侧固定为 16:9；右侧显示所选比例全屏拉伸后，同一图形的横向变化。</CardDescription></CardHeader>
          <CardContent><AspectSketch width={selected.width} height={selected.height} /></CardContent>
        </Card>
      </div>
      <div className="stack">
        <Card className="panel-card">
          <CardHeader><CardTitle>操作顺序</CardTitle><CardDescription>自动恢复默认开启；退出游戏后会还原监视器和桌面图标。</CardDescription></CardHeader>
          <CardContent className="flex flex-col gap-5">
            <div className="flow-steps">
              {[
                ["1", "修复配置", display.configRepaired],
                ["2", "应用拉伸", display.stage >= 2],
                ["3", "恢复桌面", restored],
              ].map(([number, label, done], index) => <div className="flow-step" key={String(number)} data-done={done} data-current={!restored && index === currentStep}>
                <b>{done ? <Check aria-hidden="true" /> : number}</b><span>{label}</span></div>)}
            </div>
            <div className="action-stack">
              <Button variant="outline" className="support-action" disabled={!canAct} onClick={() => run("display.repair", undefined, "配置处理完成")}>
                修复游戏配置
              </Button>
              <Button className="primary-action" disabled={!canAct || !display.configRepaired} onClick={() => run("display.switch")}>开始真实拉伸 <ArrowRight data-icon="inline-end" /></Button>
              <Button variant="outline" className="support-action" disabled={!canAct || !display.saved} onClick={() => run("display.restore")}>
                <RotateCcw data-icon="inline-start" />恢复原始显示
              </Button>
            </div>
            <div className="flex items-center justify-between gap-3 border-t pt-4">
              <span className="text-sm">游戏退出后自动恢复</span>
              <Switch checked={display.autoRestore} disabled={!canAct} onCheckedChange={(enabled) => run("display.auto", { enabled })} aria-label="游戏退出后自动恢复" />
            </div>
          </CardContent>
        </Card>
        <Card className="panel-card">
          <CardHeader><CardTitle>当前状态</CardTitle></CardHeader>
          <CardContent className="flex flex-col gap-3">
            <p className="text-sm leading-6 text-muted-foreground">{display.status}</p>
            {display.saved && <div className="empty-note"><MonitorUp aria-hidden="true" />原始状态：{display.saved.width} × {display.saved.height} @ {display.saved.refresh}Hz</div>}
            {!display.configRepaired && <div className="empty-note"><AlertCircle aria-hidden="true" />请先完全退出游戏并修复配置。</div>}
          </CardContent>
        </Card>
      </div>
    </div>
  </>
}
