type Props = { width: number; height: number }

export function AspectSketch({ width, height }: Props) {
  const gcd = (a: number, b: number): number => b ? gcd(b, a % b) : a
  const divisor = gcd(width, height) || 1
  const aspect = width / height
  const stretch = (16 / 9) / aspect
  const ratioLabel = width / divisor + ":" + height / divisor
  const circleRadius = Math.min(27, 110 / stretch)
  const ellipseWidth = circleRadius * stretch
  return <figure>
    <svg className="aspect-diagram" viewBox="0 0 600 225" role="img"
      aria-label={`16:9 默认参照与 ${ratioLabel} 全屏拉伸到 16:9 对比，横向缩放 ${stretch.toFixed(3)} 倍`}>
      <text x="145" y="24" textAnchor="middle" fill="var(--muted-foreground)" fontSize="13">16:9 默认参照</text>
      <text x="455" y="24" textAnchor="middle" fill="var(--muted-foreground)" fontSize="13">{ratioLabel} 全屏拉伸</text>
      <rect x="20" y="42" width="250" height="140.625" rx="3" fill="var(--muted)" stroke="var(--input)" />
      <rect x="330" y="42" width="250" height="140.625" rx="3" fill="var(--muted)" stroke="var(--input)" />
      <circle cx="145" cy="112.3125" r={circleRadius} fill="var(--accent)" stroke="var(--primary)" strokeWidth="1.5" />
      <ellipse cx="455" cy="112.3125" rx={ellipseWidth} ry={circleRadius} fill="var(--accent)" stroke="var(--primary)" strokeWidth="1.5" />
      <path d="M280 112H319M311 107L319 112L311 117" fill="none" stroke="var(--muted-foreground)" strokeWidth="1.3" />
      <text x="145" y="207" textAnchor="middle" fill="var(--muted-foreground)" fontSize="12">相同图形 · 保持原始宽高</text>
      <text x="455" y="207" textAnchor="middle" fill="var(--primary)" fontSize="12">横向 {stretch.toFixed(3)}× · 纵向 1×</text>
    </svg>
    <figcaption className="aspect-summary"><span>{width} × {height} → 16:9 显示器铺满</span><span>几何缩放示意，游戏视野由游戏决定</span></figcaption>
  </figure>
}
