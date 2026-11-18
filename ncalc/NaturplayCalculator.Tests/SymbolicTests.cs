using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class SymbolicTests
    {
        private static CalculationResult Exact(string input, bool radians = true) => new CalculationEngine().Evaluate(input,
            new CalculationOptions { Mode = CalculationMode.Symbolic, UseRadians = radians });

        [Theory]
        [InlineData("1/2", "1/2")]
        [InlineData("2/4", "1/2")]
        [InlineData("1/3+1/6", "1/2")]
        [InlineData("0.1+0.2", "3/10")]
        [InlineData("9007199254740993+1", "9007199254740994")]
        [InlineData("1E-20", "1/100000000000000000000")]
        [InlineData("sqrt(8)", "2√2")]
        [InlineData("sqrt(18)+sqrt(8)", "5√2")]
        [InlineData("sqrt(2)*sqrt(8)", "4")]
        [InlineData("sqrt(2)*sqrt(3)", "√6")]
        [InlineData("sqrt(4/9)", "2/3")]
        [InlineData("sqrt(1/2)", "√2/2")]
        [InlineData("1/sqrt(2)", "√2/2")]
        [InlineData("sqrt(2)/sqrt(8)", "1/2")]
        [InlineData("1/(1+sqrt(2))", "√2-1")]
        [InlineData("sqrt(-8)", "2i√2")]
        [InlineData("i^-1", "-i")]
        [InlineData("i^4", "1")]
        [InlineData("2^3^2", "512")]
        [InlineData("(-8)yroot3", "-2")]
        [InlineData("a+a", "2a")]
        [InlineData("a-a", "0")]
        [InlineData("(a+b)c+bc", "ac+2bc")]
        [InlineData("(a+b)^2", "a^2+2ab+b^2")]
        [InlineData("x/2+x/3", "5x/6")]
        [InlineData("a/b-a/b", "0")]
        [InlineData("(x^2-1)/(x-1)", "x+1")]
        [InlineData("x*x/x", "x")]
        [InlineData("sqrt(x^2)", "Abs(x)")]
        [InlineData("sqrt(12)*x+sqrt(3)*x", "3x√3")]
        [InlineData("sin(pi/6)", "1/2")]
        [InlineData("cos(pi/4)", "√2/2")]
        [InlineData("tan(pi/6)", "√3/3")]
        [InlineData("ln(e)", "1")]
        [InlineData("lg(1000)", "3")]
        [InlineData("D(1,2,3)", "2/3")]
        [InlineData("50nCr25", "126410606437752")]
        [InlineData("(1,2,,3,4)^-1", "(-2,1,,3/2,-1/2)")]
        [InlineData("Abs(a,b,,c,d)", "ad-bc")]
        [InlineData("(a,b,,c,d)^T", "(a,c,,b,d)")]
        public void SimplifiesToExactCanonicalExpressions(string input, string expected)
        {
            var result = Exact(input);
            Assert.Equal(CalculationMode.Symbolic, result.Value.Mode);
            Assert.Equal(expected, result.Value.DisplayText);
            Assert.Equal(result.Value.Code, Exact(result.Value.Code).Value.Code);
        }

        [Theory]
        [InlineData("(a+b)c+bc")][InlineData("(a+b)^3")][InlineData("x/2+x/3")]
        [InlineData("(x^2-1)/(x-1)")][InlineData("sqrt(12)*x+sqrt(3)*x")]
        [InlineData("(a+b)/(a-b)+(a-b)/(a+b)")][InlineData("sin(pi/12)")]
        public void BothModesShareMeaningAndRoundTripThroughTheSameParser(string input)
        {
            var variables = new Dictionary<string, MathValue> { ["a"] = new MathValue(0.7), ["b"] = new MathValue(-1.4), ["c"] = new MathValue(2.5), ["x"] = new MathValue(1.25) };
            var options = new CalculationOptions { Variables = variables };
            var engine = new CalculationEngine();
            double original = engine.Evaluate(input, options).Answer.Cmx.Re;
            double reduced = engine.Evaluate(Exact(input).Value.Code, options).Answer.Cmx.Re;
            Assert.InRange(Math.Abs(original - reduced), 0, Math.Max(1e-12, Math.Abs(original) * 1e-12));
        }

        [Fact]
        public void CancellationKeepsTheOriginalDomainRestriction()
        {
            var result = Exact("a/a");
            Assert.Equal("1", result.Value.DisplayText);
            Assert.Contains("a ≠ 0", result.Value.Conditions);
            Assert.Equal("Abs(x)", Exact("sqrt(x^2)").Value.DisplayText);
            Assert.Throws<MathException>(() => Exact("a/(b-b)"));
        }
        [Fact]
        public void ExactAnsAndUnresolvedAnglesSurviveModeChanges()
        {
            var engine = new CalculationEngine();
            var exact = Exact("1/3").Value;
            Assert.Equal("1", engine.Evaluate("Ans*3", new CalculationOptions { Mode = CalculationMode.Symbolic, PreviousAnswer = exact }).Value.DisplayText);
            var degrees = Exact("sin(10)", false).Value;
            double numeric = engine.Evaluate("Ans", new CalculationOptions { PreviousAnswer = degrees, UseRadians = true }).Answer.Cmx.Re;
            Assert.InRange(Math.Abs(numeric - Math.Sin(Math.PI / 18)), 0, 1e-14);
            Assert.Equal("1/2", Exact("sin(30)", false).Value.DisplayText);
        }
        [Theory]
        [InlineData("de-DE")][InlineData("fr-FR")][InlineData("tr-TR")]
        public void ExactArithmeticIsIndependentOfCulture(string culture)
        {
            var previous = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture); Assert.Equal("3/10", Exact("0.1+0.2").Value.DisplayText); }
            finally { CultureInfo.CurrentCulture = previous; }
        }
        [Fact]
        public void SymbolicExpansionAndIntegerSizeAreBounded()
        {
            Assert.Throws<MathException>(() => Exact("(a+b+c+d+f+g+h+j)^20"));
            Assert.Throws<MathException>(() => Exact("2^1000000000"));
            Assert.Throws<MathException>(() => Exact("1E99999999"));
            Assert.Throws<OperationCanceledException>(() => new CalculationEngine().Evaluate("(a+b)^2", new CalculationOptions { Mode = CalculationMode.Symbolic }, new CancellationToken(true)));
        }
        [Fact]
        public void NumericModeReportsUnboundVariablesAndParserPreservesPrecedence()
        {
            Assert.Throws<MathException>(() => new CalculationEngine().Evaluate("a+1"));
            Assert.Equal(4, new CalculationEngine().Evaluate("sqrt(4)^2").Answer.Cmx.Re);
            Assert.Equal(-4, new CalculationEngine().Evaluate("-2^2").Answer.Cmx.Re);
            Assert.Equal("ac+2bc", Exact("(a+b)c+bc [comment: variable demo]").Value.DisplayText);
        }
    }
}
