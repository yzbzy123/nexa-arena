---
name: Nexa Arena
description: 原生 Windows 浅色游戏准备工作台，以明确选择、执行核验和可见恢复为中心。
colors:
  primary: "#1f262f"
  accent: "#0066cc"
  accent-soft: "#e8f1fb"
  background: "#f4f7fa"
  card: "#ffffff"
  border: "#e0e5eb"
  text: "#1f262f"
  muted: "#616d79"
  disabled: "#e4e8ed"
  success: "#177d5e"
typography:
  headline:
    fontFamily: "Microsoft YaHei UI"
    fontSize: "18pt"
    fontWeight: 700
  title:
    fontFamily: "Microsoft YaHei UI"
    fontSize: "11pt"
    fontWeight: 700
  body:
    fontFamily: "Microsoft YaHei UI"
    fontSize: "10pt"
    fontWeight: 400
  label:
    fontFamily: "Microsoft YaHei UI"
    fontSize: "8.5pt"
    fontWeight: 400
  button:
    fontFamily: "Microsoft YaHei UI"
    fontSize: "9.5pt"
    fontWeight: 400
  code:
    fontFamily: "Consolas"
    fontSize: "9pt"
    fontWeight: 400
rounded:
  preset: "6px"
  button: "8px"
  card: "12px"
spacing:
  compact: "8px"
  inset: "12px"
  group: "16px"
  pane: "18px"
  card: "20px"
  page: "24px"
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.card}"
    typography: "{typography.button}"
    rounded: "{rounded.button}"
    padding: "4px 12px"
  button-secondary:
    backgroundColor: "{colors.card}"
    textColor: "{colors.text}"
    typography: "{typography.button}"
    rounded: "{rounded.button}"
    padding: "4px 12px"
  choice:
    backgroundColor: "{colors.background}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
  numeric-field:
    backgroundColor: "{colors.background}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
  navigation-selected:
    backgroundColor: "{colors.accent-soft}"
    textColor: "{colors.accent}"
    typography: "{typography.body}"
    height: "44px"
  card:
    backgroundColor: "{colors.card}"
    textColor: "{colors.text}"
    rounded: "{rounded.card}"
    padding: "{spacing.card}"
  tuning-row:
    backgroundColor: "{colors.card}"
    textColor: "{colors.text}"
    padding: "5px 10px"
    height: "84px"
  preset-selected:
    backgroundColor: "{colors.accent-soft}"
    textColor: "{colors.text}"
    rounded: "{rounded.preset}"
  workflow:
    backgroundColor: "{colors.background}"
    textColor: "{colors.text}"
    height: "76px"
---

# Design System: Nexa Arena

## Overview

**Creative North Star: "原生 Windows 游戏准备工作台"**

浅灰框架、白色工作面与清晰的分组构成一个安静、紧凑的本地工具台。顶部选择当前游戏，左侧定位工具，内容区先呈现操作对象，再呈现设置、执行状态与恢复入口。深炭色突出当前主要操作，蓝色标记选择、步骤和可查看的详情。

界面以原生 WinForms 的输入、列表、键盘焦点和系统对话框为基础，自绘导航、步骤条与按钮保持一致的边界和反馈。显示页将目标、等高人物对照和配置操作整合在同一工作面；优化页以分类、搜索、分组设置行和详情检查区帮助逐项判断。视觉层级服务于核对和执行，不承诺游戏效果。

**Key Characteristics:**

- 浅色原生桌面框架，顶部游戏范围与左侧工具导航。
- 深炭色主要操作，蓝色选择反馈，清楚的禁用状态。
- 对象、影响范围、结果和恢复入口保持可见。

源码是尺寸和状态的实现依据：`ModernUI.cs`、`ArenaDesign.cs`、`NativeDisplay.cs`、`NativeOptimizer.cs`、`NativeCrosshairs.cs` 与 `ToolboxPages.cs`。尺寸以 96 DPI 设计基准记录；字体保持源码的点单位。

