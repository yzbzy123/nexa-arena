import type { CrosshairData } from "./types"

export type PixelRect = { x0: number; y0: number; x1: number; y1: number }
const roundAway = (n: number) => Math.sign(n) * Math.floor(Math.abs(n) + .5)

export function rescaleCrosshair(data: CrosshairData, height: number): CrosshairData {
  const result = { ...data, screenHeight: height }
  if (height === data.screenHeight) return result
  const factor = Math.fround(height / data.screenHeight)
  for (const key of ["gap", "length", "thickness", "dynamicSpreadLimit", "splitDistance"] as const) {
    const value = data[key]
    result[key] = value > 0 ? Math.max(1, roundAway(Math.fround(value * factor)))
      : value < 0 && key === "gap" ? Math.min(-1, roundAway(Math.fround(value * factor))) : value
  }
  return result
}

export function staticCrosshairRects(data: CrosshairData, center: number): PixelRect[] {
  if (data.style !== 4 && data.style !== 6) throw new Error("这个样式需要独立的动态或曲线渲染器。")
  const result: PixelRect[] = []
  const t = data.thickness, length = data.length, gap = Math.max(0, data.gap)
  if (t <= 0) return result
  const before = Math.floor((t + 1) / 2), after = t - before
  const oddOffset = before === after ? 0 : -1
  const add = (x0: number, y0: number, x1: number, y1: number) => result.push({ x0, y0, x1, y1 })
  if (data.centerDot || data.style === 6) add(center-before,center-before,center+after,center+after)
  if (data.style === 4 && length > 0) {
    add(center-gap-length,center-before,center-gap,center+after)
    add(center+gap+oddOffset,center-before,center+gap+oddOffset+length,center+after)
    if (!data.tStyle) add(center-before,center-gap-length,center+after,center-gap)
    add(center-before,center+gap+oddOffset,center+after,center+gap+oddOffset+length)
  }
  return result
}

const toLinear = (byte: number) => {
  const x = byte / 255
  return x <= .04045 ? x / 12.92 : ((x + .055) / 1.055) ** 2.4
}
const toSrgb = (value: number) => Math.round(Math.max(0, Math.min(1,
  value <= .0031308 ? value * 12.92 : 1.055 * value ** (1 / 2.4) - .055)) * 255)

export function staticCrosshairPixels(data: CrosshairData, size: number, background: number[] = [32,41,54]): Uint8ClampedArray<ArrayBuffer> {
  const rectangles = staticCrosshairRects(data, Math.floor(size / 2))
  const bytes = new Uint8ClampedArray(size * size * 4)
  const fillRgb = [data.r,data.g,data.b].map(toLinear)
  const outlineRgb = [data.outlineR,data.outlineG,data.outlineB].map(toLinear)
  const ground = background.map(toLinear)
  const leading = data.outlineMode ? 1 : 0, trailing = data.outlineMode === 1 ? 1 : 0
  for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
    const fill = rectangles.some(r => x >= r.x0 && x < r.x1 && y >= r.y0 && y < r.y1) ? data.a / 255 : 0
    const outline = data.outlineMode && rectangles.some(r => x >= r.x0-leading && x < r.x1+trailing && y >= r.y0-leading && y < r.y1+trailing) ? data.outlineA / 255 : 0
    // CS2's crosshair shader attenuates the outline before its final fill blend.
    const behind = outline * (1-fill) * (1-fill), alpha = fill + behind
    const offset = (y*size+x)*4
    for (let channel = 0; channel < 3; channel++)
      bytes[offset+channel] = toSrgb(fillRgb[channel]*fill + outlineRgb[channel]*behind + ground[channel]*(1-alpha))
    bytes[offset+3] = 255
  }
  return bytes
}
