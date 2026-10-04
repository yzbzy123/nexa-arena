---
name: Nexa Arena
description: 浅色精密工作台，统一全应用的操作界面。
colors:
  primary: "#355adb"
  background: "#f4f5f7"
  foreground: "#212a36"
  card: "#fff"
  secondary: "#eef1f6"
  secondary-foreground: "#273344"
  muted: "#edf0f5"
  muted-foreground: "#606b7a"
  accent: "#e8edfc"
  accent-foreground: "#264bbb"
  destructive: "#b42332"
  border: "#d9dfe7"
  input: "#cbd3df"
  ring: "#708ce8"
  success: "#23734b"
typography:
  headline:
    fontFamily: "'Manrope Variable','Microsoft YaHei UI',sans-serif"
    fontSize: "24px"
    fontWeight: 650
    lineHeight: 1.2
    letterSpacing: "-0.025em"
  title:
    fontFamily: "'Manrope Variable','Microsoft YaHei UI',sans-serif"
    fontSize: "15px"
    fontWeight: 500
    lineHeight: 1.375
  section:
    fontFamily: "'Manrope Variable','Microsoft YaHei UI',sans-serif"
    fontSize: "14px"
    fontWeight: 650
  body:
    fontFamily: "'Manrope Variable','Microsoft YaHei UI',sans-serif"
    fontSize: "13.125px"
    lineHeight: 1.428571
  caption:
    fontFamily: "'Manrope Variable','Microsoft YaHei UI',sans-serif"
    fontSize: "12px"
    lineHeight: 1.65
  label:
    fontFamily: "'Manrope Variable','Microsoft YaHei UI',sans-serif"
    fontSize: "11px"
  button:
    fontFamily: "'Manrope Variable','Microsoft YaHei UI',sans-serif"
    fontSize: "13.125px"
    fontWeight: 500
    lineHeight: 1.428571
  readout:
    fontFamily: "'Manrope Variable','Microsoft YaHei UI',sans-serif"
    fontSize: "16px"
    fontWeight: 650
  result:
    fontFamily: "'Manrope Variable','Microsoft YaHei UI',sans-serif"
    fontSize: "clamp(27px,3vw,38px)"
    fontWeight: 650
    letterSpacing: "-0.03em"
  code:
    fontFamily: "Consolas,monospace"
    fontSize: "11px"
    lineHeight: 1.8
rounded:
  sm: "2px"
  md: "4px"
  lg: "5px"
  xl: "6px"
  4xl: "12px"
spacing:
  control: "8px"
  row: "12px"
  card: "15px"
  field: "17px"
  section: "18px"
  split: "20px"
  page: "24px"
  gutter: "26px"
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.card}"
    typography: "{typography.button}"
    rounded: "{rounded.lg}"
    padding: "0 9.375px"
    height: "30px"
  button-outline:
    backgroundColor: "{colors.background}"
    textColor: "{colors.foreground}"
    typography: "{typography.button}"
    rounded: "{rounded.lg}"
    padding: "0 9.375px"
    height: "30px"
  button-secondary:
    backgroundColor: "{colors.secondary}"
    textColor: "{colors.secondary-foreground}"
    typography: "{typography.button}"
    rounded: "{rounded.lg}"
    padding: "0 9.375px"
    height: "30px"
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.foreground}"
    typography: "{typography.button}"
    rounded: "{rounded.lg}"
    padding: "0 9.375px"
    height: "30px"
  button-destructive:
    textColor: "{colors.destructive}"
    typography: "{typography.button}"
    rounded: "{rounded.lg}"
    padding: "0 9.375px"
    height: "30px"
  button-link:
    textColor: "{colors.primary}"
    typography: "{typography.button}"
    rounded: "{rounded.lg}"
    padding: "0 9.375px"
    height: "30px"
  input:
    backgroundColor: "transparent"
    textColor: "{colors.foreground}"
    typography: "{typography.body}"
    rounded: "{rounded.lg}"
    padding: "3.75px 9.375px"
    height: "30px"
  navigation:
    backgroundColor: "transparent"
    rounded: "{rounded.md}"
    height: "40px"
    width: "100%"
  navigation-active:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.primary}"
    rounded: "{rounded.md}"
    height: "40px"
    width: "100%"
  badge-outline:
    backgroundColor: "transparent"
    textColor: "{colors.foreground}"
    rounded: "{rounded.4xl}"
    padding: "1.875px 7.5px"
    height: "18.75px"
  badge-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.card}"
    rounded: "{rounded.4xl}"
    padding: "1.875px 7.5px"
    height: "18.75px"
  card:
    backgroundColor: "{colors.card}"
    textColor: "{colors.foreground}"
    typography: "{typography.body}"
    rounded: "{rounded.xl}"
    padding: "15px"