## Colors

深炭色承载执行重点，单一蓝色建立选择与流程秩序，白色和冷灰色区分工作区域。

### Primary

- **深炭执行色**（`primary`）：显示流程当前主要操作、工具主按钮和“应用勾选项”。
- **Windows 蓝**（`accent`）：当前导航、游戏范围、步骤、预设边框和详情链接。
- **浅蓝选择面**（`accent-soft`）：当前导航、当前游戏、分辨率预设、正在检查的设置行和次要按钮悬停。

### Neutral

- **冷灰背景**（`background`）：应用框架、步骤条、快捷键条、部分字段和显示页操作面。
- **白色工作面**（`card`）：内容区、普通卡片、设置行和次要按钮。
- **浅灰边界**（`border`）：自绘卡片和次要按钮边框。
- **深灰正文**（`text`）：标题、字段和可操作内容。
- **中灰辅助文**（`muted`）：说明、路径提示、状态补充和禁用文字。
- **禁用灰面**（`disabled`）：不可执行的自绘按钮底色。

`success` 为源码中的成功/启用状态绿色；仅用于状态，不替代选择色。

侧车的八步色阶由上述颜色推导，用于文档预览，不扩展原生主题。

**The State Rule.** 颜色必须伴随文字、勾选、下划线或明确的启用状态，不能单独承担操作结果。

## Typography

中文界面统一使用 Microsoft YaHei UI，保证 Windows 原生字体与中文控件兼容；代码输出使用 Consolas。这是桌面字体选择，不引入网页字体。页面标题采用 `headline`，品牌文字为粗体（14pt），分组标题采用 `title`，优化详情标题为粗体（12pt），设置行标题为粗体（10pt）。字段与导航采用 `body`，页头补充采用 `label`，按钮采用 `button`；工具说明常规（9pt），侧栏作者与群号常规（8pt）。

保持中文自然字距，不对中文标签强加全大写或装饰性字距。导航和选择框使用省略号处理长内容；关键路径在详情区或文件选择器中保留核对方式。

## Layout

原生窗体默认客户区（1380 × 900），最小窗体 `MinimumSize`（1200 × 800），两者不是同一尺寸口径。窗体使用 DPI 自动缩放；不把最小宽度称为网页断点。

顶部命令栏高（62px），左侧导航宽（210px），右侧页标题区高（64px）。顶部居中提供 VALORANT / CS2 游戏范围，右侧显示版本（1.0.0）或安全预览状态。导航项高（44px），随侧栏可用宽度伸展，标签可省略。侧栏底部信息区高（100px），展示免费工具箱、作者 2ndWind 与QQ群 1105050795，点击复制群号。

显示页外边距为左右（22px）、上（12px）、下（16px）。顶端三步流程区域高（76px），中间工作区按目标与预览（65%）、配置与操作（35%）分栏。左右面板内边距均为（18px），两面板间距（14px）。底部快捷键区域高（44px）。三个预设横向等分，人物对照占据剩余高度，自定义尺寸和全部模式位于对照下方。右侧依次呈现配置选择、扫描与手动定位、修复、应用、自动恢复偏好、恢复入口和结果。

优化页外边距为左右（22px）、上（8px）、下（12px）。系统调优页将类别、搜索、刷新与选择操作放在上方；设置列表和检查区按（66% / 34%）分栏。分组标题高（32px），设置行高（84px）、内边距上下（5px）左右（10px），行底使用分隔线。固定底部操作区高（48px），列表单独滚动，应用、还原和方案操作保持可达。管理页对象列表与当前设置/影响详情按（53% / 47%）分栏。

其他工具采用原生列表、子页签或纵向滚动卡片。`ResponsiveStack` 使用左右及底部（24px）、顶部（20px）内边距；普通卡片间距（16px）。页面布局以对应源码为准，紧凑窗体通过已有伸缩、滚动与省略规则工作，不新增页面重排。

