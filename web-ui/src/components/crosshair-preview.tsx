import { useEffect, useRef } from "react"
import type { CrosshairData } from "@/lib/types"
import { rescaleCrosshair, staticCrosshairPixels } from "@/lib/crosshair-renderer"

export function CrosshairPreview({ data, gameWidth, gameHeight, outputWidth, outputHeight, stretched = true, zoom = 1, thumbnail = false, name }: {
  data: CrosshairData; gameWidth: number; gameHeight: number; outputWidth: number; outputHeight: number;
  stretched?: boolean; zoom?: number; thumbnail?: boolean; name: string
}) {
  const ref = useRef<HTMLCanvasElement>(null)
  useEffect(() => {
    const canvas = ref.current
    if (!canvas) return
    const draw = () => {
      const bounds = canvas.getBoundingClientRect(), dpr = window.devicePixelRatio || 1
      canvas.width = Math.max(1,Math.round(bounds.width*dpr)); canvas.height = Math.max(1,Math.round(bounds.height*dpr))
      const ctx = canvas.getContext("2d")
      if (!ctx) return
      ctx.fillStyle = "#202936";ctx.fillRect(0,0,canvas.width,canvas.height)
      const scaled = rescaleCrosshair(data,gameHeight)
      const extent = Math.max(scaled.gap+scaled.length+scaled.thickness+3,scaled.thickness+3)
      const crop = Math.max(32,Math.ceil(extent*2/2)*2)
      const source = document.createElement("canvas");source.width=source.height=crop
      const pixelContext=source.getContext("2d")
      if (!pixelContext) return
      pixelContext.putImageData(new ImageData(staticCrosshairPixels(scaled,crop),crop,crop),0,0)
      const sx = stretched ? outputWidth/gameWidth : 1, sy = stretched ? outputHeight/gameHeight : 1
      const pixelZoom = thumbnail ? 1 : zoom
      const width=crop*sx*pixelZoom, height=crop*sy*pixelZoom
      ctx.imageSmoothingEnabled = stretched && (!Number.isInteger(sx*pixelZoom) || !Number.isInteger(sy*pixelZoom))
      ctx.drawImage(source,Math.floor(canvas.width/2-width/2),Math.floor(canvas.height/2-height/2),width,height)
    }
    draw()
    const observer = new ResizeObserver(draw);observer.observe(canvas)
    return () => observer.disconnect()
  }, [data,gameWidth,gameHeight,outputWidth,outputHeight,stretched,zoom,thumbnail])
  return <canvas ref={ref} className={thumbnail ? "crosshair-thumb" : "crosshair-stage"} role="img" aria-label={`${name}，${gameWidth}×${gameHeight}，${stretched ? "全屏拉伸" : "原始像素"}静态准星，${zoom}倍观察`} />
}
