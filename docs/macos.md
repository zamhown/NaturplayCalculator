# macOS / Uno Desktop

Mac 应用使用 Uno 7 的 Skia Desktop AppKit 宿主，复用计算、会话、公式排版和光标编辑内核。入口是 [Desktop 项目](../ncalc/NaturplayCalculator.Desktop/NaturplayCalculator.Desktop.csproj)，可用 [Desktop 解决方案](../ncalc/NaturplayCalculator.Desktop.slnx) 打开；Windows 继续使用原 WinUI 3 项目。

## 构建

安装 [global.json](../global.json) 指定的 .NET 10 SDK 和 Xcode Command Line Tools。在 macOS 仓库根目录运行：

```sh
./scripts/build-macos.sh --run
# 仅在有意更新依赖时更新锁文件
./scripts/build-macos.sh --update-lock
# 在 Mac 上交叉打包 Intel 版本
./scripts/build-macos.sh --arch x64
```

脚本默认执行共享回归测试和锁定还原，生成自包含 `.app` 与保留执行权限的 ZIP，输出到 `artifacts/macos/osx-arm64/` 或 `artifacts/macos/osx-x64/`。接收方无需安装 .NET。`--skip-tests` 适用于测试已通过后的界面打包迭代；`NATURPLAY_DOTNET` 可指定 SDK 可执行文件。不要并行构建不同架构，以免共享 `obj` 目录互相覆盖。

Uno SDK 版本在 `global.json` 固定，运行时包由 Uno SDK 管理。各项目的 `packages.osx-*.lock.json` 独立保存发布时加入的运行时依赖，不改变 Windows 和可移植构建的锁文件。Desktop 的 `packages.*debug.lock.json` 另行锁定 Uno 只在 Debug 中启用的开发工具；Release 不包含这些工具。

本地包使用临时签名，没有 Developer ID 签名或 Apple 公证。面向外部分发时应配置开发者证书、公证和隔离冷启动验收。

## 材质和交互

