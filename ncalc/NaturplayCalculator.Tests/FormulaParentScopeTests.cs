using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Input;
using Xunit;

namespace NaturplayCalculator.Tests;

public sealed class FormulaParentScopeTests
{
    private static FormulaCaretMap Map(string source)
    {
        var formula = ExpressionPreview.Create(source, true).Formula;
        return new FormulaCaretMap(source, formula, new TexLayoutEngine().Render(formula.Latex));
    }

    private static FormulaEditSession Scope(string source, string buffer)
    {
        var formula = ExpressionPreview.Create(source, true).Formula;
        var region = formula.EditRegions.First(region =>
        {
            FormulaEditSession.GetContentRange(source, region, out int start, out int end);
            return source.Substring(start, end - start) == buffer;
        });
        return new FormulaEditSession(source, region, region.InputStart + region.InputLength);
    }

    private static FormulaEditSession At(string source, FormulaCaretSlot slot) =>
        new(source, slot.Region, slot.TextIndex, slot.FollowsFraction);

    [Theory]
    [InlineData("1/2+3", "1", 3)]
    [InlineData("1/2+3", "2", 3)]
    [InlineData("2^3+4", "2", 3)]
    [InlineData("2^3+4", "3", 3)]
    [InlineData("sqrt(9)+4", "9", 7)]
    [InlineData("sin(9)+4", "9", 6)]
    [InlineData("(1,2,,3,4)+5", "3", 10)]
    [InlineData("3!+4", "3", 2)]
    [InlineData("(2+3)*5+6", "2+3", 5)]
    [InlineData("2+(3+4)+5", "3+4", 7)]
    public void LeavingAStructuralChildMovesAfterTheWholeStructureInItsParent(string source, string buffer, int expectedCaret)
    {
        var map = Map(source);
        var session = Scope(source, buffer);
        var parent = map.ParentScope(session, map.AfterEdit(session.Caret, session.ContentStart, session.ContentEnd));
        Assert.NotNull(parent);
        Assert.Contains(parent, map.Slots);
        Assert.Null(parent.Region);
        Assert.Equal(expectedCaret, parent.TextIndex);
        var resumed = At(source, parent);
        Assert.Equal(source, resumed.Source);
        Assert.Equal(source, resumed.Buffer);
        Assert.False(resumed.IsScoped);
        Assert.Equal(expectedCaret, resumed.Caret);
        Assert.Null(map.ParentScope(resumed, parent));
    }

    [Theory]
    [InlineData("1/2+3", "1", 0)]
    [InlineData("1/2+3", "2", 0)]
    [InlineData("2^3+4", "2", 0)]
    [InlineData("2^3+4", "3", 0)]
    [InlineData("4+sqrt(9)+5", "9", 2)]
    [InlineData("4+sin(9)+5", "9", 2)]
    [InlineData("4+(1,2,,3,4)+5", "3", 2)]
    [InlineData("4+3!+5", "3", 2)]
    [InlineData("4+(2+3)*5", "2+3", 2)]
    [InlineData("2+(3+4)+5", "3+4", 2)]
    public void LeavingBackwardMovesBeforeTheWholeStructureInItsParent(string source, string buffer, int expectedCaret)
    {
        var map = Map(source);
        var session = Scope(source, buffer);
        var parent = map.ParentScope(session, map.AfterEdit(session.Caret, session.ContentStart, session.ContentEnd), after: false);
        Assert.NotNull(parent);
        Assert.Null(parent.Region);
        Assert.Equal(expectedCaret, parent.TextIndex);
        Assert.Equal(source, At(source, parent).Source);
    }

    [Fact]
    public void BackwardScopeNavigationLeavesNestedStructuresOneLevelAtATime()
    {
        const string source = "10+sqrt(1/(2+3))+4";
        var map = Map(source);
        var denominator = Scope(source, "2+3");
        var first = map.ParentScope(denominator, after: false);
        Assert.NotNull(first);
        var radicand = At(source, first);
        Assert.Equal("1/(2+3)", radicand.Buffer);
        Assert.Equal(radicand.ContentStart, radicand.Caret);
        var second = map.ParentScope(radicand, first, after: false);
        Assert.NotNull(second);
        Assert.Null(second.Region);
        Assert.Equal(3, second.TextIndex);
        Assert.Equal(source, At(source, second).Source);
    }

    [Fact]
    public void NestedScopesAreLeftOneAtATimeAndKeepTheNextTermOutside()
    {
        const string source = "10+sqrt(1/(2+3))+4";
        var map = Map(source);
        var denominator = Scope(source, "2+3");
        var first = map.ParentScope(denominator);
        Assert.NotNull(first);
        var radicand = At(source, first);
        Assert.Equal("1/(2+3)", radicand.Buffer);
        Assert.Equal(radicand.ContentEnd, radicand.Caret);
        Assert.True(radicand.IsScoped);
        var second = map.ParentScope(radicand, first);
        Assert.NotNull(second);
        Assert.Null(second.Region);
        Assert.Equal(source.LastIndexOf("+4"), second.TextIndex);
        var root = At(source, second);
        Assert.Equal(source, root.Buffer);
        Assert.Null(map.ParentScope(root, second));
    }

