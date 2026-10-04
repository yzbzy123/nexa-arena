import { lazy, Suspense, useCallback, useEffect, useState } from "react"
import { toast, Toaster } from "sonner"
import { AudioLines, BookOpen, Crosshair, Keyboard, Monitor, RotateCcw, Settings2, SlidersHorizontal } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { demo, type Bootstrap, type DisplayState } from "@/lib/types"
import { invoke, isDesktop, onNativeEvent } from "@/lib/bridge"
import { ValorantPage } from "@/components/pages/valorant"
import { Cs2Page } from "@/components/pages/cs2"
import { SensitivityPage } from "@/components/pages/sensitivity"
import { AudioPage } from "@/components/pages/audio"
const OptimizerPage=lazy(()=>import("@/components/pages/optimizer").then(module=>({default:module.OptimizerPage})))
import { SettingsPage } from "@/components/pages/settings"
import { GuidePage } from "@/components/pages/guide"

const pages = [
  { id: "valorant", label: "VALORANT", title: "真实拉伸", detail: "选择模式、修复配置、切换与恢复", icon: Monitor },
  { id: "cs2", label: "CS2", title: "准星与画面", detail: "新版分享码、滤镜与指令", icon: Crosshair },
  { id: "sensitivity", label: "灵敏度", title: "灵敏度中心", detail: "CS2 / VALORANT 跨游戏换算", icon: SlidersHorizontal },
  { id: "audio", label: "游戏音频", title: "游戏音频", detail: "输出设备、麦克风和应用会话", icon: AudioLines },
  { id: "optimizer", label: "游戏优化", title: "游戏优化", detail: "按游戏配置 Windows 选项", icon: Settings2 },
  { id: "settings", label: "快捷键", title: "托盘与快捷键", detail: "日常操作与键位", icon: Keyboard },
  { id: "guide", label: "使用指南", title: "使用指南", detail: "设置步骤与恢复说明", icon: BookOpen },
] as const
type PageId = (typeof pages)[number]["id"]

export function App() {
  const [boot, setBoot] = useState<Bootstrap | null>(isDesktop ? null : demo)
  const [display, setDisplay] = useState<DisplayState>(demo.display)
  const [active, setActive] = useState<PageId>("valorant")
  const [busy, setBusy] = useState<string | null>(null)
  const [startupError, setStartupError] = useState<string | null>(null)

  const refreshDisplay = useCallback(async () => {
    if (!isDesktop) return
    try { setDisplay(await invoke<DisplayState>("display.state")) }
    catch { /* Keep the last confirmed state while the display re-enumerates. */ }
  }, [])

  useEffect(() => {
    if (!isDesktop) return
    invoke<Bootstrap>("bootstrap").then((value) => {
      setBoot(value)
      setDisplay(value.display)
    }).catch((error: Error) => setStartupError(error.message))
    const timer = window.setInterval(refreshDisplay, 2000)
    const off = onNativeEvent((name, value) => {
      if (name === "notice") toast.info((value as { message: string }).message)
    })
    return () => { window.clearInterval(timer); off() }
  }, [refreshDisplay])

  async function run<T>(action: string, data?: Record<string, unknown>, success?: string): Promise<T | null> {
    const readOnly = ["commands.generate","sensitivity.calculate","readiness.inspect","timing.measure"].includes(action)
    if (!isDesktop || (boot?.preview && !readOnly)) { toast.info("请在正式桌面版执行此操作"); return null }
    if (busy) return null
    setBusy(action)
    try {
      const result = await invoke<T>(action, data)
      if (success) toast.success(success)
      await refreshDisplay()
      return result
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "操作未完成")
      return null
    } finally { setBusy(null) }
  }

  async function copy(text: string) {
    try {
      if (isDesktop) await invoke("clipboard.set", { text })
      else await navigator.clipboard.writeText(text)
      toast.success("已复制到剪贴板")
    } catch (error) { toast.error(error instanceof Error ? error.message : "复制失败") }
  }

  if (startupError) return (
    <main className="startup-error"><Alert variant="destructive"><AlertTitle>界面暂未加载</AlertTitle>
      <AlertDescription>{startupError}。请关闭并重新打开；C# 宿主会保留显示恢复能力。</AlertDescription></Alert></main>
  )
  if (!boot) return <div className="startup-error" role="status">正在读取显示器与工具设置…</div>

  const page = pages.find((item) => item.id === active)!
  return (
    <>
    <div className="app-shell">
      <aside className="app-sidebar" aria-label="主导航">
        <div className="app-brand">
          <div className="brand-mark" aria-hidden="true">N</div>
          <div className="brand-text"><span className="brand-title">NEXA ARENA</span><span className="brand-subtitle">游戏工具箱</span></div>
        </div>
        <nav className="app-nav">
          {pages.map(({ id, label, icon: Icon }) => (
            <Button key={id} variant="ghost" className="nav-link" data-active={active === id}
              aria-label={label} aria-current={active === id ? "page" : undefined} title={label}
              onClick={() => setActive(id)}>
              <Icon className="nav-icon" aria-hidden="true" /><span className="nav-label">{label}</span>
            </Button>
          ))}
        </nav>
        <div className="sidebar-spacer" />
        <div className="sidebar-version">Nexa Arena 1.0.0</div>
      </aside>
      <div className="app-main">
        <header className="topbar">
          <div className="topbar-title"><span className="workspace-path">工作区 <span aria-hidden="true">/</span> {page.label}</span></div>
          <div className="topbar-tools">
            <span className="readout">{display.width} × {display.height} · {display.refresh} Hz</span>
            {boot.preview && <Badge variant="outline" className="preview-mark">安全预览</Badge>}
            <Button variant="outline" size="sm" disabled={!display.saved || !!busy || boot.preview}
              onClick={() => run("display.restore", undefined, "恢复操作已启动")}>
              <RotateCcw data-icon="inline-start" />恢复显示
            </Button>
          </div>
        </header>
        <div className="page-scroll">
          <div className="page-frame">
            {active === "valorant" && <ValorantPage boot={boot} display={display} busy={busy} run={run} />}
            {active === "cs2" && <Cs2Page boot={boot} display={display} busy={busy} run={run} copy={copy} />}
            {active === "sensitivity" && <SensitivityPage boot={boot} busy={busy} run={run} copy={copy} />}
            {active === "audio" && <AudioPage busy={busy} preview={boot.preview} run={run} />}
            {active === "optimizer" && <Suspense fallback={<p role="status">正在载入优化工具…</p>}><OptimizerPage busy={busy} preview={boot.preview} run={run} /></Suspense>}
            {active === "settings" && <SettingsPage boot={boot} busy={busy} run={run} />}
            {active === "guide" && <GuidePage />}
          </div>
        </div>
      </div>
      <footer className="app-footer"><span>免费游戏工具箱 · 作者：2ndWind</span><button aria-label="复制QQ群号 1105050795" onClick={() => copy("1105050795")}>QQ群 1105050795 · 点击复制群号</button></footer>
    </div>
    <Toaster theme="light" position="bottom-right" richColors />
    </>
  )
}

export default App
