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

// The workbench API retains unevaluated calls and supports scalars and matrices uniformly.
var document = MathEngine.Parse("sin30[degrees]", new(syntax: InputSyntax.Calculator, angleUnit: AngleUnit.Degrees));
var exact = MathEngine.Evaluate(document);
Console.WriteLine($"Calculator input: {MathExpressionFormatter.Format(exact.Value)}");
var expanded = MathEngine.Evaluate("expand((x+1)^2)", new(maxSteps: 8));
Console.WriteLine($"Symbolic expansion: {MathExpressionFormatter.Format(expanded.Value)}");
Console.WriteLine($"Polynomial cancellation: {ExpressionFormatter.Format(ExpressionParser.Parse("(x^2-1)/(x-1)").Cancel("x"))}");
var saved = MathJson.Serialize(MathEngine.Evaluate("matrix(row(1,2),row(3,4))").Value);
var restored = MathJson.DeserializeMathExpression(saved);
var determinant = MathEngine.Evaluate(MathExpression.Call("det", restored)).Value;
if (determinant.Scalar != ComplexExpression.Integer(-2)) throw new InvalidOperationException("Exact matrix round-trip failed.");
Console.WriteLine($"Unified exact result: {MathExpressionFormatter.ToTeX(restored)}");
var certified = await MathEngine.CalculateAsync("ln(sin(1))", new(40));
await MathEngine.WriteAsync(certified, Console.Out);
Console.WriteLine();
