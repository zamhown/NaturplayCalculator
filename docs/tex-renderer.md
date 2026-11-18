# TeX 公式模块

计算和展示通过标准数学 TeX 字符串连接。`NaturplayCalculator.Core` 不引用渲染项目；`Naturplay.Tex` 不引用计算项目或 Windows API；`Naturplay.Tex.Uwp` 只把平台字体和绘图能力接到 TeX 内核。两种计算模式共用同一个序列化入口。

```text
数值 / 符号计算 → ExpressionNode → ExpressionTexFormatter
                                      │
                               TeX 公式正文字符串
                                      │
                    TexParser → TeX 语法树 → TexLayoutEngine
                                               │
                          绘图指令 + 点击区域 + 光标位置
                               │                       │
                        TexFormulaView             TexSvgWriter
```

`MainPage.Formulas.cs` 是应用适配层：保存可复制的计算代码、设置颜色、连接原有输入位置。公式控件不认识 `ExpressionNode`、`CalculationValue`、`Settings` 或数值 / 符号模式。输入和结果均已移除原有的 512 字符纯文本回退阈值。

## 传输协议

`TexExpression.Protocol` 为 `naturplay.math-tex/1`。`Latex` 是公式正文，不带 `$…$`、`\(…\)` 或文档环境；没有自定义计算命令、HTML 或内部函数标识。格式化器示例：

| 计算内容 | TeX |
| --- | --- |
| `1/2` | `\frac{1}{2}` |
| `1/2π` | `\frac{1}{2\pi }` |
| `1/2*π`（符号显示） | `\frac{1}{2}\pi ` |
| `3sqrt(2)` | `3\sqrt{2}` |
| `sqrt(sin((3a+2)/2))` | `\sqrt{\sin \left(\frac{3a+2}{2}\right)}` |
| `(a+b)^2` | `\left(a+b\right)^{2}` |
| 二行二列矩阵 | `\begin{pmatrix}a&b\\c&d\end{pmatrix}` |

`{}` 不产生可见括号，必要的数学分组使用 `\left(…\right)`。科学计数法转换为 `1.25\times 10^{-3}`；作为幂底数或阶乘操作数时，必须整体分组。固定弧度的内部名称 `sinrad` 等仍序列化为标准 `\sin`；角度语义保留在计算值和独立说明中，不进入函数名称。

`SourceMap` 是可选的宿主旁路元数据，把 TeX 的 UTF-16 范围映射到原计算输入位置。只传递 `Latex` 即可完整渲染。渲染器自己的光标、点击区域和选区全部使用 TeX UTF-16 偏移，不能与计算输入偏移混用。生成的语法括号没有伪造输入位置；折叠数字的省略号映射到完整隐藏数字范围。

插入光标通过 `CaretOffsetAtInput` / `InputInsertionAt` 在两个偏移体系间转换。`CaretStart` / `CaretEnd` 单独记录插入边界，不能直接把命令源码范围当作可见位置：例如输入 `/` 前后分别对应分子末尾、分母开头。预览序列化同时接收原始输入，用来记录实际函数名长度、矩阵和函数的括号边界；预测出来的括号不冒充真实输入。点击公式使用最近的 `TexCaretStop`，可以选择字符的前后两侧以及末尾。

未填项输出标准 TeX `\phantom{0}`，保留一个数字的尺寸而不画出数字。内核在 `TexHitRegion.IsEmptySlot` 中标出该空间，宿主通过 `ShowEmptySlots` 决定是否强调显示；UWP 使用动态 `TextControlSelectionHighlightColor`，与当前 TextBox 的文本选中底色共用主题资源，随主题和高对比度资源更新。不要使用旧版 `TextSelectionHighlightColorThemeBrush`，它在普通主题中是固定紫色。显式 `\square` 仍然是数学方框符号。补出的缺项在源码映射中使用 `InputLength = 0`，锚定实际输入的插入位置，不移动后面已有数字或变量的索引。

## 未完成输入的预测