## Elevation & Depth

工作面保持平面，通过白/灰色差、单线边框和间距分组，不使用应用内阴影或透明模糊。系统标题栏、菜单和对话框由 Windows 绘制。悬停、按下和焦点立即响应，没有入场动画。

## Shapes

自绘按钮采用温和圆角，普通卡片使用更宽圆角，分辨率选择使用较紧圆角，见前述令牌。优化设置行保持平整直角面与底部分隔线。原生输入、列表、复选框和系统对话框保留系统形状，不把所有内容改成独立卡片。

## Components

### Buttons

当前主要操作使用深炭底白字；次要按钮使用白底深色文字和边界。自绘文字的绘制内缩为上下（4px）、左右（12px）。白色按钮悬停使用浅蓝；其他颜色按 `ControlPaint.Light` 处理，按下按 `ControlPaint.Dark` 处理。键盘焦点显示 Windows 焦点框；禁用按钮使用独立灰面和辅助文字色。

显示流程会依据真实配置与会话状态切换主操作重点。缺少配置时不可修复或应用；未修复时不可应用。恢复入口由已保存会话控制，不能为视觉一致而始终启用。

### Cards / Containers

`ModernCard` 使用不透明工作面、单线边框及卡片内边距；显示页右侧使用冷灰操作面。优化设置行包含勾选标题、文字状态、说明、作用范围和“查看详情”；当前检查行使用浅蓝背景，详情区呈现生效方式、当前值、目标值、原值保存说明及错误。说明、字段、结果与操作按功能分组；错误与作用范围必须在相应操作附近可读。

分类和搜索只改变可见设置行，勾选由完整选择模型保存；不支持项目不可勾选。检查项与勾选项分别表达正在阅读和准备执行。勾选和导入方案只改变选择，只有明确的应用操作才会执行设置。

### Inputs / Fields

选择框采用只选列表、冷灰背景和常规正文；项目行高为源码的（24px）。数字框与原生文本框保留系统编辑、选择和焦点行为。字段有明确名称；文件配置提供重新扫描和手动选择。搜索按标题、说明和类别筛选，无匹配结果时提供调整提示。不要只依赖提示文字说明输入意义。

### Navigation

主导航在浅色侧栏纵向排列，使用线条图标和文字；当前页同时显示浅蓝背景和蓝色图文。顶部游戏范围使用两个并排按钮，选中面和文字随底层游戏模型同步；操作繁忙期间锁定，完成后恢复。页内工具按钮和优化页签用蓝色文字与下划线标记当前页。键盘焦点框保持可见，列表沿用 Windows 交互。

### Proportion Comparison

三步流程条呈现“核对配置 → 应用拉伸 → 恢复桌面”，副标题随真实修复、应用和恢复会话更新。快捷键条提供 Ctrl + Alt + S 切换与 Ctrl + Alt + H 恢复。

使用 `assets/aspect-model.png` 中同一人物、姿势和高度。两块浅灰画面不加黑边；右侧只将人物宽度乘以 `(16/9) / (目标宽/目标高)`，同时显示比例与倍率。说明必须指出这是比例示意，不代表游戏实际视野。

## Do's and Don'ts

### Do:

- **Do** 从原生主题和对应页面源码复用颜色、字体、尺寸与状态。
- **Do** 同时保留操作对象、影响范围、结果和恢复方式。
- **Do** 使用相同人物与相同高度，只按目标宽高比调整右侧宽度。
- **Do** 检查默认与紧凑窗体下的内容、焦点、列表和底部操作。

### Don't:

- **Don't** 添加装饰动画、透明模糊或应用内阴影。
- **Don't** 把设计样稿的示意值当作当前机器的读取结果。
- **Don't** 用人物示意或配置读数宣称实际视野、帧率、延迟或兼容性收益。
- **Don't** 隐藏失败、受保护/不支持状态，或把载入方案当作自动应用。
