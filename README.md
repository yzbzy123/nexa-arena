# Nexa Arena

免费的 CS2 / VALORANT Windows 游戏工具箱。

版本：**1.0.0** · 作者：**2ndWind** · QQ群：**1105050795**

## 功能

- VALORANT 显示比例与真实拉伸流程、配置修复、显示模式、监视器及桌面图标恢复。
- CS2 准心分享码、静态预览、画面滤镜和本地练习指令。
- 灵敏度与 DPI 换算：等转身距离、水平微调匹配、自定义比例。
- 输出设备、麦克风、应用音频会话、托盘与快捷键。
- 系统调优、启动项、计划任务、可选服务、DNS 和驱动高级属性。
- NVIDIA 配置、实验性 MSI 与中断亲和性、已识别的 NVIDIA 音频设备。
- 游戏进程方案、待机内存、定时器请求、负载检查及 Windows 检查/修复。
- 当前用户 APPX / MSIX 应用管理和本地调优方案。

具体操作影响见 [FEATURES.md](FEATURES.md)。

## 下载与运行

从 [Releases](https://github.com/yzbzy123/nexa-arena/releases) 下载完整压缩包，解压后运行 NexaArena.exe。

- Windows 10 / 11，64 位。
- 需要 .NET Framework 4.8 和 Microsoft Edge WebView2 Evergreen Runtime。
- EXE、WebView2 DLL、web 目录及授权文本必须放在一起。
- 系统设置和设备操作需要管理员权限。

工具不会添加未经确认的自动优化，不注入游戏，也不绕过反作弊。请按页面流程执行拉伸，并在设备调校前备份重要数据。

## 从源码构建

需要 PowerShell 7、Node.js 24、npm 和 Windows .NET Framework 编译器。

```powershell
pwsh -File ./tools/Setup-WebView2.ps1
npm ci --prefix web-ui
pwsh -File ./build.ps1 -NoPublish
```

构建结果在 dist-web。运行 build.ps1 可生成完整 release 运行包。

```powershell
pwsh -File ./tools/Test-Toolbox.ps1
pwsh -File ./tools/Test-Restore.ps1
```

Test-Toolbox.ps1 -NativeRead 增加本机只读枚举，不应用系统调优。界面预览通过 build-web.ps1 -Preview 生成。

## 操作与恢复边界

- 可逆设置写入前保存原值、写后校验；目标身份或外部现值变化时不擅自覆盖。
- DPAPI 原值备份受当前 Windows 用户保护，不能直接迁移到其他用户或机器。
- 应用卸载可能移除数据与安装文件；系统文件修复不能通过普通设置备份撤销。
- MSI 与中断路由属于实验性驱动配置，重启后可能出现设备异常。
- 配置读取不证明运行时效果；工具不保证帧率或延迟收益。

运行时设置位于 %LOCALAPPDATA%\\NexaArena。目录说明见 [WORKSPACE.md](WORKSPACE.md)，安全说明见 [SECURITY.md](SECURITY.md)。

## 许可证

项目代码采用 [MIT License](LICENSE)。第三方组件与相关声明遵循各自授权，详见 [第三方声明](assets/ThirdPartyNotices.txt)、WebView2 SDK 授权和运行包内前端授权文本。
