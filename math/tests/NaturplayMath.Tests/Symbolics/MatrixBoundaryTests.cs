using System.Numerics;
using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using Xunit;

namespace NaturplayMath.Tests.Symbolics;

public class MatrixBoundaryTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MatrixAdditionScalingTransposeAndPowersRespectAlgebra(bool explicitContext)
    {
        var context = explicitContext ? new ComputationContext() : null;
        var a = ExpressionMatrix.FromRationals(new Rational[,] { { new(1), new(2) }, { new(3), new(4) } });
        var b = a.Transpose();
        var sum = a.Add(b, context); var scaled = a.Scale(ComplexExpression.I, context);
        Assert.Equal(ComplexExpression.Integer(5), sum[0, 1]);
        Assert.Equal(ComplexExpression.Integer(8), sum[1, 1]);
        Assert.Equal(ComplexExpression.Constant(default, new Rational(3)), scaled[1, 0]);
        Assert.Equal(-scaled[1, 0], scaled.Transpose(true)[0, 1]);
        var square = a.Pow(2, context); var cube = a.Pow(3, context);
        Assert.Equal(ComplexExpression.Integer(7), square[0, 0]);
        Assert.Equal(ComplexExpression.Integer(54), cube[0, 1]);
        Assert.Equal(ComplexExpression.One, a.Pow(0, context)[1, 1]);
        var identity = a.Multiply(a.Pow(-1, context), context);
        Assert.Equal(ComplexExpression.One, identity[0, 0]); Assert.Equal(ComplexExpression.Zero, identity[0, 1]);
    }

    [Fact]
    public async Task MatrixApproximationSubstitutesVariablesAndClonesItsStorage()
    {
        var values = new[,] { { new ComplexExpression(Expression.Symbol("x"), Expression.Pi), ComplexExpression.One } };
        var matrix = new ExpressionMatrix(values);
        values[0, 0] = ComplexExpression.Zero;
        var variables = new Dictionary<string, Rational> { ["x"] = new(7, 3) };
        var options = new ApproximationOptions(new Rational(1, BigInteger.Pow(10, 20)));
        var result = await matrix.ApproximateAsync(options, variables, new());
        Assert.Equal(1, result.Rows); Assert.Equal(2, result.Columns);
        Assert.Equal(RealInterval.Exact(new Rational(7, 3)), result[0, 0].Real);
        Numerics.ReferenceAssert.Near(Math.PI, result[0, 0].Imaginary);
        var second = await ExpressionMatrix.Identity(1).ApproximateAsync(options);
        Assert.Equal(RealInterval.Exact(Rational.One), second[0, 0].Real);
        var source = new[,] { { second[0, 0] } };
        var copy = new MatrixEnclosure(source);
        source[0, 0] = result[0, 0];
        Assert.Equal(second[0, 0], copy[0, 0]);
        Assert.Equal(0, ExpressionMatrix.Identity(0).Approximate(options).Rows);
    }

    [Fact]
    public void RankHandlesZeroRowsComplexPivotsAndUnboundValues()
    {
        Assert.Equal(0, ExpressionMatrix.Identity(0).Rank());
        var m = new ExpressionMatrix(new[,] { { ComplexExpression.Zero, ComplexExpression.I }, { ComplexExpression.Zero, ComplexExpression.Zero } });
        Assert.Equal(1, m.Rank());
        Assert.Equal(1, m.RankCertified(new(new Rational(1, 100))));
        var symbolic = new ExpressionMatrix(new[,] { { new ComplexExpression(Expression.Symbol("x")) } });
        Assert.Throws<UndeterminedRankException>(() => symbolic.Rank());
        Assert.Equal(0, symbolic.RankCertified(new(new Rational(1, 100)), new Dictionary<string, Rational> { ["x"] = Rational.Zero }));
        Assert.Equal(1, symbolic.RankCertified(new(new Rational(1, 100)), new Dictionary<string, Rational> { ["x"] = Rational.One }));
        var imaginarySymbol = new ExpressionMatrix(new[,] { { new ComplexExpression(Expression.Integer(0), Expression.Symbol("x")) } });
        Assert.Throws<UndeterminedRankException>(() => imaginarySymbol.Rank());
        var guarded = ExpressionParser.ParseComplex("im(ln(x))");
        Assert.NotEmpty(guarded.Real.Conditions);
        Assert.Equal(guarded.Real.Conditions.Select(c => c.ToString()), guarded.Imaginary.Conditions.Select(c => c.ToString()));
        Assert.Throws<UndeterminedRankException>(() => new ExpressionMatrix(new[,] { { guarded } }).Rank());
    }

    [Fact]
    public void InvalidShapesAndSamplesFailExplicitly()
    {
        var a = ExpressionMatrix.Identity(2);
        Assert.Throws<ArgumentOutOfRangeException>(() => ExpressionMatrix.Identity(-1));
        Assert.Throws<ArgumentException>(() => a.Add(ExpressionMatrix.Identity(1)));
        Assert.Throws<ArgumentException>(() => a.Add(new(new ComplexExpression[2, 0])));
        Assert.Throws<ArgumentException>(() => a.Multiply(ExpressionMatrix.Identity(1)));
        var rectangle = new ExpressionMatrix(new ComplexExpression[0, 1]);
        Assert.Throws<MathDomainException>(() => rectangle.Determinant());
        Assert.Throws<ArgumentNullException>(() => a.Add(null!));
        Assert.Throws<ArgumentNullException>(() => a.Scale(null!));
        Assert.Throws<ArgumentNullException>(() => a.Multiply(null!));
        Assert.Throws<ArgumentNullException>(() => new ExpressionMatrix(new ComplexExpression[1, 1]));
        Assert.Throws<ArgumentNullException>(() => SymbolicStatistics.Mean(null!));
        Assert.Throws<ArgumentException>(() => SymbolicStatistics.Mean([]));
        Assert.Throws<ArgumentNullException>(() => SymbolicStatistics.Mean([null!]));
        Assert.Throws<ArgumentException>(() => SymbolicStatistics.Covariance([ComplexExpression.One], [ComplexExpression.One, ComplexExpression.One]));
        Assert.Throws<ArgumentNullException>(() => a.Approximate(null!));
        Assert.Throws<ArgumentNullException>(() => MatrixNumericalExtensions.Approximate(null!, new(new Rational(1, 10))));
        Assert.Equal(ComplexExpression.One, SymbolicStatistics.Covariance([ComplexExpression.Zero, ComplexExpression.Integer(2)], [ComplexExpression.Zero, ComplexExpression.Integer(2)], new()));
    }
}
