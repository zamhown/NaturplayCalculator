using System.Numerics;
using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
using NaturplayMath.Tests.Numerics;
using Xunit;

namespace NaturplayMath.Tests.Text;

public class FunctionOracleTests
{
    public static IEnumerable<object[]> RealCases()
    {
        (string Names, string Input, double Expected)[] cases =
        [
            ("sqrt", "2", Math.Sqrt(2)), ("root", "2,3", Math.Cbrt(2)),
            ("pow", "2,1.5", Math.Pow(2, 1.5)), ("exp", "0.5", Math.Exp(.5)),
            ("ln", "2", Math.Log(2)), ("lg", "2", Math.Log10(2)), ("log", "3,2", Math.Log(3, 2)),
            ("abs", "-0.5", .5), ("sin sinrad", "0.5", Math.Sin(.5)),
            ("cos cosrad", "0.5", Math.Cos(.5)), ("tan tanrad", "0.5", Math.Tan(.5)),
            ("asin arcsin asinrad arcsinrad", "0.5", Math.Asin(.5)),
            ("acos arccos acosrad arccosrad", "0.5", Math.Acos(.5)),
            ("atan arctan atanrad arctanrad", "0.5", Math.Atan(.5)),
            ("sinh", "0.5", Math.Sinh(.5)), ("cosh", "0.5", Math.Cosh(.5)), ("tanh", "0.5", Math.Tanh(.5)),
            ("asinh arsh", "0.5", Math.Asinh(.5)), ("acosh arch", "2", Math.Acosh(2)),
            ("atanh arth", "0.5", Math.Atanh(.5)), ("fact factorial", "5", 120),
            ("dfact doublefactorial", "7", 105), ("floor", "-1.5", -2), ("ceil ceiling", "-1.5", -1),
            ("trunc truncate", "-1.5", -1), ("choose ncr", "7,3", 35), ("permute npr", "7,3", 210),
            ("mod", "-7,3", -1), ("conj re", "-0.5", -.5), ("im", "3", 0), ("arg", "-3", Math.PI)
        ];
        foreach (var (names, input, expected) in cases)
            foreach (var name in names.Split(' ')) yield return [name + "(" + input + ")", expected];
    }

    [Theory]
    [MemberData(nameof(RealCases))]
    public void EveryRealFunctionAliasMatchesIndependentReference(string text, double expected)
    {
        ReferenceAssert.Near(expected, ExpressionParser.Parse(text).Approximate(20));
        var complex = ExpressionParser.ParseComplex(text).Approximate(20);
        ReferenceAssert.Near(expected, complex.Real);
        ReferenceAssert.Near(0, complex.Imaginary);
    }

    public static IEnumerable<object[]> AngleCases()
    {
        foreach (string name in new[] { "sin", "cos", "tan" })
            yield return [name + "(37)", name switch { "sin" => Math.Sin(37 * Math.PI / 180), "cos" => Math.Cos(37 * Math.PI / 180), _ => Math.Tan(37 * Math.PI / 180) }];
        foreach (string name in new[] { "asin", "arcsin", "acos", "arccos", "atan", "arctan" })
            yield return [name + "(0.3)", (name is "asin" or "arcsin" ? Math.Asin(.3) : name is "acos" or "arccos" ? Math.Acos(.3) : Math.Atan(.3)) * 180 / Math.PI];
        foreach (string name in new[] { "sinrad", "cosrad", "tanrad", "asinrad", "arcsinrad", "acosrad", "arccosrad", "atanrad", "arctanrad" })
            yield return [name + "(0.3)", name switch
            {
                "sinrad" => Math.Sin(.3), "cosrad" => Math.Cos(.3), "tanrad" => Math.Tan(.3),
                "asinrad" or "arcsinrad" => Math.Asin(.3), "acosrad" or "arccosrad" => Math.Acos(.3), _ => Math.Atan(.3)
            }];
    }

    [Theory]
    [MemberData(nameof(AngleCases))]
    public void DegreeModeConvertsAnglesButRadAliasesRemainRadians(string text, double expected)
    {
        var options = new ExpressionParseOptions(angleUnit: AngleUnit.Degrees);
        ReferenceAssert.Near(expected, ExpressionParser.Parse(text, options).Approximate(20));
        var result = ExpressionParser.ParseComplex(text, options).Approximate(20);
        ReferenceAssert.Near(expected, result.Real);
        ReferenceAssert.Near(0, result.Imaginary);
    }

