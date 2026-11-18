# NaturplayMath

基于 .NET 10 和 BigInteger 的精确符号／任意精度计算库。输入数字、整数、分数和表达式保持精确；仅在需要数值输出时生成带严格上下界的区间。显示位数和进制不会改变后续计算使用的原值。

已实际生成 `ln(sin(1))` 小数点后 **1,000,000 位**，并与独立 MPFR 定向舍入结果逐位核对一致。实测记录及复现方式见 [精度与性能验收](docs/verification.md)。百万位是已验证案例，不是精度上限。

## 使用

引用 [NaturplayMath.csproj](src/NaturplayMath/NaturplayMath.csproj)。数学库没有第三方运行时依赖。

~~~csharp
using NaturplayMath.Calculation;
using NaturplayMath.Computation;
using NaturplayMath.Formatting;

using var stop = new CancellationTokenSource();
var progress = new Progress<ComputationProgress>(p =>
    Console.WriteLine($"{p.Stage}: {p.Detail}, {p.CompletedWork}/{p.TotalWork}"));

var result = await MathEngine.CalculateAsync(
    "ln(sin(1))",
    new NumberFormat(1_000_000, trimTrailingZeros: false),
    context: new ComputationContext(progress: progress),
    cancellationToken: stop.Token);

// 每个分量的显示数字都通过了区间上下界舍入一致性检查。
await MathEngine.WriteAsync(result, Console.Out);

// 再次使用原表达式，不使用上一次显示的小数串。
var hexadecimal = await MathEngine.ApproximateAsync(result.Value, new NumberFormat(100, radix: 16));
~~~

`MathEngine` 是面向计算器的统一入口，支持标量、复数和矩阵。`Parse` 只建立表达式；`Evaluate` / `EvaluateAsync` 做精确求值，允许保留变量和未知函数；`Approximate` / `ApproximateAsync` 请求认证数字；`CalculateAsync` 串起整个流程。复数初等函数采用主值分支。

~~~csharp
using NaturplayMath.Text;
using NaturplayMath.Serialization;

var symbolic = MathEngine.Evaluate("expand((x+1)^2)", new(maxSteps: 8));
Console.WriteLine(MathExpressionFormatter.Format(symbolic.Value));
var document = MathEngine.Parse("sin30[角度]",
    new(syntax: InputSyntax.Calculator, angleUnit: AngleUnit.Degrees));
var half = MathEngine.Evaluate(document); // 精确 1/2，注释在 Comments
var matrix = MathEngine.Evaluate("transpose(matrix(row(1,2),row(3,4)))");
var restored = MathJson.DeserializeMathExpression(MathJson.Serialize(matrix.Value));
~~~

进阶接口 `CalculationEngine.Calculate`、`CalculateComplex` 和对应异步方法继续提供独立实／复数求值及细化策略，与新入口共用精确表达式、认证内核和格式化器。

## 计算与符号能力

| 范围 | API 或文本形式 |
| --- | --- |
| 精确数值 | BigInteger；规范 Rational 分数；精确有限小数／科学计数法 |
| 基本运算 | `+ - * / ^`、一般实数／复数幂、`sqrt`、`root(x,n)`、`x yroot n` |
| 初等函数 | `exp ln lg log(x,b)`；`sin cos tan`；`asin acos atan`；`sinh cosh tanh asinh acosh atanh` |
| 计算器别名 | `arcsin arccos arctan arsh arch arth`；`sinrad cosrad tanrad` 和反函数的 rad 别名 |
| 离散运算 | `! !! fact dfact nCr nPr choose permute mod floor ceil trunc` |
| 复数 | `i`；`abs conj arg re im`；ComplexExpression 与 ComplexEnclosure |
| 常量与变量 | `pi` / `π`、`e`、命名变量、由变量字典传入的 `Ans` |
| 矩阵 | ExpressionMatrix；统一入口支持 `matrix(row(...),row(...))`、`transpose adjoint det inverse rank`，保留矩阵形状 |
| 统计 | `mean variance samplevariance covariance correlation`；协方差对第二项取共轭，方差采用模平方 |
| 符号变换 | 默认紧凑化简；显式 `Expand`、有理系数单变量 `Cancel(variable)`、有预算的 `SimplifyRadicals`；文本支持 `expand` / `radicals` |
| 存储 | MathJson：实／复表达式、矩阵、实区间及通用 MathExpression；整数写为字符串，定义域无损保留 |

函数可以任意嵌套组合。实数模式拒绝不在实数定义域内的运算；复数模式采用 `arg(z) ∈ (-π, π]`，平方根取实部非负的主值。负实数的奇次 `root` 保持计算器的实根约定，复数 `^` 使用主值幂。

构造和代入时自动进行可证明的化简：有理数约分、合并同类项／幂、可消去根式、`sqrt(x²)=abs(x)`、`sin²(x)+cos²(x)=1`、π 的常见有理倍角、`ln(exp(x))=x` 等。`x/x` 约成 1 后仍保留 `x != 0`；`0*ln(x)` 仍要求 `x > 0`。符号相等采用规范结构，不通过小数近似猜测等式。

~~~csharp
using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using NaturplayMath.Numerics;
using NaturplayMath.Text;

var exact = ExpressionParser.Parse("(1/3)*3");
exact.TryGetRational(out var one); // 精确 1
var identity = ExpressionParser.Parse("sin(x)^2+cos(x)^2"); // 精确 1
var enclosure = Expression.Integer(2).Sqrt().Approximate(100);
// enclosure.Width <= 10^-100，端点都是精确分数。
~~~

## 解析与进制

