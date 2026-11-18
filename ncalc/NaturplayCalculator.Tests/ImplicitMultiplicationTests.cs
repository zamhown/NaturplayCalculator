using System.Collections.Generic;
using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class ImplicitMultiplicationTests
    {
        private static CalculationOptions Options(CalculationMode mode) => new CalculationOptions {
            Mode = mode, UseRadians = true, Variables = new Dictionary<string, MathValue> {
                ["a"] = new MathValue(12), ["b"] = new MathValue(2), ["c"] = new MathValue(3),
                ["g"] = new MathValue(3), ["x"] = new MathValue(3) } };

        [Theory]
        [InlineData("18/3π", "18/(3*pi)")]
        [InlineData("18/3*π", "(18/3)*pi")]
        [InlineData("1/2x", "1/(2*x)")]
        [InlineData("a/bc", "a/(b*c)")]
        [InlineData("1/2(3+4)", "1/(2*(3+4))")]
        [InlineData("1/2√9", "1/(2*sqrt(9))")]
        [InlineData("1/2sin(1)", "1/(2*sin(1))")]
        [InlineData("1/2^3x", "1/(2^3*x)")]
        [InlineData("1/2x^2", "1/(2*x^2)")]
        [InlineData("-1/2x", "-1/(2*x)")]
        [InlineData("1/2x/3", "(1/(2*x))/3")]
        [InlineData("1/2x*3", "(1/(2*x))*3")]
        [InlineData("sin(√((9g-6)/9π))^(2+1)", "sin(sqrt((9*g-6)/(9*pi)))^(2+1)")]
        [InlineData("sin(√((9g-6)/9*π))^(2+1)", "sin(sqrt(((9*g-6)/9)*pi))^(2+1)")]
        public void InputPreviewBothEvaluationModesAndTexAgree(string input, string explicitMeaning)
        {
            var parser = new ExpressionParser();
            Assert.Equal(parser.Parse(explicitMeaning).Root.Key, parser.Parse(input).Root.Key);
            var engine = new CalculationEngine();
            foreach (var mode in new[] { CalculationMode.Numeric, CalculationMode.Symbolic })
            {
                var options = Options(mode);
                var expected = engine.Evaluate(explicitMeaning, options).Value;
                Assert.Equal(expected.Code, engine.Evaluate(input, options).Value.Code);
                var preview = ExpressionPreview.Create(input, mode == CalculationMode.Symbolic);
                Assert.Equal(parser.Parse(input).Root.Key, preview.Root.Key);
                var document = new TexParser().Parse(preview.Formula.Latex);
                var visibleMeaning = TexTestProjection.Linearize(document.Root);
                Assert.Equal(expected.Code, engine.Evaluate(visibleMeaning, options).Value.Code);
            }
        }

        [Theory]
        [InlineData("sin(√((9g-6)/9π))^(2+1)", true)]
        [InlineData("sin(√((9g-6)/9*π))^(2+1)", false)]
        public void PiIsPlacedInTheIntendedSpatialContainer(string source, bool inDenominator)
        {
            var formula = ExpressionPreview.Create(source, true).Formula;
            var layout = new TexLayoutEngine().Render(formula.Latex);
            var fraction = layout.Document.Descendants().Single(n => n.Kind == TexKind.Fraction);
            Assert.Equal(inDenominator, TexTestProjection.Snapshot(fraction.Children[1]).Contains("π"));
        }

        [Theory]
        [InlineData("a/b*c")][InlineData("a/(b*c)")][InlineData("a/b/c*x")]
        [InlineData("(a mod b)*c")][InlineData("a*(b mod c)")][InlineData("a/(2*x^2)")]
        public void CompactTextAndCopiedCodeRoundTripWithoutChangingDivisionScope(string source)
        {
            var expression = new ExpressionParser().Parse(source).Root;
            var engine = new CalculationEngine(); var options = Options(CalculationMode.Numeric);
            var expected = engine.Evaluate(source, options).Value.Code;
            Assert.Equal(expected, engine.Evaluate(expression.ToCode(), options).Value.Code);
            Assert.Equal(expected, engine.Evaluate(expression.ToDisplayString(), options).Value.Code);
        }
    }
}
