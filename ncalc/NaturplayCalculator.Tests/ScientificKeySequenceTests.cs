using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.History;
using NaturplayCalculator.Input;
using NaturplayCalculator.Model;
using NaturplayCalculator.Presentation;
using Xunit;

namespace NaturplayCalculator.Tests;

public sealed class ScientificKeySequenceTests
{
    // Exercise the same edit -> serialize -> render -> scope-map cycle as the
    // host. The driver only routes keys and follows the returned selection;
    // operand selection and arithmetic remain production-code responsibilities.
    private sealed class Keypad : IDisposable
    {
        public CalculatorWorkspace Workspace { get; }
        public string Source => Workspace.Session.Draft;
        public string Buffer => Session?.Buffer ?? Source;
        public bool IsScoped => Session?.IsScoped == true;
        public int Position { get; private set; }
        public int SelectionLength { get; private set; }
        public FormulaEditSession Session { get; private set; }
        private FormulaCaretMap map;
        private FormulaCaretSlot slot;
        private readonly List<string> trace = new();
        public string Trace => string.Join(Environment.NewLine, trace);

        public Keypad(CalculatorWorkspace workspace = null)
        {
            Workspace = workspace ?? new CalculatorWorkspace();
            Position = Source.Length;
            Render();
        }

        public void Keys(string sequence)
        {
            foreach (string key in sequence.Split(' ', StringSplitOptions.RemoveEmptyEntries)) Press(key);
        }

        public void Press(string key)
        {
            if (key == "↑")
            {
                var parent = map?.ParentScope(Session, slot);
                Assert.True(parent != null, "No parent for ↑\n" + Trace);
                Select(parent);
                Remember(key);
                return;
            }

            bool afterResult = Session == null && Workspace.Session.DraftIsSolved && SelectionLength == 0;
            bool continues = afterResult && OperatorEditor.ContinuesAnswer(key);
            string buffer = afterResult ? continues ? "Ans" : "" : Buffer;
            int local = afterResult ? buffer.Length : Position - (Session?.ContentStart ?? 0);
            bool followsFraction = Session?.FollowsFractionAt(Position) == true;
            int? completedStart = Session?.CompletedOperandStartAt(Position);
            if (OperatorEditor.TryApply(buffer, local, SelectionLength, key, out var operation, followsFraction, completedStart))
            {
                int selection, target;
                if (Session != null)
                {
                    var edit = Session.ReplaceBuffer(operation.Text, operation.SelectionStart);
                    selection = Session.ContentStart + operation.SelectionStart;
                    target = Session.ContentStart + operation.SlotStart;
                    Apply(edit);
                }
                else
                {
                    selection = operation.SelectionStart;
                    target = operation.SlotStart;
                    Apply(InputEditor.Insert(operation.Text, selection, 0, ""),
                        continues ? WorkspaceEditContext.DisplayedResult
                        : afterResult ? WorkspaceEditContext.NewCalculation : WorkspaceEditContext.Preserve);
                }
                if (operation.FocusSlot)
                {
                    Assert.NotNull(map);
                    Select(map.AfterEdit(selection, target, target + operation.SlotLength));
                }
                else if (operation.CompletesOperand && operation.SelectionLength == 0)
                {
                    Session ??= new FormulaEditSession(Source, (TexEditRegion)null, selection);
                    Session.MarkCompletedOperand(target, selection);
                }
                Position = selection;
                SelectionLength = operation.SelectionLength;
                Session?.MoveCaret(Position);
                Remember(key);
                return;
            }

            if (Session != null)
            {
                var parent = key == ")" ? map?.ParentScope(Session, slot) : null;
                Apply(Session.InsertKey(Position, SelectionLength, key), parent: parent);
            }
            else
                Apply(InputEditor.InsertKey(afterResult ? "" : Source, afterResult ? 0 : Position, SelectionLength, key),
                    afterResult ? WorkspaceEditContext.NewCalculation : WorkspaceEditContext.Preserve);
            Remember(key);
        }

        public void SelectText(int start, int length)
        {
            Session = null;
            Position = start;
            SelectionLength = length;
        }

        public async Task<double> Evaluate()
        {
            Session = null;
            Assert.True(await Workspace.EvaluateAsync(), Workspace.Error + "\n" + Trace);
            Position = Source.Length;
            SelectionLength = 0;
            Remember("=");
            return Workspace.Result.Cmx.Re;
        }

        private void Apply(InputEdit edit, WorkspaceEditContext context = WorkspaceEditContext.Preserve,
            FormulaCaretSlot parent = null)
        {
            Workspace.Edit(edit.Text, context);
            Position = edit.Caret;
            SelectionLength = 0;
            if (edit.ExitsFormulaScope) Session = null;
            Render();
            if (edit.ExitsFormulaScope && parent != null) Select(map.FromGeometry(parent.Geometry));
        }

