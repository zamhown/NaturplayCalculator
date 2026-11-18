# NaturplayMath 架构

## 值、精度和任务分离

~~~mermaid
flowchart LR
    Input[文本 / 类型化 API] --> Term[不可变 MathExpression 调用树]
    Term --> Exact[精确标量 / 矩阵 / 未求值调用]
    Exact --> Simplify[精确化简与定义域]
    Simplify --> Kernel[BigInteger 定点区间求值]
    Kernel --> Certify[检查最终误差界]
    Certify --> Format[两端舍入一致的显示]
    Certify -->|不足| Kernel
    Exact --> Store[无损 JSON]
~~~

有限小数保存成精确分数，循环小数保存为分子／分母，无理数保留符号。近似结果是独立区间类型，不会隐式替代精确输入。格式、角度、取消和资源预算不属于数学值的身份。

## 模块与依赖

| 项目 | 负责的内容 | 项目依赖 |
| --- | --- | --- |
| Core | Rational、RealInterval、整数／组合数算法、计算会话、基础异常 | 无 |
| Symbolics | 通用 MathExpression、规范实／复表达式、定义域、显式变换、矩阵与统计 | Core |
| Numerics | BinaryInterval、初等函数内核、误差目标、认证求值、异步数值入口 | Core、Symbolics |
| NaturplayMath | 双输入前端、MathEngine 统一流程、格式、JSON；独立实／复数进阶入口 | 三个数学模块 |

四个编译单元协同维护。使用方通常只引用入口项目；单纯分数算术可只引用 Core。通过项目依赖和测试防止反向引用。受控的 InternalsVisibleTo 允许下游访问不可变节点和共享会话，不公开任意构造非法 AST 的入口。

### 精确层

Rational 是不可变的 BigInteger 分子／正分母，始终约分，默认值为零。IntegerMath 提供整数幂、平方根／一般根的向下整数结果、GCD、LCM；Combinatorics 使用乘积树计算阶乘、双阶乘、排列组合。数学指数、根次数和进制均使用 BigInteger。

RealInterval 保存精确分数端点。误差传播支持四则、绝对值和整数幂；重叠区间的大小关系为 Undetermined。仅相同点区间能证明相等。

### 符号层

MathExpression 表示 Scalar、Matrix、Symbol、Call 四种不可变项。参数数组复制后只读，矩阵保留维度；调用不在构造时求值，未知函数及尚未判定的秩可以继续参与表达式组合。Rewrite 提供受取消和操作预算控制的自底向上重写，不暴露可变内部节点。

Expression 是已规范化的实标量表达式。MathExpression 保留操作和参数，适合程序构建、函数扩展与符号工作流；Expression 负责已有精确规则、定义域和高精度求值。复数或矩阵变量应在 MathExpression／解析文档阶段绑定；降为 Expression 后的变量是实数，后续代入必须遵守数域。原解析文档可以重复用于不同绑定。

Expression 包含不可变根节点和单独保留的定义域条件。工厂自动合并常量、同类项和幂，化简根式、常见三角特值与恒等式。默认不展开所有乘积，以免表达式规模指数增长。

显式 Expand 展开乘积与正整数幂；Cancel(variable) 对 Q(variable) 中的有理函数做多项式 GCD 约分，并保留原定义域；SimplifyRadicals 提取平方因子，试除因子最多到 1000 后做精确平方检查，未分解的部分保持符号。所有变换接受计算预算。Head、Arguments、SymbolName、ConstantValue、IntegerExponent 和 FreeSymbols 提供只读检查；FreeSymbols 包含仅存在于定义域中的变量。

