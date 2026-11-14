using NaturplayMath.Calculation;
using NaturplayMath.Computation;
using NaturplayMath.Formatting;
using NaturplayMath.Numbers;
using NaturplayMath.Numerics;
using NaturplayMath.Serialization;
using NaturplayMath.Symbolics;
using NaturplayMath.Text;

var third = NumberParser.ParseExact("1/3");
Console.WriteLine($"Exact fraction: {third}; times three: {third * new Rational(3)}");
var x = Expression.Symbol("x");
Console.WriteLine($"Preserved domain: {ExpressionFormatter.Format(x / x)}");
Console.WriteLine($"Exact identity: {ExpressionFormatter.Format(x.Sin().Pow(2) + x.Cos().Pow(2))}");

using var stop = new CancellationTokenSource();
var progress = new Progress<ComputationProgress>(p =>
{
    if (p.Stage is ComputationStage.Refining or ComputationStage.Completed)
        Console.WriteLine($"{p.OperationId}: {p.Stage}, {p.Elapsed}");
});
var result = await CalculationEngine.CalculateAsync("ln(sin(1))", new(new NumberFormat(100, trimTrailingZeros: false)),
    context: new(progress: progress), cancellationToken: stop.Token);
Console.WriteLine($"Certified decimal: {result.Text}");
Console.WriteLine($"Same exact expression, hexadecimal: {CalculationEngine.Calculate(result.Expression, new(new NumberFormat(32, 16))).Text}");
Console.WriteLine($"Principal complex square root: {(await CalculationEngine.CalculateComplexAsync("sqrt(-1)")).Text}");
Console.WriteLine($"Lossless JSON: {result.Expression == MathJson.DeserializeExpression(MathJson.Serialize(result.Expression))}");
var matrix = ExpressionMatrix.FromRationals(new Rational[,] { { new(1), new(2) }, { new(3), new(4) } });
Console.WriteLine($"Exact determinant: {ExpressionFormatter.Format(matrix.Determinant().Real)}");
