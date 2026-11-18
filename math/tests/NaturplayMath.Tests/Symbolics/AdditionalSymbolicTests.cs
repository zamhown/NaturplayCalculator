using System.Numerics;
using NaturplayMath.Numbers;
using NaturplayMath.Serialization;
using NaturplayMath.Symbolics;
using NaturplayMath.Tests.Numerics;
using Xunit;

namespace NaturplayMath.Tests.Symbolics;

public class AdditionalSymbolicTests
{
    [Fact]
    public void ComplexIntegerPowersMatchRepeatedExactMultiplication()
    {
        var z = ComplexExpression.Constant(new Rational(1), new Rational(2));
        var power = ComplexExpression.One;
        for (int exponent = 0; exponent <= 9; exponent++)
        {
            Assert.Equal(power, z.Pow(new BigInteger(exponent)));
            Assert.Equal(power, z.Pow(ComplexExpression.Integer(exponent), new()));
            Assert.Equal(ComplexExpression.One, z.Pow(new BigInteger(-exponent), new()) * power);
            power *= z;
        }
        Assert.Throws<MathDomainException>(() => ComplexExpression.Zero.Pow(BigInteger.Zero));
        Assert.Equal(ComplexExpression.Zero, ComplexExpression.Zero.Pow(ComplexExpression.Constant(new Rational(1, 3))));
        Assert.Throws<MathDomainException>(() => ComplexExpression.Zero.Pow(ComplexExpression.I));
        Assert.Equal(ComplexExpression.I, ComplexExpression.Integer(-1).Pow(ComplexExpression.Constant(new Rational(1, 2))));
        var unknown = new ComplexExpression(Expression.Symbol("x"));
        Assert.Throws<MathDomainException>(() => unknown.Pow(BigInteger.Zero).Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(0) }));
        Assert.Equal(ComplexExpression.One, unknown.Pow(BigInteger.Zero).Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(2) }));
    }

    [Fact]
    public void ComplexPowerNodesSimplifyAfterDelayedSubstitution()
    {
        var x = new ComplexExpression(Expression.Symbol("x"));
        var y = new ComplexExpression(Expression.Symbol("y"));
        var p = x.Pow(y);
        foreach (var (a, b, expected) in new[] { (1, 2, 1), (2, 3, 8), (0, 3, 0) })
            Assert.Equal(ComplexExpression.Integer(expected), p.Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(a), ["y"] = Expression.Integer(b) }));
        Assert.Equal(ComplexExpression.One, ComplexExpression.One.Pow(ComplexExpression.I));
        var root = ComplexExpression.Constant(new Rational(-3), new Rational(-4)).Sqrt();
        Assert.Equal(ComplexExpression.Constant(new Rational(1), new Rational(-2)), root);
        var zeroExponent = p.Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(0), ["y"] = Expression.Constant(new Rational(1, 3)) });
        Assert.Equal(ComplexExpression.Zero, zeroExponent);
        Assert.Throws<MathDomainException>(() => p.Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(0), ["y"] = Expression.Constant(new Rational(-1, 3)) }));
    }

    [Fact]
    public void SpecialAnglesAgreeWithIndependentTrigonometryAcrossAllQuadrants()
    {
        for (int multiple = -48; multiple <= 48; multiple++)
        {
            var angle = Expression.Pi * Expression.Constant(new Rational(multiple, 12));
            ReferenceAssert.Near(Math.Sin(multiple * Math.PI / 12), angle.Sin().Approximate(20));
            ReferenceAssert.Near(Math.Cos(multiple * Math.PI / 12), angle.Cos().Approximate(20));
        }
        foreach (var text in new[] { "sin(2*pi*x)", "sin(x*y)", "sin(2*x)", "sin(x*y*z)" })
            Assert.Equal(ExpressionParser.Parse(text), MathJson.DeserializeExpression(MathJson.Serialize(ExpressionParser.Parse(text))));
        Assert.Equal(Expression.Integer(0), Expression.Integer(0).Asinh());
        Assert.Equal(Expression.Integer(0), Expression.Integer(0).Tanh());
        Assert.Equal(Expression.Integer(2), Expression.Integer(100).Log10());
        Assert.Equal(Expression.Integer(0), Expression.Integer(1).Log(Expression.Integer(2)));
        Assert.Equal(Expression.Integer(-3), Expression.Integer(8).Log(Expression.Constant(new Rational(1, 2))));
        Assert.Equal(Expression.Integer(3), Expression.Constant(new Rational(1, 8)).Log(Expression.Constant(new Rational(1, 2))));
        foreach (var (v, b) in new[] { (0, 2), (2, 0), (2, 1) })
            Assert.Throws<MathDomainException>(() => Expression.Integer(v).Log(Expression.Integer(b)));
        foreach (var (value, basis) in new[] { (new Rational(100), new Rational(3, 2)), (new Rational(100, 3), new Rational(3, 2)), (new Rational(10), new Rational(2)) })
            ReferenceAssert.Near(Math.Log((double)value.Numerator / (double)value.Denominator, (double)basis.Numerator / (double)basis.Denominator), Expression.Constant(value).Log(Expression.Constant(basis)).Approximate(20));
    }

    [Fact]
    public void RootsRespectParityAndRejectInvalidDegreesAndDiscreteArguments()
    {
        var x = Expression.Symbol("x");
        Assert.Equal(x, x.NthRoot(1));
        Assert.Equal(x, x.Pow(3).NthRoot(3));
        Assert.Equal(x.Abs(), x.Pow(4).NthRoot(4));
        Assert.Equal(x, x.NthRoot(3).Pow(3));
        Assert.Equal(Expression.Integer(-2), Expression.Integer(-8).NthRoot(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => x.NthRoot(0));
        Assert.Throws<MathDomainException>(() => Expression.Integer(-16).NthRoot(4));
        Assert.Throws<MathDomainException>(() => Expression.Constant(new Rational(3, 2)).Factorial());
        Assert.Throws<MathDomainException>(() => Expression.Integer(3).Choose(Expression.Constant(new Rational(1, 2))));
        var root = x.Pow(2).NthRoot(3);
        Assert.False(root.TryGetRational(out _));
        var session = new ComputationContext().Start();
        Assert.Throws<InvalidOperationException>(() => ExpressionNormalizer.Discrete(NodeKind.Exp, [Node.Constant(Rational.One)], session));
        Assert.Equal(Node.Constant(new Rational(4)).Key, ExpressionNormalizer.GeneralPower(Node.Constant(new Rational(2)), Node.Constant(new Rational(2)), session).Key);
        Assert.Equal(Node.Constant(Rational.One).Key, ExpressionNormalizer.GeneralPower(Node.Constant(Rational.One), x.Root, session).Key);
    }

    [Fact]
    public void ReconstructingIntrinsicDomainsVisitsEveryConstrainedNode()
    {
        foreach (string input in new[] { "x^-2", "x^2", "sqrt(x)", "root(x,4)", "root(x,3)", "x^y", "fact(x)", "dfact(x)", "choose(x,y)", "permute(x,y)", "arg(x)", "asin(x)", "ln(x)+ln(x)*y" })
        {
            var original = ExpressionParser.Parse(input);
            var rebuilt = Expression.WithIntrinsicConditions(original.Root);
            Assert.Equal(original, rebuilt);
        }
        var z = ExpressionParser.ParseComplex("(x+i)^(y+i)");
        Assert.Equal(z, new ComplexExpression(Expression.WithIntrinsicConditions(z.Real.Root), Expression.WithIntrinsicConditions(z.Imaginary.Root)));
    }

    [Fact]
    public void ExpressionEqualityAndIdentifiersHaveExplicitContracts()
    {
        var x = Expression.Symbol("x"); var same = Expression.Symbol("x"); var y = Expression.Symbol("y");
        Assert.True(x.Equals((object)same)); Assert.False(x.Equals((object)y));
        Assert.False(x.Equals((object?)null)); Assert.False(x.Equals("x")); Assert.False(x.Equals((Expression?)null));
        Assert.True(x == same); Assert.True(x != y); Assert.False(x == y);
        Expression? missing = null;
        Assert.True(missing == null); Assert.False(missing == x); Assert.False(x == missing);
        Assert.Equal(x.GetHashCode(), same.GetHashCode());
        Assert.Throws<ArgumentNullException>(() => new Expression(null!, []));
        foreach (string? name in new[] { null, "", " ", "1x", "x?" }) Assert.Throws<ArgumentException>(() => Expression.Symbol(name!));
        Assert.Equal("V(2:_a)|", Expression.Symbol("_a").ToString());
        Assert.Equal("V(2:x1)|", Expression.Symbol("x1").ToString());
    }
}
