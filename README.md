# Naturplay Calculator

Windows WinUI 3 自然书写计算器，支持数值计算、符号计算、复数、矩阵、统计，以及 JSON / XML 树形查看器。

## 计算模式

在菜单「计算设置」中切换「数值计算」和「符号计算」，勾选标记表示当前模式。默认数值模式保持双精度近似计算；符号模式使用精确有理数，保留分数、根式和未声明的变量。结果仍以自然书写方式排版，复制时使用可重新解析的表达式。

| 输入 | 符号模式结果 |
| --- | --- |
| `1/3+1/6` | `1/2` |
| `0.1+0.2` | `3/10` |
| `sqrt(8)` | `2√2` |
| `1/sqrt(2)` | `√2/2` |
| `(a+b)c+bc` | `ac+2bc` |
| `(a+b)^2` | `a²+2ab+b²` |
| `(x²-1)/(x-1)`（输入时用 `^2`） | `x+1`，保留 `x-1 ≠ 0` |
| `sqrt(x^2)` | `Abs(x)` |
| `sin(pi/6)`（RAD） / `sin(30)`（DEG） | `1/2` |

变量默认是实数，使用单个字母，不需要声明；相邻字母相乘，例如 `bc` 是 `b*c`。`π` / `pi`、`e`、`i`、`Ans` 和函数名保留特殊含义；`^T` 表示转置。默认化简采用展开乘积、合并同类项、约分和根式整理。支持精确矩阵运算、行列式及常见三角常量。

历史条目保存各自的计算模式、角度模式、精确结果、原始 Ans 和适用条件。浏览历史不会重新计算或改变 Ans。数值模式可以近似求值不含未赋值变量的符号 Ans；符号模式接续已有数值 Ans 时，把它的十进制表示当作精确输入，不推测原来是否来自某个分数或根式。

未求值的三角函数仍显示标准的 `sin`、`cos` 等名称，公式下方单独说明角度单位。符号结果会保存固定的角度含义，切换 DEG / RAD 不会改变已有结果或 Ans 的含义。

符号化简使用确定的规则，并不承诺所有表达式都存在唯一的「最简式」。不能继续化简的函数保留为表达式；不进行通用因式分解、方程求解或求导。矩阵幂的指数必须是已知整数；含变量的矩阵秩保留为 `rank(...)`，矩阵逆保留消元所需的非零条件。函数消去、分式约分会保留适用条件，续算和保存也不会丢失这些条件。

## 构建和测试