默认紧凑表示与显式定向变换参考 [SymPy 的工程建议](https://docs.sympy.org/latest/explanation/best-practices.html#avoid-simplify)。当前尚未实现通用模式匹配、用户假设推理、求导、积分、方程求解或多变量多项式 GCD；开放项结构与重写入口用于后续扩展，不将它们标记为已完成 CAS 能力。

条件有 NonZero、Nonnegative、Positive、Defined。Defined 用于一般幂等需要联合判断多个参数的函数；即使函数项被抵消，仍要验证其定义域。Substitute 同时代入根和条件，重新化简，替换值携带的条件也会传播。JSON 读取重新构造内在条件，不能通过删掉保存的 conditions 绕过定义域。

ComplexExpression 用两个实表达式表示笛卡尔坐标，两个分量都携带整个复表达式的定义域。基本算术保持精确；初等函数归约到实数内核、主辐角、主平方根和复幂节点。一般复幂节点单独处理零底数，避免先展开 log 后错误排除零底数、正实指数的合法幂值。

矩阵存储复表达式。行列式使用无除法的特征多项式递推，不因选择符号枢轴而添加虚假的非零条件。逆使用伴随矩阵与行列式，保留真实的 det != 0 条件。底层秩要求证明每个零判定；统一符号入口保留未确定的 rank 调用，数值阶段再调用认证零判定。标量加矩阵不隐式解释成加单位阵，1×1 矩阵不自动退化为标量，行列式明确使用 det。

统计使用精确表达式累加，不因巨大偏移丢失方差。Covariance(x,y) 对第二项取共轭，Variance(x) 因此是模平方的均值；总体与样本方差明确区分。这与 [NumPy 的复数方差定义](https://numpy.org/doc/stable/reference/generated/numpy.var.html) 相同，不沿用旧应用的非共轭复矩约定。

结构相等不等于任意数学等价。内部规范键目前是字符串，极大型符号树会有额外内存开销；后续可在不更改公共数值语义的情况下换成节点索引。

### 数值层

每次求值建立本地 BinaryKernel 和节点缓存。BinaryInterval 的端点是 BigInteger，统一解释为端点除以 2^p；乘除和开方向外舍入。公开结果只在最终转换为 Rational 端点，没有在每个 Taylor 项上累积巨大分母。

流程是精确代入／化简 → 检查所有定义域 → 自底向上区间求值 → 检查最终宽度。精确有理常量直接返回点区间。失败时依据实际宽度、输入分辨率、三角归约的位数损失提高工作精度；初始位数估计只影响速度。

对数采用 AGM；π、sin/cos/exp 使用二分求和及参数归约；atan 采用逐步提高精度的 Newton 候选和残差认证。具体误差推导见 [accuracy.md](accuracy.md)。一般幂、根、反函数和复函数复用这些认证内核。

### 入口层

MathEngine.Parse 建立 MathDocument，保留源文本、注释和解析设置，除精确字面量外不执行数学运算，`1/0` 可以先解析再在求值时报错。规范语法保留命名变量、要求显式乘法；计算器语法支持单字母连乘、简写函数、根号、矩阵和注释。两者共用解析器，通过不同的运算域分别生成标量表达式或通用调用树。

自然输入与规范表达式分层参考 [SymPy 可选解析变换](https://docs.sympy.org/latest/modules/parsing.html#parsing-transformations-reference)。角度调用在新前端转成含精确 π／180 的弧度调用；MathExpression 不依赖后续 UI 设置。大进制数字使用 radix(base, quotedDigits) 明确标识，注释不参与数学值的身份。

MathEngine.Evaluate 执行精确运算，返回 MathResult，包含值、可选的有界步骤轨迹和注释；未知函数保持 Call，已知函数即使参数尚未求值也检查参数个数。Approximate 单独生成认证数值，CalculateAsync 提供一站式调用。嵌套秩判定调用数值核心，不提前发出 Completed；整个统一调用只发布一个最终完成事件。变量字典在异步启动前快照。

ExpressionParser 共用一套词法／优先级逻辑，通过实数和复数运算域构造相应表达式。输入字面量不读取显示精度。弧度／角度转换使用精确 π 和 180，显式 rad 函数绕过角度设置。

NumberParser 和 NumberFormatter 采用分治进制转换。2–36 进制使用常规字母数字，更大进制使用 `[十进制数码]`，以支持任意 BigInteger 进制。IntervalFormatter 先比较两个端点舍入后的整数单位，相同后只做一次输出转换。

CalculationEngine 协调完整流程。在同一内部会话内进行解析、代入、求值、显示和必要的再次细化；Completed 只在最终文本获得认证后发出。返回精确表达式以支持更换精度和进制。异步 API 使用 Task.Run 执行 CPU 工作，快照变量字典并链接取消令牌。

ComputationContext 可重复使用且默认每次调用独立；内部 bound copy 只在当前同步任务内共享 ComputationSession。进度为实际阶段、局部工作量、工作位数、检查点和耗时。同步回调需保持轻量，UI 可以传入 Progress<T>。

MathJson 用版本化对象存储表达式、条件、复数、矩阵、区间及通用调用树；数学整数编码为十进制字符串。当前 JSON 结构深度上限为 256，属于协议资源限制。MathExpressionFormatter 负责文本／TeX 展示；无损复制和历史使用 MathJson，不能重新解析展示文本来恢复条件。

## 明确边界

- 数学值不使用 float、double、decimal；int 等只用于容器、索引和资源策略。
- 精度没有人为数字上限，但受内存、时间和运行时容器上限约束。单次 BigInteger 操作不能被中途取消。
- 不保证任意符号等式、奇点和分支边界都能在有限资源内判定。区间无法认证时返回异常，不返回猜测数字。
- 分块输出面向已认证文本，目前不是无缓存的逐位生成器。
- 本次库独立交付，未替换 NaturplayCalculator 原有 UI 和计算引擎。矩阵 UI、历史记录和 TeX 编辑仍由应用负责。

## 扩展与测试

新增函数必须同时覆盖节点构造、精确规则、定义域、代入、JSON、显示、区间算法和参考测试。新增数域使用组合关系，不把复数、矩阵塞进 Rational 的继承体系。

[测试](../tests/NaturplayMath.Tests/)验证数学性质与外部契约；[Demo](../samples/NaturplayMath.Demo/)演示公开 API；[Benchmarks](../samples/NaturplayMath.Benchmarks/)负责高精度实际输出；[MPFR 校验工具](../tools/verify-benchmark.py)仅用于独立开发验收，不是库运行依赖。
