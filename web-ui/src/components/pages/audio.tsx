import { useEffect, useState } from "react"
import { Mic, RefreshCcw, Volume2 } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Switch } from "@/components/ui/switch"
import { Slider } from "@/components/ui/slider"
import { invoke, isDesktop } from "@/lib/bridge"
import type { AudioDevice, AudioSession } from "@/lib/types"
import type { ActionRunner } from "./shared"
import { Choice, PageHeading } from "./shared"

export function AudioPage({ busy, preview, run }: { busy: string | null; preview: boolean; run: ActionRunner }) {
  const [devices, setDevices] = useState<{ output: AudioDevice[]; input: AudioDevice[] }>({ output: [], input: [] })
  const [selectedOutput, setSelectedOutput] = useState("")
  const [selectedInput, setSelectedInput] = useState("")
  const [sessions, setSessions] = useState<AudioSession[]>([])
  const [volume, setVolume] = useState(100)
  const [muted, setMuted] = useState(false)
  const [micVolume, setMicVolume] = useState(100)
  const [micMuted, setMicMuted] = useState(false)
  const [confirm, setConfirm] = useState<{ id: string; flow: number; name: string } | null>(null)

  function adopt(next: { output: AudioDevice[]; input: AudioDevice[] }) {
    setDevices(next)
    const output = next.output.find((x) => x.Id === selectedOutput) ?? next.output.find((x) => x.IsDefault) ?? next.output[0]
    const input = next.input.find((x) => x.Id === selectedInput) ?? next.input.find((x) => x.IsDefault) ?? next.input[0]
    setSelectedOutput(output?.Id ?? "")
    setSelectedInput(input?.Id ?? "")
    if (output) { setVolume(output.Volume); setMuted(output.Muted) }
    if (input) { setMicVolume(input.Volume); setMicMuted(input.Muted) }
  }
  async function refresh() {
    if (!isDesktop) return
    adopt(await invoke<{ output: AudioDevice[]; input: AudioDevice[] }>("audio.devices"))
  }
  useEffect(() => {
    if (!isDesktop) return
    invoke<{ output: AudioDevice[]; input: AudioDevice[] }>("audio.devices").then((next) => {
      setDevices(next)
      const output = next.output.find((x) => x.IsDefault) ?? next.output[0]
      const input = next.input.find((x) => x.IsDefault) ?? next.input[0]
      setSelectedOutput(output?.Id ?? "")
      setSelectedInput(input?.Id ?? "")
      if (output) { setVolume(output.Volume); setMuted(output.Muted) }
      if (input) { setMicVolume(input.Volume); setMicMuted(input.Muted) }
    }).catch(() => undefined)
  }, [])
  useEffect(() => {
    if (selectedOutput && isDesktop) invoke<AudioSession[]>("audio.sessions", { deviceId: selectedOutput }).then(setSessions).catch(() => setSessions([]))
  }, [selectedOutput])
  const output = devices.output.find((item) => item.Id === selectedOutput)
  const input = devices.input.find((item) => item.Id === selectedInput)
  const disabled = preview || !!busy

  return <>
    <PageHeading title="游戏音频">选择输出和麦克风，再单独应用设备或应用会话音量。</PageHeading>
    <div className="form-actions"><Button variant="outline" onClick={() => refresh()} disabled={!isDesktop}><RefreshCcw data-icon="inline-start" />刷新设备</Button></div>
    <div className="split-layout">
      <Card className="panel-card"><CardHeader><CardTitle><Volume2 className="mr-2 inline size-4" aria-hidden="true" />输出设备</CardTitle>
        <CardDescription>切换系统默认输出也会影响其他使用默认设备的程序。</CardDescription></CardHeader>
        <CardContent className="flex flex-col gap-5">
          {devices.output.length === 0 ? <div className="empty-note">尚未读取到可用输出设备。请在桌面版中刷新。</div> : <>
            <Choice label="设备" value={selectedOutput} onChange={(id) => { setSelectedOutput(id); const device=devices.output.find((item)=>item.Id===id); if(device){setVolume(device.Volume);setMuted(device.Muted)} }}
              options={devices.output.map((x) => ({ value: x.Id, label: x.Name + (x.IsDefault ? "（默认）" : "") }))} />
            <FieldGroup><Field><FieldLabel>设备音量：{volume}%</FieldLabel>
              <Slider value={[volume]} onValueChange={(value) => setVolume(value[0])} min={0} max={100} step={1} /></Field></FieldGroup>
            <div className="flex items-center justify-between text-sm"><span>静音此输出设备</span><Switch checked={muted} onCheckedChange={setMuted} aria-label="静音输出设备" /></div>
            <div className="form-actions">
              <Button variant="outline" disabled={disabled || !output || output.IsDefault} onClick={() => output && setConfirm({ id: output.Id, flow: 0, name: output.Name })}>设为默认输出</Button>
              <Button disabled={disabled || !output} onClick={async () => { const ok = await run("audio.volume", { id: selectedOutput, volume, muted }, "输出音量已应用"); if (ok) refresh() }}>应用音量</Button>
            </div>
          </>}
        </CardContent>
      </Card>
      <Card className="panel-card"><CardHeader><CardTitle><Mic className="mr-2 inline size-4" aria-hidden="true" />麦克风</CardTitle>
        <CardDescription>此处只读取并设置设备音量、默认设备和静音状态，不录音。</CardDescription></CardHeader>
        <CardContent className="flex flex-col gap-5">
          {devices.input.length === 0 ? <div className="empty-note">尚未读取到可用麦克风。</div> : <>
            <Choice label="输入设备" value={selectedInput} onChange={(id) => { setSelectedInput(id); const device=devices.input.find((item)=>item.Id===id); if(device){setMicVolume(device.Volume);setMicMuted(device.Muted)} }}
              options={devices.input.map((x) => ({ value: x.Id, label: x.Name + (x.IsDefault ? "（默认）" : "") }))} />
            <FieldGroup><Field><FieldLabel>麦克风音量：{micVolume}%</FieldLabel>
              <Slider value={[micVolume]} onValueChange={(value) => setMicVolume(value[0])} min={0} max={100} step={1} /></Field></FieldGroup>
            <div className="flex items-center justify-between text-sm"><span>静音此麦克风</span><Switch checked={micMuted} onCheckedChange={setMicMuted} aria-label="静音麦克风" /></div>
            <div className="form-actions">
              <Button variant="outline" disabled={disabled || !input || input.IsDefault} onClick={() => input && setConfirm({ id: input.Id, flow: 1, name: input.Name })}>设为默认输入</Button>
              <Button disabled={disabled || !input} onClick={async () => { const ok = await run("audio.volume", { id: selectedInput, volume: micVolume, muted: micMuted }, "麦克风音量已应用"); if (ok) refresh() }}>应用音量</Button>
            </div>
          </>}
        </CardContent>
      </Card>
    </div>
    {sessions.length > 0 && <Card className="panel-card"><CardHeader><CardTitle>应用会话音量</CardTitle>
      <CardDescription>仅修改当前选中输出设备上的会话。</CardDescription></CardHeader>
      <CardContent>{sessions.map((session) => <SessionRow key={session.SessionId} session={session} disabled={disabled}
        apply={async (level, mute) => {
          const ok = await run("audio.session", { deviceId: selectedOutput, sessionId: session.SessionId, volume: level, muted: mute }, "应用音量已应用")
          if (ok) setSessions(await invoke<AudioSession[]>("audio.sessions", { deviceId: selectedOutput }))
        }} />)}</CardContent>
    </Card>}
    <Dialog open={!!confirm} onOpenChange={(open) => { if (!open) setConfirm(null) }}>
      <DialogContent><DialogHeader><DialogTitle>切换系统默认设备</DialogTitle>
        <DialogDescription>将“{confirm?.name}”设为默认{confirm?.flow === 0 ? "输出" : "输入"}和通信设备，其他应用也可能切换。</DialogDescription></DialogHeader>
        <DialogFooter><Button variant="outline" onClick={() => setConfirm(null)}>取消</Button>
          <Button onClick={async () => { if (confirm) { const ok = await run("audio.default", { id: confirm.id, flow: confirm.flow }, "默认设备已切换"); setConfirm(null); if (ok) refresh() } }}>确认切换</Button>
        </DialogFooter></DialogContent>
    </Dialog>
  </>
}

function SessionRow({ session, disabled, apply }: { session: AudioSession; disabled: boolean; apply: (level: number, muted: boolean) => void }) {
  const [level, setLevel] = useState(session.Volume)
  const [muted, setMuted] = useState(session.Muted)
  return <div className="list-row"><div className="min-w-0 flex-1"><div className="list-row-title">{session.Name}</div>
    <div className="mt-3 flex items-center gap-4"><Slider className="max-w-52" value={[level]} onValueChange={(value) => setLevel(value[0])} min={0} max={100} />
      <span className="quiet-data w-11">{level}%</span><Switch checked={muted} onCheckedChange={setMuted} aria-label={`静音 ${session.Name}`} /></div></div>
    <Button variant="outline" disabled={disabled} onClick={() => apply(level, muted)}>应用</Button></div>
}