项目先按 [微软的 UWP 现代 .NET 迁移指南](https://learn.microsoft.com/zh-cn/windows/uwp/dotnet-native/modernize-uwp-apps-with-dotnet) 升级到 .NET 10，现已进一步迁移为 WinUI 3 / Windows App SDK 2.6.5。Windows 界面使用 `SystemBackdropElement` 和 `DesktopAcrylicController`，分别为显示区与按键区提供窗口背后的 Acrylic，沿用原主题色以及 0.6 / 0.9 的 TintOpacity。新旧渲染管线的最终外观尚待实机对比，不能据参数相同认定像素级一致。

完整构建需要 Windows、Visual Studio 2026 的 Windows 应用开发工具与 C++ 桌面开发工具（Native AOT 链接器）、Windows SDK `10.0.26100.0` 和 .NET 10 SDK。本地验证版本为 SDK `10.0.403`；`global.json` 允许已安装的稳定 .NET 10 功能版本。最低系统版本为 Windows 10 `19041`。支持的应用架构为 `x64`、`x86`、`ARM64`，不再提供 ARM32。

在本目录运行 PowerShell：

```powershell
# 不依赖 Windows UI 的 .NET 10 回归测试（Windows / Linux / macOS）
./NaturplayCalculator/scripts/check.ps1 -CoreOnly -Configuration Release

# 回归测试 + x64 WinUI 3 调试构建
./NaturplayCalculator/scripts/check.ps1 -Configuration Debug

# 回归测试 + x64 Native AOT 编译和未签名 MSIX 打包
./NaturplayCalculator/scripts/check.ps1 -Configuration Release

# Windows x64：将可移植库原生编译后实际运行
./NaturplayCalculator/scripts/check-aot.ps1

# 其他 Windows 架构：完整构建与包架构校验
./NaturplayCalculator/scripts/check.ps1 -Configuration Release -Platform x86
./NaturplayCalculator/scripts/check.ps1 -Configuration Release -Platform ARM64

# 在可运行目标架构的机器上执行 Native AOT 冒烟
./NaturplayCalculator/scripts/check-aot.ps1 -RuntimeIdentifier win-x86
```

脚本默认锁定依赖还原。修改依赖或 SDK 版本时使用 `-UpdateLock` 并检查 `packages.lock.json`。WinUI XAML 构建使用 Visual Studio 2026 MSBuild；纯 .NET 库和测试使用 `dotnet`。Release 启用 `GenerateAppxPackageOnBuild`，确保 MSIX 包装的是 Native AOT 输出，不能仅将普通 Build 目录打包。开发调试使用自包含 CoreCLR。

CI 位于 `.github/workflows/ci.yml`，支持 push、pull request 和手动触发：actionlint 验证工作流；Windows、Linux、macOS 运行可移植回归；Windows x64 / x86 / ARM64 各运行 WinUI 3 Debug / Release 构建。Release 校验 MSIX 清单、实际 PE 架构、Native AOT、最低系统版本和能力声明，并上传未签名包与构建日志。三种 Windows 架构及 Linux / macOS 均执行对应平台的核心 Native AOT 冒烟；ARM64 Windows 使用原生 ARM64 runner。远端执行结果和实机限制见 [现代 .NET 迁移说明](NaturplayCalculator/docs/modern-dotnet.md)。

本机交叉编译 ARM64 Release 还需要 Visual Studio 的 `Microsoft.VisualStudio.Component.VC.Tools.ARM64` 组件。`check-aot.ps1 -PublishOnly` 可只生成目标程序，不将未执行的程序计为冒烟通过。不要并行运行共享同一工作目录的不同架构构建；CI 的矩阵任务各自使用独立 checkout。

## 代码边界

| 项目 / 目录 | 职责 |
| --- | --- |
| `NaturplayCalculator.Core/Calculation` | 统一入口、求值遍历、结果模型、数值运算域、Ans、角度模式、取消和预算；文档查看入口 |
| `NaturplayCalculator.Core/Expressions` | 两种模式共用的解析器、不可变表达式树、规范代码、文本及 TeX 输出协议 |
| `Naturplay.Tex` | 独立 TeX 解析、公式布局、绘图指令、源码定位、光标编辑模型及 SVG 导出；无计算或 Windows 依赖 |
| `NaturplayCalculator.Presentation` | 无 Windows 依赖的公式光标映射，连接计算内核与 TeX 排版；宿主和测试引用同一程序集 |
| `Naturplay.Tex.WinUI` | Windows 原生公式控件、字体度量和键盘 / 指针适配；只依赖 TeX 内核、WinUI 3 与 Win2D |
| `NaturplayCalculator.Core/Symbolics` | 精确有理数、稀疏多项式、根式与分式化简、符号函数和矩阵运算 |
| `NaturplayCalculator.Core/Model` | 复数和数值矩阵基础类型；旧 `Element` 保留给兼容代码与矩阵单元，不再作为应用求值入口 |
| `NaturplayCalculator.Core/History` | 与 UI 无关的会话和历史记录，以及带版本的显式 JSON 编解码 |
| `NaturplayCalculator.Core/Input` | 插入、删除、输入续算和协议启动表达式解析 |
| `JsonParser` | 使用 Newtonsoft.Json 流式读写的 AOT 兼容适配层，保留树形查看器的数据接口和旧语法 |
| `NaturplayCalculator/MainPage.Calculation.cs` | 后台计算、输入预览、过期结果抑制、结果和步骤渲染 |
| `NaturplayCalculator/MainPage.Formulas.cs` | 将 TeX 交给公式控件，连接现有输入定位、主题和复制数据 |
| `NaturplayCalculator/MainPage.History.cs`、`MainPage.Mode.cs` | 启动恢复、历史导航、草稿保存、计算模式和角度上下文 |
| `NaturplayCalculator/MainPage.Platform.cs`、`Services` | Windows 文件保存、弹窗排队、导出和平台操作 |
| `NaturplayCalculator.Tests` | 可脱离 Windows UI 运行的 xUnit 回归测试 |

新增计算功能应从 `CalculationEngine` 或 `ContentEvaluator` 进入。两种模式共享 `ExpressionParser → ExpressionNode → ExpressionEvaluation<T> → CalculationValue`；`NumericDomain` 和 `SymbolicDomain` 实现同一个运算域接口。步骤和存储复用不可变表达式树，公式展示通过 TeX 字符串连接；渲染模块不接触计算树、计算模式或全局设置。

隐式乘法（相邻因子）优先于显式乘除，低于幂：`a/2b` 表示 `a/(2*b)`，`a/2*b` 表示 `(a/2)*b`。这条规则由数值计算、符号计算和输入预览共用；文本结果和复制代码保留必要的分组，避免重新粘贴后改变分母范围。

```csharp
var result = new CalculationEngine().Evaluate("(a+b)c+bc", new CalculationOptions
{
    Mode = CalculationMode.Symbolic,
    UseRadians = true,
    CaptureSteps = true
});
// result.Value.DisplayText: ac+2bc
// result.Value.Code: a*c+2*b*c
// result.Value.Conditions: applicable constraints (also retained for Ans)
```

统一结果访问 `Value`；`Answer` 仅为旧数值调用提供兼容，符号结果调用它会明确报错。扩展运算时在共享语法层登记一次，并在两个运算域中分别实现其数值和精确语义。公式链路为 `ExpressionTexFormatter → TeX → TexParser → TexLayoutEngine → TexFormulaView`；旧的 `MathLayout` 和 `MathElement` 已移除。TeX 的 `{}` 只组织语法，只有显式括号才显示，分数和根式不再添加多余括号。输入、步骤、结果和历史复用此通道。

渲染器参考 KaTeX 的基线 / 字高 / 字深排版、MathJax 的输入和输出分层，以及 MathLive 的光标与选区模型，自行实现数学 TeX 子集，不依赖这些库的运行时。计算器通过源码映射支持直接点击公式编辑，独立的原始 TeX 编辑入口仍由宿主显式开启。协议、支持语法、API 和限制见 [TeX 渲染器设计](NaturplayCalculator/docs/tex-renderer.md)。

输入期间使用容错预览预测公式结构：未填的分子、分母、函数参数和矩阵单元格用系统文本选中底色标注；例如 `(2,2,,2` 显示为两行两列矩阵，右下角留空。数学输入不会因暂时不完整或超过原来的 512 字符阈值而回退为原始文本。实际求值仍只读取真实输入；预测不会自动填入数值或更改历史。

输入栏与公式使用系统强调色的插入光标，点击哪一处，哪一处闪烁，另一处常亮；按等号后两处隐藏，再编辑时恢复。两处拥有独立的光标槽位与双向映射：公式槽位保存所在行和位置，分母末尾与整个分式之后即使映射到同一个文本位置，也不会合并。方向键、Home/End 和 Shift 选择按当前热区的槽位工作。

公式槽位按可见原子的插入边界生成。TeX 命令结束空格、数学间距，以及函数名与参数左括号之间的排版缝隙不会增加停靠位置；`sin(...)` 从函数前直接进入参数。位于整个分式之后时输入 π 等新因子，会显式写入乘号，保持它位于分母之外。

公式局部编辑会保护所在容器的括号边界：输入 `)` 越过已有闭括号或退出当前层级，局部新括号不会吞掉外层后缀；省略显示的源码分组仍保留编辑范围。错误输入中的孤立右括号保持可见，预览不会因此给整个表达式添加一层虚构的大括号。

公式编辑保持在被点击的分子、分母、指数、根号内容或矩阵单元格中；例如点击 `(9-6)/9` 的分母后输入 `+1`，得到 `(9-6)/(9+1)`，在整个分式之后输入则得到 `(9-6)/9+1`。原生输入控件编辑局部缓冲区，序列化负责必要的括号，不向 IME 组合文本写入这些括号；切回输入栏则使用普通线性文本编辑。输入栏由独立的 `ExpressionInputBox` 管理，按实际文本位置滚动；缺项底色与真正的文本选区仍使用系统选中色。公式列表保留独立 hover 和结果预览的自动强调，用户点击不再切换选中行；自动结果底色采用主题强调色的 18% 透明度。

公式按 Cambria Math 的实际字形绘制边界排版，而非逐个 TextBlock 的整行高度，避免把段落行距重复计入分数和根号。斜体的左右伸出及下伸部分纳入字形边界和光标定位，光标不压在 `g` 等字母上。结构旁边的光标覆盖相邻分式、根式、上下标或矩阵的完整高度；进入分子、分母、上标或单元格后恢复局部高度。

不要在 UI 线程直接调用求值器，也不要把 UI 对象、全局 `Ans` 或可变表达式树共享给计算任务。历史只保存数据，步骤使用独立快照。

## 已修复的行为

- 数字读取、显示、JSON 和历史序列化使用固定区域格式；系统语言不会将 `1.5 + 2.5` 算成 `40`。
- 极小数不再被统一截断为零；`10^-16 * 10^16 = 1`，纯虚数和科学计数法可以正常计算。
- 幂运算按右结合计算，支持复数负整数、分数和复数指数的主值；矩阵指数必须是整数。矩阵求逆和行列式使用主元消元。
- 修复双阶乘、排列组合边界、单个数的方差，以及大数 / 极小数统计中间值的溢出和下溢。
- 阶乘、表达式深度、矩阵尺寸和计算量都有边界；取消计算和编辑输入后，旧任务不能覆盖新结果。
- 历史同时保存结果、原始 Ans 和角度模式。草稿是否已计算及其结果单独保存，不再依据“输入文字相同”猜测结果。
- JSON 的反斜杠、控制字符和 Unicode 往返不丢失；XML 禁用 DTD 和外部实体，并限制树形节点及属性数量。
- 修复 Delete / Backspace 边界、选区替换、计算结束后的输入续算，以及 `Ans` 被彩蛋命令截获的问题。
- `ncalc://1/2` 保留完整表达式；推荐使用 `ncalc://calculate?expression=1%2B2`。应用已打开时也会处理新的协议激活。
- 重复打开弹窗会排队，图片导出和平台异步失败会被处理并记录调试诊断。

## 数值与资源边界

数值计算使用双精度浮点。复数分数 / 复数指数幂取主值；实数奇次方根保留实根。矩阵秩的消元使用相对消去容差，病态矩阵仍受浮点精度限制。统计保留原有总体方差和非共轭复数矩定义。

数学表达式最多 4096 个字符、48 层括号、32×32 矩阵。精确整数限制约 4096 位二进制，展开多项式最多 256 项，结果代码最多 16384 个字符，表达式树深度最多 128 层；精确整数分解也受计算预算约束。默认每次计算预算为 250000 次检查和 2 秒；UI 步骤最多 32 个快照，并限制自然排版长度和总渲染量。JSON / XML 最多 256 KiB 字符、64 层、约 2048 个展示节点。超过边界会给出错误，不在 UI 线程继续长时间求值。

## 历史迁移与恢复

新会话位于应用的 `LocalFolder/calculator-session-v3.json`。写入使用临时文件替换，保留上一次有效文件 `calculator-session-v3.backup.json`。主文件读取失败会尝试备份；损坏原文件在后续写入前另行保留，不直接丢弃。

启动时依次尝试 v3 主文件、v3 备份、v2 主文件和 v2 备份；v2 记录迁移为数值模式，保留原有结果和上下文，不重新求值。v2 文件保持原样，后续写入 v3。

旧 `LocalSettings` 的 `laststr`、`htyn`、`htyN` 会导入，旧键保留。旧记录没有保存结果或 Ans 上下文，因此显示提示，并且不会在启动或浏览时自动重新计算。按等号才会使用当前上下文计算。历史保留最近 100 条，并限制表达式总长度为 256 KiB 字符。

## 签名与发布

普通构建默认不签名，不依赖本机证书。发布时通过外部证书存储或 CI 密钥配置提供签名信息；不要将 PFX / P12 放入源码。旧测试证书已从本地项目移至 `%LOCALAPPDATA%\NaturplayCalculator\Signing\Legacy-20261112`，重构前源码备份位于同级 `Backups\before-governance-20261112-015046`。

本次工具进程中的 `%LOCALAPPDATA%\NaturplayCalculator` 写入被 MSIX 重定向。备份、签名材料与 QA 文件的真实物理目录位于 `%LOCALAPPDATA%\Packages\OpenAI.Codex_2p2nqsd0c76g0\LocalCache\Local\NaturplayCalculator`；向部署服务传递路径时，应使用真实物理路径。

发布前还需完成 [Windows 界面与数据恢复验收](NaturplayCalculator/docs/verification.md)，并使用正式发布流程签名和验证安装包。
