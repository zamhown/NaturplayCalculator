using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using NaturplayMath.Serialization;
using Xunit;
namespace NaturplayMath.Tests.Symbolics;

public class MatrixAndStatisticsTests
{
    [Fact]
    public void DeterminantDoesNotInventSymbolicPivotConditions()
    {
        var x = new ComplexExpression(Expression.Symbol("x"));
        var a = new ExpressionMatrix(new[,] { { x, ComplexExpression.One }, { ComplexExpression.One, x } });
        var determinant = a.Determinant();
        Assert.Empty(determinant.Real.Conditions);
        Assert.Equal(Expression.Integer(-1), determinant.Real.Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(0) }));
        Assert.Equal(Expression.Integer(3), determinant.Real.Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(2) }));
    }

    [Fact]
    public void DeterminantsInversesAndRanksRemainExact()
    {
        var a = ExpressionMatrix.FromRationals(new Rational[,] { { new(2), new(3), new(5) }, { new(7), new(11), new(13) }, { new(17), new(19), new(23) } });
        Assert.Equal(ComplexExpression.Integer(-78), a.Determinant());
        var identity = a.Multiply(a.Inverse());
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) Assert.Equal(ComplexExpression.Integer(i == j ? 1 : 0), identity[i, j]);
        Assert.Equal(3, a.Rank());
        var singular = ExpressionMatrix.FromRationals(new Rational[,] { { new(1), new(2) }, { new(2), new(4) }, { new(3), new(6) } });
        Assert.Equal(1, singular.Rank());
        Assert.Equal(ComplexExpression.One, new ExpressionMatrix(new ComplexExpression[0, 0]).Determinant());
        Assert.Equal(a[1, 2], a.Transpose()[2, 1]);
        Assert.Equal(a.Pow(2)[0, 1], a.Multiply(a)[0, 1]);
        Assert.Equal(a.Determinant(), MathJson.DeserializeMatrix(MathJson.Serialize(a)).Determinant());
    }

    [Fact]
    public void RankNeverTreatsSmallNonzeroValuesAsZero()
    {
        var tiny = ComplexExpression.Constant(new Rational(1, IntegerMath.Pow(10, 1000)));
        var matrix = new ExpressionMatrix(new[,] { { tiny, ComplexExpression.Zero }, { ComplexExpression.Zero, new ComplexExpression(Expression.Pi) } });
        Assert.Throws<UndeterminedRankException>(() => matrix.Rank());
        Assert.Equal(2, matrix.RankCertified(new(new Rational(1, 1000))));
    }

    [Fact]
    public void PopulationStatisticsPreserveHugeOffsetsAndComplexConvention()
    {
        var offset = IntegerMath.Pow(10, 1000);
        var x = new[] { ComplexExpression.Integer(offset + 1), ComplexExpression.Integer(offset + 2), ComplexExpression.Integer(offset + 3) };
        var y = new[] { ComplexExpression.Integer(2), ComplexExpression.Integer(4), ComplexExpression.Integer(6) };
        Assert.Equal(ComplexExpression.Integer(offset + 2), SymbolicStatistics.Mean(x));
        Assert.Equal(ComplexExpression.Constant(new Rational(2, 3)), SymbolicStatistics.Variance(x));
        Assert.Equal(ComplexExpression.Constant(new Rational(4, 3)), SymbolicStatistics.Covariance(x, y));
        Assert.True(SymbolicStatistics.Correlation(x, y).Approximate(40).Real.Contains(Rational.One));
        Assert.Equal(ComplexExpression.Integer(-1), SymbolicStatistics.Variance(new[] { ComplexExpression.I, -ComplexExpression.I }));
        Assert.Throws<DivideByZeroException>(() => SymbolicStatistics.Correlation(new[] { ComplexExpression.One }, new[] { ComplexExpression.One }));
    }
}
