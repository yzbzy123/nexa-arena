type Request = { id: number; action: string; data?: Record<string, unknown> }
type Reply = { replyTo?: number; ok?: boolean; data?: unknown; error?: string; eventName?: string }

declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (value: Request) => void
        addEventListener: (name: "message", listener: (event: MessageEvent<Reply>) => void) => void
      }
    }
  }
}

let sequence = 0
const pending = new Map<number, { resolve: (value: unknown) => void; reject: (error: Error) => void; timer: number }>()
const listeners = new Set<(name: string, data: unknown) => void>()

window.chrome?.webview?.addEventListener("message", (event) => {
  const message = event.data
  if (typeof message?.replyTo === "number") {
    const waiting = pending.get(message.replyTo)
    if (!waiting) return
    pending.delete(message.replyTo)
    window.clearTimeout(waiting.timer)
    if (message.ok) waiting.resolve(message.data)
    else waiting.reject(new Error(message.error || "操作未完成"))
  } else if (message?.eventName) {
    listeners.forEach((listener) => listener(message.eventName!, message.data))
  }
})

export const isDesktop = Boolean(window.chrome?.webview)

export function invoke<T>(action: string, data?: Record<string, unknown>): Promise<T> {
  if (!window.chrome?.webview) return Promise.reject(new Error("请在 Nexa Arena 桌面程序中使用此操作。"))
  const id = ++sequence
  return new Promise<T>((resolve, reject) => {
    const timer = window.setTimeout(() => {
      pending.delete(id)
      reject(new Error("桌面操作等待超时，请检查窗口中的确认提示。"))
    }, 120000)
    pending.set(id, { resolve: resolve as (value: unknown) => void, reject, timer })
    window.chrome!.webview!.postMessage({ id, action, data })
  })
}

export function onNativeEvent(listener: (name: string, data: unknown) => void) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}