`DesktopAcrylicBackdrop` 在 AppKit 宿主中创建真正的 `NSVisualEffectView`，使用窗口背后的系统模糊与活跃状态。上层 XAML 只提供透明度、明暗层次和边界；没有截图模糊或背景位图。实现依据 [Uno 系统背景文档](https://platform.uno/docs/articles/features/system-backdrop.html)。

所有 UI 灰阶从 AppKit 的 `NSColor` 语义色读取，按窗口当前的 Aqua / DarkAqua 或高对比外观解析为 sRGB，并保留系统 alpha。主材质由 `NSVisualEffectView` 自行处理；函数与运算两侧共用 `underPageBackgroundColor`，中央数字区采用 `controlBackgroundColor`，常数栏、编辑工具区和四则区不再叠加额外灰色分区。正文与公式使用 `labelColor`，辅助文字、图标和中间步骤使用 `secondaryLabelColor`；分隔线、选择及悬停反馈也来自系统语义色。菜单和对话框的 Uno 灰阶资源同步映射，系统外观、对比度或强调色变化时重建调色板并保留输入选区与局部编辑状态。等号使用 `NSColor.systemRedColor`，DEL / AC 和第二组函数选中态使用 `systemOrangeColor` 配系统白字；编辑光标、焦点下划线及错误分别使用系统强调色、焦点色和红色。全部通过同一原生语义色桥接获取，不再保留手写 RGB／十六进制颜色。

沿用 Windows 的半透明分区、羽毛图形与红色等号。Mac 版提供浅色、深色、跟随系统，以及关闭毛玻璃的外观选项；保留系统窗口按钮、拖动、缩放和原生文件面板。历史记录在所有窗口尺寸下均通过工具栏按钮弹出，不占用常驻侧栏。

默认以 500 × 748 的紧凑窗口启动，最小尺寸为 300 × 360 个逻辑像素。标题拖拽区保持 32 个逻辑像素，下面的内容通过 `Viewbox` 随窗口尺寸统一缩放；以 748 为内容设计高度，并限制高窄窗口的放大倍率，使内容设计宽度至少为 320。普通竖屏中，演算区、输入带、键盘按 6:1:7 分配高度；输入带左端为菜单、右端为系统红色等号。键盘按 3:5:3 分为函数、数字和运算区域，数字区顶部为 44 pt 高的编辑工具区，其余高度交给数字行，侧栏按七个等高行组织。DEL / AC 使用系统橙色强调，按键文字通过仅允许缩小的独立 `Viewbox` 适配单元格，完整点击面积保持不变。数字、函数、四则、进阶运算、清除和等号的设计字号分别为 30、16、22、19、15 和 24；全部按键文字居中，随外层窗口放大的最终字级不超过设计值的 1.1 倍，较大的窗口主要增加留白。

当窗口宽高比达到 4:3 时，界面切为左翼、中央和右翼宽度 1:3:1 的横屏布局。左翼展开函数；中央从上到下为 44 pt 高的编辑工具区、输入带和演算区，后两者按 1:13 分配剩余高度；右翼从上到下为清除、括号、四则、数字和进阶运算，高度比为 1:1:2:5:3。当竖屏键盘的高度达到窗口宽度时，界面改为全宽堆叠的窄长布局，顶部编辑工具区保持 44 pt，其余高度由上方运算、数字、下方运算和函数按 2:4:1:2 分配。横竖切换依据窗口比例，持续拖动尺寸时更新内容缩放；跨布局切换保留输入选区和公式局部编辑状态。初始尺寸尚未确定时不进入窄长布局。

空态与数字区顶栏复用 Windows 的羽毛和手写 sin 图形，`Controls/BrandMark.xaml` 保留原 SVG 的四条矢量路径，并通过控件 `Foreground` 随主题调色。界面文字采用无衬线字体，模式切换使用小型圆角标签；原式、步骤与带等号的结果从左向下连续排列，不再使用衬线字标、右对齐结果标签或编号式历史侧栏。布局在 `MainView.Design.cs`，按键的悬停、按下与键盘焦点样式在 `InstrumentStyles.xaml`。

每行公式独立横向滚动，整个公式区纵向滚动，长原式不会把短结果推离可见区域。“过程”按钮切换中间计算步骤，连续相同的步骤和最终结果从过程列表中去重，最终结果保留独立大字号。工具按钮位于演算区底部，设置菜单位于输入带，均避开 AppKit 标题拖拽区；外观切换在菜单关闭后重建界面并恢复输入焦点。编辑工具区提供撤销、关于和重做；公式结构移动保留方向键和公式右键菜单。底部没有常驻状态条，计算进度、错误和保存提示按需出现在演算区。

工具栏图钉和设置菜单提供置顶开关。开启后复用现有的可编辑窗口，通过 AppKit 的浮动窗口层级、collection behavior 和 `NSApplication` 的 `activationPolicyAccessory` 加入所有 Spaces，包括其他应用的全屏空间。Accessory 策略仅在置顶期间启用：Dock 入口暂时隐藏，同时禁用窗口最小化，避免单个窗口被收起后失去恢复入口。关闭置顶后恢复原来的应用激活策略、窗口层级、集合行为和最小化能力。跨空间显示的是同一个窗口，不创建副本；置顶偏好随外观设置保存。

公式通过共享 `Naturplay.Tex` 的真实字形度量、绘制指令和编辑映射渲染到 Uno GPU 画布。Mac 使用系统 STIX 数学字体。数值、符号、复数、矩阵、统计、RAD / DEG、Ans、步骤、JSON / XML 查看与会话格式复用同一计算内核。

- 回车计算，Escape 清空或取消；⌘L 选中算式，⌘⇧C 复制可重新解析的结果，⌘S 导出 SVG。
- 复制、剪切、粘贴、全选和输入法由 Uno 文本框处理，避免重复处理系统快捷键。
- ⌘Z / ⇧⌘Z、原生“编辑”菜单和屏幕按钮共用工作区撤销记录，覆盖按键、粘贴、局部公式编辑、AC、模式和角度切换。恢复草稿及其结果，不回滚全局 Ans 或计算历史；记录限于本次打开的工作区，并有步数和文本容量上限。
- 菜单图形按等号的实际字形墨迹宽度缩放，保持两者视觉宽度一致。撤销／重做围绕四向导航区排列；导航区使用系统填充色，中央羽毛保留关于入口。方向按钮可长按连续移动，布局随窄屏／横屏调整，点击区域与图形尺寸分开控制。
- 运算按钮按光标和按键类别编辑；局部作用域只限定可编辑范围，不等同于选中整个区域。四则运算在光标处插入并遵守正常优先级；平方、倒数、阶乘、百分数、幂作用于紧邻左侧的完整操作数；sin、√、括号等在光标处建立向右填写的模板。明确选中文字后才包裹选区，并保护外部表达式的优先级。÷ 是行内普通除法，键盘上的 a/b 进入分子／分母模板；指数、根式与分数内部保持局部输入，通过输入框折箭头或 ⌥→ 逐级退出。右括号只关闭括号或函数。矩阵模板插入新矩阵，并选中可直接替换的零值单元格。
- 点击公式的分子、分母、指数或矩阵单元格可进入局部编辑。局部输入框右侧的上箭头逐级回到父作用域，光标停在退出结构的末尾；到达全文后恢复清除按钮。导航保留原式、计算上下文和撤销记录，分式外侧的后续输入也保持在分母之外。
- 结果后按数字、常数或前缀函数开始新算式；按四则、幂、平方等后接运算以显示结果作为 Ans 续算。点击旧公式、移动光标、删除或显式选中文字则编辑原式，保留其历史 Ans 上下文。小数点按当前数字处理；e、π、i、Ans 按完整常数插入，避免 `2 → e → 3` 被当成科学计数法 `2e3`。
- 历史恢复保留原计算结果及角度，不改写 Ans；编辑或取消会使旧计算结果失效。
- 四向按钮与无修饰键的物理方向键共用结构导航：左右经过可插入的数学位置，上下在所属分数、幂、根式或矩阵同列内移动，不跳入无关项。结果出现后，左键在原式末尾进入编辑，右键在开头进入编辑；上下回看更早／较新的计算，跳过当前已显示记录。空白草稿按上键回看最近一条，下键可回到原空白草稿；未完成表达式的上下键保持结构编辑。输入框右侧折箭头表示退出结构，⌥← / ⌥→ 分别跳到所属结构之前／之后；2nd 后按左右按钮作用相同。长结果的左右键优先滚动，点击原式或 ⌘L 进入编辑。
- 输入公式采用 150 ms 缓出的字形与结构补间：已有字形、分数线、根号笔画移动到新位置，新增／删除内容淡入淡出；快速输入从当前帧续接。光标、命中与输入始终使用目标布局，不等待动画。读取 macOS“减少动态效果”偏好；大表达式超过图元预算时立即显示。
- 设置菜单可导出会话，或通过系统文件面板打开会话、JSON、XML。

### 科学计算器输入依据

按键模型参考 Casio ClassWiz 的 [基本输入](https://support.casio.com/global/en/calc/manual/fx-570CW_991CW_en/inputting_expressions_and_values/basic_input_rules.html)、[自然书写模板与已有表达式作为参数](https://support.casio.com/global/en/calc/manual/fx-570CW_991CW_en/inputting_expressions_and_values/inputting_an_expression_using_natural_textbook_format.html)、[幂与根式](https://support.casio.com/global/en/calc/manual/fx-570CW_991CW_en/basic_calculations/powers_power_roots_and_reciprocals.html)、[分数](https://support.casio.com/global/en/calc/manual/fx-570CW_991CW_en/basic_calculations/fraction_calculations.html) 和 [运算优先级](https://support.casio.com/global/en/calc/manual/fx-570CW_991CW_en/technical_information/calculation_priority_sequence.html)。采用自然书写的前缀、后缀和模板边界，不模拟具体机型的全部键盘。

桌面适配保留文本选区、鼠标点选和多步撤销。(-) 输入负数前缀，2nd 只作用于下一键；减法键替换待填写的二元运算符属于桌面输入容错，不作为 Casio 官方规则。普通键盘 / 映射到行内 ÷，分数模板及粘贴的旧 / 表达式保留上下分式。源码粘贴和科学记数法不被按键层重排。

隐式乘法保留 Casio 的因子分组：`6÷2(1+2)=1`，明确输入乘号则 `6÷2×(1+2)=9`；常量 e 与科学记数法使用不同词法边界。精确结果旁可切换小数显示，不改变原结果、Ans、模式、历史和计算过程；数值结果不伪造精确来源。连续等号以最新 Ans 迭代，历史重放的第一次计算保留原 Ans。错误提供源位置及定位入口，DEL 识别完整函数、空根式、空指数和空分数；局部结构边界上的 DEL 先回到所属结构边界。

当前输入结构使用系统主文字色，其余部分使用系统次级文字色。长公式保持水平滚动位置并跟随光标。结果格式切换参考 [FORMAT](https://support.casio.com/global/en/calc/manual/fx-570CW_991CW_en/changing_calculation_result_format/using_the_format_menu.html)，错误定位参考 [Errors](https://support.casio.com/global/en/calc/manual/fx-570CW_991CW_en/technical_information/errors.html)。

方向键的结果回看参考 Casio 的 [Calculation History and Replay](https://support.casio.com/global/en/calc/manual/fx-570CW_991CW_en/basic_calculations/calculation_history.html) 和 [Replay Function](https://support.casio.com/pdf/004/fx9750g_ch02.pdf)。普通上下键不等于首尾跳转；内联矩阵同列移动、保留空白草稿和桌面长按速度是本产品适配，并非声称复刻某台硬件的全部行为。

## 本地数据与验收

会话和外观保存在 `~/Library/Application Support/Naturplay Calculator/`。保存采用串行、临时文件和原子替换，保留上一个有效会话备份；损坏原文件和导入前的会话另存为独立恢复文件。测试可用 `NATURPLAY_DATA_DIR` 指向隔离目录。

`~/Library/Logs/Naturplay Calculator/window.log` 记录实际缩放比例、窗口尺寸和原生毛玻璃视图；异常写入同目录的 `errors.log`。日志只用于验收，不进入产品界面。

发布前必须在实际 `.app` 中检查：冷启动、键盘和按键计算、公式局部编辑、切换主题保留结果、矩阵与精确分数、原生保存并回读 SVG、会话导入、退出重开恢复结果，以及窄窗口历史。构建和签名验证不能替代这些操作。Intel 包需在 Intel Mac 上另行执行相同验收。

### 2026-11-15 本机记录

在 macOS 27.2 / Apple Silicon 上，Release 自包含应用已实际完成冷启动、Command 粘贴、精确分数与根式、多项式展开、矩阵求逆、分母局部编辑、历史恢复、窄窗口历史弹层、深浅色切换、原生 SVG 保存与文件回读、会话导出再导入，以及退出重开恢复。例：点击 `(9-6)/9` 的分母，再按 `+`、`1`，得到 `(9-6)/(9+1) = 3/10`。

652 项共享测试、17 项脚本测试、仓库检查和 actionlint 通过；Debug 锁定构建与两种架构的 Release 锁定打包通过。两种应用包均通过 `codesign --verify --deep --strict`。Intel 仅交叉构建，远端 CI、Intel 实机与 Apple 公证不计为已验收。

本日上一轮宽版界面重设计后再次验证：32 项相加的长原式与短结果保持独立布局、矩阵求逆、分母局部编辑得到 `3/10`、第二组函数切换、外观切换后立即使用 ⌘L 输入、窄窗公式滚动与历史弹层。该轮 ARM64 / Intel Release 包均已重新生成，仓库检查通过。

上一轮 Windows 同源紧凑布局恢复中央大数字、两侧函数与运算、羽毛空态和连续输入带，并在宽窗口使用两翼布局。两种架构的 Release 锁定打包、签名验证和 411 项源文件／配置仓库检查通过。在 ARM64 实机再次验证冷启动与草稿恢复、精确根式与分数、鼠标等号计算、清空后的羽毛空态、历史弹层点击矩阵恢复、深浅色切换、窗口放大与还原。修复并实际验证已计算后的箭头编辑：`12+3=15` 后点击左箭头、数字 `2`，原式变为 `12+23`。共享内核未在该轮修改，沿用前述 652 项回归结果。

该轮截图保存在 `artifacts/macos/naturplay_compact_dark.png`、`naturplay_compact_welcome.png` 和 `naturplay_wide_dark.png`。系统窗口共享标记由 macOS 自动显示，不属于应用界面。自动化工具在应用失去前台时出现过鼠标不可用及合成键盘异常；重新启动后的前台鼠标路径已重新验证，未将后台自动化结果计为键盘焦点遍历验收。

本轮继续对齐 Windows XAML 的比例和缩放策略，新增 4:3 横屏断点、窄长堆叠布局和跨 Spaces 置顶，调整计算过程展示并移除常驻状态条。系统窗口菜单的 Fill 操作已实测切换为 1:3:1 横屏布局。算式 `(2+3)*(4+5)` 的过程依次显示 `5(4+5)`、`5×9`，最终结果 `45` 只显示一次。

置顶已在 Finder 的全屏空间中实测。最初仅使用浮动窗口层级与 collection behavior 的方案未通过此场景；最终实现增加置顶期间的 Accessory 激活策略，窗口进入全屏空间并保持可见。运行时记录为 `Pinned=True`、`Level=3`、`CollectionBehavior=0x41109`、`OnActiveSpace=True`、`Visible=True`、`OcclusionState=0x2002`、`ActivationPolicy=1`、`StyleMask=0x800B`。取消置顶后恢复为 `Level=0`、`CollectionBehavior=0`、`ActivationPolicy=0`、`StyleMask=0x800F`，Dock 入口与最小化能力恢复。

本轮已实测默认 500 × 780 窗口、约 401 × 780 窄窗和 1728 × 1021 横屏，以及正常退出后原会话恢复。原生自动化工具在进一步调整为窄长窗口时出现 `noWindowsAvailable` 和拖动中断，因此未将窄长断点计为实机通过；Uno AppKit 源码确认 presenter 最小宽高会直接传递到 `NSWindow.minSize`，没有另外叠加原生最小尺寸约束。

系统灰阶改动已通过独立 AppKit 取色核对浅色、深色及系统 alpha，UI 调用统一使用语义名称，不将采样 RGB 写回代码。系统灰阶最终版本在 ARM64 实机完成深浅色冷启动、原会话恢复和浅色横屏检查；浅色验收通过启动偏好切换，不计为本轮菜单点击切换已通过。新增桥接未修改计算内核。ARM64 / Intel Release 锁定打包及 `codesign --verify --deep --strict` 均通过，`git diff --check` 通过；Intel 仍只交叉构建，未做实机验收。截图为 `naturplay_system_dark.png`、`naturplay_system_light.png`、`naturplay_system_wide.png`。

强调色最终统一为运行时 `NSColor`：等号为 `systemRedColor`，DEL / AC 为 `systemOrangeColor`，不再使用 Calculator 资源取样值或手写深浅色值。公式 GPU 绘制中的光标与空槽也接入系统色；SVG 导出文字使用 Aqua 下的 `textColor` 读回值。颜色变化检测包含系统红、橙和焦点色。

按键细节一轮收敛后，ARM64 实机检查了 500 × 780 竖屏和 1728 × 1021 横屏：按键文字更小并保持居中，侧栏灰阶统一，放大窗口时字号不超过设计值的 1.1 倍；常数栏 `2nd` 使用独立水平留白避免过度压缩。原会话正常恢复。ARM64 / Intel Release 包重建、签名校验和 diff 格式检查通过。截图为 `naturplay_refined_portrait.png` 和 `naturplay_refined_wide.png`；本轮原生自动化未能完成 2nd 点击，未将该次尝试记为交互通过。

随后修正了行框居中仍导致字形偏移的问题。独立数学符号使用 `CenteredSymbol`，按 Uno 实际基线及同字体的 Skia 墨迹边界校正位置；数字、函数及小数点／逗号继续共享各自的文字基线。菜单符号显式使用系统 Apple Symbols，避免测量 Helvetica Neue 缺失字形。`ExpressionTextBox` 直接居中内部文本元素，并以 `TrimToCapHeight` 去掉顶部多余空间、保留下行空间；正文、占位文字、光标与选区使用一致的行度量。

最终 ARM64 竖屏截图中，输入带中心为 y=758.5，原算式文字墨迹中心为 757.5、等号为 758、菜单为 759（Retina 截图像素）。实测选区、中文与重音输入未被裁切，恢复 `Abs(1,2,,5,3)` 后回车得到 `-7`。数学符号修正在横屏也已检查；最后的输入行框修正完成了竖屏实测。两种架构 Release 包已重建并通过签名检查。最终竖屏截图为 `naturplay_alignment_portrait.png`。

编辑工具一轮更新后，676 项共享测试通过。ARM64 实机已验证：选中 `a+b` 插入分数后进入分母、填写 `2` 并计算；矩阵首格替换为 `5`；局部编辑的 ⌘Z／⇧⌘Z；AC 后通过按钮恢复完整原式和结果，再重做清空；横屏放大和还原。实测修复了模板菜单关闭时全文输入框抢回焦点的问题。截图为 `naturplay_editing_tools_portrait.png`、`naturplay_structure_menu.png` 和 `naturplay_editing_tools_wide.png`。本轮测试前的用户会话另存于 `artifacts/macos/qa-before-editing-tools/`，验收后恢复。

运算符作用域更新后，748 项共享测试通过。ARM64 实机验证：选中 `7/9+2` 的分母按 √，再按 ÷、2，得到 `7/(sqrt(9)/2)+2 = 20/3`；指数中连续建立幂与根式；矩阵第三格 `3+4` 连续平方、根号和正负切换，其余三格不变；撤销后仍停留在整个矩阵格；菜单行列式得到 −2；按钮输入 `2×−3` 得到 −6，再平方续算得到 36；关于按钮正常打开。新增测试覆盖历史公式的 Ans 上下文、续算、会话序列化与撤销重做。ARM64 / Intel 发布包及签名均更新，Intel 尚未在实际 Intel Mac 上验收。用户会话备份位于 `artifacts/macos/qa-before-scope-operators/`，验收结束恢复；局部矩阵操作截图为 `naturplay_scope_operators_matrix.png`。

自动括号精简后，760 项共享测试通过。运算符生成源码时，以严格解析后的 AST 结构判断是否需要额外分组，保留原有输入、注释及空槽；不通过整式重格式化修改原文。实机重现 `9 → xʸ → 6 → + → 9`，完整源码为 `9^(6+9)`，不再生成 `(9)^((6)+9)`。测试同时验证 `(2+3)^2`、`(-2)^2`、`(2^3)^2`、紧凑函数参数和重复阶乘等必须保留的括号。当前用户草稿已改为相同表达式的精简形式，历史及其他会话字段不变；原会话备份为 `artifacts/macos/qa-before-parentheses/`，截图为 `naturplay_parentheses_refined.png`。

局部输入的“上一级”更新后，781 项共享测试通过。ARM64 实机验证：`9^(6+9)` 的指数退出后回到全文；`10+sqrt(1/(2+3))+4` 的分母逐层返回根号内部、再返回完整算式，光标停在根式之后、末项 `+4` 之前；`1/2+3` 退出分母后按 `4` 得到 `1/2*4+3`，撤销及切换角度模式仍保留分式外侧位置。空指数 `2^()+3` 中按钮保持可用，返回后空槽不丢失；全文清除按钮正常工作。纯作用域导航不修改算式或新增撤销记录。ARM64 / Intel Release 包均重新构建并通过签名检查，Intel 仅交叉构建。验收后按字节恢复 `artifacts/macos/qa-before-parent-scope/` 中的原会话、备份及外观，测试状态另存为 `artifacts/macos/qa-parent-scope-result/`。

### 2026-11-16 科学计算器输入重设计

873 项共享测试通过，覆盖按键顺序、前缀／后缀、选区、模板边界、常数与小数、Ans 上下文，以及撤销后的编辑位置。ARM64 实机通过：`2+3×4=14`、`8÷2+2=6`、`2+3` 后按平方得到 `2+3^2=11`；根号参数退出后得到 `sqrt(9)+4=7`；指数通过 ↑ 退出后得到 `2+3^(2)+4=15`。独立分数模板中输入分母 `2+2` 得到 `8/(2+2)=2`，点击分母首项后平方只修改该项。嵌套根号的右括号逐级退出。

完成平方后继续输入数字保持在结构之外：`8÷2 → x² → 3` 得到 `8/2^2*3`。撤销再按平方保持原来的完整操作数，得到 `8/(2^2)^2=1/2`。计算后点击源码也能恢复原式编辑：`12+3=15`，点击 `12` 后按 `4` 得到 `124+3=127`；不进入编辑直接按数字则开始新算式。修复使用包含已处理事件的指针订阅，因为 Uno TextBox 会先处理点击再定位光标；原生清除按钮与非左键不触发此恢复逻辑。自动化窗口失去前台后的一组点击未生效，使用窗口的 Raise 动作恢复前台后重测通过。

最终 ARM64 / Intel Release 包均已重建并通过 `codesign --verify --deep --strict`，`git diff --check` 通过；Intel 仅交叉构建。验收后按字节恢复 `artifacts/macos/qa-before-scientific-input/` 的原会话、备份和外观，重新打开最终 ARM64 应用并核对草稿 `sqrt(9+6)`、原有 27 条历史及 RAD 设置。测试状态另存于 `artifacts/macos/qa-scientific-input-result/`。

### 2026-11-16 四向导航与表达式动画

907 项共享测试通过，新增结构方向导航、Replay 历史边界／Ans 上下文及动画插值、重复字形、快速续接、居中补偿和大表达式预算测试。ARM64 实机冷启动后检查了四向导航区和菜单图标；从已计算的 `9^(2)=81` 按左键，再按 `+2` 得到根作用域的 `9^(2)+2`。连续计算 `2+2=4`、`3+3=6` 后按上键直接显示前一条 `2+2=4`。

随后检测到用户正在原生窗口中试用，停止覆盖输入，保留当时的 `8+3/2` 局部编辑状态；未将后续尚未完成的下键／右键操作计为实机通过。开始前的原会话、备份和外观存于 `artifacts/macos/qa-before-navigation-motion/`（草稿 `9^(2)`，29 条历史），未覆盖用户正在继续使用的新会话。两种架构的最终 Release 包均已重建并通过严格签名验证，Intel 仍仅交叉构建。动画逐帧插值由自动测试覆盖；减少动态效果偏好和长按时序未做本轮系统设置切换／实机计时验收。
