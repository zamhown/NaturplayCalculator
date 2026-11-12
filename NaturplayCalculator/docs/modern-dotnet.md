# .NET 10、WinUI 3 与跨平台边界

项目先按 [UWP 现代 .NET 迁移指南](https://learn.microsoft.com/zh-cn/windows/uwp/dotnet-native/modernize-uwp-apps-with-dotnet) 升级运行时，再迁移到 WinUI 3。当前 Windows 宿主为 .NET 10 + Windows App SDK 2.6.5 + MSIX 桌面应用，使用 Microsoft.UI.Xaml，已不再使用 UWP XAML 或 AppContainer。WinUI 3 本身仍是 Windows UI；跨平台复用来自下列独立库。

## 项目与复用

| 项目 | 目标 | 平台边界 |
| --- | --- | --- |
| NaturplayCalculator | net10.0-windows10.0.26100.0 | WinUI 3 窗口、Acrylic、设置、存储、剪贴板、导出、协议激活 |
| Naturplay.Tex.WinUI | net10.0-windows10.0.26100.0 | WinUI 3 公式控件、Win2D 字体度量、键盘与指针适配 |
| NaturplayCalculator.Core | net10.0 | 数学、输入编辑、表达式、文档解析、会话模型与编解码 |
| Naturplay.Tex | net10.0 | TeX 解析、布局、光标几何、绘图命令与 SVG |
| NaturplayCalculator.Presentation | net10.0 | 公式槽位与输入索引映射，不引用 Windows 类型 |
| JsonParser | net10.0 | 流式 JSON 兼容层 |

新平台可以引用 Core、Presentation 和 Tex，提供 ITexFontMetrics，消费绘图指令或 SVG，并接入自己的键盘、指针和输入法。文件存储可复用 SessionCodec 与会话模型。Windows 的窗口、StorageFile、剪贴板和 Acrylic 留在宿主，不进入公共库。公式光标映射的宿主与测试引用同一 Presentation 程序集。

## Acrylic

WinUI 3 的 AcrylicBrush 仅处理应用内部背景，不能直接替代 UWP 的 HostBackdrop。本项目使用 Windows App SDK 2.x 的 SystemBackdropElement，为每个原有区域建立独立的 DesktopAcrylicController。参见微软 [系统背景文档](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops) 和 [应用内 Acrylic 文档](https://learn.microsoft.com/en-us/windows/apps/develop/ui/in-app-acrylic)。

实现位于 MainPage.Acrylic.cs 和 Controls/TintedAcrylicBackdrop.cs。显示区保留 TintOpacity 0.6；控制区与数字区保留 0.9。暗色分别保留 RGB 23 / 28 / 36，浅色分别保留 RGB 230 / 220 / 230。系统回退色与各自 tint 相同。Acrylic 开关、深浅主题、横竖布局保留；关闭时直接使用原纯色。

自定义 SystemBackdrop 使用 XAML 提供的默认配置，接入窗口激活、主题及系统材质策略，并在断开目标时释放控制器。不支持 Desktop Acrylic 的系统使用纯色。由于 UWP 与 Windows App SDK 的材质算法不同，保留 tint 数值不代表像素级一致；深浅主题、窗口后方动态内容、缩放/旋转布局和透明效果关闭时的视觉验收仍需完成。

## Windows 宿主适配

- UseWinUI、Windows App SDK 2.6.5、Microsoft.Graphics.Win2D 1.4.0、CommunityToolkit.WinUI.Controls.Sizers 8.2.251219 替代 UWP / WinUI 2 依赖。
- MainWindow 使用 AppWindow 管理窗口大小、全屏、紧凑置顶和标题栏。窗口关闭先等待会话保存；编辑时的自动草稿保存继续工作。
- Program 使用 AppInstance 保持单实例并重定向重复启动。协议激活仍交给既有 ActivationInput；主窗口已打开时也处理新表达式。
- 对话框显式指定 XamlRoot；FileSavePicker 初始化所属 HWND；导出 DPI 来自 XamlRoot；键盘状态使用 InputKeyboardSource。
- 资源改用 Windows App SDK ResourceLoader。包身份、Publisher、ncalc 协议、LocalSettings 键和会话文件格式不变，以便正式同身份升级继续读取原数据；尚未实际验证覆盖安装。
- Manifest 改为 Windows.Desktop / Windows.FullTrustApplication，并声明桌面应用需要的 runFullTrust。Windows 最低版本保持 10.0.19041.0。
- 已完整移除 Cortana 命令、状态字段、三种语言的唤醒说明与初始化失败提示，不再打包 Commands.xml。移除旧 PhoneIdentity 和未使用的麦克风、图片库、视频库、移动存储声明；文件导出仍通过带 HWND 的 FileSavePicker 授权访问用户选定文件，桌面包仅保留 runFullTrust。

## Native AOT

应用和公共库继续使用 SDK 项目格式与 Native AOT。WinRT 自定义控件声明为 partial，供 CsWinRT 生成封送代码。JSON 使用 JsonTextReader / JsonTextWriter，避免动态 JToken 和反射序列化；保留旧语法、注释、重复键、大整数与资源限制。

字体测量沿用已知基线加 DrawBounds 的实现，不依赖 CanvasLineMetrics 数组封送。Release 必须启用 GenerateAppxPackageOnBuild，确保 MSIX 收集 Native AOT 输出。构建脚本将警告视为错误，并检查包内有原生 EXE、没有应用托管 DLL 和 CoreCLR。Debug 使用自包含 CoreCLR；Windows App SDK 随应用自包含分发。

## 验证与限制

本地环境：Visual Studio 2026、.NET SDK 10.0.403、Windows SDK 10.0.26100.0。构建命令见 README。2026-11-12 已通过 x64 Debug 构建、Release Native AOT / MSIX 构建及 629 项可移植回归测试；迁移后的最终检查记录见构建日志。

首次迁移时，自动审批以 blocked by policy 拒绝了 QA 包部署。随后用户明确要求部署 Debug 包，已注册并启动独立的 Naturplay WinUI3 Debug（x64，1.13.96.1，包身份 NaturplayCalculator.WinUI3.Debug）。本次部署使用自包含 CoreCLR 调试输出，布局目录为 artifacts/winui3-debug-qa-v1，协议为 ncalc-winui3-debug；正式应用和之前的 QA 包没有被覆盖。

实际启动暴露了帮助窗口引用不存在的 mode_symbolic/Content 资源的问题，已修正为资源文件中的 mode_symbolic/Text；三种语言的 34 个直接资源引用均检查通过。修复后的 Debug 界面已显示用户输入的 9 + 9 及结果 18，深色 Acrylic 背景模糊可见。没有改动用户正在输入的内容。Release 应用启动、浅色材质、系统回退和完整交互仍未实机验收，不能视为与旧版像素级一致。

兼容性检查已扩展为以下矩阵，构建与运行结果分开记录：

| 检查 | 2026-11-12 本地结果 | CI 配置 |
| --- | --- | --- |
| 可移植测试 | Debug / Release 各 629 项通过 | Windows、Linux、macOS |
| x64 WinUI 3 | Debug、Release Native AOT MSIX 通过 | Windows VS2026，两种配置 |
| x86 WinUI 3 | Debug、Release Native AOT MSIX 通过 | Windows VS2026，两种配置 |
| ARM64 WinUI 3 | Debug 交叉构建通过；Release 缺少本机 C++ ARM64 链接工具 | Windows 11 ARM64 VS2026，两种配置 |
| 核心 Native AOT 执行 | win-x64、win-x86 通过 | 三种 Windows 架构、linux-x64、osx-arm64 |
| 工作流校验 | actionlint 1.7.12 通过 | 每次执行 actionlint |

Release 包校验直接读取 EXE 的 PE machine 和 CLR header，避免仅凭文件名或清单认定架构正确；同时验证 Windows.Desktop / 10.0.19041.0、所需 WinUI 运行库及资源、能力声明，并拒绝残留 Commands.xml 和托管运行时。实际构建日志位于 artifacts/checks。AOT 冒烟使用独立 SmokeRuntimeIdentifier 属性，避免 -r 将平台传播到可移植库而破坏锁定还原。

远端 CI 尚待执行。本机是 Windows 11 x64；包内的 Windows 10 最低版本校验不等于 Windows 10 实机运行验证，核心冒烟也不等于 WinUI 界面启动验证。Windows 10、ARM64 界面运行以及完整数据恢复、导出和输入法仍需按 [Windows 验收清单](verification.md) 实测。

宿主错误写入自己的 LocalFolder/diagnostics.log，超过 256 KiB 后在下次写入前重置，不上传。MSIX 默认不签名，发布需正式签名和安装验证。
