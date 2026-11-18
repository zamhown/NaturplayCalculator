using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests;

public sealed class CasioDisplayAndErrorTests
{
    [Theory]
    [InlineData("6÷2(1+2)", @"6\div 2\left(1+2\right)", "1")]
    [InlineData("6÷2*(1+2)", @"6\div 2\times \left(1+2\right)", "9")]
    [InlineData("6÷2π", @"6\div 2\pi ", null)]
    [InlineData("3÷2", @"3\div 2", "1.5")]
    [InlineData("3/2", @"\frac{3}{2}", "1.5")]
    public void InputDistinguishesDivisionFractionAndExplicitMultiplication(string source, string latex, string answer)
    {
        foreach (bool symbolic in new[] { false, true })
        {
            var preview = ExpressionPreview.Create(source, symbolic);
            Assert.Equal(latex, preview.Formula.Latex);
            Assert.NotEmpty(new TexLayoutEngine().Render(preview.Formula.Latex).Carets);
        }
        if (answer != null) Assert.Equal(answer, new CalculationEngine().Evaluate(source).Value.Code);
    }

    [Theory]
    [InlineData("14÷0×2", 3, 1)]
    [InlineData("2+1/(3-3)", 4, 5)]
    [InlineData("1+*2", 2, 1)]
    [InlineData("1.2.3", 3, 1)]
    [InlineData("sin(2", 5, 0)]
    public void ErrorLocationPointsAtTheFailingSource(string source, int start, int length)
    {
        foreach (var mode in new[] { CalculationMode.Numeric, CalculationMode.Symbolic })
        {
            var error = Assert.Throws<MathException>(() => new CalculationEngine().Evaluate(source, new CalculationOptions { Mode = mode }));
            Assert.Equal(start, error.SourceStart);
            Assert.Equal(length, error.SourceLength);
        }
    }
}