    [Fact]
    public void ParentOfFractionDenominatorPreservesTheOuterFractionAffinity()
    {
        const string source = "1/2+3";
        var map = Map(source);
        var parent = map.ParentScope(Scope(source, "2"));
        Assert.NotNull(parent);
        Assert.True(parent.FollowsFraction);
        var root = At(source, parent);
        Assert.Equal("1/2*4+3", root.Insert(root.Caret, 0, "4").Text);
    }

    [Fact]
    public void RebindingTheRootAtAFractionBoundaryKeepsItsOuterAffinity()
    {
        const string source = "1/2+3";
        var original = Map(source);
        var parent = original.ParentScope(Scope(source, "2"));
        Assert.Equal(3, parent.TextIndex);
        var restored = Map(source).FromText(parent.TextIndex, preferRootScope: true);
        Assert.Null(restored.Region);
        Assert.Equal(3, restored.TextIndex);
        Assert.True(restored.FollowsFraction);
        Assert.Equal(parent.Geometry.Row, restored.Geometry.Row);
        var root = At(source, restored);
        Assert.Equal(source, root.Buffer);
        Assert.Equal("1/2*4+3", root.Insert(root.Caret, 0, "4").Text);
    }

    [Fact]
    public void RebindingAfterLeavingANestedRootKeepsTheParentPosition()
    {
        const string source = "10+sqrt(1/(2+3))+4";
        var original = Map(source);
        var radicand = original.ParentScope(Scope(source, "2+3"));
        var parent = original.ParentScope(At(source, radicand), radicand);
        var restored = Map(source).FromText(parent.TextIndex, preferRootScope: true);
        Assert.Null(restored.Region);
        Assert.Equal(source.LastIndexOf("+4"), restored.TextIndex);
        Assert.Equal(parent.Geometry.Row, restored.Geometry.Row);
        var root = At(source, restored);
        Assert.Equal("10+sqrt(1/(2+3))+5+4", root.Insert(root.Caret, 0, "+5").Text);
    }

    [Fact]
    public void RootPreferencePreservesBoundaryAndNonRootFallbackBehavior()
    {
        var map = Map("sqrt(12)+3");
        Assert.Same(map.FromText(0), map.FromText(0, preferRootScope: true));
        Assert.Same(map.FromText(10), map.FromText(10, preferRootScope: true));
        Assert.Same(map.FromText(6), map.FromText(6, preferRootScope: true));
    }

    [Fact]
    public void NativeBufferBoundsTakePriorityOverANarrowerVisualCaret()
    {
        const string source = "(1,2,,sqrt((3+4)^2),5)";
        var map = Map(source);
        var cell = Scope(source, "sqrt((3+4)^2)");
        var exponent = map.Slots.First(slot => slot.Region != null && At(source, slot).Buffer == "2");
        var parent = map.ParentScope(cell, exponent);
        Assert.NotNull(parent);
        Assert.Null(parent.Region);
        Assert.Equal(source.Length, parent.TextIndex);
    }

    [Fact]
    public void LeavingAnEmptyExponentKeepsItsPlaceholderAndMovesAfterThePower()
    {
        const string source = "2^()+3";
        var map = Map(source);
        var empty = Scope(source, "");
        var parent = map.ParentScope(empty);
        Assert.NotNull(parent);
        Assert.Null(parent.Region);
        Assert.Equal(4, parent.TextIndex);
        Assert.Equal(source, At(source, parent).Source);
    }

    [Fact]
    public void NavigationAfterAnEditUsesTheNewRegionBounds()
    {
        const string initial = "1/2+3";
        var denominator = Scope(initial, "2");
        var edit = denominator.Insert(denominator.Caret, 0, "+4");
        Assert.Equal("1/(2+4)+3", edit.Text);
        var map = Map(edit.Text);
        var parent = map.ParentScope(denominator, map.AfterEdit(edit.Caret, denominator.ContentStart, denominator.ContentEnd));
        Assert.NotNull(parent);
        Assert.Null(parent.Region);
        Assert.Equal(edit.Text.IndexOf("+3"), parent.TextIndex);
        Assert.Equal(edit.Text, At(edit.Text, parent).Source);
    }

    [Theory]
    [InlineData("9+(2")]
    [InlineData("9+sqrt(")]
    [InlineData("9+[note")]
    public void GeneratedClosersOutsideTheNativeBufferDoNotBlockLeavingTheScope(string buffer)
    {
        const string initial = "7/9+2";
        var denominator = Scope(initial, "9");
        var edit = denominator.ReplaceBuffer(buffer, buffer.Length);
        Assert.Equal(buffer, denominator.Buffer);
        var map = Map(edit.Text);
        var parent = map.ParentScope(denominator, map.AfterEdit(edit.Caret, denominator.ContentStart, denominator.ContentEnd));
        Assert.NotNull(parent);
        Assert.Null(parent.Region);
        Assert.Equal(edit.Text.LastIndexOf("+2"), parent.TextIndex);
        Assert.Equal(edit.Text, At(edit.Text, parent).Source);
    }
}