---

# Design System: Nexa Arena

## Overview

**Creative North Star: "浅色精密工作台"**

Nexa Arena 采用浅色精密工作台：窄白色导航、冷灰工作区和不透明白色面板建立清晰的操作环境；蓝色集中在动作、当前选择和关键数值上。整体紧凑、克制、可扫描，中文内容与数字读数保持同等可读性。

各页面共享字体、输入控件、分隔线与留白语言。层级来自字号、内容分组和相邻区域的关系；准星与比例图是运行时数据绘制的预览，界面本身由代码构成。当前实现不使用产品位图底板，开发截图仅作为验收证据。

**Key Characteristics:**

- 窄白色导航与冷灰工作区形成稳定框架。
- 小圆角、细分隔线和不透明白色面板。
- 蓝色承担动作、选择与数值强调。
- Manrope 数字与中文系统字体共同构成紧凑阅读层级。
- 滚动工作区与持续可见的页脚保持独立。

规范性 token 位于上述 frontmatter，对应 `src/index.css`、`src/App.tsx` 和 `src/components/ui/*`。各页面遵循相同的导航、字体、输入和状态语言；布局以实际源码为准。

## Colors

冷中性灰承托白色工作面，精密蓝提供连续的交互标识。token 名沿用实现中的语义名称，避免另建不兼容的色彩字典。

### Primary

- **精密蓝**（primary）：主动作、当前导航项、选择边框、链接和关键换算结果。
- **选择浅蓝**（accent）与**选择深蓝**（accent-foreground）：选中背景及菜单焦点文字。
- **焦点蓝**（ring）：键盘焦点边框与半透明焦点环。

### Neutral

- **冷灰工作区**（background）：内容画布和粘性工具栏的底色。
- **不透明白**（card）：导航、顶栏、页脚、工作面板和弹出内容；主按钮文字复用相同白色。
- **石墨正文**（foreground）：标题、正文、分组和主要读数。
- **辅助浅灰**（secondary）与**辅助石墨**（secondary-foreground）：次级按钮。
- **状态浅灰**（muted）与**说明灰**（muted-foreground）：中性悬停、说明、标签和次要状态。
- **细分隔灰**（border）：区域分隔与轮廓；**输入轮廓灰**（input）使编辑区更易定位。

### Semantic status

- **错误红**（destructive）：错误、无效输入以及组件库的 destructive 变体。
- **完成绿**（success）：已完成步骤和操作结果文字。状态文字应与色彩共同出现。

**The 状态可读 Rule.** 当前项同时保留色彩与文字、边框或位置标识；焦点环与禁用态必须可辨认。

侧车中的八阶 tonalRamp 为设计面板的色彩展示而合成，不是新增的实现 token，也不授权扩展应用调色板。

## Typography

**Display / Body Font:** Manrope Variable，中文回退为 Microsoft YaHei UI，最终回退为 sans-serif。字体通过 Fontsource 导入；整个应用共用一个字体族。**Code Font:** Consolas，回退为 monospace。

根字号为（15px）。Tailwind 组件尺寸按该根字号解析：常规按钮与正文为 body/button 角色，卡片标题为 title 角色。没有单独的营销式超大 display 层级。

### Hierarchy

