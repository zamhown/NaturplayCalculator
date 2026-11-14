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

本次最终源码的 149 项测试全部通过，Demo、锁定依赖恢复、仓库检查、actionlint 工作流检查及 git diff 空白检查通过。

~~~powershell
dotnet test math/NaturplayMath.sln --no-restore -c Release --blame-hang-timeout 2m
dotnet run --project math/samples/NaturplayMath.Demo/NaturplayMath.Demo.csproj --no-restore -c Release
./scripts/check-repo.ps1
./scripts/check-workflows.ps1
~~~

测试内容包括十万位整数、任意进制往返、精确续算、定义域抵消与保存、初等／复函数的独立参考、巨大角度和极小对数参数、严重相消、矩阵／统计、异步取消、预算、阶段和并发隔离。CI 配置覆盖三系统；本次实际执行的是 Windows 本机检查，未声称已经运行远程 CI。