`ExpressionParser.ParsePreview` 与严格求值解析共用词法、优先级和函数表，加入仅用于展示的缺项、分组和恢复行节点。`ExpressionPreview.Create` 输出同一套 TeX 协议，不把自动补出的结构交给计算器、Ans 或历史值。输入、错误提示上方的表达式都使用该预测路径。

缺少操作数、空函数参数、不完整根式 / 分式 / 上标、未闭合括号及不齐的矩阵行都会保留结构并逐处标出缺项。例如 `(2,2,,2` 预测为 `((2,2),(2,空))` 两行两列矩阵；`(2,2,,` 的第二行有两个空项，`sqrt(1/)+sin()` 则在分母和正弦参数中分别标出空项。连续输入和中间删除不会因为严格语法暂时不成立而退回原始文本。

长的和、乘积在预览中使用平坦恢复行，避免左结合表达式撞上树深度限制。表达式超过原计算资源限额时仍展示有结构的前缀，并明确显示省略号与限制提示；不会伪装成完整表达式。可识别的 JSON / XML 文档前缀继续走文档展示。

复制和历史仍使用可再次计算的 `CalculationValue.Code`，不会把显示 TeX 当成计算输入。当前计算解析器不接收 TeX。

## 实现和参考

以下实现用于理解设计，没有复制其实现代码，也没有加载 JavaScript 渲染器、WebView 或外部渲染服务：