- **Headline**：页面一级标题；紧凑字距与中等粗度建立页面入口。
- **Title**：工作面板标题；常规卡片使用较轻字重。
- **Section**：面板内部的小分组及准星检查器标题。
- **Body**：控件与卡片常规内容；根字号同时供小窗口输入框使用。
- **Caption**：说明文字；页面引导使用（12px、1.6 行高），普通区段说明使用 caption 的行高。页面引导最长（72ch）。
- **Label**：读数标签、列表次要信息和步骤文本；品牌副标题、页脚、作用域标签等更低层级信息使用（10px）。
- **Readout**：状态条数据；内存读数使用（18px、650 字重）。
- **Result**：关键换算结果的响应式字号。数字规模改变时保留相邻说明和操作的层级。
- **Code**：可复制代码文本，允许长字符串换行。

**The 数字对齐 Rule.** 状态、分辨率、内存读数与换算结果使用等宽数字；代码文本使用独立等宽字体。

## Layout

应用壳体占满宿主视口。桌面采用（172px）白色导航和剩余宽度工作区；导航独立纵向滚动，右侧包含紧凑顶栏和可滚动内容。顶栏最小高度（49px），水平留白（24px）。工作区默认内边距为（24px 26px 30px），内容最大宽度（1320px）并居中，页面分组间距使用 section。

页脚跨越两列，最小高度（33px），通过外层 grid 的独立行持续留在视口底部；正文滚动不带走页脚。Sonner 通知容器是壳体外的同级节点，位于右下角，不占据应用 grid 的行列。

常规双列采用较宽编辑区与较窄状态区，默认比例（1.3 : 0.7），右列最小宽度（290px），列间距使用 split。表单配对字段采用等宽两列与 field 间距。列表以连续行和细分隔线组织；不要给每一行新增嵌套卡片。准星列表与检查器采用相邻列，检查器桌面顶端粘住，数据选择更新旁边预览而不重新安排行动区。

### Responsive behavior

| 视口上限 | 当前实现 |
| --- | --- |
| 1080px | 导航压至 146px，页面留白 20px，双列间距 16px；准星检查器宽度 270px。 |
| 860px | 导航压至 64px 图标栏，隐藏品牌文字、导航文字与版本；保留 aria-label 和 title。常规双列及配对字段改单列，顶栏次要读数隐藏。 |
| 680px | 准星布局改单列，检查器取消粘性；页面留白 18px。页脚可换行，优化操作栏转为普通单列布局，列表徽章移到正文下方。 |
| 460px | 导航移到顶部一行图标，移除侧栏边框并加底部分隔；品牌隐藏，步骤改为纵向。壳体采用导航、内容、页脚三行。 |

这些断点是当前应用的响应式行为，不覆盖组件库本身的输入字号断点。输入在组件库的（768px）断点以上使用 body 字号，以下使用根字号以保持编辑可读性。

## Elevation & Depth

**The 平面工作区 Rule.** 静态工作面板不使用浮起阴影；以白色面、细轮廓和分隔线建立结构。

工作面板使用不透明白色；状态条和列表采用 border 的（1px）实线轮廓。基础卡片组件带 foreground 的低透明度 ring，但当前页面的普通工作面板使用 `box-shadow:none` 覆盖它，所以它们依靠白色面与留白分组。内容深度来自面色、间距、细分隔线和选中状态。

### Shadow Vocabulary

- **输入焦点**：ring 的（50%）透明度形成（3px）外环，并改变边框；用于按钮、输入和可聚焦组件。
- **选择弹层**：组件库的中等阴影为 `0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1)`，同时具有轻轮廓，表示临时覆盖关系。
- **模态层**：不透明白色内容、轻轮廓和（10%）黑色遮罩；当前组件在支持 backdrop-filter 时给遮罩轻模糊。此处理属于临时模态遮罩，工作面板保持不透明。

控件默认状态过渡为（150ms、cubic-bezier(0.4, 0, 0.2, 1)）；弹层开合使用（100ms）渐隐及轻缩放。按钮按下移动（1px）。全局 prefers-reduced-motion 会关闭动画与过渡。

## Shapes

工作面板与列表采用小圆角；输入和普通按钮使用 lg，紧凑按钮及导航项使用 md，卡片使用 xl。轮廓与分隔线保持细而清晰，不制造厚边框。

徽章是明确例外：组件库沿用 4xl 的胶囊式小标签。开关同样保留圆润轨道。它们的形状用于状态或开关识别，不扩散到大面积工作容器。准星缩略图为（3px）圆角，预览台为 md；这是数据预览框的现有形状。

