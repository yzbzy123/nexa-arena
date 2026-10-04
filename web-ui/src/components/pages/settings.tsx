import { useState } from "react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Checkbox } from "@/components/ui/checkbox"
import { Switch } from "@/components/ui/switch"
import type { Bootstrap, Hotkey, Settings } from "@/lib/types"
import type { ActionRunner } from "./shared"
import { Choice, PageHeading } from "./shared"

const keyOptions = [..."ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"].map((key) => ({ value: String(key.charCodeAt(0)), label: key }))
  .concat(Array.from({ length: 11 }, (_, i) => ({ value: String(112 + i), label: `F${i + 1}` })))
const labels: Record<string, string> = {
  switch: "切换 4:3", restore: "恢复显示", window: "显示主窗口", microphone: "麦克风静音",
}

export function SettingsPage({ boot, busy, run }: { boot: Bootstrap; busy: string | null; run: ActionRunner }) {
  const [settings, setSettings] = useState<Settings>(boot.sensitivity.settings)
  function updateHotkey(index: number, patch: Partial<Hotkey>) {
    setSettings((old) => ({ ...old, hotkeys: old.hotkeys.map((item, i) => i === index ? { ...item, ...patch } : item) }))
  }
  return <>
    <PageHeading title="托盘与快捷键">最小化可以留在托盘；关闭窗口仍会按恢复流程退出。</PageHeading>
    <Card className="panel-card"><CardHeader><CardTitle>窗口行为</CardTitle></CardHeader>
      <CardContent className="flex items-center justify-between gap-4">
        <span className="text-sm">最小化时隐藏到托盘</span>
        <Switch checked={settings.minimizeToTray} onCheckedChange={(value) => setSettings((old) => ({ ...old, minimizeToTray: value }))} aria-label="最小化时隐藏到托盘" />
      </CardContent>
    </Card>
    <Card className="panel-card"><CardHeader><CardTitle>快捷键</CardTitle>
      <CardDescription>至少使用 Ctrl 或 Alt；保存时会检查重复和系统占用。</CardDescription></CardHeader>
      <CardContent className="flex flex-col gap-4">
        {settings.hotkeys.map((hotkey, index) => <div className="list-row" key={hotkey.action}>
          <div className="flex min-w-0 items-center gap-3"><Switch checked={hotkey.enabled}
            onCheckedChange={(enabled) => updateHotkey(index, { enabled })} aria-label={`启用${labels[hotkey.action]}`} />
            <strong className="min-w-32 text-sm">{labels[hotkey.action]}</strong></div>
          <div className="flex flex-wrap items-center gap-3">
            {[[2, "Ctrl"], [1, "Alt"], [4, "Shift"]].map(([bit, name]) => <label key={bit} className="flex items-center gap-1.5 text-xs">
              <Checkbox checked={(hotkey.modifiers & Number(bit)) !== 0} onCheckedChange={(checked) => updateHotkey(index, {
                modifiers: checked ? hotkey.modifiers | Number(bit) : hotkey.modifiers & ~Number(bit) })} />{name}</label>)}
            <Choice label="按键" value={String(hotkey.key)} onChange={(value) => updateHotkey(index, { key: Number(value) })} options={keyOptions} />
          </div>
        </div>)}
        <div className="form-actions"><Button variant="outline" onClick={() => setSettings((old) => ({ ...old, hotkeys: [
          { action: "switch", enabled: true, modifiers: 3, key: 83 },
          { action: "restore", enabled: true, modifiers: 3, key: 72 },
          { action: "window", enabled: true, modifiers: 3, key: 78 },
          { action: "microphone", enabled: false, modifiers: 3, key: 77 },
        ] }))}>填入默认值</Button>
          <Button disabled={!!busy || boot.preview} onClick={async () => {
            const saved = await run<Settings>("settings.save", { ...settings }, "快捷键已应用")
            if (saved) setSettings(saved)
          }}>保存并应用</Button></div>
      </CardContent>
    </Card>
  </>
}
