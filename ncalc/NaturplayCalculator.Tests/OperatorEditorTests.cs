using System;
using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Input;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests;

public sealed class OperatorEditorTests
{
    private static OperatorEdit Apply(string source, string operation, int? caret = null, int length = 0, bool followsFraction = false)
    {
        Assert.True(OperatorEditor.TryApply(source, caret ?? source.Length, length, operation, out var edit, followsFraction));
        return edit;
    }

    [Theory]
    [InlineData("2+3", "×", "2+3*", "4", "14")]
    [InlineData("8/2", "+", "8/2+", "3", "7")]
    [InlineData("8/2", "×", "8/2*", "3", "12")]
    [InlineData("2+6", "÷", "2+6÷", "3", "4")]
    [InlineData("2+5", "nCr", "2+5 nCr ", "2", "12")]
    [InlineData("2+5", "nPr", "2+5 nPr ", "2", "22")]
    public void InfixKeysPreserveWrittenPrecedence(string source, string operation, string expected, string right, string answer)
    {
        var edit = Apply(source, operation);
        Assert.Equal(expected, edit.Text);
        Assert.False(edit.FocusSlot);
        var filled = InputEditor.Insert(edit.Text, edit.SelectionStart, 0, right);
        Assert.Equal(answer, new CalculationEngine().Evaluate(filled.Text).Value.Code);
    }

    [Fact]
    public void AnInfixKeyUsesTheActualCursorInsteadOfTheScopeEnd()
    {
        var edit = Apply("12+3", "×", 1);
        Assert.Equal("1*2+3", edit.Text);
        Assert.Equal(2, edit.SelectionStart);
    }

    [Theory]
    [InlineData("√", "2+3 sqrt()")]
    [InlineData("sin", "2+3 sin()")]
    [InlineData("asin", "2+3 arcsin()")]
    [InlineData("log", "2+3 lg()")]
    [InlineData("ln", "2+3 ln()")]
    [InlineData("eˣ", "2+3 e^()")]
    [InlineData("10ˣ", "2+3 10^()")]
    [InlineData("(", "2+3 ()")]
    public void PrefixKeysOpenANewArgumentAtTheCursor(string operation, string expected)
    {
        var edit = Apply("2+3", operation);
        Assert.Equal(expected, edit.Text);
        Assert.True(edit.FocusSlot);
        Assert.Equal(expected.Length - 1, edit.SelectionStart);
        Assert.Equal(edit.SelectionStart, edit.SlotStart);
        Assert.Equal(0, edit.SlotLength);
        Assert.False(ExpressionPreview.Create(edit.Text, true).IsLimited);
    }

    [Theory]
    [InlineData("2+", "sin", "2+sin()")]
    [InlineData("2*", "√", "2*sqrt()")]
    [InlineData("2*-", "√", "2*-sqrt()")]
    [InlineData("", "sin", "sin()")]
    [InlineData("", "(", "()")]
    public void PrefixKeysFillAnEmptyRightSideWithoutWrappingTheLeft(string source, string operation, string expected)
    {
        Assert.Equal(expected, Apply(source, operation).Text);
    }

    [Fact]
    public void PrefixInsertionInTheMiddleKeepsBothNeighbours()
    {
        var edit = Apply("2+3", "sin", 2);
        Assert.Equal("2+sin() 3", edit.Text);
        Assert.Equal(6, edit.SelectionStart);
    }

    [Theory]
    [InlineData("9", "x²", "9^2", "81")]
    [InlineData("2+3", "x²", "2+3^2", "11")]
    [InlineData("(2+3)", "x²", "(2+3)^2", "25")]
    [InlineData("-2", "x²", "-2^2", "-4")]
    [InlineData("(-2)", "x²", "(-2)^2", "4")]
    [InlineData("2^3", "x²", "(2^3)^2", "64")]
    [InlineData("2+3", "n!", "2+3!", "8")]
    [InlineData("3!", "n!", "(3!)!", "720")]
    [InlineData("3!", "n!!", "(3!)!!", "48")]
    [InlineData("2+4", "1/x", "2+4^(-1)", "2.25")]
    [InlineData("2+5", "%", "2+5/100", "2.05")]
    public void PostfixKeysBindOnlyTheAdjacentCompleteValue(string source, string operation, string expected, string answer)
    {
        var edit = Apply(source, operation);
        Assert.Equal(expected, edit.Text);
        Assert.False(edit.FocusSlot);
        Assert.Equal(expected.Length, edit.SelectionStart);
        Assert.Equal(answer, new CalculationEngine().Evaluate(edit.Text).Value.Code);
    }

