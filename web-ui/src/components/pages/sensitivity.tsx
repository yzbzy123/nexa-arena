import { useEffect, useState } from "react"
import { ArrowLeftRight, Copy, Save } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { invoke, isDesktop } from "@/lib/bridge"
import type { Bootstrap, Settings } from "@/lib/types"
import type { ActionRunner } from "./shared"
import { Choice, NumberField, PageHeading } from "./shared"

const games = [{ value: "CS2", label: "COUNTER-STRIKE 2" }, { value: "VALORANT", label: "VALORANT" }]

export function SensitivityPage({ boot, busy, run, copy }: {
  boot: Bootstrap; busy: string | null; run: ActionRunner; copy: (text: string) => Promise<void>
}) {
  const [inputs, setInputs] = useState<Settings>(boot.sensitivity.settings)
  const [result, setResult] = useState<{ value: number; formatted: string; targetGame: string } | null>(null)
  const [error, setError] = useState<string | null>(null)
  const set = <K extends keyof Settings>(key: K, value: Settings[K]) => setInputs((old) => ({ ...old, [key]: value }))
  const sameGame=inputs.sourceGame===inputs.targetGame
  const applicability=inputs.method==="screen-center" ? "匹配未开镜时屏幕中心的水平微调；瓦的非 16:9 选项按真实拉伸计算，普通 HUD 拉伸不适用。"
    : inputs.method==="turn-distance" ? "匹配未开镜的转身距离；分辨率不参与这项换算。"
    : sameGame ? "同一游戏只做 DPI 换算，经验比值不参与计算。" : "使用你设置的个人经验比值，可按实际手感校准。"

  useEffect(() => {
    if (!isDesktop) return
    let active = true
    const timer = window.setTimeout(() => {
      invoke<{ value: number; formatted: string; targetGame: string }>("sensitivity.calculate", { ...inputs })
        .then((value) => { if (active) { setResult(value); setError(null) } })
        .catch((cause: Error) => { if (active) { setResult(null); setError(cause.message) } })
    }, 120)
    return () => { active = false; window.clearTimeout(timer) }
  }, [inputs])

  function swap() {
    if (!result || !Number.isFinite(result.value) || result.value <= 0 || result.value > 100) return
    setInputs((old) => ({ ...old, sourceGame: old.targetGame, targetGame: old.sourceGame,
      sourceResolution: old.targetResolution, targetResolution: old.sourceResolution,
      sourceDpi: old.targetDpi, targetDpi: old.sourceDpi,
      sensitivity: Number(result.value.toFixed(6)) }))
  }

  return <>
    <PageHeading title="灵敏度中心">选择你要匹配的手感；更换 DPI 时结果会同步更新。</PageHeading>
    <div className="split-layout">
      <Card className="panel-card"><CardHeader><CardTitle>换算输入</CardTitle>
        <CardDescription>两侧分辨率只在水平微调方案中参与计算。</CardDescription></CardHeader>
        <CardContent className="flex flex-col gap-6">
          <Choice label="换算方案" value={inputs.method} onChange={(value) => set("method", value)} options={boot.sensitivity.methods.map((item) => ({value:item.id,label:item.label}))} />
          <div className="field-columns">
            <div className="stack">
              <p className="section-title">来源设置</p>
              <Choice label="来源游戏" value={inputs.sourceGame} onChange={(value) => set("sourceGame", value as Settings["sourceGame"])} options={games} />
              <Choice label="来源分辨率" value={inputs.sourceResolution} onChange={(value) => set("sourceResolution", value)} options={boot.sensitivity.resolutions.map((item) => ({value:item.key,label:item.label}))} disabled={inputs.method !== "screen-center"} />
              <NumberField label="鼠标 DPI" value={inputs.sourceDpi} onChange={(value) => set("sourceDpi", value)} min={50} max={100000} />
            </div>
            <div className="stack">
              <p className="section-title">目标设置</p>
              <Choice label="目标游戏" value={inputs.targetGame} onChange={(value) => set("targetGame", value as Settings["targetGame"])} options={games} />
              <Choice label="目标分辨率" value={inputs.targetResolution} onChange={(value) => set("targetResolution", value)} options={boot.sensitivity.resolutions.map((item) => ({value:item.key,label:item.label}))} disabled={inputs.method !== "screen-center"} />
              <NumberField label="目标 DPI" value={inputs.targetDpi} onChange={(value) => set("targetDpi", value)} min={50} max={100000} />
            </div>
          </div>
          <div className="field-columns">
            <NumberField label="当前游戏灵敏度" value={inputs.sensitivity} onChange={(value) => set("sensitivity", value)} min={.000001} max={100} step={.000001} />
            {inputs.method === "custom-ratio" && <NumberField label="经验比值（CS / 瓦）" value={inputs.ratio} onChange={(value) => set("ratio", value)} min={.01} max={100} step={.01} disabled={sameGame} />}
          </div>
        </CardContent>
      </Card>
      <div className="stack">
        <Card className="panel-card"><CardHeader><CardTitle>换算结果</CardTitle><CardDescription>更改输入后自动计算。</CardDescription></CardHeader>
          <CardContent className="flex flex-col gap-5">
            <div>{result ? <><span className="block text-sm text-muted-foreground">{result.targetGame}</span>
              <strong className="number-output">{result.formatted}</strong></> :
              <span className="section-caption">{error || (boot.preview ? "请在桌面版中计算。" : "正在计算…")}</span>}</div>
            <p className="section-caption">{applicability}</p>
            <div className="form-actions">
              <Button variant="outline" disabled={!result} onClick={swap}><ArrowLeftRight data-icon="inline-start" />反向换算</Button>
              <Button variant="outline" disabled={!result} onClick={() => copy(result!.formatted)}><Copy data-icon="inline-start" />复制结果</Button>
            </div>
          </CardContent>
        </Card>
        <Card className="panel-card"><CardHeader><CardTitle>保存输入</CardTitle></CardHeader>
          <CardContent className="flex flex-col gap-4">
            <p className="section-caption">只保存工具内的方案、DPI 与数值，不修改游戏或鼠标驱动设置。</p>
            <Badge variant="outline" className="w-fit">{inputs.method === "screen-center" ? "屏幕中心水平微调" : inputs.method === "turn-distance" ? "等 cm/360" : "个人经验比例"}</Badge>
            <Button disabled={!!busy || boot.preview} onClick={() => run("sensitivity.save", { ...inputs }, "输入已保存")}><Save data-icon="inline-start" />保存当前输入</Button>
          </CardContent>
        </Card>
      </div>
    </div>
  </>
}
