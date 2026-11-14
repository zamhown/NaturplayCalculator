using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using NaturplayMath.Serialization;
using Xunit;

namespace NaturplayMath.Tests.Numerics;

public class ExtendedFunctionTests
{
    [Theory]
    [InlineData("asin", "0.30469265401539750797200296122752916695456003170677")]
    [InlineData("atan", "0.29145679447786709199560462143289119350316759901206")]
    [InlineData("asinh", "0.29567304756342243910271052973351708245138101183537")]
    [InlineData("atanh", "0.30951960420311171547406734906106943758409160758917")]
    [InlineData("acosh", "1.76274717403908605046521864995958461805632065652327")]
    [InlineData("tanh", "0.99505475368673045133188018525548847509781385470028")]
    [InlineData("cube", "1.25992104989487316476721060727822835057025146470150")]
    [InlineData("power", "2.66514414269022518865029724987313984827421131371465")]
    public void ExtendedFunctionsMatchIndependentReference(string name, string expected)
    {
        var x = Expression.Constant(new Rational(3, 10));
        var expression = name switch
        {
            "asin" => x.Asin(),
            "atan" => x.Atan(),
            "asinh" => x.Asinh(),
            "atanh" => x.Atanh(),
            "acosh" => Expression.Integer(3).Acosh(),
            "tanh" => Expression.Integer(3).Tanh(),
            "cube" => Expression.Integer(2).NthRoot(3),
            _ => Expression.Integer(2).Pow(Expression.Integer(2).Sqrt())
        };
        Assert.Equal(expression, MathJson.DeserializeExpression(MathJson.Serialize(expression)));
        Assert.True(expression.Approximate(70).TryFormat(new NumberFormat(50, trimTrailingZeros: false), out var text));
        Assert.Equal(expected, text);
    }

    [Fact]
    public void RootsAndRationalPowersKeepExactValuesAndDomains()
    {
        Assert.Equal(Expression.Integer(-2), Expression.Integer(-8).NthRoot(3));
        Assert.Equal(Expression.Constant(new Rational(9, 4)), Expression.Constant(new Rational(27, 8)).Pow(new Rational(2, 3)));
        Assert.Throws<MathDomainException>(() => Expression.Integer(-8).NthRoot(2));
        var x = Expression.Symbol("x"); var y = Expression.Symbol("y");
        var expression = x.Pow(y) - x.Pow(y);
        Assert.Throws<MathDomainException>(() => expression.Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(0), ["y"] = Expression.Integer(0) }));
        var symbolicZeroPower = x.Pow(y).Substitute(new Dictionary<string, Expression> { ["y"] = Expression.Integer(0) });
        Assert.NotEmpty(symbolicZeroPower.Conditions);
        Assert.Throws<MathDomainException>(() => symbolicZeroPower.Substitute(new Dictionary<string, Expression> { ["x"] = Expression.Integer(0) }));
        Assert.True(Expression.Integer(-2).NthRoot(3).Approximate(50).Upper.Sign < 0);
    }

    [Fact]
    public void InverseAndHyperbolicIdentitiesEncloseExactValues()
    {
        var x = Expression.Constant(new Rational(3, 10));
        Assert.True(x.Asin().Sin().Approximate(50).Contains(new Rational(3, 10)));
        Assert.True(x.Sinh().Asinh().Approximate(50).Contains(new Rational(3, 10)));
        Assert.True((x.Cosh().Pow(2) - x.Sinh().Pow(2)).Approximate(50).Contains(Rational.One));
        Assert.Throws<MathDomainException>(() => Expression.Integer(2).Asin());
        Assert.Throws<MathDomainException>(() => Expression.Integer(1).Atanh());
        var saturated = Expression.Integer(1000000).Tanh().Approximate(70);
        Assert.True(saturated.Lower > new Rational(999, 1000) && saturated.Upper <= Rational.One);
    }

    [Fact]
    public void IntegerRootBoundsHoldAtLargeSizesAndDegrees()
    {
        var root = IntegerMath.Pow(10, 1000) + 123;
        var cube = IntegerMath.Pow(root, 3);
        Assert.Equal(root, IntegerMath.RootFloor(cube, 3));
        Assert.Equal(root - 1, IntegerMath.RootFloor(cube - 1, 3));
        Assert.Equal(root, IntegerMath.RootFloor(cube + 1, 3));
        Assert.Equal(System.Numerics.BigInteger.One, IntegerMath.RootFloor(5, IntegerMath.Pow(10, 100)));
    }
}