    [Theory]
    [InlineData("sin9", "(sin9)^2")]
    [InlineData("2+sin(9)", "2+sin(9)^2")]
    [InlineData("9+(2", "9+(2^2")]
    [InlineData("(1,2,,3,4", "(1,2,,3,4^2")]
    public void PostfixBindingHonoursFunctionsAndTheCurrentUnfinishedRow(string source, string expected)
    {
        Assert.Equal(expected, Apply(source, "x²").Text);
    }

    [Theory]
    [InlineData("1/2", "x²", "1/2^2", "(1/2)^2")]
    [InlineData("2+3/4", "x²", "2+3/4^2", "2+(3/4)^2")]
    public void VisualFractionExitHasAnExplicitOperandBoundary(string source, string operation, string linear, string outside)
    {
        Assert.Equal(linear, Apply(source, operation).Text);
        Assert.Equal(outside, Apply(source, operation, followsFraction: true).Text);
    }

    [Theory]
    [InlineData("7/9^2", 2, "7/(9^2)^2")]
    [InlineData("9/100", 0, "(9/100)^2")]
    public void CompletedValueSpanSurvivesRepeatedPostfixKeys(string source, int operandStart, string expected)
    {
        Assert.True(OperatorEditor.TryApply(source, source.Length, 0, "x²", out var edit,
            followsFraction: true, completedOperandStart: operandStart));
        Assert.Equal(expected, edit.Text);
        Assert.True(edit.CompletesOperand);
        Assert.Equal(operandStart, edit.SlotStart);
        Assert.Equal(edit.SelectionStart, edit.SlotStart + edit.SlotLength);
    }

    [Fact]
    public void PowerAndFractionCaptureTheCompletedValueSpan()
    {
        Assert.True(OperatorEditor.TryApply("2+9/100", 7, 0, "xʸ", out var power, completedOperandStart: 2));
        Assert.Equal("2+(9/100)^()", power.Text);
        Assert.True(power.FocusSlot);
        Assert.False(power.CompletesOperand);
        Assert.True(OperatorEditor.TryApply("2+9/100", 7, 0, "fraction", out var fraction, completedOperandStart: 2));
        Assert.Equal("2+9/100/()", fraction.Text);
    }

    [Fact]
    public void PrefixAfterAFractionStartsANewFactorRatherThanChangingTheDenominator()
    {
        var edit = Apply("1/2", "√", followsFraction: true);
        Assert.Equal("1/2*sqrt()", edit.Text);
        Assert.True(edit.FocusSlot);
    }

    [Theory]
    [InlineData("2+3", "2+3^()")]
    [InlineData("(2+3)", "(2+3)^()")]
    [InlineData("-2", "-2^()")]
    [InlineData("2^3", "(2^3)^()")]
    public void PowerCreatesAnExponentForTheAdjacentValue(string source, string expected)
    {
        var edit = Apply(source, "xʸ");
        Assert.Equal(expected, edit.Text);
        Assert.True(edit.FocusSlot);
        Assert.Equal(expected.Length - 1, edit.SelectionStart);
    }

    [Fact]
    public void FractionTemplateIsSeparateFromTheDivisionOperator()
    {
        Assert.Equal("2+3÷", Apply("2+3", "÷").Text);
        Assert.False(Apply("2+3", "÷").FocusSlot);
        var fraction = Apply("2+3", "fraction");
        Assert.Equal("2+3/()", fraction.Text);
        Assert.True(fraction.FocusSlot);
        Assert.Equal(5, fraction.SelectionStart);
        var empty = Apply("", "a/b");
        Assert.Equal("()/()", empty.Text);
        Assert.Equal(1, empty.SelectionStart);
    }

    [Theory]
    [InlineData("2*", "+", "2+")]
    [InlineData("2*-", "+", "2+")]
    [InlineData("2+", "÷", "2÷")]
    [InlineData("2/", "×", "2*")]
    [InlineData("2 nCr ", "×", "2 *")]
    public void AnUnfilledInfixOperatorCanBeCorrected(string source, string operation, string expected)
    {
        var edit = Apply(source, operation);
        Assert.Equal(expected, edit.Text);
        Assert.False(edit.FocusSlot);
    }

