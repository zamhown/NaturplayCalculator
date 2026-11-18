using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace NaturplayCalculator.Tests;

// These portable checks protect event wiring and visual contracts; live WinUI testing
// is still required to verify binding updates, focus, hit testing and native rendering.
public class UiMarkupTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Ui = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static XDocument Page() => XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Ui", "MainPage.xaml"));
    private static XDocument Styles() => XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Ui", "CalculatorControls.xaml"));
    private static XElement Named(XDocument document, string name) => document.Descendants().Single(e => (string)e.Attribute(X + "Name") == name || (string)e.Attribute("Name") == name);
    private static Dictionary<string, string> Setters(XElement state) => state.Descendants(Ui + "Setter").ToDictionary(e => (string)e.Attribute("Target"), e => (string)e.Attribute("Value"));

    [Fact]
    public void AllThreeKeypadLayoutsKeepTheirOriginalDigitActions()
    {
        var page = Page();
        foreach (int digit in Enumerable.Range(0, 10))
        foreach (string suffix in new[] { "", "_1", "s" })
        {
            var button = Named(page, $"b{digit}{suffix}");
            Assert.Equal($"b{digit}_Click", (string)button.Attribute("Click"));
            Assert.Contains(button.Descendants(Ui + "TextBlock"), e => e.Value == digit.ToString());
        }
        Assert.Equal("bequal_Click", (string)Named(page, "bequal").Attribute("Click"));
        Assert.Equal("bsetting_Click", (string)Named(page, "bsetting").Attribute("Click"));
    }

    [Fact]
    public void AllKeysReceiveLiveThemeUpdatesWithoutLocalStyleOverrides()
    {
        var keys = Page().Descendants(Ui + "Button").Where(b => ((string)b.Attribute("Style"))?.StartsWith("{x:Bind ", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(159, keys.Length);
        Assert.Equal(9, keys.Count(b => ((string)b.Attribute("Style")).Contains("ConstantKeyStyle")));
        foreach (var key in keys)
        {
            Assert.Contains("Mode=OneWay", (string)key.Attribute("Style"));
            foreach (string property in new[] { "Foreground", "Background", "BorderBrush", "Margin", "RequestedTheme" })
                Assert.Null(key.Attribute(property));
        }
    }

    [Theory]
    [InlineData("Dark", "#FF232323", "#FF383838", "#FF505050", "#FF680000")]
    [InlineData("Light", "#FFDCDCDC", "#FFD2D2D2", "#FFBEBEBE", "#FF980000")]
    public void ThemePalettesAndNativeControlThemesStayConsistent(string theme, string background, string operators, string important, string equals)
    {
        var resources = Styles().Descendants(Ui + "SolidColorBrush").ToDictionary(e => (string)e.Attribute(X + "Key"), e => (string)e.Attribute("Color"));
        Assert.Equal(background, resources[$"Calculator{theme}BackgroundBrush"]);
        Assert.Equal(operators, resources[$"Calculator{theme}OperatorBackgroundBrush"]);
        Assert.Equal(important, resources[$"Calculator{theme}ImportantOperatorBackgroundBrush"]);
        Assert.Equal(equals, resources[$"Calculator{theme}EqualBackgroundBrush"]);
        var setters = Setters(Named(Page(), $"Calculator{theme}"));
        Assert.Equal(theme, setters["page.RequestedTheme"]);
        Assert.Equal("White", setters["bequal.Foreground"]);
        Assert.Null(Named(Page(), "slider").Attribute("RequestedTheme"));
        Assert.Null(Named(Page(), "slider").Attribute("Style"));
    }

    [Theory]
    [InlineData("PrimaryOperators", "⏷", "Visible", "Collapsed")]
    [InlineData("ShiftedOperators", "⏶", "Collapsed", "Visible")]
    public void ShiftUpdatesBothOperatorLayouts(string state, string symbol, string primary, string shifted)
    {
        var setters = Setters(Named(Page(), state));
        foreach (string suffix in new[] { "", "_1" })
        {
            Assert.Equal(symbol, setters[$"shiftGlyph{suffix}.Text"]);
            Assert.False(setters.ContainsKey($"bshift{suffix}.Content"));
            var glyph = Named(Page(), $"shiftGlyph{suffix}");
            Assert.Equal(Ui + "Viewbox", glyph.Parent.Name);
            Assert.Equal("DownOnly", (string)glyph.Parent.Attribute("StretchDirection"));
            Assert.Equal(primary, setters[$"leftButtons1{suffix}.Visibility"]);
            Assert.Equal(shifted, setters[$"leftButtons2{suffix}.Visibility"]);
        }
    }

    [Fact]
    public void ViewerAndToolbarStatesPreserveVisibilityAndHeight()
    {
        var page = Page();
        Assert.Equal("Visible", Setters(Named(page, "FormulaResults"))["listView.Visibility"]);
        Assert.Equal("Collapsed", Setters(Named(page, "FormulaResults"))["treeView.Visibility"]);
        Assert.Equal("Collapsed", Setters(Named(page, "DocumentResults"))["listView.Visibility"]);
        Assert.Equal("Visible", Setters(Named(page, "DocumentResults"))["treeView.Visibility"]);
        Assert.Equal("0", Setters(Named(page, "DisplayToolsHidden"))["displayToolsRow.Height"]);
        Assert.Equal("32", Setters(Named(page, "DisplayToolsVisible"))["displayToolsRow.Height"]);
        Assert.Equal("Collapsed", Setters(Named(page, "DisplayToolsHidden"))["toolgrid.Visibility"]);
        Assert.Equal("Visible", Setters(Named(page, "DisplayToolsVisible"))["toolgrid.Visibility"]);
        // The explicit toggle remains reachable even when the entire toolbar is collapsed.
        Assert.NotEqual(Named(page, "toolgrid"), Named(page, "displayToolsToggle").Parent);
        Assert.Equal("DisplayToolsToggle_Click", (string)Named(page, "displayToolsToggle").Attribute("Click"));
        foreach (string region in new[] { "displayGrid", "inputGrid", "gridSplitter" })
        {
            Assert.Equal("DisplayRegion_PointerEntered", (string)Named(page, region).Attribute("PointerEntered"));
            Assert.Equal("DisplayRegion_PointerExited", (string)Named(page, region).Attribute("PointerExited"));
        }
    }

    [Fact]
    public void EveryPageStateTargetsAnExistingElementAndTransitionsRemainImmediate()
    {
        var page = Page();
        Assert.Empty(page.Descendants(Ui + "VisualTransition"));
        foreach (var setter in page.Descendants(Ui + "Setter"))
            Assert.NotNull(Named(page, ((string)setter.Attribute("Target")).Split('.')[0]));
    }
}
