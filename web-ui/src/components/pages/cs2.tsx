import { useState } from "react"
import { ArrowUpRight, Copy, Download, RefreshCcw } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Checkbox } from "@/components/ui/checkbox"
import { Textarea } from "@/components/ui/textarea"
import { Switch } from "@/components/ui/switch"
import { CrosshairPreview } from "@/components/crosshair-preview"
import { rescaleCrosshair } from "@/lib/crosshair-renderer"
import { isDesktop } from "@/lib/bridge"
import type { Bootstrap, DisplayState } from "@/lib/types"
import type { ActionRunner } from "./shared"
import { Choice, NumberField, PageHeading } from "./shared"

type Options = {
  fpsLimit: number; showFps: boolean; practice: boolean; unlimitedAmmo: boolean;
  buyAnywhere: boolean; grenadePreview: boolean; buyKey: string; items: string[]
}

export function Cs2Page({ boot, display, busy, run, copy }: {
  boot: Bootstrap; display: DisplayState; busy: string | null; run: ActionRunner; copy: (text: string) => Promise<void>
}) {
  const [selectedIndex, setSelectedIndex] = useState(0)
  const [gameResolution, setGameResolution] = useState("1440x1080")
  const [zoom, setZoom] = useState("2")
  const [stretched, setStretched] = useState(true)
  const [gameWidth, gameHeight] = gameResolution.split("x").map(Number)
  const initialOutput = display.saved ?? boot.display
  const [outputResolution,setOutputResolution]=useState(`${initialOutput.width}x${initialOutput.height}`)
  const [outputWidth,outputHeight]=outputResolution.split("x").map(Number)
  const output={width:outputWidth,height:outputHeight}
  const outputModes=[{value:"1920x1080",label:"1920 × 1080"},{value:"2560x1440",label:"2560 × 1440"},{value:"3840x2160",label:"3840 × 2160"},
    {value:"2560x1080",label:"2560 × 1080"},{value:"3440x1440",label:"3440 × 1440"}]
  if(!outputModes.some(item=>item.value===outputResolution))outputModes.push({value:outputResolution,label:`${outputWidth} × ${outputHeight}`})
  const selected = boot.cs2[selectedIndex]
  const geometry = selected?.preview ? rescaleCrosshair(selected.preview,gameHeight) : null
  const [options, setOptions] = useState<Options>({ fpsLimit: 300, showFps: false, practice: false,
    unlimitedAmmo: true, buyAnywhere: true, grenadePreview: true, buyKey: "F6", items: [] })
  const [generated, setGenerated] = useState("")
  const active = !busy && !boot.preview
  const set = <K extends keyof Options>(key: K, value: Options[K]) => setOptions((old) => ({ ...old, [key]: value }))
  const toggleItem = (id: string) => set("items", options.items.includes(id) ? options.items.filter((item) => item !== id) : [...options.items, id])

  return <>
    <PageHeading title="COUNTER-STRIKE 2">准星导入、画面滤镜和本地练习指令。</PageHeading>
    <Tabs defaultValue="crosshairs" className="w-full">
      <TabsList variant="line" className="w-full justify-start gap-3 border-b pb-2">
        <TabsTrigger value="crosshairs">准星</TabsTrigger><TabsTrigger value="filters">画面滤镜</TabsTrigger><TabsTrigger value="commands">指令生成</TabsTrigger>
      </TabsList>
      <TabsContent value="crosshairs" className="pt-3">
        <div className="flex items-center justify-between gap-4 pb-4"><p className="section-caption">在游戏设置 → 准星/瞄准镜 → 分享或导入中粘贴。</p>
          <a className="more-crosshairs" href="https://crosshair.club/builder" target="_blank" rel="noopener noreferrer" onClick={(event) => {
            if(isDesktop){event.preventDefault();void run("crosshair.more")}
          }}>查看更多 CS2 准心<ArrowUpRight aria-hidden="true" /></a></div>
        {boot.cs2.length === 0 ? <div className="empty-note">当前没有加载准星数据。</div> : <div className="crosshair-layout">
          <div className="crosshair-library" role="group" aria-label="CS2 准星样式">
            {boot.cs2.map((preset,index) => <button className="crosshair-row" type="button" key={preset.name} aria-pressed={index===selectedIndex} onClick={()=>setSelectedIndex(index)}>
              {preset.preview && <CrosshairPreview thumbnail data={preset.preview} gameWidth={gameWidth} gameHeight={gameHeight} outputWidth={output.width} outputHeight={output.height} stretched={stretched} name={preset.name} />}
              <span><strong>{preset.name}</strong><small>基准分辨率 {preset.resolution}</small></span>
              <span className="section-caption">{preset.preview?.centerDot && preset.preview.length===0 ? "中心点" : "静态十字"}</span>
            </button>)}
          </div>
          {selected?.preview && geometry && <div className="crosshair-inspector">
            <div className="flex items-center justify-between gap-3"><h2 className="section-title">{selected.name}</h2><span className="section-caption">未开镜 · 静态</span></div>
            <div className="field-columns"><Choice label="游戏分辨率" value={gameResolution} onChange={setGameResolution}
              options={boot.sensitivity.resolutions.map(item=>({value:item.key,label:item.label}))} />
              <Choice label="显示器原生分辨率" value={outputResolution} onChange={setOutputResolution} options={outputModes} /></div>
            <CrosshairPreview data={selected.preview} gameWidth={gameWidth} gameHeight={gameHeight} outputWidth={output.width} outputHeight={output.height} stretched={stretched} zoom={Number(zoom)} name={selected.name} />
            <div className="flex flex-wrap items-end justify-between gap-4"><label className="flex items-center gap-3 text-sm"><span>全屏拉伸</span><Switch checked={stretched} onCheckedChange={setStretched} /></label>
              <div className="min-w-24"><Choice label="观察倍率" value={zoom} onChange={setZoom} options={[1,2,4,8].map(value=>({value:String(value),label:value+"×"}))} /></div></div>
            <div className="crosshair-stats"><div><span>长度</span><strong>{geometry.length} px</strong></div><div><span>粗细</span><strong>{geometry.thickness} px</strong></div><div><span>间距</span><strong>{geometry.gap} px</strong></div></div>
            <p className="section-caption">1× 为显示像素；请核对显示器原生分辨率。倍率只放大观察，拉伸滤波由驱动决定。</p>
            <code className="crosshair-code">{selected.code}</code>
            <Button onClick={()=>copy(selected.code)}><Copy data-icon="inline-start" />复制导入码</Button>
          </div>}
        </div>}
      </TabsContent>
      <TabsContent value="filters" className="pt-3">
        <Card className="panel-card"><CardHeader><CardTitle>画面滤镜</CardTitle>
          <CardDescription>仅作用于 Windows 显示输出；HDR 可能阻止滤镜生效。</CardDescription></CardHeader>
          <CardContent>
            {boot.filters.map((filter) => <div className="list-row" key={filter.name}>
              <div className="flex min-w-0 items-center gap-3">
                <span className="size-8 rounded-lg border" style={{ backgroundColor: filter.color }} aria-hidden="true" />
                <div><div className="list-row-title">{filter.name}</div><span className="list-row-meta">{filter.description}</span></div>
              </div>
              <Button variant="outline" size="sm" disabled={!active} onClick={() => run("filter.apply", { name: filter.name }, "滤镜已应用")}>应用</Button>
            </div>)}
            <div className="form-actions pt-4"><Button variant="outline" disabled={!active} onClick={() => run("filter.restore", undefined, "已恢复原始颜色")}>恢复原始颜色</Button></div>
          </CardContent>
        </Card>
      </TabsContent>
      <TabsContent value="commands" className="pt-3">
        <div className="split-layout">
          <Card className="panel-card"><CardHeader><CardTitle>指令选项</CardTitle><CardDescription>生成文本后再决定是否复制或导出，不会自动执行。</CardDescription></CardHeader>
            <CardContent className="flex flex-col gap-5">
              <NumberField label="FPS 上限（0 为不限）" min={0} max={1000} value={options.fpsLimit} onChange={(v) => set("fpsLimit", v)} />
              <div className="field-columns">
                <Choice label="购买绑定" value={options.buyKey} onChange={(v) => set("buyKey", v)} options={["F6","F7","F8","F9","F10","F11","B","V","C","X","Z"].map((key) => ({value:key,label:key}))} />
                <div className="flex flex-col justify-center gap-3 text-sm">
                  <CheckOption label="显示游戏内 FPS" checked={options.showFps} onChange={(v) => set("showFps", v)} />
                  <CheckOption label="加入本地练习房参数" checked={options.practice} onChange={(v) => set("practice", v)} />
                </div>
              </div>
              {options.practice && <div className="flex flex-col gap-2 border-t pt-4 text-sm">
                <CheckOption label="无限弹药" checked={options.unlimitedAmmo} onChange={(v) => set("unlimitedAmmo", v)} />
                <CheckOption label="任意位置购买" checked={options.buyAnywhere} onChange={(v) => set("buyAnywhere", v)} />
                <CheckOption label="投掷物预览" checked={options.grenadePreview} onChange={(v) => set("grenadePreview", v)} />
              </div>}
              <FieldGroup><Field><FieldLabel>购买物品</FieldLabel>
                <div className="grid grid-cols-2 gap-3 rounded-lg border p-3">
                  {boot.buyItems.map((item) => <CheckOption key={item.id} label={item.name}
                    checked={options.items.includes(item.id)} onChange={() => toggleItem(item.id)} />)}
                </div></Field></FieldGroup>
              <Button onClick={async () => {
                const result = await run<string>("commands.generate", options)
                if (result !== null) setGenerated(result)
              }}><RefreshCcw data-icon="inline-start" />生成指令</Button>
            </CardContent>
          </Card>
          <Card className="panel-card"><CardHeader><CardTitle>生成结果</CardTitle>
            <CardDescription>练习参数只适用于本地练习房。</CardDescription></CardHeader>
            <CardContent className="flex flex-col gap-4">
              <Textarea readOnly value={generated} placeholder="点击“生成指令”后在这里检查结果。" className="min-h-80 font-mono text-xs" />
              <div className="form-actions">
                <Button variant="outline" disabled={!generated} onClick={() => copy(generated)}><Copy data-icon="inline-start" />复制</Button>
                <Button variant="outline" disabled={!active || !generated} onClick={() => run("commands.export", options)}><Download data-icon="inline-start" />导出 .cfg</Button>
              </div>
            </CardContent>
          </Card>
        </div>
      </TabsContent>
    </Tabs>
  </>
}

function CheckOption({ label, checked, onChange }: { label: string; checked: boolean; onChange: (checked: boolean) => void }) {
  return <label className="flex cursor-pointer items-center gap-2"><Checkbox checked={checked} onCheckedChange={(value) => onChange(value === true)} />{label}</label>
}
