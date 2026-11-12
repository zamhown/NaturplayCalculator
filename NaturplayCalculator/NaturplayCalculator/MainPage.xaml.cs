using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI;
using Colors = Microsoft.UI.Colors;
using Windows.Storage;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using Windows.Graphics.Display;
using Microsoft.UI.Xaml.Documents;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.System.Profile;
using Windows.Devices.Input;
using NaturplayCalculator.Model;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.ViewModel;
using System.Diagnostics;
using Microsoft.UI.Xaml.Shapes;
using Windows.Globalization;
using System.Threading.Tasks;
//using Mntone.SvgForXaml;
//using WinRTXamlToolkit.Controls;
using JsonParser;
using System.Xml;
using System.Threading;
using System.Numerics;

using Windows.UI.ViewManagement;
using Windows.UI.Notifications;

using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;
using Windows.System;

//“空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 上有介绍

namespace NaturplayCalculator
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>


    public sealed partial class MainPage : Page
    {

        public static readonly SolidColorBrush TextBlackBrush = new SolidColorBrush(Colors.Black);
        public static readonly SolidColorBrush TextWhiteBrush = new SolidColorBrush(Colors.White);
        public static SolidColorBrush BgBrush = new SolidColorBrush(Color.FromArgb(255, 0x23, 0x23, 0x23));
        public static readonly SolidColorBrush TransparentBrush = new SolidColorBrush(Colors.Transparent);

        private static readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public bool SolveWhenInput = true;
        public bool ShowCalculationSteps = true;
        public bool OpenLiveTile = true;

        bool solved = false;
        InputString NowS = new InputString("");
        HistoryManager historyManager = new HistoryManager();
        int saveCount = 1;
        double scale = 1.5;
        double windowScale = 1;
        Canvas inputC;
        int textPtr = 0;
        MainWindow self;

        public AboutDialog AboutWindow;
        public WhatsNewDialog WhatsNewWindow;
        public HelpDialog HelpWindow;
        public AlertDialog AlertWindow;

        int windowState = -1;
        string lastInput = "";
        bool shift = false;
        bool tapFirst = true;
        bool rightHanded = true;
        bool showDirectionKeys = true;
        bool showAdvancedOperators = true;
        bool acrylic = true;
        bool isAlwaysOnTop = false;
        bool alwaysOnTopModeChanging = false;
        bool emojiUI = false;

        bool userWhite, userEmoji, userFS;

        delegate void uiHandle(object sender, RoutedEventArgs e);
        Dictionary<string, uiHandle> commandDic = new Dictionary<string, uiHandle>();
        private TextBox _text => inputBox.Editor;

        //当前右键选中的显示区列表项
        ListViewItem currentSelectedListViewItem = null;
        //WinRTXamlToolkit.Controls.TreeViewItem currentSelectedTreeViewItem = null;
        TreeViewNode currentSelectedTreeViewItem = null;

        //当前是列表框状态还是树形列表框状态
        bool treeViewState = false;

        //当前计算的实时答案
        CalculationValue presentAnswer = null;

        //控件组（用来改变风格）
        public List<Button> NumButton;
        public List<Button> ConButton;
        public List<Button> ConstButton;
        public List<Button> OprButton;
        public List<Button> ImOprButton;
        public List<Button> FunctionButton;
        public List<Button> EmojiButton;
        public List<ContentDialog> Dialogs;

        bool pointerHoverDisplayArea = false;
        bool pointerHoverInputArea = false;
        bool pointerHoverGridSplitter = false;

        double mainGridRow0, mainGridRow1, mainGridRow2;


        public bool White
        {
            get
            {
                return Settings.White;
            }
            private set
            {
                Settings.White = value;

                //反亚克力材质Flag
                if (value)
                {
                    SolidColorBrush myBrush = new SolidColorBrush(Color.FromArgb(255, 0x17, 0x17, 0x17));
                    displayRectangle.Fill = myBrush;
                }
                else
                {
                    // display background
                    SolidColorBrush myBrush = new SolidColorBrush(Color.FromArgb(255, 230, 230, 230));
                    displayRectangle.Fill = myBrush;
                }

                if (value)
                {
                    BgBrush = new SolidColorBrush(Color.FromArgb(255, 0x23, 0x23, 0x23));
                    originalListItem.Foreground = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255));
                    treeView.Foreground = TextWhiteBrush;
                    slider.RequestedTheme = ElementTheme.Light;
                    var toolBrush = TextWhiteBrush;
                    bbig.Foreground = toolBrush;
                    bsmall.Foreground = toolBrush;
                    mpre.Foreground = toolBrush;
                    mnext.Foreground = toolBrush;
                    mdelHis.Foreground = toolBrush;
                    tHisPtr.Foreground = toolBrush;
                    btop.Foreground = toolBrush;

                    listView.RequestedTheme = ElementTheme.Dark;
                    treeView.RequestedTheme = ElementTheme.Dark;
                    sviewer.RequestedTheme = ElementTheme.Dark;

                    bsetting.Background = new SolidColorBrush(Color.FromArgb(255, 63, 63, 63));
                    bsetting.Foreground = new SolidColorBrush(Colors.White);
                    bsetting.RequestedTheme = ElementTheme.Dark;
                    bequal.Background = new SolidColorBrush(Color.FromArgb(255, 104, 0, 0));
                    bequal.Foreground = new SolidColorBrush(Colors.White);
                    bequal.RequestedTheme = ElementTheme.Dark;
                    inputRectangle.Fill = new SolidColorBrush(Color.FromArgb(255, 48, 48, 48));
                    inputBox.Foreground = new SolidColorBrush(Colors.White);
                    inputBox.RequestedTheme = ElementTheme.Dark;

                    NumButton.ForEach(e =>
                    {
                        e.RequestedTheme = ElementTheme.Dark;
                        e.BorderBrush = null;
                        e.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                        e.Margin = new Thickness(1);
                        e.Foreground = new SolidColorBrush(Colors.White);
                    });
                    ConButton.ForEach(e =>
                    {
                        e.RequestedTheme = ElementTheme.Dark;
                        e.BorderBrush = null;
                        e.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                        e.Margin = new Thickness(1);
                        e.Foreground = new SolidColorBrush(Colors.White);
                    });
                    ConstButton.ForEach(e =>
                    {
                        e.RequestedTheme = ElementTheme.Dark;
                        e.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                        e.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                        e.Margin = new Thickness(1);
                        e.Foreground = new SolidColorBrush(Colors.White);
                    });
                    constButtons.Background = new SolidColorBrush(Color.FromArgb(255, 45, 45, 45));
                    constButtons2.Background = constButtons.Background;
                    OprButton.ForEach(e =>
                    {
                        e.RequestedTheme = ElementTheme.Dark;
                        e.BorderBrush = null;
                        e.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                        e.Margin = new Thickness(1);
                        e.Foreground = new SolidColorBrush(Colors.White);
                    });
                    blr.Background = new SolidColorBrush(Color.FromArgb(255, 56, 56, 56));
                    blr2.Background = blr.Background;
                    bshift.Background = blr.Background;
                    leftButtons1.Background = blr.Background;
                    leftButtons1_1.Background = blr.Background;
                    leftButtons2.Background = blr.Background;
                    leftButtons2_1.Background = blr.Background;
                    rightButtons.Background = blr.Background;
                    bigleftButtons.Background = blr.Background;
                    specialLeftBlock1.Background = blr.Background;
                    specialLeftBlock2.Background = blr.Background;
                    specialRightBlock2.Background = blr.Background;
                    ImOprButton.ForEach(e =>
                    {
                        e.RequestedTheme = ElementTheme.Dark;
                        e.BorderBrush = null;
                        e.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                        e.Margin = new Thickness(1);
                        e.Foreground = new SolidColorBrush(Colors.White);
                    });
                    importantOprButtons.Background = new SolidColorBrush(Color.FromArgb(255, 80, 80, 80));
                    upButtons.Background = importantOprButtons.Background;
                    downButtons.Background = importantOprButtons.Background;
                    specialRightBlock1.Background = importantOprButtons.Background;
                    FunctionButton.ForEach(e =>
                    {
                        e.RequestedTheme = ElementTheme.Light;  // Dark模式下汉堡按钮hover效果怪
                        e.BorderBrush = null;
                        e.BorderThickness = new Thickness();
                    });
                    Dialogs.ForEach(e =>
                    {
                        e.RequestedTheme = ElementTheme.Dark;
                    });
                }
                else
                {
                    BgBrush = new SolidColorBrush(Color.FromArgb(255, 220, 220, 220));
                    originalListItem.Foreground = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0));
                    treeView.Foreground = TextBlackBrush;
                    slider.RequestedTheme = ElementTheme.Dark;
                    var toolBrush = TextBlackBrush;
                    bbig.Foreground = toolBrush;
                    bsmall.Foreground = toolBrush;
                    mpre.Foreground = toolBrush;
                    mnext.Foreground = toolBrush;
                    mdelHis.Foreground = toolBrush;
                    tHisPtr.Foreground = toolBrush;
                    btop.Foreground = toolBrush;

                    listView.RequestedTheme = ElementTheme.Light;
                    treeView.RequestedTheme = ElementTheme.Light;
                    sviewer.RequestedTheme = ElementTheme.Light;

                    bsetting.Background = new SolidColorBrush(Color.FromArgb(255, 180, 180, 180));
                    bsetting.Foreground = new SolidColorBrush(Colors.Black);
                    bsetting.RequestedTheme = ElementTheme.Light;
                    bequal.Background = new SolidColorBrush(Color.FromArgb(255, 152, 0, 0));
                    bequal.Foreground = new SolidColorBrush(Colors.White);
                    bequal.RequestedTheme = ElementTheme.Dark;
                    inputRectangle.Fill = new SolidColorBrush(Color.FromArgb(255, 215, 215, 215));
                    inputBox.Foreground = new SolidColorBrush(Colors.Black);
                    inputBox.RequestedTheme = ElementTheme.Light;

                    NumButton.ForEach(e =>
                    {
                        e.RequestedTheme = ElementTheme.Light;
                        e.BorderBrush = null;
                        e.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                        e.Margin = new Thickness(1);
                        e.Foreground = new SolidColorBrush(Colors.Black);
                    });
                    ConButton.ForEach(e =>
                    {
                        e.RequestedTheme = ElementTheme.Light;
                        e.BorderBrush = null;
                        e.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                        e.Margin = new Thickness(1);
                        e.Foreground = new SolidColorBrush(Colors.Black);
                    });
                    ConstButton.ForEach(e =>
                    {
                        e.RequestedTheme = ElementTheme.Light;
                        e.BorderBrush = null;
                        e.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                        e.Margin = new Thickness(1);
                        e.Foreground = new SolidColorBrush(Colors.Black);
                    });
                    constButtons.Background = new SolidColorBrush(Color.FromArgb(255, 190, 190, 190));
                    constButtons2.Background = constButtons.Background;
                    OprButton.ForEach(e =>
                    {
                        e.RequestedTheme = ElementTheme.Light;
                        e.BorderBrush = null;
                        e.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                        e.Margin = new Thickness(1);
                        e.Foreground = new SolidColorBrush(Colors.Black);
                    });
                    blr.Background = new SolidColorBrush(Color.FromArgb(255, 210, 210, 210));
                    blr2.Background = blr.Background;
                    bshift.Background = blr.Background;
                    leftButtons1.Background = blr.Background;
                    leftButtons1_1.Background = blr.Background;
                    leftButtons2.Background = blr.Background;
                    leftButtons2_1.Background = blr.Background;
                    rightButtons.Background = blr.Background;
                    bigleftButtons.Background = blr.Background;
                    specialLeftBlock1.Background = blr.Background;
                    specialLeftBlock2.Background = blr.Background;
                    specialRightBlock2.Background = blr.Background;
                    ImOprButton.ForEach(e =>
                    {
                        e.RequestedTheme = ElementTheme.Light;
                        e.BorderBrush = null;
                        e.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                        e.Margin = new Thickness(1);
                        e.Foreground = new SolidColorBrush(Colors.Black);
                    });
                    importantOprButtons.Background = new SolidColorBrush(Color.FromArgb(255, 190, 190, 190));
                    upButtons.Background = importantOprButtons.Background;
                    downButtons.Background = importantOprButtons.Background;
                    specialRightBlock1.Background = importantOprButtons.Background;
                    FunctionButton.ForEach(e =>
                    {
                        e.RequestedTheme = ElementTheme.Light;
                        e.BorderBrush = null;
                        e.BorderThickness = new Thickness();
                    });
                    Dialogs.ForEach(e =>
                    {
                        e.RequestedTheme = ElementTheme.Light;
                    });
                }
                foreach (var i in listView.Items)
                {
                    if (i is ListViewItem lvi && lvi.Content is Canvas c)
                        foreach (var m in c.Children)
                        {
                            if (m is Naturplay.Tex.WinUI.TexFormulaView formula)
                                formula.Foreground = FormulaBrush();
                            else if (m is Shape ms)
                                ms.Stroke = FormulaBrush();
                            else if (m is TextBlock mt)
                                mt.Foreground = FormulaBrush();
                        }
                }
                // History contains data only; visible controls are restyled below.
                Acrylic = Acrylic;
                TapFirst = TapFirst;
                changeBackground();
                doWithTitleBar(value);
                updateAboutLogo();
            }
        }

        public bool FoldingNum
        {
            get
            {
                return Settings.FoldingNum;
            }
            private set
            {
                Settings.FoldingNum = value;
            }
        }

        public bool Shift
        {
            get
            {
                return shift;
            }
            set
            {
                shift = value;
                if (shift)
                {
                    bshift.Content = "⏶";
                    bshift_1.Content = "⏶";

                    leftButtons1.Visibility = Visibility.Collapsed;
                    leftButtons2.Visibility = Visibility.Visible;

                    leftButtons1_1.Visibility = Visibility.Collapsed;
                    leftButtons2_1.Visibility = Visibility.Visible;
                }
                else
                {
                    bshift.Content = "⏷";
                    bshift_1.Content = "⏷";

                    leftButtons2.Visibility = Visibility.Collapsed;
                    leftButtons1.Visibility = Visibility.Visible;

                    leftButtons2_1.Visibility = Visibility.Collapsed;
                    leftButtons1_1.Visibility = Visibility.Visible;
                }
            }
        }

        public bool TapFirst
        {
            get
            {
                return tapFirst;
            }
            private set
            {
                tapFirst = value;
                if (value)
                {
                    if (specialLeftBlockPlace1.Children.Contains(specialLeftBlock1))
                    {
                        specialLeftBlockPlace1.Children.Remove(specialLeftBlock1);
                        specialLeftBlockPlace2.Children.Remove(specialLeftBlock2);
                        specialRightBlockPlace1.Children.Remove(specialRightBlock1);
                        specialRightBlockPlace2.Children.Remove(specialRightBlock2);

                        specialLeftBlockPlace1.Children.Add(specialRightBlock1);
                        specialLeftBlockPlace2.Children.Add(specialRightBlock2);
                        specialRightBlockPlace1.Children.Add(specialLeftBlock1);
                        specialRightBlockPlace2.Children.Add(specialLeftBlock2);
                    }
                    var borderBrush = White ? new SolidColorBrush(Color.FromArgb(255, 0x54, 0x54, 0x54)) : new SolidColorBrush(Color.FromArgb(255, 128, 128, 128));
                    specialLeftBlockBigPlace.BorderBrush = borderBrush;
                    specialLeftBlockBigPlace.BorderThickness = new Thickness(5);
                    smallnumButtons.BorderBrush = borderBrush;
                    smallnumButtons.BorderThickness = new Thickness(5);
                }
                else
                {
                    if (specialLeftBlockPlace1.Children.Contains(specialRightBlock1))
                    {
                        specialLeftBlockPlace1.Children.Remove(specialRightBlock1);
                        specialLeftBlockPlace2.Children.Remove(specialRightBlock2);
                        specialRightBlockPlace1.Children.Remove(specialLeftBlock1);
                        specialRightBlockPlace2.Children.Remove(specialLeftBlock2);

                        specialLeftBlockPlace1.Children.Add(specialLeftBlock1);
                        specialLeftBlockPlace2.Children.Add(specialLeftBlock2);
                        specialRightBlockPlace1.Children.Add(specialRightBlock1);
                        specialRightBlockPlace2.Children.Add(specialRightBlock2);
                    }
                    specialLeftBlockBigPlace.BorderBrush = null;
                    specialLeftBlockBigPlace.BorderThickness = new Thickness(0);
                    smallnumButtons.BorderBrush = null;
                    smallnumButtons.BorderThickness = new Thickness(0);
                }
                if (windowState == 1)
                    doWithEqual(value);
            }
        }

        public bool RightHanded
        {
            get
            {
                return rightHanded;
            }
            private set
            {
                rightHanded = value;
                if (value)
                {
                    if (bigleftButtonsArea.Children.Contains(bigrightButtons))
                    {
                        bigleftButtonsArea.Children.Remove(bigrightButtons);
                        bigrightButtonsArea.Children.Remove(bigleftButtons);
                        leftButtonsArea.Children.Remove(rightButtons);
                        rightButtonsArea.Children.Remove(leftButtons);

                        bigleftButtonsArea.Children.Add(bigleftButtons);
                        bigrightButtonsArea.Children.Add(bigrightButtons);
                        leftButtonsArea.Children.Add(leftButtons);
                        rightButtonsArea.Children.Add(rightButtons);

                        Grid.SetColumn(bsetting, 0);
                        Grid.SetColumn(bequalPlace1, 2);
                    }
                }
                else
                {
                    if (bigleftButtonsArea.Children.Contains(bigleftButtons))
                    {
                        bigleftButtonsArea.Children.Remove(bigleftButtons);
                        bigrightButtonsArea.Children.Remove(bigrightButtons);
                        leftButtonsArea.Children.Remove(leftButtons);
                        rightButtonsArea.Children.Remove(rightButtons);

                        bigleftButtonsArea.Children.Add(bigrightButtons);
                        bigrightButtonsArea.Children.Add(bigleftButtons);
                        leftButtonsArea.Children.Add(rightButtons);
                        rightButtonsArea.Children.Add(leftButtons);

                        Grid.SetColumn(bsetting, 2);
                        Grid.SetColumn(bequalPlace1, 0);
                    }
                }
                TapFirst = TapFirst;
                ShowAdvancedOperators = ShowAdvancedOperators;
            }
        }

        public bool ShowDirectionKeys
        {
            get
            {
                return showDirectionKeys;
            }
            private set
            {
                showDirectionKeys = value;
                if (value)
                {
                    conButtonsArea.Visibility = Visibility.Visible;
                    if (windowState == 0)
                    {
                        Grid.SetRow(numBlockShadow, 1);
                        Grid.SetRowSpan(numBlockShadow, 1);
                    }
                    else if (windowState == 1)
                    {
                        mainGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                    }
                }
                else
                {
                    conButtonsArea.Visibility = Visibility.Collapsed;
                    if (windowState == 0)
                    {
                        Grid.SetRow(numBlockShadow, 0);
                        Grid.SetRowSpan(numBlockShadow, 2);
                    }
                    else if (windowState == 1)
                    {
                        mainGrid.RowDefinitions[0].Height = new GridLength(0, GridUnitType.Star);
                    }
                }
            }
        }

        public bool ShowAdvancedOperators
        {
            get
            {
                return showAdvancedOperators;
            }
            private set
            {
                showAdvancedOperators = value;
                if (value)
                {
                    leftButtonsAreaShadow.Visibility = Visibility.Visible;
                    rightButtonsAreaShadow.Visibility = Visibility.Visible;
                    Grid.SetColumn(midButtons, 1);
                    Grid.SetColumnSpan(midButtons, 1);
                    midButtons.RowDefinitions[1].Height = new GridLength(4, GridUnitType.Star);
                }
                else
                {
                    if (RightHanded)
                    {
                        leftButtonsAreaShadow.Visibility = Visibility.Collapsed;
                        rightButtonsAreaShadow.Visibility = Visibility.Visible;
                        Grid.SetColumn(midButtons, 0);
                    }
                    else
                    {
                        leftButtonsAreaShadow.Visibility = Visibility.Visible;
                        rightButtonsAreaShadow.Visibility = Visibility.Collapsed;
                        Grid.SetColumn(midButtons, 1);
                    }
                    Grid.SetColumnSpan(midButtons, 2);
                    midButtons.RowDefinitions[1].Height = new GridLength(6, GridUnitType.Star);
                }
                changeTheSizeOfWindow(true);
            }
        }

        public bool IsEmojiUI
        {
            get
            {
                return emojiUI;
            }
            set
            {
                emojiUI = value;
                if (value)
                {
                    EmojiButton.ForEach(e =>
                    {
                        if (e.Content is Viewbox vb && vb.Child is Grid g && g.Children.Count > 1)
                        {
                            for (var i = 0; i < g.Children.Count; i++)
                                g.Children[i].Visibility = Visibility.Collapsed;
                            g.Children[1 + Convert.ToInt32(Math.Floor(
                                (g.Children.Count - 1) * new Random().NextDouble()
                                ))].Visibility = Visibility.Visible;
                        }
                    });
                } else
                {
                    EmojiButton.ForEach(e =>
                    {
                        if (e.Content is Viewbox vb && vb.Child is Grid g && g.Children.Count > 1)
                        {
                            for (var i = 1; i < g.Children.Count; i++)
                                g.Children[i].Visibility = Visibility.Collapsed;
                            g.Children[0].Visibility = Visibility.Visible;
                        }
                    });
                }
            }
        }

        private bool TreeViewState
        {
            get
            {
                return treeViewState;
            }
            set
            {
                treeViewState = value;

                listView.Items.Clear();
                //treeView.Items.Clear();
                treeView.RootNodes.Clear();

                if (value)
                {
                    listView.Visibility = Visibility.Collapsed;
                    treeView.Visibility = Visibility.Visible;
                }
                else
                {
                    treeView.Visibility = Visibility.Collapsed;
                    listView.Visibility = Visibility.Visible;
                }
            }
        }

        public MainPage()
        {
            this.InitializeComponent();
            AppResources.ConfigureCalculator();
            _text.MaxLength = JSON.MaxInputLength;
            _text.TextChanged += _text_TextChanged;
            _text.SelectionChanged += _text_SelectionChanged;
            _text.Paste += _text_Paste;
            _text.KeyUp += _text_KeyUp;
            _text.PreviewKeyDown += FormulaPreviewKeyDown;
            _text.TextCompositionStarted += (s, e) => composingFormula = activeInputSurface == InputSurface.Formula;
            _text.TextCompositionEnded += (s, e) => { composingFormula = false; RebindFormulaBuffer(); };
            inputBox.CaretRequested += ActivateTextInput;

            NumButton = new List<Button>
            {
                b0, b1, b2, b3, b4, b5, b6, b7, b8, b9, bdou, bpoint,
                b0_1, b1_1, b2_1, b3_1, b4_1, b5_1, b6_1, b7_1, b8_1, b9_1, bdou_1, bpoint_1,
                b0s, b1s, b2s, b3s, b4s, b5s, b6s, b7s, b8s, b9s, bdous, bpoints,
            };
            ConButton = new List<Button>
            {
                bup, blefta, babout, brighta, bdown
            };
            ConstButton = new List<Button>
            {
                be, bpi, bi,
                be_1, bpi_1, bi_1,
                be2, bpi2, bi2
            };
            OprButton = new List<Button>
            {
                bshift, bshift_1,

                bncr, bnpr, bsin, barcsin, bcos, barccos, btan, barctan, bln, byroot, bave, bd,
                b10n, blg, bsinh, barsh, bcosh, barch, btanh, barth, brank, bt, bcov, brho,
                blr, bpowerself, bsqrt, bnfact, babs, bmod, bans,

                bncr_1, bnpr_1, bsin_1, barcsin_1, bcos_1, barccos_1, btan_1, barctan_1, bln_1, byroot_1, bave_1, bd_1,
                b10n_1, blg_1, bsinh_1, barsh_1, bcosh_1, barch_1, btanh_1, barth_1, brank_1, bt_1, bcov_1, brho_1,
                blr_1, bpowerself_1, bsqrt_1, bnfact_1, babs_1, bmod_1, bans_1,

                bncr2, bnpr2, bsin2, barcsin2, bcos2, barccos2, btan2, barctan2, bln2, byroot2, bave2, bd2,
                b10n2, blg2, bgroup1, bgroup2, brank2, bt2, bcov2, brho2,
                blr2, bpowerself2, bsqrt2, bnfact2, babs2, bmod2, bans2,
            };
            ImOprButton = new List<Button>
            {
                bleft, bright, bplus, bminus, bpower, bdevide,
                bleft_1, bright_1, bplus_1, bminus_1, bpower_1, bdevide_1,
                bleft2, bright2, bplus2, bminus2, bpower2, bdevide2
            };
            FunctionButton = new List<Button>
            {
                bequal, bsetting, bac, bc, bac_1, bc_1, bac2, bc2
            };
            EmojiButton = new List<Button>
            {
                b0, b1, b2, b3, b4, b5, b6, b7, b8, b9,
                b0_1, b1_1, b2_1, b3_1, b4_1, b5_1, b6_1, b7_1, b8_1, b9_1,
                b0s, b1s, b2s, b3s, b4s, b5s, b6s, b7s, b8s, b9s,
                bup, blefta, babout, brighta, bdown,
                bleft, bright, blr, bplus, bminus, bpower, bdevide,
                bleft_1, bright_1, blr_1, bplus_1, bminus_1, bpower_1, bdevide_1,
                bleft2, bright2, blr2, bplus2, bminus2, bpower2, bdevide2,
                bac, bc,
                bac_1, bc_1,
                bac2, bc2,
                btop, mnext, mpre, mdelHis, bbig, bsmall
            };

            AboutWindow = new AboutDialog(this);
            WhatsNewWindow = new WhatsNewDialog(this);
            HelpWindow = new HelpDialog(this);
            AlertWindow = new AlertDialog(this);

            Dialogs = new List<ContentDialog>
            {
                AboutWindow, WhatsNewWindow, HelpWindow, AlertWindow
            };

            // 设置阴影
            SharedShadow1.Receivers.Add(bg1);
            SharedShadow1.Receivers.Add(mainGrid);
            SharedShadow1.Receivers.Add(conButtonsArea);

            SharedShadow2.Receivers.Add(bg1);
            SharedShadow2.Receivers.Add(displayGrid);
            SharedShadow2.Receivers.Add(downGrid);

            SharedShadow3.Receivers.Add(midButtons);

            SharedShadow4.Receivers.Add(conButtonsArea);

            SharedShadow3_1.Receivers.Add(numBlockShadow_1);

            SharedShadow4_1.Receivers.Add(numBlockShadow_1);
            SharedShadow4_1.Receivers.Add(conButtonsArea_1);

            const int zUnit = 32;
            bigleftButtonsAreaShadow.Translation += new Vector3(0, 0, zUnit);
            bigrightButtonsAreaShadow.Translation += new Vector3(0, 0, zUnit);
            numBlockShadow.Translation += new Vector3(0, 0, zUnit);
            leftButtonsAreaShadow.Translation += new Vector3(0, 0, zUnit);
            rightButtonsAreaShadow.Translation += new Vector3(0, 0, zUnit);
            inputBlockShadow.Translation += new Vector3(0, 0, (int)(zUnit * 1.5));
            upButtons.Translation += new Vector3(0, 0, zUnit);
            downButtons.Translation += new Vector3(0, 0, zUnit);

            self = App.MainWindow;
            if (localSettings.Values.ContainsKey("windowFullScreen") && (bool)localSettings.Values["windowFullScreen"])
                self.EnterFullScreen();
            else
            {
                //更改窗口大小
                if (deviceIsDesktop())
                {
                    double w = 0;
                    double h = 0;

                    if (localSettings.Values.ContainsKey("windowWidth"))
                        w = (double)localSettings.Values["windowWidth"];
                    if (localSettings.Values.ContainsKey("windowHeight"))
                        h = (double)localSettings.Values["windowHeight"];

                    if (w <= 0 || h <= 0)
                    {
                        var ds = PointerDevice.GetPointerDevices();
                        if (ds.Count > 0)
                        {
                            h = Math.Min(ds[0].ScreenRect.Height * 0.8, 780);
                            w = h * (500f / 780f);
                        }
                    }
                    var desiredSize = new Size(w, h);
                    Debug.WriteLine(w + " " + h);
                    // ApplicationView.PreferredLaunchViewSize = desiredSize;
                    // ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
                    self.ResizeContent(desiredSize);
                }
            }

            // 避免在Windows平板设备上软键盘总是自动弹出
            if (Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Controls.TextBox", "PreventKeyboardDisplayOnProgrammaticFocus"))
            {
                _text.PreventKeyboardDisplayOnProgrammaticFocus = true;
            }



            if (localSettings.Values.ContainsKey("hudu"))
                Settings.UseRadians = (bool)localSettings.Values["hudu"];
            else
                Settings.UseRadians = true;
            inputUseRadians = null;
            if (localSettings.Values.TryGetValue("calculationMode", out var storedMode) && storedMode is int mode && Enum.IsDefined(typeof(CalculationMode), mode))
                Settings.CalculationMode = (CalculationMode)mode;
            UpdateModeControls();
            Thudu.IsChecked = Settings.UseRadians;
            Tnohudu.IsChecked = !Settings.UseRadians;

            if (localSettings.Values.ContainsKey("solveWhenInput"))
                SolveWhenInput = (bool)localSettings.Values["solveWhenInput"];
            else
                SolveWhenInput = true;
            Tsolve.IsChecked = SolveWhenInput;

            if (localSettings.Values.ContainsKey("openLiveTile"))
                OpenLiveTile = (bool)localSettings.Values["openLiveTile"];
            else
                OpenLiveTile = true;
            TLiveTile.IsChecked = OpenLiveTile;

            if (localSettings.Values.ContainsKey("directionKeys"))
                ShowDirectionKeys = (bool)localSettings.Values["directionKeys"];
            else
                ShowDirectionKeys = true;
            TDirectionKeys.IsChecked = ShowDirectionKeys;

            if (localSettings.Values.ContainsKey("showAO"))
                ShowAdvancedOperators = (bool)localSettings.Values["showAO"];
            else
                ShowAdvancedOperators = !deviceIsMobile();
            TShowAO.IsChecked = ShowAdvancedOperators;

            if (localSettings.Values.ContainsKey("showCalculationSteps"))
                ShowCalculationSteps = (bool)localSettings.Values["showCalculationSteps"];
            else
                ShowCalculationSteps = true;
            TShowCalculationSteps.IsChecked = ShowCalculationSteps;

            //亚克力材质Flag
            if (acrylicIsAvailable())
            {
                if (localSettings.Values.ContainsKey("acrylic"))
                    acrylic = (bool)localSettings.Values["acrylic"];
                else
                    acrylic = true;
            }
            else
            {
                acrylic = false;
                TAcrylic.IsEnabled = false;
            }
            TAcrylic.IsChecked = Acrylic;

            if (localSettings.Values.ContainsKey("followSystem"))
                Tfs.IsChecked = (bool)localSettings.Values["followSystem"];
            else
                Tfs.IsChecked = true;
            if (Tfs.IsChecked)
            {
                White = getSystemTheme() != "Light";
                Tblack.IsChecked = false;
                Twhite.IsChecked = false;
            }
            else
            {
                if (localSettings.Values.ContainsKey("white"))
                    White = (bool)localSettings.Values["white"];
                else
                    White = true;
                Tblack.IsChecked = White;
                Twhite.IsChecked = !White;
            }

            if (localSettings.Values.ContainsKey("tapFirst"))
                TapFirst = (bool)localSettings.Values["tapFirst"];
            else
                TapFirst = deviceIsMobile();
            TTapFirst.IsChecked = TapFirst;
            TClickFirst.IsChecked = !TapFirst;

            if (localSettings.Values.ContainsKey("rightHanded"))
                RightHanded = (bool)localSettings.Values["rightHanded"];
            else
                RightHanded = true;
            TRightHanded.IsChecked = RightHanded;
            TLeftHanded.IsChecked = !RightHanded;

            if (localSettings.Values.ContainsKey("foldingNum"))
                FoldingNum = (bool)localSettings.Values["foldingNum"];
            else
                FoldingNum = true;
            TFoldingNum.IsChecked = FoldingNum;

            if (localSettings.Values.ContainsKey("shift"))
                Shift = (bool)localSettings.Values["shift"];
            else
                Shift = false;

            if (localSettings.Values.ContainsKey("scale"))
                scale = (double)localSettings.Values["scale"];
            else
                scale = deviceIsDesktop() ? 1.2 : 1.5;
            slider.Value = scale * 100;

            TClearLast.IsChecked = localSettings.Values.TryGetValue("clearLast", out var clearLast) && clearLast is bool clear && clear;
            updateRecordMenu();

            btop.IsEnabled = supportOverlay();
            Ttop.IsEnabled = btop.IsEnabled;
            if (btop.IsEnabled)
            {
                if (localSettings.Values.ContainsKey("alwayOnTop"))
                {
                    isAlwaysOnTop = !(bool)localSettings.Values["alwayOnTop"];
                    toggleTop();
                }
            }

            if (localSettings.Values.ContainsKey("emoji"))
                IsEmojiUI = (bool)localSettings.Values["emoji"];
            else
                IsEmojiUI = false;
            TEmoji.IsChecked = IsEmojiUI;

            if (localSettings.Values.ContainsKey("mainGridRow0"))
            {
                mainGridRow0 = (double)localSettings.Values["mainGridRow0"];
                mainGridRow1 = (double)localSettings.Values["mainGridRow1"];
                mainGridRow2 = (double)localSettings.Values["mainGridRow2"];
            } else
            {
                mainGridRow0 = 6;
                mainGridRow1 = 1;
                mainGridRow2 = 7;
            }

            /*
            foreach(var s in localSettings.Values.Keys)
            {
                Debug.WriteLine(s);
            }
            */

            commandDic["about"] = babout_Click;
            commandDic["donate"] = bdonate1_Click;
            commandDic["help"] = MenuFlyoutItem_Click_1;
            commandDic["white"] = Twhite_Click;
            commandDic["black"] = Tblack_Click;
            commandDic["rad"] = ToggleMenuFlyoutItem_Click;
            commandDic["deg"] = ToggleMenuFlyoutItem_Click_1;
            commandDic["rate"] = AboutWindow.breview_Click;
            commandDic["review"] = AboutWindow.breview_Click;
            commandDic["money"] = AboutWindow.bdonate_Click;
            commandDic["zamhown"] = AnsMan;
            commandDic["howard zhang"] = AnsMan;
            commandDic["version"] = version;


            //显示更新说明
            var versionString = Settings.GetVersion();
            if (!localSettings.Values.ContainsKey(versionString))
            {
                showingInitialDialog = true;

                //显示新人引导
                if (!localSettings.Values.Keys.Any(s => s.StartsWith("1.13")))
                {
                    userWhite = White;
                    userEmoji = IsEmojiUI;
                    userFS = Tfs.IsChecked;

                    White = false;
                    Tblack.IsChecked = White;
                    Twhite.IsChecked = !White;
                    Tfs.IsChecked = false;

                    IsEmojiUI = true;
                    TEmoji.IsChecked = true;

                    teachingTip1.Title = AppResources.GetString("teaching_tip_1_title");
                    teachingTip1.Subtitle = AppResources.GetString("teaching_tip_1_content");
                    teachingTip1.ActionButtonContent = AppResources.GetString("teaching_tip_1_action");

                    teachingTip1.IsOpen = true;
                }

                localSettings.Values[versionString] = true;
                localSettings.Values["used"] = true;
            }

            // 隐藏显示工具区
            displayGrid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Pixel);
        }

        private bool supportOverlay() => true;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HandleActivation(e.Parameter);
        }

        private void changeTheSizeOfWindow(bool force = false)
        {
            var CurrentViewState = new Rect(0, 0, ActualWidth, ActualHeight);
            double AppWidth = CurrentViewState.Width;
            double AppHeight = CurrentViewState.Height;
            if (AppWidth <= 0 || AppHeight <= 0) return;

            if (AppHeight > AppWidth * 0.75)
            {
                frame.Margin = new Thickness(0, 0, 0, 0);
                mainGrid.RowDefinitions[0].Height = new GridLength(mainGridRow0, GridUnitType.Star);
                mainGrid.RowDefinitions[0].MinHeight = 100;
                mainGrid.RowDefinitions[1].Height = new GridLength(mainGridRow1, GridUnitType.Star);
                mainGrid.RowDefinitions[2].Height = new GridLength(mainGridRow2, GridUnitType.Star);
                mainGrid.RowDefinitions[2].MinHeight = 300;
            }
            else
            {
                if (deviceIsDesktop() && !self.IsFullScreenMode)
                    frame.Margin = new Thickness(0, 32 * windowScale, 0, 0);
                else
                    frame.Margin = new Thickness(0, 0, 0, 0);
                mainGrid.RowDefinitions[0].Height = new GridLength(ShowDirectionKeys ? 1 : 0, GridUnitType.Star);
                mainGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
                mainGrid.RowDefinitions[2].Height = new GridLength(13, GridUnitType.Star);
            }

            /* 之前处理窗体过窄的逻辑
            if (AppWidth < AppHeight * 9 / 15)
            {
                bigGrid.Width = 468;
                windowScale = bigGrid.Width / AppWidth;
                bigGrid.Height = AppHeight * windowScale;
                mainGrid.RowDefinitions[0] = new RowDefinition()
                {
                    Height = new GridLength((7 + 1) * bigGrid.Height / (780.0 / 14 * (7 + 1)) - (7 + 1), GridUnitType.Star),
                    MinHeight = 100
                };
            }
            else
            {
            */
            bigGrid.Height = 780;
            windowScale = bigGrid.Height / AppHeight;
            bigGrid.Width = AppWidth * windowScale;
            mainGrid.RowDefinitions[0] = new RowDefinition()
            {
                Height = new GridLength((windowState == 0) ? mainGridRow0 : (ShowDirectionKeys ? 1 : 0), GridUnitType.Star)
            };
            //}

            if (AppHeight > AppWidth * 0.75)
            {
                if (windowState != 0 || force)
                {
                    windowState = 0;

                    App.MainWindow.ExtendsContentIntoTitleBar = true;

                    frame.ColumnDefinitions[0].Width = new GridLength(0, GridUnitType.Star);
                    frame.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                    frame.ColumnDefinitions[2].Width = new GridLength(0, GridUnitType.Star);
                    buttonGrid.ColumnDefinitions[0].Width = new GridLength(3, GridUnitType.Star);
                    buttonGrid.ColumnDefinitions[1].Width = new GridLength(5, GridUnitType.Star);
                    buttonGrid.ColumnDefinitions[2].Width = new GridLength(3, GridUnitType.Star);
                    if (!force)
                        ShowAdvancedOperators = ShowAdvancedOperators;
                    midButtons.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                    midButtons.RowDefinitions[1].Height = new GridLength(ShowAdvancedOperators ? 4 : 6, GridUnitType.Star);
                    numBlockShadow.Visibility = Visibility.Visible;
                    Grid.SetRow(displayGrid, 0);
                    Grid.SetRow(inputBlockShadow, 1);
                    Grid.SetRow(downGrid, 2);
                    Grid.SetRow(displayMaterial, 0);
                    Grid.SetRowSpan(displayMaterial, 2);
                    inputGrid.Margin = new Thickness(0, 0, 0, 0);
                    inputBlockShadow.Margin = new Thickness(0, 0, 0, 0);
                    conButtonsArea.Margin = new Thickness(0, 0, 0, 0);
                    displayGrid.Margin = new Thickness(0, 0, 0, 0);
                    displayMaterial.Margin = new Thickness(0, 0, 0, 0);

                    changeBackground();

                    doWithTitleBar(White);
                    doWithEqual(false);
                    ShowDirectionKeys = ShowDirectionKeys;

                    gridSplitter.Visibility = Visibility.Visible;
                }
                if (deviceIsDesktop() && !self.IsFullScreenMode)
                    viewbox2.Margin = new Thickness(0, 32 * windowScale, 0, 0);
                else
                    viewbox2.Margin = new Thickness(0, 10, 0, 0);
            }
            else
            {
                if (windowState != 1 || force)
                {
                    windowState = 1;

                    //App.MainWindow.ExtendsContentIntoTitleBar = false;
                    App.MainWindow.ExtendsContentIntoTitleBar = true;

                    frame.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                    frame.ColumnDefinitions[1].Width = new GridLength(deviceIsDesktop() ? 3 : 2, GridUnitType.Star);
                    frame.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
                    buttonGrid.ColumnDefinitions[0].Width = new GridLength(0, GridUnitType.Star);
                    buttonGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                    buttonGrid.ColumnDefinitions[2].Width = new GridLength(0, GridUnitType.Star);
                    leftButtonsAreaShadow.Visibility = Visibility.Collapsed;
                    rightButtonsAreaShadow.Visibility = Visibility.Collapsed;
                    midButtons.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                    midButtons.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Star);
                    numBlockShadow.Visibility = Visibility.Collapsed;
                    Grid.SetRow(displayGrid, 2);
                    Grid.SetRow(inputBlockShadow, 1);
                    Grid.SetRow(downGrid, 0);
                    Grid.SetRow(displayMaterial, 2);
                    Grid.SetRowSpan(displayMaterial, 2);
                    inputGrid.Margin = new Thickness(0, 0, 0, 0);
                    inputBlockShadow.Margin = new Thickness(10, 0, 10, 0);
                    conButtonsArea.Margin = new Thickness(10, 0, 10, 0);
                    displayGrid.Margin = new Thickness(10, 0, 10, 0);
                    displayMaterial.Margin = new Thickness(10, 0, 10, 0);
                    viewbox2.Margin = new Thickness(0, 10, 0, 0);

                    page.Background = BgBrush;
                    bigGrid.Background = page.Background;

                    doWithTitleBar(White);
                    doWithEqual(TapFirst);
                    ShowDirectionKeys = ShowDirectionKeys;

                    gridSplitter.Visibility = Visibility.Collapsed;

                    Settings.Log("landscape");
                }
            }

            if (!alwaysOnTopModeChanging)
            {
                if (isAlwaysOnTop)
                {
                    localSettings.Values["windowPicWidth"] = ActualWidth;
                    localSettings.Values["windowPicHeight"] = ActualHeight;
                }
                else
                {
                    localSettings.Values["windowWidth"] = ActualWidth;
                    localSettings.Values["windowHeight"] = ActualHeight;
                    localSettings.Values["windowFullScreen"] = self.IsFullScreenMode;
                }
            }
        }

        private void changeBackground()
        {
            if (deviceIsMobile())
                page.Background = displayRectangle.Fill;
            else
                page.Background = BgBrush;

            bigGrid.Background = page.Background;
        }

        private void refreshOriginalListItemWidth()
        {
            originalListItem.Width = Math.Max((sviewer.ActualWidth / scale - 20) / windowScale, 0);
        }

        private void doWithEqual(bool tapfirst)
        {
            if (tapfirst)
            {
                if (windowState == 1)
                {
                    inputGrid.ColumnDefinitions[RightHanded ? 2 : 0].Width = new GridLength(0, GridUnitType.Star);
                    inputGrid.ColumnDefinitions[1].Width = new GridLength(19, GridUnitType.Star);
                    inputGrid.ColumnDefinitions[RightHanded ? 0 : 2].Width = new GridLength(3, GridUnitType.Star);
                    if (bequalPlace1.Children.Contains(bequal))
                    {
                        bigrightButtons.RowDefinitions[4].Height = new GridLength(6, GridUnitType.Star);
                        smallnumButtons.RowDefinitions[4].Height = new GridLength(0.8, GridUnitType.Star);
                        bequalPlace1.Children.Remove(bequal);
                        bequalPlace2.Children.Add(bequal);
                    }
                }
                else
                {
                    inputGrid.ColumnDefinitions[0].Width = new GridLength(3, GridUnitType.Star);
                    inputGrid.ColumnDefinitions[1].Width = new GridLength(16, GridUnitType.Star);
                    inputGrid.ColumnDefinitions[2].Width = new GridLength(3, GridUnitType.Star);
                }
            }
            else if (!tapfirst)
            {
                inputGrid.ColumnDefinitions[0].Width = new GridLength(3, GridUnitType.Star);
                inputGrid.ColumnDefinitions[1].Width = new GridLength(16, GridUnitType.Star);
                inputGrid.ColumnDefinitions[2].Width = new GridLength(3, GridUnitType.Star);
                if (bequalPlace2.Children.Contains(bequal))
                {
                    bigrightButtons.RowDefinitions[4].Height = new GridLength(5, GridUnitType.Star);
                    smallnumButtons.RowDefinitions[4].Height = new GridLength(0, GridUnitType.Star);
                    bequalPlace2.Children.Remove(bequal);
                    bequalPlace1.Children.Add(bequal);
                }
            }
        }

        private void doWithTitleBar(bool white)
        {
            //设置标题栏颜色
            var titleBar = App.MainWindow.AppWindow.TitleBar;
            Color bgColor, bgHoverColor;
            if (white)
            {
                bgColor = Color.FromArgb(255, 0x23, 0x23, 0x23);
                bgHoverColor = Color.FromArgb(255, 0x48, 0x48, 0x48);

                titleBar.ForegroundColor = Colors.White;
                titleBar.InactiveForegroundColor = Colors.White;
                titleBar.ButtonHoverBackgroundColor = bgHoverColor;
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonInactiveForegroundColor = Colors.White;
            }
            else
            {
                bgColor = Color.FromArgb(255, 220, 220, 220);
                bgHoverColor = Color.FromArgb(255, 90, 90, 90);

                titleBar.ForegroundColor = Colors.Black;
                titleBar.InactiveForegroundColor = Colors.Black;
                titleBar.ButtonHoverBackgroundColor = bgHoverColor;
                titleBar.ButtonForegroundColor = Colors.Black;
                titleBar.ButtonInactiveForegroundColor = Colors.Black;
            }

            if (windowState == 0)
            {
                titleBar.BackgroundColor = Colors.Transparent;
                titleBar.InactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            }
            else
            {
                titleBar.BackgroundColor = Colors.Transparent;
                titleBar.InactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                //titleBar.BackgroundColor = bgColor;
                //titleBar.InactiveBackgroundColor = bgColor;
                //titleBar.ButtonBackgroundColor = bgColor;
                //titleBar.ButtonInactiveBackgroundColor = bgColor;
            }
        }

        private void clear()
        {
            CancelPendingCalculation();
            formulaEdit = null;
            formulaCaret = null; formulaCaretMap = null; formulaCaretSource = null; ResumeEditingCarets();
            inputPreviousAnswer = null; inputUseRadians = null; inputMode = null; UpdateModeControls();
            if (TreeViewState)
            {
                TreeViewState = false;
            }

            listView.Items.Clear();
            inputC = null;
            presentAnswer = null;
            quickSetInputTextWithoutInvokingChange("");
            NowS = new InputString("");
            textPtr = 0;
            TextChanged();
            solved = false;
            historyManager.Ptr = historyManager.Items.Count;
            updateRecordMenu();

        }

        private void insertNew(string text, string autoBefore = "")
        {
            if (text == null) return;
            ResumeEditingCarets();
            bool newCalculation = solved;
            string source = newCalculation ? "" : lastInput;
            int caret = newCalculation ? 0 : textPtr;
            if (caret == 0) text = autoBefore + text;
            var edit = !newCalculation && formulaEdit != null
                ? formulaEdit.Insert(caret, _text.SelectionLength, text)
                : NaturplayCalculator.Input.InputEditor.Insert(source, caret, newCalculation ? 0 : _text.SelectionLength, text);
            if (edit.Text.Length > JSON.MaxInputLength)
            {
                ShowCalculationError(AppResources.GetString("calculation_limit")); return;
            }
            if (newCalculation) clear();
            var exit = FormulaScopeExit(edit);
            quickSetInputTextWithoutInvokingChange(edit.Text);
            textPtr = edit.Caret;
            TextChanged();
            _text.SelectionLength = 0;
            FinishInputCaret(edit, exit);
        }

        private void buttonClick(string newstr, string autoBefore = "")
        {
            insertNew(newstr, autoBefore);
            Settings.Log("ui_click_" + newstr.Trim());
        }

        private void b1_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("1");

        }

        private void b2_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("2");
        }

        private void b3_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("3");
        }

        private void b4_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("4");
        }

        private void b5_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("5");
        }

        private void b6_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("6");
        }

        private void b7_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("7");
        }

        private void b8_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("8");
        }

        private void b9_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("9");
        }

        private void b0_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("0");
        }

        private void bpoint_Click(object sender, RoutedEventArgs e)
        {
            buttonClick(".");
        }

        private void bminus_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("-");
        }

        private void bpower_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("*", "Ans");
        }

        private void bdevide_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("/", "Ans");
        }

        private void bleft_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("(");
            //自动补全右括号
            //insertNew(")", "", InputStringType.Auto);
            //setThePtr(textPtr - 1);
        }

        private void bright_Click(object sender, RoutedEventArgs e)
        {
            //右括号是否被自动补全
            //if (textPtr < NowS.Count && NowS[textPtr].C == ')' && NowS[textPtr].Type == InputStringType.Auto)
            //    setThePtr(textPtr + 1);
            //else
            //{
            buttonClick(")");
            //}
        }

        private void bequal_Click(object sender, RoutedEventArgs e)
        {
            solve();
        }

        private void solve() { HideEditingCarets(); _ = CalculateAsync(); }

        private void bplus_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("+", "Ans");
        }

        private void doIt() { _ = CalculateAsync(); }



        private TreeViewNode makeTree(JsonElement e)
        {
            var ti = new TreeViewNode();
            if (e is JsonObject)
            {
                JsonObject j = e as JsonObject;
                ti.Content = "{Object}";
                foreach (JsonKeyValue kv in j.KeyValue)
                {
                    var vi = makeTree(kv.Value);
                    vi.Content = $"\"{kv.Key}\" : {vi.Content}";
                    ti.Children.Add(vi);
                }
            }
            else if (e is JsonArray)
            {
                JsonArray j = e as JsonArray;
                ti.Content = $"[Array({j.Elements.Count})]";
                int i = 0;
                foreach (JsonElement c in j.Elements)
                {
                    var vi = makeTree(c);
                    vi.Content = $"{i++} : {vi.Content}";
                    ti.Children.Add(vi);
                }
            }
            else if (e is JsonValue)
                ti.Content = (e as JsonValue).ToString();
            return ti;
        }

        private TreeViewNode makeTree(XmlNode e)
        {
            var ti = new TreeViewNode();

            if (e.Name == "#text")
            {
                ti.Content = $"\"{e.InnerText}\"";
            }
            else if (e.Name == "#comment")
            {
                ti.Content = $"{e.OuterXml}";
            }
            else
            {
                ti.Content = $"<{e.Name}>";
            }

            if (e.Attributes != null && e.Attributes.Count > 0)
            {
                var tas = new TreeViewNode()
                {
                    Content = $"Attributes({e.Attributes.Count})"
                };
                foreach (System.Xml.XmlAttribute a in e.Attributes)
                {
                    var ta = new TreeViewNode()
                    {
                        Content = $"{a.Name} = \"{a.Value}\""
                    };
                    tas.Children.Add(ta);
                }
                ti.Children.Add(tas);
            }

            if (e.ChildNodes != null && e.ChildNodes.Count > 0)
            {
                var tcs = new TreeViewNode()
                {
                    Content = $"ChildNodes({e.ChildNodes.Count})"
                };
                foreach (XmlNode c in e.ChildNodes)
                {
                    tcs.Children.Add(makeTree(c));
                }
                ti.Children.Add(tcs);
            }

            return ti;
        }

        private void refreshTile(CalculationValue ans)
        {
            if (!OpenLiveTile) return;

            var tileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquare150x150Text01);
            var tileTextAttributes = tileXml.GetElementsByTagName("text");
            tileTextAttributes[0].InnerText = AppResources.GetString("tileText");
            tileTextAttributes[1].InnerText = ans.ToString();

            var wideTileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileWide310x150Text01);
            tileTextAttributes = wideTileXml.GetElementsByTagName("text");
            tileTextAttributes[0].InnerText = AppResources.GetString("tileText");
            tileTextAttributes[1].InnerText = ans.ToString();

            IXmlNode node = tileXml.ImportNode(wideTileXml.GetElementsByTagName("binding").Item(0), true);
            tileXml.GetElementsByTagName("visual").Item(0).AppendChild(node);

            TileNotification tileNotification = new TileNotification(tileXml);
            tileNotification.ExpirationTime = DateTimeOffset.UtcNow.AddHours(1);
            TileUpdateManager.CreateTileUpdaterForApplication().Update(tileNotification);
        }





        private void bac_Click(object sender, RoutedEventArgs e)
        {
            clear();
        }

        private void bc_Click(object sender, RoutedEventArgs e)
        {
            backspace();
        }

        private void backspace() { DeleteInput(true); }

        private void DeleteInput(bool backward)
        {
            ResumeEditingCarets();
            var edit = formulaEdit != null ? formulaEdit.Delete(textPtr, _text.SelectionLength, backward)
                : NaturplayCalculator.Input.InputEditor.Delete(lastInput, textPtr, _text.SelectionLength, backward);
            if (edit.Text == lastInput) return;
            quickSetInputTextWithoutInvokingChange(edit.Text);
            NowS = new InputString(edit.Text); NowS.GiveIndex();
            textPtr = edit.Caret;
            TextChanged();
            setThePtr(edit.Caret);
        }

        private void del() { DeleteInput(false); }

        private void bpowerself_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("^", "Ans");
        }

        private void bsqrt_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("√");
        }

        private void b000_Click(object sender, RoutedEventArgs e)
        {
            buttonClick(",", "(Ans");
        }

        private void bsin_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("sin");
        }

        private void barcsin_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("arcsin");
        }

        private void barccos_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("arccos");
        }

        private void bcos_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("cos");
        }

        private void btan_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("tan");
        }

        private void barctan_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("arctan");
        }

        private void byroot_Click(object sender, RoutedEventArgs e)
        {
            buttonClick(" yroot ", "Ans");
        }

        private void bnfact_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("!", "Ans");
        }

        private void be_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("e");
        }

        private void bln_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("ln");
        }

        private void bpi_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("π");
        }

        private void TextChanged(bool reCompute = false)
        {
            RefreshInputPresentation();
            CancelPendingCalculation();
            ScheduleDraftSave();
            presentAnswer = null; solved = false; inputC = null;
            TreeViewState = false;
            var source = new InputString(lastInput); source.GiveIndex();
            NowS.Copy(source);
            if (lastInput.Length == 0)
            {
                if (activeInputSurface == InputSurface.Formula) AddInputPreview();
                UpdateFormulaCaret(0);
                return;
            }
            try
            {
                if (ExpressionPreview.IsMathCandidate(lastInput))
                {
                    AddInputPreview();
                }
                else addListViewItem(new TextBlock { Text = lastInput, TextWrapping = TextWrapping.Wrap, MaxWidth = 1000 });
            }
            catch (Exception ex)
            {
                Settings.ReportError("Render input", ex);
                listView.Items.Clear();
                // Unexpected renderer failures are diagnosed, never presented as a
                // successful plain-text approximation of a mathematical formula.
                addListViewItem(new TextBlock { Text = AppResources.GetString("invalid_expression"), TextWrapping = TextWrapping.Wrap });
            }
            if (SolveWhenInput && !restoringSession && lastInput.Length <= ExpressionLimits.MaxLength) _ = PreviewAsync(lastInput);
            UpdateFormulaCaret(textPtr);
        }

        private InputString tryToSaveTheWrongExpression(InputString input)
        {
            if (input.Count > ExpressionLimits.MaxLength) return input;
            string text = input.ToString().TrimStart();
            if (text.StartsWith("{") || text.StartsWith("<") || text.StartsWith("\"") || text.StartsWith("'")) return input;
            int depth = 0;
            bool comment = false, escaped = false;
            foreach (var c in input)
            {
                if (comment)
                {
                    if (c.C == ']' && !escaped) comment = false;
                    escaped = c.C == '\\' && !escaped;
                }
                else if (c.C == '[') comment = true;
                else if (c.C == '(') depth++;
                else if (c.C == ')') depth--;
                if (depth > ExpressionLimits.MaxDepth) throw new MathException(AppResources.GetString("calculation_limit"));
            }
            for (int i = 0; i < depth; i++) input += new InputChar(')');
            return input;
        }

        private void bncr_Click(object sender, RoutedEventArgs e)
        {
            buttonClick(" nCr ", "Ans");
        }

        private void bnpr_Click(object sender, RoutedEventArgs e)
        {
            buttonClick(" nPr ", "Ans");
        }

        private void ToggleMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            Settings.UseRadians = true;
            inputUseRadians = true;
            Thudu.IsChecked = true;
            Tnohudu.IsChecked = false;
            if (lastInput != "")
            {
                if (solved)
                    doIt();
                else
                    TextChanged(true);
            }

            localSettings.Values["hudu"] = true;
        }

        private void ToggleMenuFlyoutItem_Click_1(object sender, RoutedEventArgs e)
        {
            Settings.UseRadians = false;
            inputUseRadians = false;
            Thudu.IsChecked = false;
            Tnohudu.IsChecked = true;
            if (lastInput != "")
            {
                if (solved)
                    doIt();
                else
                    TextChanged(true);
            }

            localSettings.Values["hudu"] = false;

        }

        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            showAbout();
        }

        private async void showAbout()
        {
            await ShowDialogAsync(AboutWindow);
            Settings.Log("show_about");
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            changeTheSizeOfWindow();
        }

        private void Tsolve_Click(object sender, RoutedEventArgs e)
        {
            SolveWhenInput = Tsolve.IsChecked;
            localSettings.Values["solveWhenInput"] = SolveWhenInput;
            TextChanged(true);
        }

        private void babs_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("Abs");
        }

        private void bt_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("^T", "Ans");
        }

        private void mnext_Click(object sender, RoutedEventArgs e)
        {
            next();
        }

        private void next()
        {
            if (historyManager.Ptr < historyManager.Items.Count - 1)
            {
                historyManager.Ptr++;
                showHistory();
            }
        }

        private void mpre_Click(object sender, RoutedEventArgs e)
        {
            pre();
        }

        private void pre()
        {
            if (historyManager.Ptr > 0)
            {
                historyManager.Ptr--;
                showHistory();
            }
        }



        private void updateRecordMenu()
        {
            mnext.IsEnabled = !(historyManager.Ptr >= historyManager.Items.Count - 1);
            mpre.IsEnabled = !(historyManager.Ptr <= 0);
            mdelHis.IsEnabled = historyManager.Ptr >= 0 && historyManager.Ptr < historyManager.Items.Count;
            if (mdelHis.IsEnabled)
                tHisPtr.Text = $"{historyManager.Ptr + 1}/{historyManager.Items.Count}";
            else
                tHisPtr.Text = "";
        }

        private void MenuFlyoutItem_Click_1(object sender, RoutedEventArgs e)
        {
            showHelp();
        }

        private async void showHelp()
        {
            await ShowDialogAsync(HelpWindow);
            Settings.Log("show_help");
        }

        private void bave_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("E");
        }

        private void bd_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("D");
        }

        private void babout_Click(object sender, RoutedEventArgs e)
        {
            showAbout();
        }

        private void bi_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("i");
        }

        private void MenuFlyoutItem_Click_2(object sender, RoutedEventArgs e)
        {
            SaveAsPicAsync(listView);
        }

        private void msmall_Click(object sender, RoutedEventArgs e)
        {
            slider.Value -= 10;
            localSettings.Values["scale"] = scale;
        }

        private void mbig_Click(object sender, RoutedEventArgs e)
        {
            slider.Value += 10;
            localSettings.Values["scale"] = scale;
        }

        private void slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            displayViewScaleChanged();
        }

        private void displayViewScaleChanged()
        {
            scale = slider.Value / 100;
            if (listView.ActualWidth > 0) viewbox2.Width = listView.ActualWidth * scale * windowScale;
            if (listView.ActualHeight > 0) viewbox2.Height = listView.ActualHeight * scale * windowScale;
        }

        private void slider_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            localSettings.Values["scale"] = scale;
        }

        private void bmod_Click(object sender, RoutedEventArgs e)
        {
            buttonClick(" mod ", "Ans");
        }

        private void bans_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("Ans");
        }

        private void blefta_Click(object sender, RoutedEventArgs e)
        {
            left();
        }

        private void left()
        {
            if (MoveFormulaCaret(-1)) return;
            if (inputC == null)
                TextChanged();
            if (textPtr > 0)
                setThePtr(--textPtr);
        }

        private void brighta_Click(object sender, RoutedEventArgs e)
        {
            right();
        }

        private void right()
        {
            if (MoveFormulaCaret(1)) return;
            if (inputC == null)
                TextChanged();
            if (textPtr < lastInput.Length)
                setThePtr(++textPtr);
        }

        private int findInputCIndex()
        {
            if (inputC == null) return -1;
            for (var i = 0; i < listView.Items.Count; i++)
            {
                if (listView.Items[i] is ListViewItem li)
                    if (ReferenceEquals(li.Content, inputC))
                        return i;
            }
            return -1;
        }

        private string inputText() => lastInput;

        private void quickSetInputTextWithoutInvokingChange(string s)
        {
            lastInput = s;
            RefreshInputPresentation();
            ScheduleDraftSave();
        }

        private void blr_Click(object sender, RoutedEventArgs e)
        {
            setThePtr(0);
            insertNew("(");
            setThePtr(lastInput.Length);
            insertNew(")");
            Settings.Log("ui_click_(+)");
        }

        private void bup_Click(object sender, RoutedEventArgs e)
        {
            up();
        }

        private void up()
        {
            if (MoveFormulaCaret(-1, boundary: true)) return;
            setThePtr(0);
        }

        private void bdown_Click(object sender, RoutedEventArgs e)
        {
            down();
        }

        private void down()
        {
            if (MoveFormulaCaret(1, boundary: true)) return;
            setThePtr(lastInput.Length);
        }

        private void Tblack_Click(object sender, RoutedEventArgs e)
        {
            White = true;
            Tblack.IsChecked = true;
            Twhite.IsChecked = false;
            Tfs.IsChecked = false;
            localSettings.Values["white"] = true;
            localSettings.Values["followSystem"] = false;
        }

        private void Twhite_Click(object sender, RoutedEventArgs e)
        {
            White = false;
            Tblack.IsChecked = false;
            Twhite.IsChecked = true;
            Tfs.IsChecked = false;
            localSettings.Values["white"] = false;
            localSettings.Values["followSystem"] = false;
            Settings.Log("white");
        }

        private void Tfs_Click(object sender, RoutedEventArgs e)
        {
            White = getSystemTheme() != "Light";
            Tblack.IsChecked = false;
            Twhite.IsChecked = false;
            Tfs.IsChecked = true;
            localSettings.Values["white"] = White;
            localSettings.Values["followSystem"] = true;
        }

        private string getSystemTheme()
        {
            if (!Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.UISettings"))
                return "Unknown";
            var DefaultTheme = new UISettings();
            var uiTheme = DefaultTheme.GetColorValue(UIColorType.Background).ToString();
            if (uiTheme == "#FF000000")
            {
                return "Dark";
            }
            else if (uiTheme == "#FFFFFFFF")
            {
                return "Light";
            }
            else
            {
                return "Other";
            }
        }

        private void bwen_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("?");
        }

        private void bsinh_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("sinh");
        }

        private void barsh_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("arsh");
        }

        private void bcosh_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("cosh");
        }

        private void barch_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("arch");
        }

        private void btanh_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("tanh");
        }

        private void barth_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("arth");
        }

        private void blg_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("lg");
        }

        private void copyToClipboard(string text)
        {
            try
            {
                var data = new DataPackage(); data.SetText(text ?? ""); Clipboard.SetContent(data);
            }
            catch (Exception ex) { Settings.ReportError("Copy", ex); }
        }

        private void MenuFlyoutItem_Click_3(object sender, RoutedEventArgs e)
        {
            copyToClipboard(inputText());
        }

        private async void MenuFlyoutItem_Click_4(object sender, RoutedEventArgs e)
        {
            try
            {
                var d = Clipboard.GetContent();
                if (d.Contains(StandardDataFormats.Text)) insertNew(await d.GetTextAsync());
            }
            catch (Exception ex) { Settings.ReportError("Paste", ex); }
        }

        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            //insertNew(e.Key.ToString());
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Back:
                    backspace();
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Delete:
                    del();
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Enter:
                    solve();
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Left:
                    left();
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Right:
                    right();
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Up:
                    up();
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Down:
                    down();
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.PageUp:
                    pre();
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.PageDown:
                    next();
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Escape:
                    clear();
                    e.Handled = true;
                    break;
                //暂时应用户要求，希望之后可自定义热键
                case Windows.System.VirtualKey.Insert:
                    clear();
                    e.Handled = true;
                    break;
                default:
                    if (_text.FocusState == FocusState.Unfocused)
                    {
                        var val = (int)e.Key;
                        if (val >= (int)Windows.System.VirtualKey.Number0 && val <= (int)Windows.System.VirtualKey.Number9)
                            insertNew(Convert.ToString(val - (int)Windows.System.VirtualKey.Number0));
                        else if (val >= (int)Windows.System.VirtualKey.NumberPad0 && val <= (int)Windows.System.VirtualKey.NumberPad9)
                            insertNew(Convert.ToString(val - (int)Windows.System.VirtualKey.NumberPad0));
                        else if (val >= (int)Windows.System.VirtualKey.A && val <= (int)Windows.System.VirtualKey.Z)
                            insertNew(Convert.ToString((char)(val - (int)Windows.System.VirtualKey.A + (int)'a')));
                        focusToTyping();
                    }
                    break;
            }
        }

        private void _text_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (inputBox.IsUpdating || _text.Text == (formulaEdit?.Buffer ?? lastInput)) return;
            ResumeEditingCarets();
            var edit = formulaEdit != null ? formulaEdit.ReplaceBuffer(_text.Text, _text.SelectionStart)
                : NaturplayCalculator.Input.InputEditor.ApplyTypedText(lastInput, _text.Text, _text.SelectionStart, solved);
            var exit = FormulaScopeExit(edit);
            if (edit.StartsNewCalculation) { inputPreviousAnswer = null; inputUseRadians = null; inputMode = null; UpdateModeControls(); }
            solved = false;
            quickSetInputTextWithoutInvokingChange(edit.Text);
            NowS = new InputString(edit.Text); NowS.GiveIndex(); textPtr = edit.Caret;
            TextChanged(); FinishInputCaret(edit, exit);
        }

        private void page_Tapped(object sender, TappedRoutedEventArgs e)
        {
            focusToTyping();
        }

        private void page_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            //focusToTyping()
        }

        private async void page_Loaded(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            try { await RestoreSessionAsync(); }
            finally { IsEnabled = true; }
            focusToTyping();
            updateAboutLogo();
            if (showingInitialDialog)
            {
                showingInitialDialog = false;
                await ShowDialogAsync(WhatsNewWindow);
            }
        }

        private void updateAboutLogo()
        {
            if (logo2.Visibility == Visibility.Visible)
            {
                logo2.Tag = White ? "logo.svg" : "logo_b.svg";
                SvgManager.LoadSvg(logo2);
            }
        }

        private void focusToTyping()
        {
            var keyboardCapabilities = new KeyboardCapabilities();  // 用于查找是否有键盘设备
            if (deviceIsDesktop() && keyboardCapabilities.KeyboardPresent != 0)
            {
                try
                {
                    inputBox.FocusInput();
                }
                catch { }
            }
        }

        private void setThePtr(int p, bool clearSelection = true, bool resumeEditing = true)
        {
            p = Math.Max(0, Math.Min(lastInput.Length, p));
            if (clearSelection) formulaSelectionAnchor = null;
            if (resumeEditing) ResumeEditingCarets();
            solved = false;
            textPtr = p;
            if (activeInputSurface == InputSurface.Formula && formulaCaretMap != null
                && (formulaCaret == null || formulaCaret.TextIndex != p || !resumeEditing))
                formulaCaret = resumeEditing ? formulaCaretMap.AfterEdit(p, formulaEdit?.ContentStart, formulaEdit?.ContentEnd)
                    : formulaCaretMap.FromText(p);
            if (formulaEdit != null && !formulaEdit.Contains(p))
            { formulaEdit = null; inputBox.SetText(lastInput); }
            inputBox.SetCaret(p, clearSelection);
            if (clearSelection) RebindFormulaBuffer();
            UpdateFormulaCaret(p);
        }

        private void TTapFirst_Click(object sender, RoutedEventArgs e)
        {
            TapFirst = true;
            TTapFirst.IsChecked = true;
            TClickFirst.IsChecked = false;
            localSettings.Values["tapFirst"] = true;
        }

        private void TClickFirst_Click(object sender, RoutedEventArgs e)
        {
            TapFirst = false;
            TTapFirst.IsChecked = false;
            TClickFirst.IsChecked = true;
            localSettings.Values["tapFirst"] = false;
        }

        private void _text_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (inputBox.IsUpdating || changingCaret) return;
            int position = inputBox.SourceSelectionStart;
            if ((formulaEdit != null || _text.Text == lastInput) && (position != textPtr || _text.SelectionLength > 0)) solved = false;
            textPtr = position;
            if (activeInputSurface == InputSurface.Formula && formulaCaretMap != null && formulaCaretSource == lastInput
                && formulaCaret?.TextIndex != position && _text.Text == formulaEdit?.Buffer)
                formulaCaret = formulaCaretMap.AfterEdit(position, formulaEdit?.ContentStart, formulaEdit?.ContentEnd);
            UpdateFormulaCaret(textPtr);
        }

        private void _text_Paste(object sender, TextControlPasteEventArgs e)
        {
            e.Handled = true;
            MenuFlyoutItem_Click_4(sender, null);
        }

        private bool deviceIsDesktop()
        {
            return AnalyticsInfo.VersionInfo.DeviceFamily.Contains("Desktop");
        }

        private bool deviceIsMobile()
        {
            return AnalyticsInfo.VersionInfo.DeviceFamily.Contains("Mobile");
        }

        private bool acrylicIsAvailable()
        {
            return Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported();
            //Microsoft.UI.Xaml.Media.XamlCompositionBrushBase
        }

        private void _text_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                //暂时应用户要求，希望之后可自定义热键
                case Windows.System.VirtualKey.Insert:
                    clear();
                    e.Handled = true;
                    break;
            }
        }

        private void TLiveTile_Click(object sender, RoutedEventArgs e)
        {
            OpenLiveTile = TLiveTile.IsChecked;
            if (!OpenLiveTile)
                TileUpdateManager.CreateTileUpdaterForApplication().Clear();
            localSettings.Values["openLiveTile"] = OpenLiveTile;
        }

        private void listViewItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            //判断是不是第一行表达式
            if (inputC != null && ReferenceEquals((sender as ListViewItem).Content, inputC))
            {
                listViewItemMenu_copyExpressionAndAnswer.Visibility = Visibility.Visible;
                listViewItemMenu_copyExpressionAndAnswer.IsEnabled = presentAnswer != null;
            }
            else
                listViewItemMenu_copyExpressionAndAnswer.Visibility = Visibility.Collapsed;

            listViewItemMenu.ShowAt(listView, e.GetPosition(this.listView));
            currentSelectedListViewItem = sender as ListViewItem;
            e.Handled = true;
        }

        //private void treeViewItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        //{
        //    treeViewItemMenu.ShowAt(treeView, e.GetPosition(this.treeView));
        //    currentSelectedTreeViewItem = sender as WinRTXamlToolkit.Controls.TreeViewItem;
        //    e.Handled = true;
        //}

        private void treeViewItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            treeViewItemMenu.ShowAt(treeView, e.GetPosition(this.treeView));
            currentSelectedTreeViewItem = sender as TreeViewNode;
            e.Handled = true;
        }

        private ListViewItem prepareListViewItem(object e)
        {
            if (e is ListViewItem lvi)
            {
                lvi.HorizontalAlignment = HorizontalAlignment.Left;
                lvi.MinWidth = 0;
                if (lvi is Controls.FormulaListItem) lvi.Style = (Style)Resources["FormulaRowStyle"];
                return lvi;
            }
            else
            {
                var item = new Controls.FormulaListItem()
                {
                    Content = e,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    MinWidth = 0,
                    Style = (Style)Resources["FormulaRowStyle"]
                };
                item.RightTapped += listViewItem_RightTapped;
                return item;
            }
        }

        private void addListViewItem(object e)
        {
            listView.Items.Add(prepareListViewItem(e));
        }

        private void insertListViewItem(int index, object e)
        {
            listView.Items.Insert(index, prepareListViewItem(e));
        }

        private void listViewItemMenu_saveAsPic_Click(object sender, RoutedEventArgs e)
        {
            if (currentSelectedListViewItem != null)
                SaveAsPicAsync(currentSelectedListViewItem);
        }

        private void listViewItemMenu_copy_Click(object sender, RoutedEventArgs e)
        {
            if (currentSelectedListViewItem?.Content is string s)
                copyToClipboard(s);
            else if (currentSelectedListViewItem?.Content is Canvas c && c.Tag is string code)
                copyToClipboard(code);
            else if (currentSelectedListViewItem?.Content is TextBlock text)
                copyToClipboard(text.Tag as string ?? text.Text);
        }

        private async void inputRichBox_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            try
            {
                var d = Clipboard.GetContent();
                var s = await d.GetTextAsync();
                mPaste.IsEnabled = true;
            }
            catch
            {
                mPaste.IsEnabled = false;
            }
            finally
            {
                mCopy.IsEnabled = (lastInput != "");
            }
            inputBoxMenu.ShowAt(inputRectangle, e.GetPosition(this.inputRectangle));
        }

        private void TDirectionKeys_Click(object sender, RoutedEventArgs e)
        {
            ShowDirectionKeys = TDirectionKeys.IsChecked;
            localSettings.Values["directionKeys"] = ShowDirectionKeys;
        }

        //亚克力材质Flag
        private void TAcrylic_Click(object sender, RoutedEventArgs e)
        {
            Acrylic = TAcrylic.IsChecked;
            localSettings.Values["acrylic"] = Acrylic;
        }

        private void TLeftHanded_Click(object sender, RoutedEventArgs e)
        {
            RightHanded = false;
            TLeftHanded.IsChecked = true;
            TRightHanded.IsChecked = false;
            localSettings.Values["rightHanded"] = false;
        }

        private void TRightHanded_Click(object sender, RoutedEventArgs e)
        {
            RightHanded = true;
            TLeftHanded.IsChecked = false;
            TRightHanded.IsChecked = true;
            localSettings.Values["rightHanded"] = true;
        }

        private void TShowAO_Click(object sender, RoutedEventArgs e)
        {
            ShowAdvancedOperators = TShowAO.IsChecked;
            localSettings.Values["showAO"] = ShowAdvancedOperators;
        }



        private void TFoldingNum_Click(object sender, RoutedEventArgs e)
        {
            FoldingNum = TFoldingNum.IsChecked;
            localSettings.Values["foldingNum"] = FoldingNum;
        }

        private void b10n_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("*10^", "Ans");
        }

        private void brank_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("r");
        }

        private void bcov_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("Cov");
        }

        private void brho_Click(object sender, RoutedEventArgs e)
        {
            buttonClick("ρ");
        }

        private void bshift_Click(object sender, RoutedEventArgs e)
        {
            Shift = !Shift;
            localSettings.Values["shift"] = Shift;
        }

        private void treeViewItemMenu_copy_Click(object sender, RoutedEventArgs e)
        {
            if (currentSelectedTreeViewItem != null)
                //copyToClipboard(currentSelectedTreeViewItem.Header.ToString());
                copyToClipboard(currentSelectedTreeViewItem.Content.ToString());
        }

        private void listView_LayoutUpdated(object sender, object e)
        {
            viewbox2.Width = displayView.ActualWidth * scale * windowScale;
            viewbox2.Height = displayView.ActualHeight * scale * windowScale;
            refreshOriginalListItemWidth();
        }

        private void treeView_LayoutUpdated(object sender, object e)
        {
            viewbox2.Width = displayView.ActualWidth * scale * windowScale;
            viewbox2.Height = displayView.ActualHeight * scale * windowScale;
        }

        private void bsetting_Click(object sender, RoutedEventArgs e)
        {
            Mjietu.IsEnabled = listView.Items.Count > 0;
        }

        private void bdonate0_Click(object sender, RoutedEventArgs e)
        {
            showAbout();
        }

        private void bdonate1_Click(object sender, RoutedEventArgs e)
        {
            showAbout();
        }

        private void bdonate2_Click(object sender, RoutedEventArgs e)
        {
            showAbout();
        }

        private void TClearLast_Click(object sender, RoutedEventArgs e)
        {
            localSettings.Values["clearLast"] = TClearLast.IsChecked;
        }

        private void listViewItemMenu_copyExpressionAndAnswer_Click(object sender, RoutedEventArgs e)
        {
            if (currentSelectedListViewItem?.Content is Canvas c && c.Tag is string code)
                copyToClipboard($"{code} = {presentAnswer}");
        }

        private void TShowCalculationSteps_Click(object sender, RoutedEventArgs e)
        {
            ShowCalculationSteps = TShowCalculationSteps.IsChecked;
            localSettings.Values["showCalculationSteps"] = ShowCalculationSteps;
        }

        private async void bdonate_Click(object sender, RoutedEventArgs e)
        {
            await LaunchLinkAsync("https://www.buymeacoffee.com/zamhown");
        }

        private void toggleTop()
        {
            if (alwaysOnTopModeChanging) return;
            alwaysOnTopModeChanging = true;
            try
            {
                bool enabled = !isAlwaysOnTop;
                double width = localSettings.Values.TryGetValue("windowPicWidth", out var w) && w is double savedWidth ? savedWidth : 400;
                double height = localSettings.Values.TryGetValue("windowPicHeight", out var h) && h is double savedHeight ? savedHeight : 600;
                self.SetCompactOverlay(enabled, new Size(width, height));
                isAlwaysOnTop = enabled;
                btopText.Text = enabled ? "" : "";
                localSettings.Values["alwayOnTop"] = enabled;
            }
            catch (Exception ex) { Settings.ReportError("Change window mode", ex); }
            finally { alwaysOnTopModeChanging = false; Ttop.IsChecked = isAlwaysOnTop; }
        }

        private void btop_Click(object sender, RoutedEventArgs e)
        {
            toggleTop();
        }

        private void Ttop_Click(object sender, RoutedEventArgs e)
        {
            toggleTop();
        }

        private void treeView_ItemInvoked(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs args)
        {
            Debug.WriteLine(((TreeViewNode)args.InvokedItem).Content);
            // if (args.InvokedItem is TreeViewNode node)
        }

        private void bequal_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            bequal.Foreground = new SolidColorBrush(Colors.White);
        }

        private void bsetting_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            bsetting.Foreground = new SolidColorBrush(White ? Colors.White : Colors.Black);
        }

        private void TEmoji_Click(object sender, RoutedEventArgs e)
        {
            IsEmojiUI = TEmoji.IsChecked;
            localSettings.Values["emoji"] = IsEmojiUI;
        }

        private void displayGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            pointerHoverDisplayArea = true;
            if (pointerHoverDisplayArea || pointerHoverInputArea || pointerHoverGridSplitter)
                displayGrid.RowDefinitions[1].Height = new GridLength(32, GridUnitType.Pixel);
        }

        private void displayGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            pointerHoverDisplayArea = false;
            if (!pointerHoverDisplayArea && !pointerHoverInputArea && !pointerHoverGridSplitter)
                displayGrid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Pixel);
        }

        private void inputGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            pointerHoverInputArea = true;
            if (pointerHoverDisplayArea || pointerHoverInputArea || pointerHoverGridSplitter)
                displayGrid.RowDefinitions[1].Height = new GridLength(32, GridUnitType.Pixel);
        }

        private void inputGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            pointerHoverInputArea = false;
            if (!pointerHoverDisplayArea && !pointerHoverInputArea && !pointerHoverGridSplitter)
                displayGrid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Pixel);
        }

        private void gridSplitter_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            pointerHoverGridSplitter = true;
            if (pointerHoverDisplayArea || pointerHoverInputArea || pointerHoverGridSplitter)
                displayGrid.RowDefinitions[1].Height = new GridLength(32, GridUnitType.Pixel);
        }

        private async void MenuFlyoutItem_Click_5(object sender, RoutedEventArgs e)
        {
            var restoreKeys = new string[] {
                "windowFullScreen",
                "windowWidth",
                "windowHeight",
                "hudu",
                "solveWhenInput",
                "openLiveTile",
                "directionKeys",
                "showAO",
                "showCalculationSteps",
                "acrylic",
                "followSystem",
                "white",
                "tapFirst",
                "rightHanded",
                "foldingNum",
                "shift",
                "scale",
                "clearLast",
                "alwayOnTop",
                "emoji",
                "mainGridRow0",
                "mainGridRow1",
                "mainGridRow2",
                "windowPicWidth",
                "windowPicHeight",
                "calculationMode"
            };
            foreach (var key in restoreKeys) localSettings.Values.Remove(key);
            await ShowAlertAsync(4);
        }

        private void gridSplitter_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            pointerHoverGridSplitter = false;
            if (!pointerHoverDisplayArea && !pointerHoverInputArea && !pointerHoverGridSplitter)
                displayGrid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Pixel);
        }

        private void downGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (
                windowState == 1
                || downGrid.ActualHeight < downGrid.ActualWidth
                || downGrid.ActualHeight == 0
            )
            {
                buttonGrid.Visibility = Visibility.Visible;
                buttonGrid_1.Visibility = Visibility.Collapsed;
                if (conButtonsArea_1.Children.Count > 0)
                {
                    conButtonsArea_1.Children.Remove(conButtons);
                    conButtonsArea.Children.Add(conButtons);
                }
            }
            else
            {
                buttonGrid_1.Visibility = Visibility.Visible;
                buttonGrid.Visibility = Visibility.Collapsed;
                if (conButtonsArea.Children.Count > 0)
                {
                    conButtonsArea.Children.Remove(conButtons);
                    conButtonsArea_1.Children.Add(conButtons);
                }
            }
            if (windowState == 0)
            {
                mainGridRow0 = mainGrid.RowDefinitions[0].Height.Value;
                mainGridRow1 = mainGrid.RowDefinitions[1].Height.Value;
                mainGridRow2 = mainGrid.RowDefinitions[2].Height.Value;
                localSettings.Values["mainGridRow0"] = mainGridRow0;
                localSettings.Values["mainGridRow1"] = mainGridRow1;
                localSettings.Values["mainGridRow2"] = mainGridRow2;
            }
        }

        private void teachingTip1_ActionButtonClick(Microsoft.UI.Xaml.Controls.TeachingTip sender, object args)
        {
            White = userWhite;
            if (userFS)
            {
                Tfs.IsChecked = true;
                Tblack.IsChecked = false;
                Twhite.IsChecked = false;
            }
            else
            {
                Tfs.IsChecked = false;
                Tblack.IsChecked = White;
                Twhite.IsChecked = !White;
            }

            IsEmojiUI = userEmoji;
            TEmoji.IsChecked = userEmoji;

            teachingTip1.IsOpen = false;
        }

        public void AnsMan(object sender, RoutedEventArgs e)
        {
            clear();
            insertNew("(?/Abs(?,Ans,?)/(√(0,π,0)/Abs(Abs(1,,1,,1,,0)/?,Abs(1,1,0,1,1,,1,1,0,1,1,,1,1,0,1,1)/Abs(Abs(1,,1,,T),?,Abs(1,,1,,T)),Abs(1,,1,,1,,0)/?)))/Abs(?,Abs(?),?,Abs(?),?,Abs(?),?)/?    [title:THE ANSWER MAN HAS BEEN LOOKING AT YOU! 🌝]");
            Settings.Log("ansman");
        }

        private void version(object sender, RoutedEventArgs e)
        {
            clear();
            insertNew(Settings.GetVersion());
        }

        public void changeEmoji()
        {
            if (IsEmojiUI && babout.Content is Viewbox vb && vb.Child is Grid g && g.Children.Count > 1)
            {
                for (var i = 0; i < g.Children.Count; i++)
                    g.Children[i].Visibility = Visibility.Collapsed;
                g.Children[1 + Convert.ToInt32(Math.Floor(
                    (g.Children.Count - 1) * new Random().NextDouble()
                    ))].Visibility = Visibility.Visible;
            }
        }
    }
}
