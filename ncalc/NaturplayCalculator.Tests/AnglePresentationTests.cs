using System;
using System.Collections.Generic;
using System.Linq;
using NaturplayCalculator.Expressions;
using Naturplay.Tex;
using NaturplayCalculator.History;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class AnglePresentationTests
    {
        [Theory]
        [InlineData("sin")][InlineData("cos")][InlineData("tan")]
        [InlineData("arcsin")][InlineData("arccos")][InlineData("arctan")]
        public void FixedAngleFunctionsUseTheSameGlyphsAndNamesAsInput(string name)
        {
            var input = ExpressionNode.Function(name, ExpressionNode.Symbol("a"));
            var result = ExpressionNode.Function(name + "rad", ExpressionNode.Symbol("a"));
            var inputNames = TexTestProjection.Document(input).Descendants().Where(n => n.Kind == TexKind.Glyph && n.AtomClass == TexAtomClass.Operator).Select(n => n.Text);
            var resultNames = TexTestProjection.Document(result).Descendants().Where(n => n.Kind == TexKind.Glyph && n.AtomClass == TexAtomClass.Operator).Select(n => n.Text);
            Assert.Equal(inputNames, resultNames);
            Assert.Equal(name + "(a)", result.ToDisplayString());
            Assert.Equal(name + "rad(a)", result.ToCode());
            Assert.True(CalculationValue.FromSymbolic(result).UsesFixedRadians);
        }

        [Fact]
        public void ReportedNestedRadicalHasAReadableFormulaAndSeparateUnitMetadata()
        {
            var result = new CalculationEngine().Evaluate("sqrt(sin((9a+6)/6))", new CalculationOptions { Mode = CalculationMode.Symbolic }).Value;
            Assert.Equal("√(sin((3a+2)/2))", result.DisplayText);
            Assert.Equal("sqrt(sinrad((3*a+2)/2))", result.Code);
            Assert.True(result.UsesFixedRadians);
            Assert.Equal("sin", Assert.Single(TexTestProjection.Document(result.Expression).Descendants(), n => n.Kind == TexKind.Glyph && n.AtomClass == TexAtomClass.Operator).Text);
        }

        [Theory]
        [InlineData("sin")][InlineData("cos")][InlineData("tan")]
        [InlineData("arcsin")][InlineData("arccos")][InlineData("arctan")]
        public void ReadableDisplayDoesNotChangeSavedAnglesOrAnsWhenSwitchingModes(string name)
        {
            var engine = new CalculationEngine();
            string input = name + "(0.25)";
            foreach (bool radians in new[] { false, true })
            {
                var exact = engine.Evaluate(input, new CalculationOptions { Mode = CalculationMode.Symbolic, UseRadians = radians }).Value;
                var restored = SessionCodec.Read(SessionCodec.Write(new CalculatorSession { Answer = exact })).Answer;
                Assert.Equal(exact.Code, restored.Code);
                Assert.Equal(exact.DisplayText, restored.DisplayText);
                Assert.DoesNotContain("rad", restored.DisplayText);
                Assert.True(restored.UsesFixedRadians);
                double expected = engine.Evaluate(input, new CalculationOptions { UseRadians = radians }).Answer.Cmx.Re;
                double actual = engine.Evaluate("Ans", new CalculationOptions { PreviousAnswer = restored, UseRadians = !radians }).Answer.Cmx.Re;
                Assert.InRange(Math.Abs(expected - actual), 0, Math.Max(1, Math.Abs(expected)) * 1e-12);
            }
        }

        [Fact]
        public void DomainNotesHideInternalNamesWithoutLosingTheirUnitOrStoredMeaning()
        {
            var result = new CalculationEngine().Evaluate("tan(a)-tan(a)", new CalculationOptions { Mode = CalculationMode.Symbolic }).Value;
            Assert.Equal("0", result.DisplayText);
            Assert.Contains("cosrad(a) ≠ 0", result.Conditions);
            Assert.Contains("cos(a) ≠ 0", result.DisplayConditions);
            Assert.True(result.UsesFixedRadians);
            var restored = SessionCodec.Read(SessionCodec.Write(new CalculatorSession { Answer = result })).Answer;
            Assert.Equal(result.Conditions, restored.Conditions);
            Assert.Equal(result.DisplayConditions, restored.DisplayConditions);
            Assert.True(restored.UsesFixedRadians);
        }

        [Fact]
        public void NonAngularResultsDoNotAcquireAnAngleNote()
        {
            var engine = new CalculationEngine();
            Assert.False(engine.Evaluate("sqrt(8)", new CalculationOptions { Mode = CalculationMode.Symbolic }).Value.UsesFixedRadians);
            Assert.False(engine.Evaluate("sin(pi/6)", new CalculationOptions { Mode = CalculationMode.Symbolic }).Value.UsesFixedRadians);
            Assert.False(ExpressionFormatter.ContainsFixedRadians(new ExpressionParser().Parse("r*a*d").Root));
            Assert.Equal("grad(a)", ExpressionFormatter.DisplayCondition("grad(a)"));
        }

        [Theory]
        [InlineData("sqrt(sin((9a+6)/6))", 1)]
        [InlineData("sin(a)", 1)]
        [InlineData("sqrt(8)+sqrt(2)", 2)]
        public void VisuallyIdenticalAngleBindingStepsAreHiddenButTheFinalCodeIsPreserved(string input, int count)
        {
            var result = new CalculationEngine().Evaluate(input, new CalculationOptions { Mode = CalculationMode.Symbolic, CaptureSteps = true });
            var steps = CalculationPresentation.ResultSequence(result.Expression, result.Steps, result.Value.Expression);
            Assert.Equal(count, steps.Count);
            Assert.Same(result.Value.Expression, steps.Last());
            Assert.Equal(result.Value.Code, steps.Last().ToCode());
            for (int i = 1; i < steps.Count; i++) Assert.NotEqual(steps[i - 1].ToDisplayString(), steps[i].ToDisplayString());
        }

        [Fact]
        public void FinalAnswerIsShownEvenWhenTheIntermediateDisplayBudgetIsExhausted()
        {
            var result = new CalculationEngine().Evaluate("sqrt(8)+sqrt(2)", new CalculationOptions { Mode = CalculationMode.Symbolic, CaptureSteps = true });
            Assert.Same(result.Value.Expression, Assert.Single(CalculationPresentation.ResultSequence(result.Expression, result.Steps, result.Value.Expression, 0)));
        }

    }
}