## Components

组件应紧凑、直接，视觉反馈围绕实际交互状态。侧车包含十个可直接放入 Shadow DOM 的 HTML/CSS 示例，使用当前 CSS token 与原生标签；不依赖 React、Tailwind、图标包或额外位图。

### Buttons

普通按钮高度见 frontmatter；小按钮为（26.25px）、大按钮为（33.75px），关键操作区现有主按钮最小高度（41px），辅助操作最小高度（35px）。图标一般为（15px），紧凑按钮图标更小。

- **Primary**：精密蓝底白字；悬停使用主色（80%）透明度。
- **Outline**：冷灰工作区底色与细轮廓；悬停进入 muted。主要辅助动作在当前页面广泛使用这一变体。
- **Ghost**：透明底，悬停进入 muted；导航使用其专门的状态覆盖。
- **Secondary**：辅助浅灰底与辅助石墨文字；悬停将底色与 foreground 的（5%）混合。
- **Destructive**：错误红的（10%）浅底与错误红文字，悬停升到（20%）。
- **Link**：精密蓝文字，悬停加下划线。secondary、destructive 和 link 是当前组件库已有的可用变体，页面动作仍以 primary、outline、ghost 为主。
- **Focus / Disabled / Invalid**：保留可见焦点环；禁用透明度（50%）并阻止交互；无效输入或按钮显示错误边框和错误外环。

### Chips / Badges

小型状态标记，outline 用于中性状态，primary 用于已应用或当前状态；secondary、destructive、ghost、link 亦为已有库变体。标签默认静态，只有作为链接使用时才应用相应悬停；不将所有标签塑造成按钮。示例同时呈现 outline 与 primary。

### Cards / Containers

白色、小圆角，默认卡片内部间距使用 card。基础卡片的低透明度 ring 在当前工作面板中被取消。小尺寸卡片的组件内边距为（11.25px）。常规卡片以标题、说明和内容建立分组；列表容器将细分隔线放在行间。状态条使用更明确的实线轮廓和 readout 数字。

### Inputs / Fields

透明底、输入轮廓灰边框，小圆角；置于白色面板后自然呈白底。输入内容与标签明确对应。聚焦改变边框并增加焦点环；aria-invalid 呈错误状态，disabled 呈较淡底色与透明度。下拉框使用同一触发器语言，弹出菜单的焦点项使用 accent。

### Navigation

导航项全宽，桌面高度见 frontmatter，字号（12px）、字重（550）、图标（16px）。默认文字为（#526074）；悬停进入 secondary 与 foreground，当前项进入 accent 与 primary。当前页保留 aria-current，图标栏仍有可访问名称和 title。顶部小窗口导航图标按钮为（39px × 38px）。

页内 tabs 使用线式变体：透明背景、muted 级默认文字、foreground 当前文字和底部（1.875px）实线标识。它们沿用同一字体与焦点语言，而不复制侧导航块状选中背景。

### Data surfaces

准星缩略图和检查台使用局部深色预览底，作为数据对比面；不改变全应用的浅色世界。图形由像素规则或 SVG 运行时绘制。列表选择使用浅蓝背景与 aria-pressed；步骤由序号、当前/完成文字颜色及边框显示状态。预览、状态与操作各有稳定位置。

## Do's and Don'ts

### Do:

- Do 在所有页面复用同一字体、色彩、输入框和按钮语言。
- Do 使用白色面板与细分隔线组织内容，保留冷灰工作区。
- Do 保留蓝色动作、清晰焦点环、禁用态与可读的状态文字。
- Do 在窄窗口按现有断点压缩导航并折叠工作列，允许内容区域滚动。
- Do 使用运行时数据绘制预览，并将开发截图留作验收证据。

### Don't:

- Don't 恢复用户已替换的大块深色侧栏、过大标题和大圆角卡片堆叠。
- Don't 将小徽章或开关的圆润形状扩展为工作面板的形状。
- Don't 用装饰阴影、渐变底板或产品位图替代当前的平面工作区。
- Don't 将页面专属内容、功能清单或能力承诺写成全局设计规则。