        private void Render()
        {
            if (Source.Length == 0) { map = null; slot = null; return; }
            var formula = ExpressionPreview.Create(Source, true).Formula;
            map = new FormulaCaretMap(Source, formula, new TexLayoutEngine().Render(formula.Latex));
            slot = Session == null ? map.FromText(Position)
                : !Session.IsScoped ? map.FromText(Position, preferRootScope: true)
                : map.AfterEdit(Position, Session.ContentStart, Session.ContentEnd);
        }

        private void Select(FormulaCaretSlot next)
        {
            slot = next;
            Position = next.TextIndex;
            SelectionLength = 0;
            Session = new FormulaEditSession(Source, next.Region, next.TextIndex, next.FollowsFraction, next.FollowsStructure);
        }

        private void Remember(string key) => trace.Add(key + " => " + Source + " | " + Buffer + " @ " + Position);
        public void Dispose() => Workspace.Dispose();
    }

    [Theory]
    [InlineData("2 + 3 × 4", 14)]
    [InlineData("8 ÷ 2 + 2", 6)]
    [InlineData("2 + 3 x²", 11)]
    [InlineData("( 2 + 3 ) x²", 25)]
    [InlineData("- 2 x²", -4)]
    [InlineData("( - 2 ) x²", 4)]
    [InlineData("± 2 x²", -4)]
    [InlineData("(-) 2 x²", -4)]
    [InlineData("2 × (-) 3", -6)]
    [InlineData("2 × − 3", -1)]
    [InlineData("2 (-) 3 )", -6)]
    [InlineData("6 ÷ 2 ( 1 + 2 )", 1)]
    [InlineData("6 ÷ 2 × ( 1 + 2 )", 9)]
    [InlineData("√ 9 ↑ + 4", 7)]
    [InlineData("2 + 3 xʸ 2 ↑ + 4", 15)]
    [InlineData("( 1 + 1 ) xʸ 2 + 2", 16)]
    [InlineData("8 fraction 2 + 2", 2)]
    [InlineData("1 5 0 × 2 0 %", 30)]
    [InlineData("2 + 3 %", 2.03)]
    public async Task KeysFollowScientificNotationAndLocalTemplateBoundaries(string sequence, double expected)
    {
        using var keypad = new Keypad();
        keypad.Keys(sequence);
        Assert.Equal(expected, await keypad.Evaluate(), 10);
    }

    [Theory]
    [InlineData("6 ÷ 2 π", 3 / Math.PI)]
    [InlineData("6 ÷ 2 e", 3 / Math.E)]
    [InlineData("6 ÷ 2 × π", 3 * Math.PI)]
    [InlineData("6 ÷ 2 e 3", 1 / Math.E)]
    [InlineData("6 ÷ 2 √ 9 )", 1)]
    public async Task JuxtaposedFactorsRemainTogetherOnTheRightOfDivision(string sequence, double expected)
    {
        using var keypad = new Keypad();
        keypad.Keys(sequence);
        Assert.Equal(expected, await keypad.Evaluate(), 10);
    }

    [Fact]
    public async Task ClosingATrigonometricArgumentMakesTheFollowingProductExternal()
    {
        using var keypad = new Keypad();
        keypad.Workspace.SetRadians(false);
        keypad.Keys("sin 3 0");
        Assert.True(keypad.IsScoped, keypad.Trace);
        Assert.Equal("30", keypad.Buffer);
        keypad.Keys(") × 2");
        Assert.False(keypad.IsScoped, keypad.Trace);
        Assert.Equal("sin(30)*2", keypad.Source);
        Assert.Equal(1, await keypad.Evaluate(), 10);
    }

    [Fact]
    public void LeavingARadicalThenClosingAFunctionLeavesNestedTemplatesOneAtATime()
    {
        using var keypad = new Keypad();
        keypad.Keys("sin √ 9");
        Assert.Equal("9", keypad.Buffer);
        keypad.Press("↑");
        Assert.Equal("sqrt(9)", keypad.Buffer);
        Assert.True(keypad.IsScoped, keypad.Trace);
        keypad.Press(")");
        Assert.Equal("sin(sqrt(9))", keypad.Source);
        Assert.False(keypad.IsScoped, keypad.Trace);
        Assert.Equal(keypad.Source.Length, keypad.Position);
        keypad.Keys("+ 4");
        Assert.Equal("sin(sqrt(9))+4", keypad.Source);
    }

    [Fact]
    public async Task FractionKeyAndDivisionKeyCreateDifferentInputBoundaries()
    {
        using var division = new Keypad();
        division.Keys("8 ÷ 2");
        Assert.False(division.IsScoped, division.Trace);
        division.Keys("+ 2");
        Assert.Equal(6, await division.Evaluate(), 10);

        using var fraction = new Keypad();
        fraction.Keys("8 fraction 2 + 2");
        Assert.True(fraction.IsScoped, fraction.Trace);
        Assert.Equal("2+2", fraction.Buffer);
        fraction.Press("↑");
        Assert.False(fraction.IsScoped, fraction.Trace);
        Assert.Equal(2, await fraction.Evaluate(), 10);
    }