    public static IEnumerable<object[]> ComplexCases()
    {
        var z = new Complex(.3, -.7);
        (string Names, Complex Expected)[] cases =
        [
            ("sqrt", Complex.Sqrt(z)), ("exp", Complex.Exp(z)), ("ln", Complex.Log(z)), ("lg", Complex.Log10(z)),
            ("abs", new(z.Magnitude, 0)), ("sin sinrad", Complex.Sin(z)), ("cos cosrad", Complex.Cos(z)),
            ("tan tanrad", Complex.Tan(z)), ("asin arcsin asinrad arcsinrad", Complex.Asin(z)),
            ("acos arccos acosrad arccosrad", Complex.Acos(z)), ("atan arctan atanrad arctanrad", Complex.Atan(z)),
            ("sinh", Complex.Sinh(z)), ("cosh", Complex.Cosh(z)), ("tanh", Complex.Tanh(z)),
            ("asinh arsh", Complex.Log(z + Complex.Sqrt(z * z + Complex.One))),
            ("acosh arch", Complex.Log(z + Complex.Sqrt(z + Complex.One) * Complex.Sqrt(z - Complex.One))),
            ("atanh arth", .5 * (Complex.Log(Complex.One + z) - Complex.Log(Complex.One - z))),
            ("conj", Complex.Conjugate(z)), ("arg", new(z.Phase, 0)), ("re", new(z.Real, 0)), ("im", new(z.Imaginary, 0))
        ];
        foreach (var (names, expected) in cases)
            foreach (var name in names.Split(' ')) yield return [name + "(0.3-0.7i)", expected.Real, expected.Imaginary];
        foreach (var (text, expected) in new[]
        {
            ("pow(0.3-0.7i,0.2+0.1i)", Complex.Pow(z, new Complex(.2, .1))),
            ("root(0.3-0.7i,3)", Complex.Pow(z, 1.0 / 3)),
            ("log(0.3-0.7i,2)", Complex.Log(z) / Math.Log(2))
        }) yield return [text, expected.Real, expected.Imaginary];
    }

    [Theory]
    [MemberData(nameof(ComplexCases))]
    public void ComplexAliasesMatchSystemNumericsAwayFromBranchCuts(string text, double real, double imaginary)
    {
        var actual = ExpressionParser.ParseComplex(text).Approximate(20);
        ReferenceAssert.Near(real, actual.Real);
        ReferenceAssert.Near(imaginary, actual.Imaginary);
    }

    [Theory]
    [InlineData("7-2", 5)] [InlineData("7/2", 3.5)] [InlineData("7 mod 2", 1)]
    [InlineData("7 ncr 3", 35)] [InlineData("7 npr 3", 210)]
    [InlineData("-8 yroot 3", -2)] [InlineData("root(-8,-3)", -.5)]
    [InlineData("e", 2.718281828459045)] [InlineData("π", Math.PI)]
    public void BinaryOperatorsAndConstantsWorkInBothDomains(string text, double expected)
    {
        ReferenceAssert.Near(expected, ExpressionParser.Parse(text).Approximate(20));
        var actual = ExpressionParser.ParseComplex(text).Approximate(20);
        ReferenceAssert.Near(expected, actual.Real);
        ReferenceAssert.Near(0, actual.Imaginary);
    }

    [Theory]
    [InlineData("sin(1,2)", typeof(ExpressionSyntaxException))]
    [InlineData("pow(2)", typeof(ExpressionSyntaxException))]
    [InlineData("floor(i)", typeof(MathDomainException))]
    [InlineData("i mod 2", typeof(MathDomainException))]
    [InlineData("2 ncr i", typeof(MathDomainException))]
    public void InvalidComplexArgumentsAreRejected(string text, Type exception) => Assert.Throws(exception, () => ExpressionParser.ParseComplex(text));

    [Fact]
    public void DomainDispatchRejectsUnknownOperationsAndArity()
    {
        var domain = new RealExpressionDomain(new(), ComputationContext.Default);
        var complex = new ComplexExpressionDomain(new(), ComputationContext.Default, null);
        Assert.Throws<InvalidOperationException>(() => domain.Binary("?", Expression.Integer(1), Expression.Integer(2)));
        Assert.Throws<ExpressionSyntaxException>(() => domain.Function("unknown", [Expression.Integer(1)], 17));
        Assert.Throws<ExpressionSyntaxException>(() => complex.Function("unknown", [ComplexExpression.One], 17));
        Assert.Throws<ExpressionSyntaxException>(() => domain.Function("sin", [], 17));
        var symbol = ExpressionParser.ParseComplex("x+1", variables: new Dictionary<string, ComplexExpression>());
        Assert.Contains("x", ExpressionFormatter.Format(symbol.Real));
    }
}
