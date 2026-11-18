using System;
using System.Collections.Generic;
using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class TexProtocolTests
    {
        [Theory]
        [InlineData("1/3+1/6", @"\frac{1}{2}")]
        [InlineData("sqrt(8)+sqrt(2)", @"3\sqrt{2}")]
        [InlineData("(a+b)c+bc", "ac+2bc")]
        [InlineData("sqrt(sin((9a+6)/6))", @"\sqrt{\sin \left(\frac{3a+2}{2}\right)}")]
        public void SymbolicAnswersUseStandardTexWithoutInternalNames(string input, string expected)
        {
            var result = new CalculationEngine().Evaluate(input, new CalculationOptions { Mode = CalculationMode.Symbolic });
            var message = new ExpressionTexFormatter().Format(result.Value.Expression, true);
            Assert.Equal(expected, message.Latex);
            Assert.NotEmpty(new TexLayoutEngine().Render(message.Latex).Commands);
            Assert.DoesNotContain("rad", message.Latex);
        }

        [Theory]
        [InlineData("(1.25e3)^2")][InlineData("(-1.25e-3)^2")]
        [InlineData("(2e0)!")][InlineData("(5nCr2)^2")][InlineData("(5nPr2)^2")]
        [InlineData("((2yroot3)^2)yroot3")][InlineData("1.25e-3+2e-3")]
        [InlineData("(1.25e3)/(2e4)")][InlineData("(1.25e3)^T")]
        public void ScientificNumbersAndCompoundScriptsKeepTheirMeaning(string input)
        {
            var tree = new ExpressionParser().Parse(input).Root;
            var tex = TexTestProjection.Document(tree);
            new TexLayoutEngine().Render(tex);
            string reconstructed = TexTestProjection.Linearize(tex.Root);
            var engine = new CalculationEngine();
            var expected = engine.Evaluate(input).Answer;
            var actual = engine.Evaluate(reconstructed).Answer;
            Assert.Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public void RendererAndCalculationAssembliesHaveNoDependencyOnEachOther()
        {
            Assert.DoesNotContain(typeof(TexParser).Assembly.GetReferencedAssemblies(), a => a.Name.Contains("Calculator") || a.Name.StartsWith("Windows"));
            Assert.DoesNotContain(typeof(CalculationEngine).Assembly.GetReferencedAssemblies(), a => a.Name == "Naturplay.Tex" || a.Name.StartsWith("Naturplay.Tex.") || a.Name.StartsWith("Windows"));
            Assert.DoesNotContain(typeof(TexRenderResult).GetProperties(), p => p.PropertyType == typeof(ExpressionNode));
            Assert.DoesNotContain(typeof(NaturplayCalculator.Input.FormulaCaretMap).Assembly.GetReferencedAssemblies(),
                a => a.Name.StartsWith("Windows") || a.Name.StartsWith("Microsoft.UI") || (a.Name.EndsWith(".Uwp") || a.Name.EndsWith(".WinUI")));
        }

        [Fact]
        public void TransportStringWorksWithoutSourceMetadataAndMetadataDoesNotLeakIntoTex()
        {
            const string input = "sqrt(a/b)+12.3456";
            var source = new ExpressionParser().Parse(input).Root;
            var message = new ExpressionTexFormatter().Format(source, true);
            var layout = new TexLayoutEngine().Render(message.Latex);
            foreach (var glyph in layout.Document.Descendants().Where(n => n.Kind == TexKind.Glyph))
            {
                int index = message.InputIndexAt(glyph.Span.Start);
                Assert.InRange(index, 0, input.Length - 1);
                Assert.Equal(glyph.Text, input[index].ToString());
            }
            Assert.Equal(@"\sqrt{\frac{a}{b}}+12.3456", message.Latex);
            Assert.Equal(0, message.InputIndexAt(0));
            Assert.Null(message.RangeAtInput(-1));
            Assert.Equal(-1, message.InputIndexAt(-1));
        }

        [Fact]
        public void FoldingOnlyChangesRequestedDisplayAndMapsEveryHiddenDigit()
        {
            var tree = new ExpressionParser().Parse("12.345678").Root;
            var formatter = new ExpressionTexFormatter();
            var folded = formatter.Format(tree, true, true);
            Assert.Equal(@"12.345\ldots ", folded.Latex);
            Assert.Equal(6, folded.RangeAtInput(8).InputStart);
            Assert.Equal(3, folded.RangeAtInput(8).InputLength);
            Assert.Equal("12.345678", formatter.Format(tree).Latex);
            Assert.Equal("12.345678", tree.ToCode());
        }

        [Theory]
        [InlineData("1/3+1/6")][InlineData("sqrt(8)+sqrt(2)")][InlineData("(a+b)c+bc")]
        [InlineData("(1,2,,3,4)^-1")][InlineData("i^2+2i")][InlineData("1e-16")]
        [InlineData("sin(pi/6)")][InlineData("Abs((1,2,,3,4))")]
        [InlineData("D(1,2,3)")][InlineData("Cov(1,2,,3,4)")]
        public void BothCalculationModesInputsStepsAndResultsUseTheSameProtocol(string input)
        {
            foreach (CalculationMode mode in Enum.GetValues(typeof(CalculationMode)))
            {
                var result = new CalculationEngine().Evaluate(input, new CalculationOptions { Mode = mode, CaptureSteps = true,
                    Variables = new Dictionary<string, MathValue> { ["a"] = new MathValue(1), ["b"] = new MathValue(2), ["c"] = new MathValue(3) } });
                foreach (var expression in new[] { result.Expression, result.Value.Expression }.Concat(result.Steps))
                {
                    var message = new ExpressionTexFormatter().Format(expression, mode == CalculationMode.Symbolic);
                    Assert.NotEmpty(new TexLayoutEngine().Render(message.Latex).Commands);
                }
            }
        }
    }
}
