using System.Numerics;
using System.Reflection;
using NaturplayMath.Numbers;
using NaturplayMath.Serialization;
using NaturplayMath.Symbolics;
using Xunit;

namespace NaturplayMath.Tests.Architecture;

public class DependencyTests
{
    private static Assembly[] LibraryAssemblies =>
        [typeof(Rational).Assembly, typeof(Expression).Assembly, typeof(NumericalEvaluator).Assembly, typeof(MathJson).Assembly];

    [Fact]
    public void AssembliesOnlyDependOnLowerLayers()
    {
        var allowed = new Dictionary<string, string[]>
        {
            ["NaturplayMath.Core"] = [],
            ["NaturplayMath.Symbolics"] = ["NaturplayMath.Core"],
            ["NaturplayMath.Numerics"] = ["NaturplayMath.Core", "NaturplayMath.Symbolics"],
            ["NaturplayMath"] = ["NaturplayMath.Core", "NaturplayMath.Symbolics", "NaturplayMath.Numerics"]
        };
        foreach (var assembly in LibraryAssemblies)
            foreach (var reference in assembly.GetReferencedAssemblies())
                if (reference.Name!.StartsWith("NaturplayMath", StringComparison.Ordinal))
                    Assert.Contains(reference.Name, allowed[assembly.GetName().Name!]);
    }

    [Fact]
    public void MathematicalApisDoNotAcceptOrReturnFloatingPointValues()
    {
        var forbidden = new[] { typeof(float), typeof(double), typeof(decimal) };
        foreach (var type in LibraryAssemblies.SelectMany(a => a.GetExportedTypes()))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static))
            {
                Assert.DoesNotContain(method.ReturnType, forbidden);
                foreach (var parameter in method.GetParameters()) Assert.DoesNotContain(parameter.ParameterType, forbidden);
            }
            foreach (var constructor in type.GetConstructors())
                foreach (var parameter in constructor.GetParameters())
                    Assert.DoesNotContain(parameter.ParameterType, forbidden);
        }
        Assert.Equal(typeof(BigInteger), typeof(Rational).GetProperty(nameof(Rational.Numerator))!.PropertyType);
        Assert.Equal(typeof(BigInteger), typeof(Rational).GetProperty(nameof(Rational.Denominator))!.PropertyType);
    }
}
