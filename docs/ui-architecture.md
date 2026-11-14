# XAML 与界面状态

`ncalc/NaturplayCalculator/MainPage.xaml` 保留三种键盘布局、事件入口和页面视觉状态；`Styles/CalculatorControls.xaml` 集中维护配色、按键样式和必要的控件模板。`MainPage.Visuals.cs` 只负责通知样式绑定、选择视觉状态和汇总指针所在区域。

## 声明式状态

- CalculatorThemeStates：深浅主题、容器颜色、前景色与系统控件主题。
- OperatorStates：两套运算符键盘及 Shift 标记。
- ResultViewStates：公式列表与文档树的显示。
- DisplayToolsStates：显示区工具栏的显隐。
- InputPreferenceStates：触摸优先布局的边框。

页面状态不添加过渡动画。159 个计算按键通过 OneWay 的强类型 x:Bind 共享样式，避免逐个修改按钮。窗口尺寸计算、控件重排、输入法、公式编辑、历史和持久化仍由 C# 处理；这些操作不适合仅用 XAML 替代。

Shift 的状态仅更新 Viewbox 内 shiftGlyph/shiftGlyph_1 的 Text，不替换按钮 Content，避免破坏缩放容器。两套布局的切换列至少 24 个布局单位，符号居中并只向下缩放，窄窗口也能完整显示。

## 系统控件边界

工具栏 Button 使用系统 DefaultButtonStyle，仅调整背景、边框和最小尺寸，保留原来的内容、事件和可用状态。toolgrid 内局部覆盖 ButtonBackgroundDisabled、ButtonBorderBrushDisabled 为透明，禁用按钮只显示减淡的图标，不画灰色底板。覆盖仅作用于工具栏。普通菜单、ScrollViewer、TreeView 和 ContentDialog 使用系统模板；Slider 只替换 Thumb 的绘制。页面主题向下继承，避免菜单按钮强制 Light、工具栏强制 Dark 和 Slider 主题反转。

FormulaListItem 保留独立的悬停与结果预览高亮；ExpressionInputBox 保留公式位置映射、自然排版光标和隐藏 TextBox 的输入法支持。直接替换为标准列表选择或普通 TextBox 会改变编辑行为。GridSplitter 的薄分隔线及橙色拖动反馈也暂时保留。普通说明弹窗以后可使用 ContentDialog 的标准按钮区，但需同时迁移关闭回调和弹窗队列。

## Slider 偏心

原生模板使用 Border 包围独立布局的 Ellipse，内圆有悬停缩放动画；页面还会通过 Viewbox 对整体进行缩放。WinUI 上游有[内圆向右下偏移的同类问题 #7737](https://github.com/microsoft/microsoft-ui-xaml/issues/7737)。

CenteredSlider 继承系统 Slider，在 OnApplyTemplate 中只为 HorizontalThumb、VerticalThumb 指定 CenteredSliderThumbTemplate。轨道、范围、命中、键盘、拖动、工具提示和主题色仍由系统控件处理。模板在同一个 22×22 Canvas 中用两个 Path/EllipseGeometry 绘制圆，共用 (11,11) 中心；内圆缩放也绕 (11,11)，并关闭这几层的布局取整。没有增加针对特定 DPI 的位移。尺寸和各状态缩放比例保留原生值；动画使用对应的系统快/普通时长。

升级 Windows App SDK 时，应检查原生 HorizontalThumb、VerticalThumb 模板部件名及尺寸，并重新验收普通、悬停、按下、禁用及深浅主题。多显示器 DPI 仍需实机覆盖。

## 触屏与底栏

仅鼠标 PointerEntered/PointerExited 驱动自动显隐；触摸抬手不再关闭底栏。显示区右下角的固定折叠按钮支持鼠标、触屏和键盘，手动展开/收起优先于自动悬停，持续到用户再次切换或重新打开页面。切换到触屏时，已由鼠标展开的底栏保持打开，避免正在点击的控件消失。

折叠按钮不属于会隐藏的 toolgrid，也不触发公式定位；隐藏时 toolgrid 使用 Collapsed，不能通过 Tab 进入不可见按钮。工具栏内的键盘焦点保持底栏可见。Presentation/Input/DisplayToolsState 保存与 WinUI 无关的状态，回归覆盖连续触摸、混合输入、手动覆盖、焦点和卸载清理。

## 验证

可移植 UiMarkupTests 检查按键事件、实时样式绑定、配色和视觉状态目标。它们不能验证 WinUI 的运行时资源查找、键盘焦点、指针命中及实际渲染；这些仍需运行 Debug 应用验收。重构前后对照应特别覆盖主题切换、Shift、工具栏显隐、三种布局及触摸优先模式。
