using System;
using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Presentation.Animation;
using Xunit;

namespace NaturplayCalculator.Tests;

public class FormulaTransitionTests
{
    private static FormulaFrame Render(string latex) => FormulaFrame.FromLayout(new TexLayoutEngine().Render(latex, 32));
    private static FormulaDrawing Glyph(FormulaFrame frame, string text) => frame.Drawing.Single(d => d.Text == text);

    [Fact]
    public void InsertingDigitMovesExistingSuffixInsteadOfCrossfadingWholeExpression()
    {
        var before = Render("12+3"); var after = Render("124+3");
        var motion = new FormulaTransition(before, after);
        var first = motion.At(0); var middle = motion.At(.5);
        Assert.Equal(Glyph(before, "+").OriginX, Glyph(first, "+").OriginX);
        Assert.Equal(1, Glyph(first, "+").Opacity);
        Assert.Equal(0, Glyph(first, "4").Opacity);
        Assert.InRange(Glyph(middle, "+").OriginX, Glyph(before, "+").OriginX, Glyph(after, "+").OriginX);
        Assert.InRange(Glyph(middle, "4").Opacity, .01, .99);
        Assert.Same(after, motion.At(1));
    }

    [Fact]
    public void DeletingDigitFadesOnlyRemovedGlyphAndMovesSurvivingTerms()
    {
        var before = Render("124+3"); var after = Render("12+3");
        var motion = new FormulaTransition(before, after);
        Assert.Equal(1, Glyph(motion.At(0), "4").Opacity);
        Assert.InRange(Glyph(motion.At(.5), "4").Opacity, .01, .99);
        Assert.DoesNotContain(motion.At(1).Drawing, d => d.Text == "4");
        Assert.Equal(1, Glyph(motion.At(.5), "+").Opacity);
    }

    [Fact]
    public void PowerTemplateInterpolatesGlyphScaleAndPosition()
    {
        var before = Render("2+3"); var after = Render("2^{+3}");
        var middle = new FormulaTransition(before, after).At(.5);
        var original = Glyph(before, "3"); var moved = Glyph(after, "3"); var current = Glyph(middle, "3");
        Assert.True(current.FontSize < original.FontSize && current.FontSize > moved.FontSize);
        Assert.InRange(current.Baseline, Math.Min(original.Baseline, moved.Baseline), Math.Max(original.Baseline, moved.Baseline));
        Assert.Equal(1, current.Opacity);
    }

    [Fact]
    public void FractionRuleGrowsWithItsNumerator()
    {
        var before = Render(@"\frac{1}{2}"); var after = Render(@"\frac{123}{2}");
        var first = before.Drawing.Single(d => d.Kind == TexDrawKind.Rule);
        var last = after.Drawing.Single(d => d.Kind == TexDrawKind.Rule);
        var middle = new FormulaTransition(before, after).At(.5).Drawing.Single(d => d.Kind == TexDrawKind.Rule);
        Assert.True(middle.Bounds.Width > first.Bounds.Width && middle.Bounds.Width < last.Bounds.Width);
        Assert.Equal(1, middle.Opacity);
    }

    [Fact]
    public void RadicalStrokePointsInterpolateWithoutReplacingTheStroke()
    {
        var before = Render(@"\sqrt{2}"); var after = Render(@"\sqrt{234}");
        var first = before.Drawing.First(d => d.Kind == TexDrawKind.Polyline);
        var last = after.Drawing.First(d => d.Kind == TexDrawKind.Polyline);
        var middle = new FormulaTransition(before, after).At(.5).Drawing.First(d => d.Kind == TexDrawKind.Polyline);
        Assert.Equal(first.Points.Count, middle.Points.Count);
        Assert.Equal(1, middle.Opacity);
        for (int i = 0; i < middle.Points.Count; i++)
            Assert.InRange(middle.Points[i].X, Math.Min(first.Points[i].X, last.Points[i].X), Math.Max(first.Points[i].X, last.Points[i].X));
    }

    [Fact]
    public void RapidTypingRetargetsFromVisibleIntermediateCoordinatesAndOpacity()
    {
        var ongoing = new FormulaTransition(Render("12+3"), Render("124+3")).At(.35);
        var redirected = new FormulaTransition(ongoing, Render("1245+3")).At(0);
        Assert.Equal(Glyph(ongoing, "+").OriginX, Glyph(redirected, "+").OriginX);
        Assert.Equal(Glyph(ongoing, "4").Opacity, Glyph(redirected, "4").Opacity);
        Assert.Equal(0, Glyph(redirected, "5").Opacity);
    }

    [Fact]
    public void RepeatedDigitsKeepTheirOrderAcrossInsertion()
    {
        var before = Render("11+1"); var after = Render("111+1");
        var first = new FormulaTransition(before, after).At(0);
        var existing = first.Drawing.Where(d => d.Text == "1" && d.Opacity == 1).ToArray();
        Assert.Equal(before.Drawing.Where(d => d.Text == "1").Select(d => d.OriginX), existing.Select(d => d.OriginX));
        Assert.Single(first.Drawing, d => d.Text == "1" && d.Opacity == 0);
    }

    [Fact]
    public void EmptyFormulaFadesInWithoutSlidingOrScaling()
    {
        var target = Render("2");
        var start = new FormulaTransition(new FormulaFrame(0, 0, Array.Empty<FormulaDrawing>()), target).At(0);
        Assert.Equal(0, Glyph(start, "2").Opacity);
        Assert.Equal(Glyph(target, "2").OriginX, Glyph(start, "2").OriginX);
        Assert.Equal(Glyph(target, "2").FontSize, Glyph(start, "2").FontSize);
    }

    [Fact]
    public void LargePastesSkipMatchingAndReturnFinalFrameImmediately()
    {
        var before = Render(new string('1', 300)); var after = Render(new string('2', 300));
        var motion = new FormulaTransition(before, after);
        Assert.False(motion.CanAnimate);
        Assert.Same(after, motion.At(0));
    }

    [Fact]
    public void VerticallyCenteredContainerKeepsInitialGlyphAtItsPreviousScreenPosition()
    {
        var before = Render("1+2"); var after = Render(@"\frac{1+2}{3}");
        var first = new FormulaTransition(before, after, preserveVerticalCenter: true).At(0);
        // A centered parent moves upward by half the growth in its child's height.
        // The first drawing frame compensates for exactly that origin change.
        Assert.Equal(Glyph(before, "+").Baseline - before.Height / 2,
            Glyph(first, "+").Baseline - after.Height / 2, 10);
        Assert.Same(after, new FormulaTransition(before, after, true).At(1));
    }

    [Fact]
    public void ProgressClampsAndDoesNotOvershoot()
    {
        var before = Render("1+2"); var after = Render("134+2"); var motion = new FormulaTransition(before, after);
        Assert.Equal(Glyph(before, "+").OriginX, Glyph(motion.At(-1), "+").OriginX);
        double previous = Glyph(before, "+").OriginX;
        for (double p = 0; p < 1; p += .05)
        {
            double x = Glyph(motion.At(p), "+").OriginX;
            Assert.InRange(x, previous, Glyph(after, "+").OriginX);
            previous = x;
        }
        Assert.Same(after, motion.At(2));
    }
}