    [Fact]
    public async Task ANewDigitAfterSquareStartsAFactorRatherThanExtendingTheExponent()
    {
        using var keypad = new Keypad();
        keypad.Keys("9 x² 3");
        Assert.Equal("9^2*3", keypad.Source);
        Assert.Equal(243, await keypad.Evaluate(), 10);
    }

    [Fact]
    public async Task RepeatedSquareKeepsItsOperandInsideALinearDivision()
    {
        using var keypad = new Keypad();
        keypad.Keys("7 ÷ 9 x² x²");
        Assert.False(keypad.IsScoped, keypad.Trace);
        Assert.Equal(7.0 / Math.Pow(9, 4), await keypad.Evaluate(), 10);
    }

    [Fact]
    public async Task SquareAfterPercentBindsTheCompletedPercentage()
    {
        using var keypad = new Keypad();
        keypad.Keys("2 + 3 % x²");
        Assert.Equal(2.0009, await keypad.Evaluate(), 10);
    }

    [Fact]
    public async Task ADecimalAfterLeavingAFractionStartsANewFactor()
    {
        using var keypad = new Keypad();
        keypad.Keys("8 fraction 2 ↑ . 5");
        Assert.EndsWith("*0.5", keypad.Source);
        Assert.Equal(2, await keypad.Evaluate(), 10);
    }

    [Fact]
    public async Task ANewDigitAfterLeavingAnExponentStaysOutsideThePower()
    {
        using var keypad = new Keypad();
        keypad.Keys("2 xʸ 3 ↑ 4");
        Assert.Equal("2^(3)*4", keypad.Source);
        Assert.False(keypad.IsScoped, keypad.Trace);
        Assert.Equal(32, await keypad.Evaluate(), 10);
    }

    [Fact]
    public void CompletedPostfixRemainsInTheCurrentExponentContainer()
    {
        using var keypad = new Keypad();
        keypad.Keys("2 xʸ 3 x² 4");
        Assert.Equal("3^2*4", keypad.Buffer);
        Assert.True(keypad.IsScoped, keypad.Trace);
        Assert.Equal("2^(3^2*4)", keypad.Source);
    }

    [Theory]
    [InlineData("2 e 3", "2 e 3", 6)]
    [InlineData("e 2", "e 2", 2)]
    [InlineData("2 e . 5", "2 e 0.5", 1)]
    public async Task EulerKeyIsAnAtomicConstantRatherThanAnExponentMarker(string sequence, string source, double multiplier)
    {
        using var keypad = new Keypad();
        keypad.Keys(sequence);
        Assert.Equal(source, keypad.Source);
        Assert.Equal(multiplier * Math.E, await keypad.Evaluate(), 10);
    }

    [Fact]
    public async Task PrefixAfterResultStartsFreshWhilePostfixContinuesTheDisplayedAnswer()
    {
        using var keypad = new Keypad();
        keypad.Keys("3 × 4");
        Assert.Equal(12, await keypad.Evaluate(), 10);
        keypad.Press("x²");
        Assert.Equal("Ans^2", keypad.Source);
        Assert.Equal("12", keypad.Workspace.Session.DraftPreviousAnswer.Code);
        Assert.Equal(144, await keypad.Evaluate(), 10);

        keypad.Keys("√ 9 )");
        Assert.Equal("sqrt(9)", keypad.Source);
        Assert.Equal(3, await keypad.Evaluate(), 10);
        keypad.Keys("7 + 2");
        Assert.Equal("7+2", keypad.Source);
        Assert.Equal(9, await keypad.Evaluate(), 10);
    }

    [Fact]
    public async Task ContinuationFromHistoryUsesDisplayedAnswerRatherThanLatestGlobalAns()
    {
        var session = new CalculatorSession { Answer = new MathValue(99) };
        var record = new HistoryRecord
        {
            Input = "Ans*2", PreviousAnswer = new MathValue(5), Answer = new MathValue(10)
        };
        session.History.Add(record);
        var workspace = new CalculatorWorkspace(session);
        workspace.Recall(record);
        using var keypad = new Keypad(workspace);
        keypad.Press("x²");
        Assert.Equal("10", workspace.Session.DraftPreviousAnswer.Code);
        Assert.Equal("99", workspace.Session.Answer.Code);
        Assert.Equal(100, await keypad.Evaluate(), 10);
    }

    [Fact]
    public async Task ExplicitSelectionCanWrapASumWithoutChangingOrdinaryPostfixInput()
    {
        using var keypad = new Keypad();
        keypad.Keys("2 + 3");
        keypad.SelectText(0, keypad.Source.Length);
        keypad.Press("x²");
        Assert.Equal("(2+3)^2", keypad.Source);
        Assert.Equal(25, await keypad.Evaluate(), 10);
    }
}
