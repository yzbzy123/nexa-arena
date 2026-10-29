# 目录结构

```text
nexa-arena/
├─ NexaArena.cs       程序入口与显示模式基础逻辑
├─ ModernUI.cs       原生 WinForms 主窗体、主题与导航
├─ NativeDisplay.cs  显示设置、配置选择与人物比例对照
├─ NativeOptimizer.cs 原生优化、设备、进程与应用管理界面
├─ NativeCrosshairs.cs 原生准心库与静态预览
├─ *.cs              Windows 服务、游戏功能与共享控件
├─ app.manifest      权限与兼容性清单
├─ build.ps1         默认原生 WinForms 构建与本地打包
├─ build-web.ps1     可选 WebView2 宿主构建与打包
├─ assets/           图标、人物示意图、提示词与第三方声明
├─ tools/            回归测试、安全预览与开发工具
├─ PRODUCT.md        Windows 桌面产品边界
├─ DESIGN.md         原生设计令牌与组件规范
├─ .impeccable/      设计侧车、原生规格与验收记录
├─ web-ui/
│  ├─ src/           可选开发前端、组件与消息桥接
│  ├─ package.json   前端依赖与脚本
│  ├─ DESIGN.md      该前端的设计规范
│  └─ PRODUCT.md     该前端的产品边界
├─ FEATURES.md       功能与风险说明
├─ SECURITY.md       安全说明与报告方式
└─ LICENSE           项目许可证
```

## 原生入口与模块

- NexaArena.cs：程序入口；默认打开 ModernMainForm。build.ps1 不编译 WebView2 宿主。
- ModernUI.cs、ArenaDesign.cs、NativeDisplay.cs、NativeOptimizer.cs、NativeCrosshairs.cs、ToolboxPages.cs：原生主题、页面、布局与交互。
- ConfigSelection.cs、ValorantLocator.cs：账号配置文件选择、校验和定位。
- TrueStretchService.cs、RestoreService.cs、DesktopIconLayout.cs：显示流程、恢复与图标布局。
- Cs2Crosshair.cs、SensitivityProjection.cs、GameAudio.cs：准心、灵敏度与音频。
- SystemTuning.cs、OptimizationManagers.cs、DeviceTuning.cs、NvidiaGameSettings.cs：系统和设备设置。
- GameProcessProfiles.cs、MemoryCleaner.cs、AppxManagement.cs、ReadinessReport.cs、SystemRepairJobs.cs：进程方案、内存、应用包、诊断和修复。
- ArenaModules.cs、ToolboxCore.cs、DesktopIntegration.cs：共享功能、配置、快捷键与托盘集成。
- WebMainForm.cs、web-ui/、build-web.ps1：可选开发前端与网页宿主；其依赖不属于默认原生发行文件。

## 本地生成目录

`dist/NexaArena.exe` 是默认原生构建输出；`build.ps1` 的本地打包目标为 `release/NexaArena.exe`。`dist`、`release`、`dist-web`、`web-ui/dist` 和测试 EXE 均为构建输出；`web-ui/node_modules` 是可选前端的本地依赖。`artifacts` 用于临时 QA，不能作为发行依赖。生成物、本机备份与个人配置不提交到源码仓库。

运行时配置与原值备份保存在 `%LOCALAPPDATA%\NexaArena`。原生构建及回归命令见 [README.md](README.md)。