统一入口默认 `InputSyntax.Canonical`：命名变量不拆分，乘法写 `*`，函数使用括号，`F(x)` 保留为函数调用。`InputSyntax.Calculator` 支持单字母连乘、省略已知函数括号、`√`、方括号注释、`(1,2,,3,4)` 矩阵和 `^T` 转置；`bc` 与 `x(2)` 在此模式是乘法。未知多字母函数使用规范入口。角度调用在解析时转换成精确 π／180 运算，保存后的表达式不依赖界面角度设置。

统一入口的大进制字面量使用 `radix(1000,"[12][345].[6]")`，明确区分数码与注释；进制参数按当前输入进制解析。下面的底层数值／标量解析 API 保留原有语法约定。

普通数码支持 2–36 进制；更大的任意整数进制用十进制数码 token，例如 1000 进制的 `[12][345].[6]`。进制本身是 BigInteger。`NumberParser.Parse` 和 `NumberFormat` 接受相同规则，改变进制不改变数学值。

表达式支持括号、隐式乘法、右结合乘方、科学计数法和 `0x` / `0b` / `0o` 数字前缀。延续主程序优先级：隐式乘法比显式乘除更紧，`a/2b` 等于 `a/(2*b)`，`-2^2=-4`。函数调用使用括号；变量名区分大小写，函数名不区分。输入为 16 进制时 `e` 保留为欧拉常量，数字 E 可写 `0xE`。矩阵通过类型化 API 构造，文本中的方括号用于大进制数码。

`ExpressionParseOptions` 分别设置输入进制、弧度／角度模式和解析深度；`NumberFormat` 设置输出进制、位数、向零截断／向下／向上／最近偶数舍入。没有全局可变精度或角度状态。

## 认证、异步与资源

内部使用 BigInteger 二进制定点区间和向外舍入。π 使用 Chudnovsky 二分求和；三角／指数函数使用参数归约和分块二分求和；对数使用有误差界的 AGM；反正切使用逐步增精度的迭代和残差界。推导与来源见 [算法与误差界](docs/accuracy.md)。

`Approximate(p, radix)` 返回宽度不大于 `radix^-p` 的区间；`ApproximationOptions` 也接受任意正 Rational 绝对误差目标。`Calculate` 会继续细化，直到区间两端在目标显示格式下产生相同结果。初始保护位只是性能估计，不能替代最终误差检查。

一次计算共用任务 ID、取消、预算和进度。进度提供阶段、当前算法真实工作量、工作位数、检查点数量和耗时；总工作量未知时 `TotalWork` 为空，不虚构整体百分比。并发调用各自隔离。`WriteAsync` 分块写出已认证结果，不提前泄露可能因进位而改变的未确认前缀。

`MathResult.Steps` 是可选、有界的精确求值轨迹，包含规则及变换前后表达式，与运行进度独立，不声称列出内核每一步代数推导。数值结果按单元格保存，输出不经过 UI 表达式树，但仍在内存中保留完整认证文本。文本／TeX 展示使用 `MathExpressionFormatter`；无损复制／历史使用 MathJson，展示文本不是存储协议。

数学数据不使用 float、double 或 decimal。机器整数仅用于索引、容器长度、显示位数和资源策略等控制信息。可用精度仍受时间、内存和 .NET 容器容量约束；取消会在算法检查点响应，不能中断正在执行的一次 BigInteger 原子运算。默认 `MaxRefinements=16` 可配置。

符号恒等式、零判定和分支边界并非都能自动证明。达到资源策略仍不能认证时抛 `InsufficientPrecisionException`，不会输出猜测值；矩阵 `Rank` 对无法证明的零判定抛 `UndeterminedRankException`，`RankCertified` 使用区间证明枢轴决策。此库提供数值求值与有限符号化简，不承诺完整 CAS、自动证明任意恒等式、积分或求解任意方程。

## 结构与验证

~~~text
src/
  NaturplayMath.Core/       BigInteger 分数、区间、整数与组合数算法、计算会话
  NaturplayMath.Symbolics/  实／复表达式、定义域、化简、矩阵与统计
  NaturplayMath.Numerics/   定点区间、高精度函数、认证求值、异步数值 API
  NaturplayMath/            解析、格式化、序列化、统一异步计算服务
~~~

依赖保持 Core ← Symbolics ← Numerics，入口引用三个模块。不可变 `MathExpression` 提供调用树、结构相等、自由变量和自底向上重写；规范实表达式提供 Head、Arguments、常量／符号／指数的只读视图，为后续 CAS 扩展提供基础。详见 [架构](docs/architecture.md)、[需求对照](docs/completion-plan.md) 和 [示例](samples/NaturplayMath.Demo/Program.cs)。主程序的精确结果模型、Ans／历史及大结果 UI 接入仍是应用侧工作，库尚不是旧引擎的直接替换件。

~~~powershell
dotnet restore math/NaturplayMath.sln --locked-mode
dotnet test math/NaturplayMath.sln --no-restore -c Release --blame-hang-timeout 2m
./math/tools/test-coverage.ps1
dotnet run --project math/samples/NaturplayMath.Demo/NaturplayMath.Demo.csproj --no-restore -c Release
~~~

测试覆盖精确数值、定义域、初等函数的独立参考、复数分支、矩阵与统计、进制往返、取消、进度和并发。覆盖率脚本要求全部测试通过，四个生产程序集的行覆盖率与分支覆盖率均精确达到 100%，报告写入 `artifacts/math-coverage/`；`-MeasureOnly` 仅用于开发时测量缺口。普通 CI 在 Windows、Linux、macOS 上运行同一检查；百万位验收由独立 benchmark 和 MPFR 参考校验完成。