    [Fact]
    public void SubtractionCorrectsAPendingOperatorWhileTheNegativeKeyStartsItsRightOperand()
    {
        Assert.Equal("2-", Apply("2*", "−").Text);
        var negative = Apply("2*", "(-)");
        Assert.Equal("2*-", negative.Text);
        Assert.Equal("-6", new CalculationEngine().Evaluate(negative.Text + "3").Value.Code);
        Assert.Equal("2-3-", Apply("2-3", "−").Text);
        Assert.Equal("-5", new CalculationEngine().Evaluate(Apply("2-3", "−").Text + "4").Value.Code);
    }

    [Theory]
    [InlineData("9", "9 (-)")]
    [InlineData("-9", "-9 (-)")]
    [InlineData("2+3", "2+3 (-)")]
    [InlineData("2*-3", "2*-3 (-)")]
    [InlineData("", "-")]
    [InlineData("2*", "2*-")]
    [InlineData("2*-", "2*--")]
    public void NegativeKeyInsertsAtTheCursorWithoutChangingThePreviousOperand(string source, string expected)
    {
        Assert.Equal(expected, Apply(source, "(-)").Text);
        Assert.Equal(expected, Apply(source, "±").Text);
    }

    [Theory]
    [InlineData("×")][InlineData("÷")][InlineData("xʸ")][InlineData("x²")]
    [InlineData("1/x")][InlineData("n!")][InlineData("%")][InlineData("nCr")]
    public void MissingLeftOperandsAreNotInvented(string operation)
    {
        var empty = Apply("", operation);
        Assert.Equal("", empty.Text);
        Assert.False(empty.FocusSlot);
        Assert.Equal("2+", Apply("2+", operation == "×" || operation == "÷" || operation == "nCr" ? "x²" : operation).Text);
    }

    [Fact]
    public void ExplicitSelectionStillAppliesAnOperationToTheWholeChosenExpression()
    {
        var root = Apply("1+2*3", "√", 2, 3);
        Assert.Equal("1+sqrt(2*3)", root.Text);
        Assert.Equal("sqrt(2*3)", root.Text.Substring(root.SelectionStart, root.SelectionLength));
        Assert.False(root.FocusSlot);
        var square = Apply(root.Text, "x²", root.SelectionStart, root.SelectionLength);
        Assert.Equal("1+sqrt(2*3)^2", square.Text);
    }

    [Fact]
    public void ASelectedTransformPreservesItsSurroundingPrecedence()
    {
        var percent = Apply("5/2", "%", 2, 1);
        Assert.Equal("5/(2/100)", percent.Text);
        Assert.Equal("250", new CalculationEngine().Evaluate(percent.Text).Value.Code);
        var negative = Apply("2^2", "±", 0, 1);
        Assert.Equal("(-2)^2", negative.Text);
        Assert.Equal("4", new CalculationEngine().Evaluate(negative.Text).Value.Code);
        var add = Apply("2^3+1", "+", 2, 1);
        Assert.Equal("2^(3+)+1", add.Text);
        Assert.True(add.FocusSlot);
        var filled = InputEditor.Insert(add.Text, add.SelectionStart, 0, "2");
        Assert.Equal("33", new CalculationEngine().Evaluate(filled.Text).Value.Code);
    }

    [Fact]
    public void WrittenCommentsAndGroupingArePreserved()
    {
        const string source = "[note\\]text]9";
        var square = Apply(source, "x²");
        Assert.Equal(source + "^2", square.Text);
        Assert.Equal(new ExpressionParser().Parse(source).Comments, new ExpressionParser().Parse(square.Text).Comments);
        Assert.Equal("sqrt(2+3)", Apply("(2+3)", "√", 0, 5).Text);
        Assert.Equal("sqrt((2+3))", Apply("((2+3))", "√", 0, 7).Text);
    }

    [Fact]
    public void MatrixInsertionDoesNotConsumeExistingInput()
    {
        var empty = Apply("", "matrix");
        Assert.Equal("(0,0,,0,0)", empty.Text);
        Assert.Equal("0", empty.Text.Substring(empty.SelectionStart, empty.SelectionLength));
        var appended = Apply("2+3", "matrix");
        Assert.Equal("2+3 (0,0,,0,0)", appended.Text);
        Assert.Equal("0", appended.Text.Substring(appended.SelectionStart, appended.SelectionLength));
        var selected = Apply("2+3", "matrix", 0, 3);
        Assert.Equal("(2+3,0,,0,0)", selected.Text);
        Assert.Equal("0", selected.Text.Substring(selected.SelectionStart, selected.SelectionLength));
    }

