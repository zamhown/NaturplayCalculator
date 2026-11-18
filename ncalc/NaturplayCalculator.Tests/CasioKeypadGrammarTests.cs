using System;
using NaturplayCalculator.Input;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests;

public sealed class CasioKeypadGrammarTests
{
    private static OperatorEdit Operator(string source, string key, int? caret = null, bool outsideFraction = false)
    {
        Assert.True(OperatorEditor.TryApply(source, caret ?? source.Length, 0, key, out var edit, outsideFraction));
        return edit;
    }

    [Theory]
    [InlineData("÷")]
    [InlineData("/")]
    public void OrdinaryDivisionRemainsDifferentFromTheFractionTemplate(string key)
    {
        var division = Operator("6", key);
        Assert.Equal("6÷", division.Text);
        Assert.False(division.FocusSlot);
        var fraction = Operator("6", "fraction");
        Assert.Equal("6/()", fraction.Text);
        Assert.True(fraction.FocusSlot);
        Assert.Equal("6/2", InputEditor.Insert("6/", 2, 0, "2").Text);
    }

    [Theory]
    [InlineData("π", 3 / Math.PI)]
    [InlineData("e", 3 / Math.E)]
    public void AConstantIsPartOfTheJuxtaposedRightOperandOfDivision(string constant, double expected)
    {
        var division = Operator("6", "÷");
        var number = InputEditor.InsertKey(division.Text, division.SelectionStart, 0, "2");
        var implicitProduct = InputEditor.InsertKey(number.Text, number.Caret, 0, constant);
        Assert.Equal(expected, new CalculationEngine().Evaluate(implicitProduct.Text).Value.Cmx.Re, 12);

        var multiply = Operator(number.Text, "×");
        var explicitProduct = InputEditor.InsertKey(multiply.Text, multiply.SelectionStart, 0, constant);
        double value = constant == "π" ? Math.PI : Math.E;
        Assert.Equal(3 * value, new CalculationEngine().Evaluate(explicitProduct.Text).Value.Cmx.Re, 12);
    }

    [Fact]
    public void AddingADigitAfterTheEulerKeyDoesNotCreateAScientificExponent()
    {
        var edit = InputEditor.InsertKey("6÷2", 3, 0, "e");
        edit = InputEditor.InsertKey(edit.Text, edit.Caret, 0, "3");
        Assert.Equal("6÷2 e 3", edit.Text);
        Assert.Equal(1 / Math.E, new CalculationEngine().Evaluate(edit.Text).Value.Cmx.Re, 12);
        Assert.Equal(2000, new CalculationEngine().Evaluate("2e3").Value.Cmx.Re);
    }

    [Fact]
    public void ANewNegativeOperandUsesTheCursorInsteadOfFlippingThePreviousNumber()
    {
        var beforeRight = Operator("10+3", "(-)", 3);
        Assert.Equal("10+-3", beforeRight.Text);
        Assert.Equal(4, beforeRight.SelectionStart);

        var afterLeft = Operator("2", "(-)");
        Assert.True(afterLeft.FocusSlot);
        var filled = InputEditor.InsertKey(afterLeft.Text, afterLeft.SelectionStart, 0, "3");
        Assert.Equal("2 (-3)", filled.Text);
        Assert.Equal(-6, new CalculationEngine().Evaluate(filled.Text).Value.Cmx.Re);
        Assert.False(OperatorEditor.ContinuesAnswer("(-)"));
    }

    [Fact]
    public void PrefixOutsideACompletedFractionDoesNotReenterItsDenominator()
    {
        var root = Operator("6/2", "√", outsideFraction: true);
        var filled = InputEditor.InsertKey(root.Text, root.SelectionStart, 0, "9");
        Assert.Equal("6/2*sqrt(9)", filled.Text);
        Assert.Equal(9, new CalculationEngine().Evaluate(filled.Text).Value.Cmx.Re);

        var negative = Operator("6/2", "(-)", outsideFraction: true);
        filled = InputEditor.InsertKey(negative.Text, negative.SelectionStart, 0, "3");
        Assert.Equal("6/2*(-3)", filled.Text);
        Assert.Equal(-9, new CalculationEngine().Evaluate(filled.Text).Value.Cmx.Re);
    }

    [Theory]
    [InlineData("x²", "6÷(2/3)^2", 13.5)]
    [InlineData("1/x", "6÷(2/3)^(-1)", 4)]
    public void PostfixAfterAFractionOnTheRightOfDivisionOnlyUsesThatFraction(string key, string expected, double answer)
    {
        var edit = Operator("6÷(2/3)", key, outsideFraction: true);
        Assert.Equal(expected, edit.Text);
        Assert.Equal(answer, new CalculationEngine().Evaluate(edit.Text).Value.Cmx.Re, 12);
    }

    [Theory]
    [InlineData("xʸ", "6÷(2/3)^(2)", 13.5)]
    [InlineData("fraction", "6÷((2/3)/(2))", 18)]
    public void TemplateAfterAFractionOnTheRightOfDivisionKeepsTheAdjacentOperand(string key, string expected, double answer)
    {
        var edit = Operator("6÷(2/3)", key, outsideFraction: true);
        var filled = InputEditor.InsertKey(edit.Text, edit.SelectionStart, 0, "2");
        Assert.Equal(expected, filled.Text);
        Assert.Equal(answer, new CalculationEngine().Evaluate(filled.Text).Value.Cmx.Re, 12);
    }
}
