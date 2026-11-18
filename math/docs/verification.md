# 精度与性能验收

## 百万位主案例

表达式：`ln(sin(1))`，角度单位为弧度；十进制，小数点后 1,000,000 位，向零截断，保留尾零。

完整流程实际运行：精确文本解析 → 符号化简 → 区间求值 → 两端舍入一致性认证 → 输出文本。结果文件长 1,000,003 个 ASCII 字符，包含负号和 `0.`。

- 最新完整流程记录：**13 分 15.886 秒**；求值阶段 13 分 12.720 秒；峰值工作集 **257,277,952 字节**（约 245.36 MiB）。
- 独立校验：gmpy2 2.2.1 / MPFR 4.2.1，在 **4,000,160 bit** 下分别向下、向上计算 sin(1) 和 ln；两端截断结果一致，且与库输出的 **全部 100 万位** 一致。
- SHA256：`48680E825FC7BEE80608EF4264D9CB6BC2449BB391F279331836B0C1E1018B59`。
- 输出前缀：`-0.17260374626909167851341097586390906984010840889640`。
- 最后 50 位：`67232566098231293696397657956400851068031315403160`。

本地生成文件保存在 `artifacts/math-benchmarks/full-pipeline/ln-sin-1-decimal-1000000.txt`，生成目录已由仓库忽略。紧凑的运行报告与独立校验报告归档于 [benchmarks](benchmarks/)，记录了加载程序集的 SHA256，不把运行中的源文件修改误当成同一程序集快照。

## 阶梯与优化记录

环境：Windows，本机 Intel Core i7-14700KF，.NET 10.0.14 / SDK 10.0.403，Release。测试过程中机器同时运行开发任务，没有固定频率或专门隔离核心，耗时仅代表本机记录。

| 小数位 | 运行批次 | 总耗时 | 独立校验 |
| --- | --- | --- | --- |
| 1,000 | 早期内核 | 约 0.275 秒 | 全部位一致 |
| 10,000 | 早期内核 | 约 1.063 秒 | 全部位一致 |
| 100,000 | 初始 AGM | 约 65.96 秒 | 全部位一致 |
| 100,000 | AGM 优化 | 约 24.91 秒 | 全部位一致 |
| 100,000 | 完整流程／单次格式转换 | 约 15.346 秒 | 全部位一致 |
| 1,000,000 | 初始百万位运行 | 19 分 37.341 秒 | 全部位一致 |
| 1,000,000 | 完整流程／精度起点与格式优化 | 13 分 15.886 秒 | 全部位一致 |
| 100,000 | 最终源码回归 | 约 15.446 秒 | 全部位一致 |

这些记录来自开发过程的不同程序集快照。优化前后同精度输出散列一致；新函数、解析与边界处理仍由当前源码的回归测试覆盖。百万位验证只针对指定主案例，不代表所有初等组合、全部分支或矩阵规模都有相同耗时。

百万位已验证，但目前纯托管 BigInteger 实现的速度仍有优化空间。没有以牺牲误差界换取速度，也没有引入运行时浮点／MPFR 依赖。

## 复现

~~~powershell
dotnet restore math/NaturplayMath.sln --locked-mode
dotnet run --project math/samples/NaturplayMath.Benchmarks/NaturplayMath.Benchmarks.csproj --no-restore -c Release -- 1000000 artifacts/math-benchmarks/reproduction
~~~

程序持续输出实际计算阶段和工作量；Ctrl+C 请求取消。生成 `.txt` 以及含时间、峰值工作集、SHA256、程序集 SHA256 的 `.json`。

独立参考在单独 Python 环境安装，仅用于开发校验：

~~~powershell
python -m venv artifacts/mpfr-reference
artifacts/mpfr-reference/Scripts/python -m pip install gmpy2==2.2.1
artifacts/mpfr-reference/Scripts/python math/tools/verify-benchmark.py artifacts/math-benchmarks/reproduction/ln-sin-1-decimal-1000000.txt
~~~

Linux/macOS 的 Python 路径为 `artifacts/mpfr-reference/bin/python`。工具必须确认独立上下界得到同一字符串，再逐位比对并写出 `.verification.json`；仅比较前缀或散列不构成独立求值校验。

