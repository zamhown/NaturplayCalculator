# 开发与验证

## 仓库结构

```text
.github/                 GitHub 工作流、Dependabot、Issue 和 PR 模板
docs/                    设计、开发与人工验收文档
scripts/                 唯一的仓库级 PowerShell 工具入口
ncalc/                   当前计算器解决方案、应用、库和回归测试
math/                    独立数学实验库，.NET 10 测试与独立 CI
artifacts/               本地日志、测试结果、工具缓存（Git 忽略）
global.json              SDK 基线与补丁滚动策略
NuGet.Config             共享 NuGet 源
.editorconfig            编辑约定
.gitattributes           文本换行与二进制文件约定
```

解决方案为 `ncalc/NaturplayCalculator.sln`。项目名称和命名空间沿用原名。新脚本只放在根目录 `scripts/`，不要在项目目录维护第二套入口。

## 环境

- PowerShell 7.2+（`pwsh`）和 Git。
- .NET SDK 10.0.403；global.json 允许同一功能版本中的较新稳定补丁。CI 的 setup-dotnet 从同一个 global.json 安装 SDK。
- 完整 Windows 构建：Visual Studio 2026 的 Windows 应用开发、C++ 桌面开发工具和 Windows SDK 10.0.26100.0。
- Windows ARM64 交叉编译还需 C++ ARM64 工具。脚本按主机架构选择原生 MSBuild。
- Linux/macOS 只构建公共库与 Native AOT 冒烟，不能构建 WinUI 宿主。Native AOT 需要对应平台的原生编译工具链。

## 常用命令

在仓库根目录执行；PowerShell 内可省略 `pwsh -NoProfile -File`。

| 命令 | 用途 |
| --- | --- |
| `pwsh -NoProfile -File ./scripts/check-repo.ps1` | 检查必要文件、脚本语法、JSON、项目/文档引用、锁文件、Action SHA 和误提交产物 |
| `pwsh -NoProfile -File ./scripts/check-workflows.ps1` | 下载并校验 actionlint，验证工作流 |
| `pwsh -NoProfile -File ./scripts/test-scripts.ps1` | 验证外部程序失败、包内容和预览边界，不安装应用 |
| `pwsh -NoProfile -File ./scripts/check.ps1 -CoreOnly -Configuration Release` | 可移植回归 |
| `pwsh -NoProfile -File ./scripts/check.ps1 -Configuration Debug -Platform x64` | 回归及 Windows 调试构建 |
| `pwsh -NoProfile -File ./scripts/check.ps1 -Configuration Release -Platform x64` | 回归、Native AOT 构建、未签名 MSIX 及包检查 |
| `pwsh -NoProfile -File ./scripts/check-aot.ps1` | 为当前受支持主机发布并执行核心 AOT 冒烟 |

Windows 支持 `x64`、`x86`、`ARM64`。不要在同一 checkout 并行执行不同配置或架构的构建，它们共享项目中间目录。仅当已经执行对应测试时使用 `-SkipTests`。AOT 跨架构发布需明确传入 `-RuntimeIdentifier` 和 `-PublishOnly`；发布成功不等于执行成功。

脚本使用严格模式、失败退出码和 finally 恢复调用目录。任何目录均可通过绝对路径调用脚本，并仍从仓库根目录解析 SDK 和 NuGet 配置。

## 依赖与工具更新

`ncalc/Directory.Build.props` 默认启用锁定还原、确定性构建和警告即错误；GitHub Actions 中还启用 ContinuousIntegrationBuild。NuGet.Config 清除机器级额外源，只保留 nuget.org。需要私有依赖时应显式评审源配置，不写入凭据。

更改 PackageReference 后，在 Windows 运行相关命令并加 `-UpdateLock`（测试、应用、AOT 项目各自还原）。审阅所有受影响的 packages.lock.json，再移除开关重跑。CI 拒绝更新锁文件。不要用普通 `-r` 向公共库传播 AOT RID；check-aot.ps1 使用可执行项目独有的 SmokeRuntimeIdentifier。

Windows 应用的 Debug 使用 CoreCLR，Release 才启用 PublishAot。两种配置的还原图不同：Debug 使用 `packages.Debug.lock.json`，Release 使用 `packages.lock.json`。更新依赖时须分别还原两种配置并审阅全部 `packages*.lock.json`。Debug 构建还会检查生成的运行时配置，拒绝混入禁用动态代码或内置 COM 的 AOT 开关。

SDK 更新修改 global.json，并重新验证所有锁文件和完整构建矩阵。较新的 SDK 补丁也可能改变运行时包，锁定还原会明确失败，需按同样流程更新锁文件。

Dependabot 每周检查 Actions 与 ncalc 的 NuGet 依赖，minor/patch 归组，major 单独审查，不自动合并。actionlint 的版本与每个平台 SHA256 位于 scripts/tools.json；更新时从官方发布核对校验值。工具缓存位于 artifacts/tools，缓存损坏会报错，不运行未通过校验的下载。

## CI 和 GitHub 设置

CI 顺序为仓库检查 → 三系统可移植回归/脚本测试与 Windows 三架构 Debug/Release → `CI result` 汇总。非 Windows runner 执行主机 AOT；Windows Release 执行目标架构 AOT。任何失败、取消或跳过都使汇总失败。支持 push、pull_request、merge_group 和手动触发。

工作流只授予 contents: read，不保留 checkout 凭据，不使用 pull_request_target 执行 PR 代码。Actions 固定完整提交 SHA，SDK 从 global.json 读取，NuGet 缓存由锁文件生成键。日志与产物保留 14 天。

维护者在 GitHub 仓库设置中可启用分支规则，将 `CI result` 设为必须通过的检查，并按团队需要设置审查、私密漏洞报告及 secret scanning。这些是服务端设置，仓库文件不能自动启用。

## 产物、预览和发布

- 测试结果：`artifacts/tests/<运行类型或架构>-<配置>/regression.trx`。
- 构建、还原、测试和 AOT 日志：`artifacts/checks/`。
- Release MSIX：`ncalc/NaturplayCalculator/AppPackages/`；包检查校验所需文件、PE 架构、无 CLR header、最低系统版本与能力声明。

隔离预览基于已经通过校验的 Release MSIX，示例：

```powershell
./scripts/preview.ps1 -PackagePath <Release.msix> -Platform x64 -Destination <新的绝对物理目录> -WhatIf
./scripts/preview.ps1 -PackagePath <Release.msix> -Platform x64 -Destination <新的绝对物理目录>
# 需要安装时，在支持 Add-AppxPackage 的 Windows PowerShell 7 会话中显式添加 -Register。
```

默认仅准备文件；注册需显式使用 `-Register`，关闭旧预览还需 `-ClosePreview`。预览使用独立身份 NaturplayCalculator.WinUI3.Preview，删除生产 ncalc 协议，并拒绝覆盖已有目录。MSIX 重定向环境须传入部署服务能读取的真实物理路径。此布局仅用于开发注册，不能重新作为正式签名包分发。

正式发布由维护者在独立流程中签名，并执行 [Windows 与数据恢复验收](verification.md)。CI 不保存证书、不上传商店、不替换已安装应用。

## 参考

工程配置依据 [GitHub Actions 安全指南](https://docs.github.com/en/actions/reference/security/secure-use)、[Dependabot 配置](https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference)和 [.NET SDK 选择规则](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)。
