# 贡献指南

感谢参与 Naturplay Calculator。缺陷和功能建议请使用 GitHub Issue 模板，提交代码请使用 Pull Request。

## 本地开始

1. 阅读 [开发与验证指南](docs/development.md)，安装 PowerShell 7.2+ 和 global.json 指定的 .NET SDK。
2. 运行 `pwsh -NoProfile -File ./scripts/check-repo.ps1`。
3. 运行 `pwsh -NoProfile -File ./scripts/test-scripts.ps1`。
4. 运行 `pwsh -NoProfile -File ./scripts/check.ps1 -CoreOnly -Configuration Release`。
5. 修改 Windows 界面、资源或打包配置时，再运行对应架构的 Debug / Release 构建，并完成相关界面验收。

所有脚本按自身位置定位仓库；可在其他目录通过脚本的绝对路径调用。

## 改动边界

- 当前应用与可移植库位于 `ncalc/`；`math/` 是旧实验项目，见其 [维护说明](math/README.md)。
- 新计算逻辑进入统一表达式和求值管线；UI、WinRT 和存储接口留在 Windows 宿主。
- 修复计算或编辑问题时添加能复现问题的回归测试。不要批量格式化无关文件。
- 使用 UTF-8，遵守 .editorconfig 和 .gitattributes；不要提交 bin、obj、MSIX、证书、个人配置或历史记录。
- 新增/升级依赖时显式使用 `-UpdateLock`，审阅锁文件，再以默认锁定模式验证。
- 文档说明最终行为、实际验证范围和已知限制；未执行的测试不能标为通过。

## 提交审查

每个 PR 聚焦一个目的，说明复现步骤、改动后的行为、测试命令与结果。UI 改动附截图；依赖变更说明原因。GitHub 的 `CI result` 汇总所有必须通过的任务，维护者可将其设为分支规则的必需检查。

贡献遵循仓库现有 [GPL v3 许可证](LICENSE)。安全问题参见 [安全报告说明](SECURITY.md)。