## 常规回归

2026-11-15 统一入口与符号架构改造后，504 项测试全部通过，无跳过；四个生产程序集的行覆盖率为 **2480/2480（100%）**，分支覆盖率为 **2974/2974（100%）**。统计来自 Release 构建的 Coverlet 10.1.0 报告，保留自动属性覆盖，没有按源文件、方法或属性排除生产代码。

本轮先用测试复现负奇数双阶乘、复数方差、自然输入歧义、角度语义保存、未求值参数的参数个数校验以及嵌套认证过早完成的问题，再修改实现；新增 API 先以契约测试定义。新增验收包括统一标量／矩阵入口、不可变调用树与重写、显式代数变换、定义域／JSON 往返、步骤、异步快照和分块输出。

Windows win-x64 NativeAOT 示例已成功发布并运行，覆盖两套求值入口、矩阵 JSON 往返、符号展开／约分和认证输出。发布使用独立构建输出和锁文件，不改常规依赖锁：

~~~powershell
dotnet publish math/samples/NaturplayMath.Demo/NaturplayMath.Demo.csproj -c Release -r win-x64 -p:PublishAot=true -p:RestoreLockedMode=false -p:NuGetLockFilePath=obj/aot.packages.lock.json --artifacts-path artifacts/math-aot-build -o artifacts/math-aot-review
./artifacts/math-aot-review/NaturplayMath.Demo.exe
~~~

此检查验证数学库的 NativeAOT 示例，不等同于主程序 WinUI 接入验收。百万位记录仍对应上方记录的历史程序集，本轮未重新执行百万位基准。

覆盖率入口为 [test-coverage.ps1](../tools/test-coverage.ps1)，要求全部测试执行成功、报告恰好包含四个生产程序集，并比较整数计数，不把 99.99% 四舍五入成通过。Windows、Linux、macOS 的 CI 使用同一入口，并上传 TRX、JSON、Cobertura 和 OpenCover 报告；本次实际运行的是 Windows 本机检查，远程三系统结果仍以 CI 为准。

普通精度用例通过 `System.Math` 与 `System.Numerics.Complex` 独立计算参考值，浮点容差仅用于测试比较；库返回的误差宽度仍用精确 Rational 单独检查。高精度用例保留 30–50 位独立常数、指数函数的有理级数上下界、相消后的独立代数表达式，并将 `ln(sin(1))` 的 1000 位结果与 [MPFR 参考数据](../tests/NaturplayMath.Tests/ReferenceData/ln-sin-one-1000.json) 逐位比较。

该参考数据由 gmpy2 2.2.1 / MPFR 4.2.1 在 4160 bit 下分别向下、向上计算 `log(sin(mpfr(1)))`，再按相应方向乘以精确 `10^1000` 后向零截断；只有两个结果完全一致才保存。生成过程独立于 .NET 库，常规单测直接读取嵌入的固定数据，不需要 Python 或 MPFR 运行时依赖。较大规模历史验证仍见上方百万位记录，本次未重跑百万位基准。

补齐测试时同时去除了由既有不变量保证不可达的重复检查：已经判过的词法运算符、已处理的零复数底数、重叠点区间的重复相等比较、复数两个分量共享定义域的重复判断等。数值内核的级数入口明确只接收归约后的非负小块，负号仍由外层函数处理。测试通过 friend assembly 检查内部区间与算法契约，没有扩大公共数学 API。

100% 覆盖率说明所有统计到的行和分支均已执行，并不构成任意输入下数学正确性的证明；独立参考、精确不变量和失败边界测试共同约束准确性。

~~~powershell
./math/tools/test-coverage.ps1
dotnet run --project math/samples/NaturplayMath.Demo/NaturplayMath.Demo.csproj --no-restore -c Release
./scripts/check-repo.ps1
./scripts/check-workflows.ps1
~~~

测试内容包括十万位整数、任意进制往返、精确续算、定义域抵消与保存、初等／复函数的独立参考、巨大角度和极小对数参数、严重相消、矩阵／统计、异步取消、预算、阶段和并发隔离。CI 配置覆盖三系统；本次实际执行的是 Windows 本机检查，未声称已经运行远程 CI。
