using System;
using System.Collections.Generic;
using System.Linq;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.History;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class SymbolicIntegrationTests
    {
        private static CalculationValue Exact(string input) => new CalculationEngine().Evaluate(input,
            new CalculationOptions { Mode = CalculationMode.Symbolic }).Value;

        [Fact]
        public void MixedModeSessionsKeepExactValuesConditionsAndOriginalContext()
        {
            var session = new CalculatorSession
            {
                Mode = CalculationMode.Numeric, DraftMode = CalculationMode.Symbolic,
                Draft = "Ans+sqrt(8)", DraftPreviousAnswer = Exact("a/a"),
                Answer = Exact("1/3"), DraftIsSolved = true, DraftAnswer = Exact("1+sqrt(8)")
            };
            session.History.Add(new HistoryRecord { Input = "1/3", Mode = CalculationMode.Symbolic, Answer = Exact("1/3"), PreviousAnswer = new MathValue(0) });
            session.History.Add(new HistoryRecord { Input = "Ans*3", Mode = CalculationMode.Numeric, Answer = new MathValue(1), PreviousAnswer = Exact("1/3") });
            var restored = SessionCodec.Read(SessionCodec.Write(session));
            Assert.Equal(CalculationMode.Numeric, restored.Mode);
            Assert.Equal(CalculationMode.Symbolic, restored.DraftMode);
            Assert.Equal("1/3", restored.Answer.Code);
            Assert.Equal(session.DraftAnswer.Code, restored.DraftAnswer.Code);
            Assert.Contains("a ≠ 0", restored.DraftPreviousAnswer.Conditions);
            Assert.Equal(CalculationMode.Symbolic, restored.History[0].Mode);
            Assert.Equal(CalculationMode.Numeric, restored.History[1].Mode);
            Assert.Equal(1, new CalculationEngine().Evaluate("Ans*3", new CalculationOptions { PreviousAnswer = restored.History[1].PreviousAnswer }).Answer.Cmx.Re);
            var next = new CalculationEngine().Evaluate("Ans+1", new CalculationOptions { Mode = CalculationMode.Symbolic, PreviousAnswer = restored.DraftPreviousAnswer }).Value;
            Assert.Contains("a ≠ 0", next.Conditions);
        }

        [Fact]
        public void ConditionsSurviveNumericAnsEvaluationAndPersistence()
        {
            var engine = new CalculationEngine();
            var symbolic = Exact("a/a");
            var numeric = engine.Evaluate("Ans+1", new CalculationOptions { PreviousAnswer = symbolic }).Value;
            Assert.Equal(2, numeric.Cmx.Re);
            Assert.Contains("a ≠ 0", numeric.Clone().Conditions);
            var restored = SessionCodec.Read(SessionCodec.Write(new CalculatorSession { Answer = numeric })).Answer;
            Assert.Contains("a ≠ 0", restored.Conditions);
            Assert.Equal(2, restored.Cmx.Re);
            Assert.Empty(engine.Evaluate("2", new CalculationOptions { PreviousAnswer = numeric }).Value.Conditions);
        }

        [Theory]
        [InlineData("ln(a)-ln(a)", "a > 0")]
        [InlineData("arcsin(x)-arcsin(x)", "-1 ≤ x ≤ 1")]
        [InlineData("tan(x)-tan(x)", "cosrad(x) ≠ 0")]
        [InlineData("a!-a!", "a ∈ ℤ≥0")]
        public void CancellingFunctionsRetainsTheirDomain(string input, string condition)
        {
            var value = Exact(input);
            Assert.Equal("0", value.Code);
            Assert.Contains(condition, value.Conditions);
        }

        [Fact]
        public void VersionTwoSessionMigratesWithoutReevaluatingItsStoredAnswer()
        {
            const string json = "{\"version\":2,\"draft\":\"Ans*2\",\"answer\":{\"real\":10,\"imaginary\":0},\"draftPreviousAnswer\":{\"real\":5,\"imaginary\":0},\"draftUseRadians\":false,\"draftIsSolved\":true,\"draftAnswer\":{\"real\":10,\"imaginary\":0},\"history\":[{\"input\":\"Ans*2\",\"kind\":\"math\",\"answer\":{\"real\":10,\"imaginary\":0},\"previousAnswer\":{\"real\":5,\"imaginary\":0},\"useRadians\":false,\"createdAt\":\"2026-01-01T00:00:00.0000000+00:00\"}]}";
            var session = SessionCodec.Read(json);
            Assert.Equal(CalculationMode.Numeric, session.Mode);
            Assert.Equal(CalculationMode.Numeric, session.DraftMode);
            Assert.Equal(CalculationMode.Numeric, Assert.Single(session.History).Mode);
            Assert.Equal(10, session.DraftAnswer.Cmx.Re);
            Assert.Equal(5, session.DraftPreviousAnswer.Cmx.Re);
            Assert.Equal(10, SessionCodec.Read(SessionCodec.Write(session)).Answer.Cmx.Re);
        }

        [Theory]
        [InlineData("Ans")][InlineData("?")]
        public void StoredSymbolicValuesCannotRecursivelyReferenceAnsOrPlaceholders(string invalid)
        {
            var json = SessionCodec.Write(new CalculatorSession { Answer = Exact("a") });
            Assert.Throws<FormatException>(() => SessionCodec.Read(json.Replace("\"expression\":\"a\"", "\"expression\":\"" + invalid + "\"")));
        }

        [Fact]
        public void InvalidModeIsRejected()
        {
            var json = SessionCodec.Write(new CalculatorSession());
            Assert.Throws<FormatException>(() => SessionCodec.Read(json.Replace("\"mode\":\"Numeric\"", "\"mode\":\"Unknown\"")));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CalculationEngine().Evaluate("1", new CalculationOptions { Mode = (CalculationMode)99 }));
        }

        [Fact]
        public void NaturalLayoutRetainsVariableSourcePositionsAndExactFractions()
        {
            var parsed = new ExpressionParser().Parse("(a+b)c+bc");
            var message = new ExpressionTexFormatter().Format(parsed.Root, true);
            Assert.Equal(new[] { 1, 3, 5, 7, 8 }, message.SourceMap
                .Where(r => "abc".Contains(message.Latex.Substring(r.TexStart, r.TexLength))).Select(r => r.InputStart));
            Assert.Equal(@"\frac{1}{2}", new ExpressionTexFormatter().Format(Exact("2/4").Expression).Latex);
            Assert.DoesNotContain(@"\times", new ExpressionTexFormatter().Format(Exact("sqrt(8)").Expression, true).Latex);
        }

        [Fact]
        public void ReusingTheParserDoesNotMutatePreviousComments()
        {
            var parser = new ExpressionParser();
            var first = parser.Parse("1 [comment: first]");
            parser.Parse("2 [comment: second]");
            Assert.Equal("comment: first", Assert.Single(first.Comments));
        }

        [Theory]
        [InlineData("((1,2,,3,4)^T)^2")]
        [InlineData("(2^3)yroot2")]
        [InlineData("(2yroot3)^2")]
        [InlineData("(8^T)yroot2")]
        public void CodeRoundTripsPreserveMixedPowerAndTransposePrecedence(string input)
        {
            var engine = new CalculationEngine();
            var code = new ExpressionParser().Parse(input).Root.ToCode();
            Assert.Equal(engine.Evaluate(input).Answer.ToString(), engine.Evaluate(code).Answer.ToString());
            Assert.Equal(Exact(input).Code, Exact(code).Code);
        }

        [Fact]
        public void OneCellMatricesRemainMatricesAcrossModesAndStorage()
        {
            var value = Exact("(1/2,)");
            Assert.True(value.IsMatrix);
            var restored = SessionCodec.Read(SessionCodec.Write(new CalculatorSession { Answer = value })).Answer;
            Assert.True(restored.IsMatrix);
            Assert.Equal("1/2", restored.Expression.Children[0].ToCode());
            Assert.Equal("8", Exact("(2,)^3").Code);
        }

        [Fact]
        public void MatrixPowersRequireKnownIntegerExponentsToPreserveNoncommutativity()
        {
            Assert.Throws<MathException>(() => Exact("(1,2,,0,1)^n"));
            Assert.NotEqual(Exact("(1,2,,0,1)*(1,0,,3,1)").Code, Exact("(1,0,,3,1)*(1,2,,0,1)").Code);
        }

        [Theory]
        [InlineData("E*x", "Ex")][InlineData("D*x", "Dx")]
        [InlineData("T+T", "2T")]
        public void UppercaseVariablesDoNotBecomeFunctionsWhenStored(string input, string expected)
        {
            var value = Exact(input);
            Assert.Equal(expected, value.DisplayText);
            var restored = SessionCodec.Read(SessionCodec.Write(new CalculatorSession { Answer = value }));
            Assert.Equal(value.Code, restored.Answer.Code);
        }

        [Fact]
        public void ExpandedPolynomialsAndFractionsMatchNumericEvaluationAcrossSigns()
        {
            string[] inputs = { "(a+b)^4", "(a^2-b^2)/(a+b)", "(a+b)/(a-b)+(a-b)/(a+b)", "sqrt(a^2)*b", "(a+b)c+bc", "(a/b)/(c/b)" };
            var engine = new CalculationEngine();
            foreach (string input in inputs)
            {
                string simplified = Exact(input).Code;
                for (int i = -4; i <= 4; i++)
                {
                    var options = new CalculationOptions { Variables = new Dictionary<string, MathValue> { ["a"] = new MathValue(i + 0.25), ["b"] = new MathValue(1.5), ["c"] = new MathValue(-2.25) } };
                    double expected = engine.Evaluate(input, options).Answer.Cmx.Re;
                    double actual = engine.Evaluate(simplified, options).Answer.Cmx.Re;
                    Assert.InRange(Math.Abs(expected - actual), 0, Math.Max(1e-10, Math.Abs(expected) * 1e-12));
                }
            }
        }
    }
}
