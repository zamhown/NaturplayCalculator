using System;
using System.Collections.Generic;
using System.Linq;
using NaturplayCalculator.Expressions;
using Naturplay.Tex;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class TexGroupingTests
    {
        // Braces in these snapshots denote spatial containers (bar, radical or raised
        // exponent), whereas parentheses denote actual visible grouping glyphs.
        [Theory]
        [InlineData("sqrt(8)+sqrt(2)", "√{8}+√{2}")]
        [InlineData("2sqrt(2)", "2√{2}")]
        [InlineData("sqrt(a+b)", "√{a+b}")]
        [InlineData("sqrt(sqrt(a))", "√{√{a}}")]
        [InlineData("sqrt(-8)", "√{-8}")]
        [InlineData("sqrt(a/b)", "√{{a}/{b}}")]
        [InlineData("sin(a)", "sin(a)")]
        [InlineData("sin(a+b)", "sin(a+b)")]
        [InlineData("sin(cos(a))", "sin(cos(a))")]
        [InlineData("ln(sqrt(a))", "ln(√{a})")]
        [InlineData("Abs(a+b)", "|a+b|")]
        [InlineData("Abs(Abs(a))", "||a||")]
        [InlineData("(a+b)/(c+d)", "{a+b}/{c+d}")]
        [InlineData("(a/b)/(c/d)", "{{a}/{b}}/{{c}/{d}}")]
        [InlineData("a/(b/c)", "{a}/{{b}/{c}}")]
        [InlineData("(a+b)^(c+d)", "(a+b)^{c+d}")]
        [InlineData("a^(b^c)", "a^{b^{c}}")]
        [InlineData("(a^b)^c", "(a^{b})^{c}")]
        [InlineData("(-a)^2", "(-a)^{2}")]
        [InlineData("-a^2", "-a^{2}")]
        [InlineData("-(a+b)", "-(a+b)")]
        [InlineData("-(a/b)", "-{a}/{b}")]
        [InlineData("a*(-b)", "a*(-b)")]
        [InlineData("a-(b-c)", "a-(b-c)")]
        [InlineData("(a+b)c+bc", "(a+b)c+bc")]
        [InlineData("a*(b+c)", "a(b+c)")]
        [InlineData("a*sin(b)", "a*sin(b)")]
        [InlineData("(sqrt(a))^2", "√{a}^{2}")]
        [InlineData("sin(a)^2", "sin(a)^{2}")]
        [InlineData("Abs(a)^2", "|a|^{2}")]
        [InlineData("(a/b)^2", "({a}/{b})^{2}")]
        [InlineData("(a+b)!", "(a+b)!")]
        [InlineData("(-a)!", "(-a)!")]
        [InlineData("(a^2)!", "(a^{2})!")]
        [InlineData("(a!)!", "(a!)!")]
        [InlineData("a!!", "a!!")]
        [InlineData("(a+b)yroot(c+d)", "root{c+d}{a+b}")]
        [InlineData("(a+b)nCr(c+d)", "C{a+b}{c+d}")]
        [InlineData("(a+b)^T", "(a+b)^{T}")]
        [InlineData("(a^T)^2", "(a^{T})^{2}")]
        [InlineData("sin((a,b))", "sin[a,b]")]
        [InlineData("Abs((a,b,,c,d))", "|a,b;c,d|")]
        [InlineData("(a,b,,c,d)^T", "[a,b;c,d]^{T}")]
        [InlineData("((a+b),sqrt(c),,d/e,-a)", "[a+b,√{c};{d}/{e},-a]")]
        public void VisibleGroupingFollowsMathematicalContainers(string input, string expected)
        {
            Assert.Equal(expected, Snapshot(TexTestProjection.Document(new ExpressionParser().Parse(input).Root, true)));
        }

        [Theory]
        [InlineData("sqrt(8)+sqrt(2)", "3√{2}")]
        [InlineData("1/3+1/6", "{1}/{2}")]
        [InlineData("(a+b)c+bc", "ac+2bc")]
        [InlineData("1/sqrt(2)", "{√{2}}/{2}")]
        [InlineData("sqrt(x^2)", "|x|")]
        public void SymbolicResultsUseTheSameLayoutAsInputAndSteps(string input, string expected)
        {
            var result = new CalculationEngine().Evaluate(input, new CalculationOptions { Mode = CalculationMode.Symbolic, CaptureSteps = true });
            Assert.Equal(expected, Snapshot(TexTestProjection.Document(result.Value.Expression, true)));
            Assert.All(result.Steps, step => Assert.NotEmpty(Snapshot(TexTestProjection.Document(step, true))));
        }

        [Fact]
        public void EveryBuiltInFunctionHasExactlyItsOwnArgumentBoundary()
        {
            string[] names = { "sin", "cos", "tan", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh", "arsh", "arch", "arth", "ln", "lg", "E", "D", "Cov", "rho", "rank", "sinrad", "cosrad", "tanrad" };
            foreach (string name in names)
            {
                var layout = TexTestProjection.Document(ExpressionNode.Function(name, ExpressionNode.Symbol("a")));
                Assert.Equal(1, layout.Descendants().Count(n => n.Kind == TexKind.Delimited));
                Assert.Single(layout.Descendants(), n => n.Kind == TexKind.Glyph && n.Text == "a");
                Assert.Equal(2, layout.Descendants().Count(n => n.Kind == TexKind.Glyph));
            }
        }

        [Theory]
        [InlineData("1.25e-10", "1.25*10^{-10}")]
        [InlineData("(-1.25e-10)^2", "(-1.25*10^{-10})^{2}")]
        [InlineData("a+(-2)", "a+(-2)")]
        [InlineData("a*(-2)", "a*(-2)")]
        public void ScientificNotationAndSignedOperandsAreNotDoubleGrouped(string input, string expected)
        {
            Assert.Equal(expected, Snapshot(TexTestProjection.Document(new ExpressionParser().Parse(input).Root, true)));
        }

        [Fact]
        public void NegativeNumericResultsAndParsedNegativeInputHaveTheSameVisibleGrouping()
        {
            var literal = ExpressionNode.Number("-2");
            var parsed = new ExpressionParser().Parse("-2").Root;
            foreach (string op in new[] { "+", "-", "*", "/", "^" })
            {
                Assert.Equal(Snapshot(TexTestProjection.Document(ExpressionNode.Binary(op, parsed, parsed))),
                    Snapshot(TexTestProjection.Document(ExpressionNode.Binary(op, literal, literal))));
            }
        }

        [Theory]
        [InlineData("(a+b)c+bc")]
        [InlineData("a-(b-c)")]
        [InlineData("a+(b-c)")]
        [InlineData("-(a+b)*c")]
        [InlineData("a*(-b)")]
        [InlineData("(-a)^2")]
        [InlineData("-a^2")]
        [InlineData("(a^2)^3")]
        [InlineData("a^(2^3)")]
        [InlineData("(a/b)^(c+1)")]
        [InlineData("(a+b)/(c+d)")]
        [InlineData("(a/b)/(c/d)")]
        [InlineData("a/(b/c)")]
        [InlineData("sqrt(a+b)+sqrt(c)")]
        [InlineData("sqrt(a^2)^2")]
        [InlineData("sin(a)^2+cos(a)^2")]
        [InlineData("a*sin(b)")]
        [InlineData("Abs(a+b)")]
        [InlineData("(a mod b)*c")]
        [InlineData("a*(b mod c)")]
        [InlineData("a mod (b*c)")]
        [InlineData("(a^2)yroot3")]
        [InlineData("((1,2,,3,4)^T)^2")]
        [InlineData("(2^3)!")]
        [InlineData("(3!)!")]
        public void VisibleGroupingPreservesTheMathematicalMeaning(string input)
        {
            var engine = new CalculationEngine();
            var original = new ExpressionParser().Parse(input).Root;
            foreach (bool implicitProducts in new[] { false, true })
            {
                string rendered = Linearize(TexTestProjection.Document(original, implicitProducts));
                foreach (double a in new[] { -2.5, 0.5, 3.25 })
                {
                    var options = new CalculationOptions { Variables = new Dictionary<string, MathValue>
                    { ["a"] = new MathValue(a), ["b"] = new MathValue(1.5), ["c"] = new MathValue(2), ["d"] = new MathValue(4) } };
                    var expected = engine.Evaluate(input, options).Answer;
                    var actual = engine.Evaluate(rendered, options).Answer;
                    Assert.Equal(expected.ToString(), actual.ToString());
                }
            }
        }

        private static string Snapshot(TexDocument document) => TexTestProjection.Snapshot(document.Root);
        private static string Linearize(TexDocument document) => TexTestProjection.Linearize(document.Root);
    }
}
