# 目录结构

```text
nexa-arena/
├─ NexaArena.cs       程序入口与显示模式基础逻辑
├─ *.cs              Windows 后端、游戏功能与桌面宿主
├─ app.manifest      权限与兼容性清单
├─ build.ps1         完整构建入口
├─ build-web.ps1     WebView2 宿主构建与打包
├─ assets/           图标与第三方声明
├─ tools/            SDK 配置、回归测试和安全预览
├─ web-ui/
│  ├─ src/           页面、组件与消息桥接
│  ├─ package.json   前端依赖与脚本
│  ├─ DESIGN.md      设计规范
│  └─ PRODUCT.md     产品边界
├─ FEATURES.md       功能与风险说明
├─ SECURITY.md       安全说明与报告方式
└─ LICENSE           项目许可证
```

## 后端模块

- WebMainForm.cs：网页宿主和原生消息桥接。
- TrueStretchService.cs、RestoreService.cs、DesktopIconLayout.cs、ValorantLocator.cs：显示流程、恢复、图标布局和配置定位。
- Cs2Crosshair.cs、SensitivityProjection.cs、GameAudio.cs：准心、灵敏度与音频。
- SystemTuning.cs、OptimizationManagers.cs、DeviceTuning.cs、NvidiaGameSettings.cs：系统和设备设置。
- GameProcessProfiles.cs、MemoryCleaner.cs、AppxManagement.cs、ReadinessReport.cs、SystemRepairJobs.cs：进程方案、内存、应用包、诊断和修复。
- ModernUI.cs、ArenaDesign.cs、ArenaModules.cs、ToolboxPages.cs、ToolboxCore.cs、DesktopIntegration.cs：共享原生逻辑和 WinForms 回退界面。

## 本地生成目录

release 是完整运行包。dist-web、dist、web-ui/dist 和测试 EXE 是构建输出；web-ui/node_modules 是本地依赖。生成物、本机备份与个人配置不提交到源码仓库。

运行时配置与原值备份保存在 %LOCALAPPDATA%\\NexaArena。
