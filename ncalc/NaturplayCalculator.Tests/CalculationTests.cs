using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class CalculationTests
    {
        private static MathValue Evaluate(string text, bool radians = true) => new CalculationEngine().Evaluate(text, new CalculationOptions { UseRadians = radians }).Answer;
        private static void Near(double expected, double actual)
        {
            Assert.True(Math.Abs(expected - actual) <= Math.Max(Math.Abs(expected) * 1e-12, 1e-30), $"Expected {expected:R}, got {actual:R}");
        }
        [Theory]
        [InlineData("1+2*3", 7)]
        [InlineData("2^3^2", 512)]
        [InlineData("(2^3)^2", 64)]
        [InlineData("(-2)^2", 4)]
        [InlineData("2(3+4)", 14)]
        [InlineData("(2+3)(4+5)", 45)]
        [InlineData("1.5+2.5", 4)]
        [InlineData("10^-16*10^16", 1)]
        [InlineData("1E-16*1E16", 1)]
        [InlineData("1e-16*1e16", 1)]
        [InlineData("1E-20", 1e-20)]
        [InlineData("2!!", 2)]
        [InlineData("4!!", 8)]
        [InlineData("6!!", 48)]
        [InlineData("5!!", 15)]
        [InlineData("(-3)!!", -1)]
        [InlineData("5!", 120)]
        [InlineData("0!", 1)]
        [InlineData("171nCr171", 1)]
        [InlineData("1000nCr999", 1000)]
        [InlineData("5nCr2", 10)]
        [InlineData("5nPr2", 20)]
        [InlineData("D(5)", 0)]
        [InlineData("D(1,2,3)", 2.0/3)]
        [InlineData("E(1,2,3)", 2)]
        [InlineData("rank(1,2,,2,4)", 1)]
        [InlineData("Abs(1,2,,3,4)", -2)]
        [InlineData("(-8)yroot3", -2)]
        public void RealArithmetic(string input, double expected)
        {
            var value = Evaluate(input); Assert.False(value.IsMatrix); Near(expected, value.Cmx.Re); Near(0, value.Cmx.Im);
        }
        [Theory]
        [InlineData("i^-1", 0, -1)]
        [InlineData("i^2", -1, 0)]
        [InlineData("(1+i)^-1", 0.5, -0.5)]
        [InlineData("i/100000", 0, 0.00001)]
        [InlineData("0.00001i", 0, 0.00001)]
        [InlineData("1E-20i", 0, 1e-20)]
        [InlineData("(-1)^1.5", 0, -1)]
        [InlineData("sqrt(-1)", 0, 1)]
        public void ComplexArithmetic(string input, double real, double imaginary)
        {
            var value = Evaluate(input).Cmx;
            Assert.InRange(Math.Abs(value.Re - real), 0, Math.Max(1e-15, Math.Abs(real)*1e-12));
            Assert.InRange(Math.Abs(value.Im - imaginary), 0, Math.Max(1e-30, Math.Abs(imaginary)*1e-12));
        }
        [Theory]
        [InlineData("en-US")][InlineData("de-DE")][InlineData("fr-FR")][InlineData("zh-CN")][InlineData("tr-TR")]
        public void ArithmeticAndFormattingAreCultureIndependent(string culture)
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Assert.Equal("4", Evaluate("1.5+2.5").ToString());
                Assert.Equal("0.5", Evaluate("1/2").ToString());
                Near(0.00001, Evaluate("i/100000").Cmx.Im);
                var inverse = Evaluate("(1,2,,3,4)^-1").Mtr; Near(-2, inverse[0][0].Value.Cmx.Re); Near(1.5, inverse[1][0].Value.Cmx.Re);
            }
            finally { CultureInfo.CurrentCulture = original; }
        }
        [Theory]
        [InlineData("2147483647!")][InlineData("2.5!")][InlineData("5.5nCr2")][InlineData("5nPr-1")]
        [InlineData("(1,0,,0,2)^1.9")][InlineData("1/0")][InlineData("0/0")][InlineData("171!")]
        [InlineData("foo")][InlineData("1.2.3")][InlineData("(1,2,,2,4)^-1")]
        [InlineData("")][InlineData("()")][InlineData("T")][InlineData("(T,1)")]
        public void InvalidInputsFailExplicitly(string input) { Assert.ThrowsAny<Exception>(() => Evaluate(input)); }
        [Fact]
        public void MatrixInverseAndPowersSupportComplexEntries()
        {
            Assert.Equal("(-i,0,,0,1)", Evaluate("(i,0,,0,1)^-1").ToString());
            Assert.Equal("(1,0,,0,1)", Evaluate("(1,2,,3,4)^0").ToString());
            Assert.Equal("(1,0,,0,1)", Evaluate("(i,0,,0,1)*(i,0,,0,1)^-1").ToString());
        }
        [Fact]
        public async Task ConcurrentEvaluationsKeepTheirOwnAnswerAndAngleMode()
        {
            var tasks = Enumerable.Range(1, 32).Select(i => Task.Run(() => new CalculationEngine().Evaluate("Ans+sin(90)", new CalculationOptions { PreviousAnswer = new MathValue(i), UseRadians = false }).Answer.Cmx.Re));
            Assert.Equal(Enumerable.Range(2, 32).Select(i => (double)i), await Task.WhenAll(tasks));
        }
        [Fact]
        public void CancellationAndBudgetsAreEnforced()
        {
            var cancelled = new CancellationToken(true);
            Assert.Throws<OperationCanceledException>(() => new CalculationEngine().Evaluate("1+2", cancellationToken: cancelled));
            Assert.Throws<MathException>(() => new CalculationEngine().Evaluate("1+2+3", new CalculationOptions { MaxOperations = 1 }));
            Assert.Throws<MathException>(() => Evaluate(new string('(', 100)+"1"+new string(')',100)));
            Assert.Throws<MathException>(() => Evaluate(new string('1', 5000)));
        }
        [Fact]
        public void StepsAreBoundedAndIndependentSnapshots()
        {
            var result = new CalculationEngine().Evaluate("(1+2)*(3+4)", new CalculationOptions { CaptureSteps = true, MaxSteps = 2 });
            Assert.InRange(result.Steps.Count, 1, 2);
            Assert.Equal(21, result.Answer.Cmx.Re);
            Assert.NotEqual(result.Expression.ToCode(), result.EvaluatedExpression.ToCode());
            var first = result.Steps[0].ToCode(); result.Answer.Value = new InputString("100");
            Assert.Equal(first, result.Steps[0].ToCode());
        }
    }
}
