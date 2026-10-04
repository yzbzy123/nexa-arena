import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { PageHeading } from "./shared"

const guides = [
  { title: "VALORANT 真实拉伸", steps: [
    "完全退出游戏，选择目标分辨率，再点击“修复游戏配置”。",
    "点击“开始真实拉伸”；画面进入中转模式后启动游戏。",
    "进入能看到武器与 HUD 的靶场或对局，再返回工具确认应用目标。",
    "游戏退出后自动恢复原分辨率、监视器和桌面图标；也可随时点击“恢复显示”。",
  ] },
  { title: "CS2 准星与画面", steps: [
    "复制新版准星导入码，进入游戏设置 → 准星/瞄准镜 → 分享或导入。",
    "竞技滤镜修改 Windows 显示输出；HDR 或驱动可能阻止生效。",
    "练习指令只用于本地房间；生成后先阅读内容，再复制或导出。",
  ] },
  { title: "灵敏度与游戏优化", steps: [
    "选择等转身距离、水平微调匹配或个人经验比例，填入 DPI 和当前灵敏度。",
    "游戏优化页先选择 CS2 或 VALORANT；进程优先级只影响当前运行的所选游戏。",
    "图形首选项按游戏设置；游戏模式和电源模式是全局选项，改动前请确认影响范围。",
  ] },
  { title: "系统调优与待机内存", steps: [
    "在游戏优化中选择游戏主程序和调优项目；应用时先保存原值，之后可勾选同一项目还原。",
    "内存与待机页的自动清理同时检查待机列表和空闲内存阈值，默认还要求 CS2 或 VALORANT 正在运行。",
    "阈值和定时器目标值会保存；新开工具时监控和精度请求保持关闭，点击后才启用。正常退出会释放定时器请求。",
  ] },
]

export function GuidePage() {
  return <>
    <PageHeading title="使用指南">按你正在做的事查步骤；出现异常时，先回到工具检查显示状态。</PageHeading>
    {guides.map((guide) => <Card className="panel-card" key={guide.title}>
      <CardHeader><CardTitle>{guide.title}</CardTitle></CardHeader>
      <CardContent><ol className="flex list-decimal flex-col gap-3 pl-5 text-sm leading-relaxed">
        {guide.steps.map((step) => <li key={step}>{step}</li>)}
      </ol></CardContent>
    </Card>)}
  </>
}