    [Theory]
    [InlineData("Abs", "Abs(1,2,,3,4)")]
    [InlineData("rank", "rank(1,2,,3,4)")]
    [InlineData("^T", "(1,2,,3,4)^T")]
    [InlineData("Cov(", "Cov(1,2,,3,4)")]
    public void ExplicitMatrixSelectionCanStillBeTransformed(string operation, string expected)
    {
        const string matrix = "(1,2,,3,4)";
        Assert.Equal(expected, Apply(matrix, operation, 0, matrix.Length).Text);
    }

    private static FormulaEditSession FocusInsertedSlot(FormulaEditSession scope, OperatorEdit edit)
    {
        var applied = scope.ReplaceBuffer(edit.Text, edit.SelectionStart);
        int selection = scope.ContentStart + edit.SelectionStart;
        int slotStart = scope.ContentStart + edit.SlotStart;
        var formula = ExpressionPreview.Create(applied.Text, true).Formula;
        var map = new FormulaCaretMap(applied.Text, formula, new TexLayoutEngine().Render(formula.Latex));
        var target = map.AfterEdit(selection, slotStart, slotStart + edit.SlotLength);
        Assert.NotNull(target.Region);
        return new FormulaEditSession(applied.Text, target.Region, target.TextIndex, target.FollowsFraction);
    }

    [Fact]
    public void ExponentInputStaysInTheExponentUntilExplicitNavigation()
    {
        var root = new FormulaEditSession("9", (TexEditRegion)null!, 1);
        var exponent = FocusInsertedSlot(root, Apply(root.Buffer, "xʸ"));
        Assert.Equal("", exponent.Buffer);
        exponent.Insert(exponent.Caret, 0, "6");
        var plus = Apply(exponent.Buffer, "+");
        exponent.ReplaceBuffer(plus.Text, plus.SelectionStart);
        Assert.Equal("9^(6+9)", exponent.Insert(exponent.Caret, 0, "9").Text);
    }

    [Fact]
    public void EditingADenominatorChangesOnlyThatLocalRow()
    {
        const string source = "7/9+2";
        var formula = ExpressionPreview.Create(source, true).Formula;
        var region = formula.EditRegions.First(r => r.InputStart == 2 && r.InputLength == 1);
        var denominator = new FormulaEditSession(source, region, 3);
        var divide = Apply(denominator.Buffer, "÷");
        Assert.False(divide.FocusSlot);
        denominator.ReplaceBuffer(divide.Text, divide.SelectionStart);
        Assert.Equal("7/(9÷3)+2", denominator.Insert(denominator.Caret, 0, "3").Text);
        var plus = Apply(denominator.Buffer, "+");
        denominator.ReplaceBuffer(plus.Text, plus.SelectionStart);
        Assert.Equal("7/(9÷3+1)+2", denominator.Insert(denominator.Caret, 0, "1").Text);
    }

    [Theory]
    [InlineData("+", true)][InlineData("−", true)][InlineData("÷", true)]
    [InlineData("x²", true)][InlineData("xʸ", true)][InlineData("n!", true)]
    [InlineData("fraction", true)][InlineData("sin", false)][InlineData("√", false)]
    [InlineData("(", false)][InlineData("±", false)][InlineData("matrix", false)]
    [InlineData("eˣ", false)][InlineData("10ˣ", false)]
    public void AnswerContinuationDependsOnKeyGrammar(string key, bool expected)
    {
        Assert.Equal(expected, OperatorEditor.ContinuesAnswer(key));
    }

    [Theory]
    [InlineData("0")][InlineData("9")][InlineData(".")][InlineData(",")]
    [InlineData("Ans")][InlineData("π")][InlineData(")")][InlineData("AC")]
    public void OrdinaryInputAndScopeExitRemainWithTheHostEditor(string key)
    {
        Assert.False(OperatorEditor.TryApply("1+2", 1, 0, key, out var edit));
        Assert.Null(edit);
    }

    [Fact]
    public void RangeBoundsAreClampedAndMissingArgumentsAreRejected()
    {
        Assert.Equal("sqrt(1+2)", Apply("1+2", "√", -1, int.MaxValue).Text);
        Assert.Equal("1+2 sqrt()", Apply("1+2", "√", int.MaxValue, -1).Text);
        Assert.Throws<ArgumentNullException>(() => OperatorEditor.TryApply(null!, 0, 0, "+", out _));
        Assert.Throws<ArgumentNullException>(() => OperatorEditor.TryApply("", 0, 0, null!, out _));
    }
}
