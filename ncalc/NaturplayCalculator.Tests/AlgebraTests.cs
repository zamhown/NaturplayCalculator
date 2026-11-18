using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class AlgebraTests
    {
        private static MathValue Eval(string text) => new CalculationEngine().Evaluate(text).Answer;
        [Theory]
        [InlineData("-2^2", -4)][InlineData("2*-3", -6)][InlineData("2--3", 5)]
        [InlineData("-(-1)", 1)][InlineData("(-8)yroot(-3)", -0.5)]
        [InlineData("2 * -3", -6)][InlineData("1\n +\t2", 3)]
        [InlineData("sin(π)", 0)][InlineData("cos(π/2)", 0)]
        [InlineData("arsh(-1E10)", -23.7189981105004)]
        [InlineData("arsh(1E-20)", 1e-20)]
        [InlineData("E(1E308,1E308)", 1e308)]
        [InlineData("D(1E308,1E308)", 0)]
        [InlineData("D(1E154,-1E154)", 1e308)]
        [InlineData("Cov(1E308,1E308,,2,4)", 0)]
        [InlineData("Cov(1E154,-1E154,,1E154,-1E154)", 1e308)]
        [InlineData("ρ(1E308,-1E308,,1E308,-1E308)", 1)]
        [InlineData("ρ(1E-200,-1E-200,,-1E-200,1E-200)", -1)]
        public void ParserAndFunctionBoundaries(string text, double expected)
        {
            double actual = Eval(text).Cmx.Re;
            Assert.True(Math.Abs(actual - expected) <= Math.Max(1e-30, Math.Abs(expected) * 1e-12), $"{text}: expected {expected:R}, got {actual:R}");
        }
        [Fact]
        public void ArithmeticMatchesIndependentReferenceForSignedInputs()
        {
            var random = new Random(174);
            for (int i = 0; i < 200; i++)
            {
                int a = random.Next(-100, 101), b = random.Next(-100,101), c = random.Next(1,50);
                string expression = string.Format(CultureInfo.InvariantCulture, "(({0})+({1}))*{2}-({0})/{2}", a,b,c);
                double expected = (a+b)*c-(double)a/c;
                Assert.InRange(Math.Abs(Eval(expression).Cmx.Re-expected), 0, 1e-10);
            }
        }
        [Fact]
        public void InverseSatisfiesIdentityForManyNonsingularMatrices()
        {
            var random = new Random(421);
            for (int i = 0; i < 50; i++)
            {
                int a=random.Next(1,20), b=random.Next(1,20), c=random.Next(1,20), d=random.Next(1,20);
                if (a*d==b*c) continue;
                string m=$"({a},{b},,{c},{d})";
                var identity=Eval(m+"*"+m+"^-1").Mtr;
                for(int row=0;row<2;row++) for(int col=0;col<2;col++)
                    Assert.InRange(Math.Abs(identity[row][col].Value.Cmx.Re-(row==col?1:0)),0,1e-10);
            }
        }
        [Fact]
        public void DocumentEvaluationRejectsDtdAndExcessiveTrees()
        {
            var evaluator = new ContentEvaluator();
            Assert.ThrowsAny<Exception>(()=>evaluator.Evaluate("<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///never-read'>]><x>&a;</x>",new CalculationOptions(),CancellationToken.None));
            Assert.Throws<MathException>(()=>evaluator.Evaluate("["+string.Join(",",Enumerable.Repeat("0",3000))+"]",new CalculationOptions(),CancellationToken.None));
            string attributes = string.Join(" ", Enumerable.Range(0, 2100).Select(i => $"a{i}='v'"));
            Assert.Throws<MathException>(() => evaluator.Evaluate("<root " + attributes + "/>", new CalculationOptions(), CancellationToken.None));
            Assert.Equal("xml",evaluator.Evaluate("<root><a>hello</a></root>",new CalculationOptions(),CancellationToken.None).Kind);
        }
        [Theory]
        [InlineData("pi+1")][InlineData("(1+i)^2")][InlineData("sqrt(4)+3")][InlineData("(1,2,,3,4)^T")]
        public void FormattingDoesNotChangeCalculationOrSourceTokens(string text)
        {
            var input = new InputString(text); input.GiveIndex();
            var element = new Element(input);
            string before = element.ToCode();
            string format = element.ToInputString().ToString();
            Assert.Equal(format, element.ToInputString().ToString());
            Assert.Equal(before, element.ToCode());
            Assert.Equal(text, input.ToString());
            Assert.Equal(Eval(text).ToString(), Element.Solve(element, false).ToString());
        }
    }
}