- [KaTeX 支持语法](https://katex.org/docs/supported.html)、[分式布局](https://github.com/KaTeX/KaTeX/blob/main/src/functions/genfrac.ts)、[上下标布局](https://github.com/KaTeX/KaTeX/blob/main/src/functions/supsub.ts)：参考基线、字高、字深、数学样式及间距的组织方式。
- [MathJax processing model](https://docs.mathjax.org/en/latest/advanced/model.html)：参考输入处理器、内部数学树和输出处理器的分离。
- [MathLive mathfield API](https://mathlive.io/mathfield/api/)：参考光标位置、方向性选区和编辑命令的分离。

`TexParser` 是有界递归下降解析器，输出带源码范围的不可变 TeX 语法树。每个节点同时保留语法范围和可编辑内容范围，避免让光标落到控制词内部。

`TexLayoutEngine` 按公式样式构造盒子，计算宽度、基线上方高度和基线下方深度，再输出字形、直线及曲线路径。二元运算符、关系、函数、标点和分组分别处理间距；上下标缩放，显示样式的大运算符支持上下限。根号和伸缩括号自行构造路径。最终边界包含笔画宽度和空编辑位置，便于截图、裁剪和导出。

字体度量通过 `ITexFontMetrics` 注入。UWP 使用 Win2D / DirectWrite 的 `CanvasTextLayout.DrawBounds` 测量 Cambria Math 实际字形边界，变量采用斜体，函数名采用正体；原生 `TextBlock` 按相同基线绘制完整字形，不裁掉下伸部分。Win2D 仅提供平台字体度量，TeX 解析、排版、根号和括号构件仍由本项目实现。无界面运行时提供确定的近似度量。当前没有读取 OpenType MATH 字体表，数学间距和构件比例使用内核的 em 参数，因此不承诺与完整 TeX / KaTeX 像素一致。SVG 使用同一绘图指令和字形基线，查看端的可用字体会影响最终字形；要求精确导出时应注入与输出字体一致的度量。

## 支持的数学语法

- 数字、Unicode 字符、普通括号、分组、注释，以及常用希腊字母、算符、关系和箭头。
- `\frac`、`\dfrac`、`\tfrac`、`\binom`、`\sqrt` 和可选根指数。
- `\phantom` 的不可见占位与独立空项几何。
- `^` / `_`，同时存在的上下标，`\sum` / `\prod` / `\int` 等大运算符、`\limits` / `\nolimits`。
- `\left` / `\right` 的圆括号、方括号、花括号、绝对值、范数、尖括号、上下取整、单边和空边界。
- `matrix`、`pmatrix`、`bmatrix`、`Bmatrix`、`vmatrix`、`Vmatrix`、`cases`、`aligned`、`array`；数组支持 `l/c/r` 对齐和竖线；允许末尾换行符。
- `\sin`、`\cos`、`\ln` 等常用函数、`\operatorname`，以及 `\text`（文本中不支持嵌套分组）。
- `\mathrm`、`\mathit`、`\mathbf`、`\mathnormal` 和 display / text / script / scriptscript 四种数学样式。
- `\hat`、`\vec`、`\bar`、`\overline`、`\underline`，以及 `\,`、`\:`、`\;`、`\!`、`\quad`、`\qquad` 等显式间距。

这是数学 TeX 子集，不是完整的 TeX 文档系统。宏定义、任意扩展包、文件读写、网络、HTML、通用文档环境和自动换行没有实现；不支持的命令会带源码位置明确报错，不静默跳过。矩阵每行列数必须一致，`cases` 需要两列。上限为 32,768 个 UTF-16 代码单元、8,192 个语法节点、256 层解析嵌套和 32×32 矩阵。TeX 的分组与参数会为一个计算节点引入多层解析深度，因此渲染层的深度上限高于计算输入上限。

## 渲染和编辑 API

```csharp
// 无 Windows / 计算器依赖。
var layout = new TexLayoutEngine().Render(@"\sqrt{\frac{1}{2}}", 24);
string svg = TexSvgWriter.Write(layout);

// 独立编辑模型：所有变更先解析和布局成功，再提交状态。
var editor = new TexEditor("x+1");
editor.SetSelection(0, 3);
editor.InsertFraction();
editor.InsertLatex("2");              // \frac{x+1}{2}
editor.MoveVertical(-1);              // 进入分子
editor.Undo();
```

编辑模型提供左右 / 上下 / 行首 / 行尾移动、方向性选区、坐标命中、TeX 插入、根式 / 分式 / 上下标模板、前后删除和 128 步撤销 / 重做。跨分支选区先扩展到完整语法容器，避免删除后把相邻算符误读成参数；未加花括号的参数在编辑时补充分组。同一源码位置在上标和外层行可能对应不同坐标，模型保留该光标归属。

```csharp
var view = new Naturplay.Tex.Uwp.TexFormulaView();
view.SetFormula(@"\frac{a}{b}");      // 默认只读
// 仅供未来的编辑宿主或独立测试使用：
view.EnableEditing();
view.Edit(model => model.InsertRoot());
view.DisableEditing();
```

启用编辑后，控件绘制光标 / 选区，处理点击定位、方向键、Shift 选区、Home / End、Backspace / Delete、Ctrl+A、Ctrl+Z / Y 和字符输入。`^` / `_` 创建脚本位置；完整 TeX 命令通过 `InsertLatex` 提交。`LatexEdited` 仅在内容变化时触发；不合法的键盘编辑通过 `EditRejected` 报告。尚未提供 IME 组合输入、完整无障碍数学朗读、拖动选区、自动补全或对外工具栏。

计算器中的公式可以直接点击定位，然后通过键盘、输入法、粘贴或计算器按钮插入和删除。`CaretTapped` 返回 TeX 行范围、插入边界及对应几何；计算器自己的 `TexExpression.EditRegions` 将它关联到分子、分母、指数、根号内容、函数参数或矩阵单元格的原始输入范围。这个 sidecar 不进入渲染模块，TeX 渲染器仍然不依赖计算语法。

`FormulaEditSession` 保存被点击的子表达式上下文，原生 TextBox 编辑该范围的局部缓冲区，输入栏仍显示完整源码。序列化时才为需要保护的范围补括号：在 `(9-6)/9` 的分母后连续输入 `+`、`1`，源码变为 `(9-6)/(9+1)`；原生编辑缓冲区始终是 `9+1`。重排和计算预览不重置上下文，删除到空分母保留一个可继续输入的空位，删除和替换不会越过当前容器的边界。已有括号保留，点击本身不修改源码。切回输入栏后恢复线性文本编辑及通常的运算符优先级。

局部输入中新开的括号在序列化时补齐，并放在外层后缀之前，不能借用外层已有的闭括号。公式热区键入 `)` 时，越过已有的闭括号或移动到当前编辑行之后的结构槽位；不会在缓冲区里重复插入被宿主保管的闭括号。显式源码分组即使省略了可见括号，仍通过 TeX 分组行与旁路编辑范围保留其边界；缺项填入内容后也不丢失该范围。按键盘与计算器按钮输入均经过相同的 `InputEdit.ExitsFormulaScope` 路径，线性输入栏保留普通文本行为。

预览中的多余 `)` 作为有源码位置的公式标点显示，严格计算仍会拒绝它。预览器不再为这个标点给整个前缀凭空套上一对大括号。连续编辑回归覆盖每次输入后的重排、槽位重映射和原生缓冲区重新绑定，而不是只在一个不变的 `FormulaEditSession` 中连续调用插入。

公式输入过程不把生成的括号写回正在组合输入的 TextBox，避免打断 IME。`ShowCaret(int?)` 显示或隐藏对应位置的光标。计算器仍不调用独立 TeX 编辑器的 `EnableEditing`，不会把 TeX 控制词当作用户的计算输入。上段提到的独立 TeX 编辑器 IME 限制不适用于这条原生 TextBox 输入路径。

`ExpressionInputBox` 封装原生 TextBox 输入、单个富文本 Run、点击测量、选区和水平滚动。光标移动不再拆分 Run 或改写文本；末尾与空输入也有独立光标。真正的文本选区使用系统选中色，光标本身不再给字符染色。

输入栏与公式共用 `BlinkingCaret`，通过动态 `SystemControlHighlightAccentBrush` 使用系统强调色，并采用 [UISettings.CaretBlinkRate](https://learn.microsoft.com/en-us/uwp/api/windows.ui.viewmanagement.uisettings.caretblinkrate) 和系统光标宽度。`MainPage.Input.cs` 保存当前输入热区：当前区域闪烁，另一处常亮。按等号立即隐藏两处光标，计算完成后的重排与 LayoutUpdated 不会使其重新出现；再次点击、导航或输入时恢复。控件卸载或窗口失活时停止计时并隐藏，激活后仅恢复仍处于编辑状态的光标。缺项底色独立保留。

`FormulaCaretMap` 是宿主层的纯映射模型：公式槽位由 TeX 行范围与插入边界共同标识，输入栏槽位由 UTF-16 文本位置标识。映射不要求一一对应，也不要求可逆。`(9-6)/9` 的 8 个公式槽位映射到文本位置 `[0,1,2,3,4,6,7,7]`，8 个文本位置映射到公式槽位 `[0,1,2,3,4,4,5,7]`。公式作为热区时保留公式槽位本身，不能通过“映射到文本再映射回来”更新它。`TexFormulaView.CaretTapped` 与 `ShowPositionedCaret` 传递和绘制完整槽位几何；文本作为热区时才使用 `FromText`。

`TexLayoutEngine` 每行按可见原子的边界生成一次槽位，不分别为前一个命令的结尾和后一个原子的开头生成两次。命令结束空格及 `\,` 等数学间距没有独立槽位；命名算符与其括号 / 矩阵参数构成函数前缀，从名称之前直接进入参数内容。上下标包装的参数也使用这条规则。函数前的光标包含完整参数结构的高度。清理仅消除排版语法产生的停靠点，不合并分子、分母和外层等不同编辑范围。文本位置若落在被省略的排版边界上，`FromText` 按原输入位置找对应槽位，不以 TeX 控制词的字符串长度推断视觉距离。

计算语法中隐式乘法的优先级为 25（显式乘除 20，幂 30）：`/9π` 的 π 位于分母，`/9*π` 的 π 位于分式外。`FollowsFraction` 是宿主根据 TeX 行中相邻分式识别的编辑意图，交给 `FormulaEditSession`；在分式外输入新因子时显式插入 `*`，在分母内部则继续编辑分母。渲染器不参与决定运算符优先级。回归同时检查原始输入、严格计算树、预览树、两种计算模式、独立读取 TeX 得到的数学结构及文本往返。

公式导航通过 `PreviewKeyDown` 在原生文本处理之前处理方向键、Home/End、Ctrl+Home/End 和 Shift 扩展选择，按钮导航走同一路径。分子与分母间的上下移动优先寻找同一分式的另一分支。公式选择仍保存结构端点，并映射原生缓冲区的真正文本选区。IME 组合期间保留其缓冲区，组合结束后再绑定新的编辑上下文。

旧排版使用 `TextBlock.DesiredSize.Height`，把字体的段落行框当成每个数学字形的边界，导致分母、根号等逐层保留不必要的下行空间。仅解释字号和行高不同，或把光标拉长，并不能解决该视觉问题。现改用实际 ink bounds 和字体基线计算 ascent/descent；数字不再继承不存在的下伸留白，`g/j` 的实际下伸仍保留，边缘留 0.5 DIP 抗锯齿余量。行容器保持上下各 6 的对称 padding。

`TexGlyphMetrics` 分开记录前进宽度与 `InkLeft/InkRight`，绘图指令用 `OriginX` 表示字形原点，`Bounds` 表示实际墨迹边界；原生绘制和 SVG 均以原点与基线定位。相邻字形预留最小可辨间隔，光标置于墨迹间隙中间，行端光标避开斜体伸出。

光标高度按每个结构槽位单独计算：当前基线字形及下伸提供最小高度，再合并该槽位左右相邻原子的完整纵向范围，包括根号和括号笔画。位于分式旁边时覆盖分子到分母，位于根式、上下标组合或矩阵外时覆盖整个结构；进入分子、分母、上标或单元格后只使用对应局部行的高度。两个复合结构之间使用两者的范围并集，远处的高结构不会拉长同一行其他普通字符间的光标。例如 `\sqrt{\frac{9g-6}{9}}` 中，根号内部、分式之后的光标与整个分式同高；根号之外的光标才覆盖根号本身。高度变化不改变槽位标识、输入映射或编辑范围。

公式列表禁用用户点击选择，结果预览仍会自动强调最新答案。两者不再共用 ListView 的交互选择状态：`PreviewAsync` 设置 `FormulaListItem.IsPreviewResult`；`FormulaListItem` 独立跟踪鼠标进入和离开，叠加中性 `SystemControlHighlightListLowBrush` 的 hover。点击输入公式、其他行或空白处不改变结果强调所在行。

旧模板的 `SelectedBackground` 使用 `SystemControlHighlightListAccentLowBrush`，当前系统强调色产生了截图中的亮紫色实心底色；列表的 `RequestedTheme` 本身已经随应用深浅主题切换。现将自动结果强调改为主题强调色的 18% 透明度，文字保持当前主题前景色。hover 与结果强调独立，避免 `SelectionMode=None` / `IsItemClickEnabled=False` 使默认列表项不再进入 hover 状态。右键复制和图片导出继续通过右键目标定位。

## 验证

`scripts/check.ps1` 同时验证计算和 TeX 测试。现有括号与数值语义回归已迁移到真实 TeX 解析树，不再通过旧排版树验证。新增检查覆盖标准 TeX 输出、固定角度名称、跨项目依赖、源码映射、资源限制、几何边界、矩阵、分式、脚本、SVG 转义与区域格式，以及编辑的原子性、光标移动、结构删除、模板和撤销恢复。

Windows 字体与原生控件仍须实际预览；历史验证记录见 [verification.md](verification.md)。隔离预览通过根目录 `scripts/preview.ps1` 从已验证的 Release MSIX 准备，默认不安装；参数和注册步骤见 [开发与验证](development.md)。
