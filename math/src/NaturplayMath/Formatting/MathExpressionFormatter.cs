using NaturplayMath.Symbolics;

namespace NaturplayMath.Formatting;

/// <summary>Presentation only. Use MathJson for copying or storing a value together with its conditions.</summary>
public static class MathExpressionFormatter
{
    public static string Format(MathExpression expression) => Render(expression, false);
    public static string ToTeX(MathExpression expression) => Render(expression, true);
    private static string Render(MathExpression expression, bool tex)
    {
        ArgumentNullException.ThrowIfNull(expression);
        string Scalar(ComplexExpression value)
        {
            string Part(Expression part) => tex ? ExpressionFormatter.ToTeX(part) : ExpressionFormatter.Format(part);
            var real = new Expression(value.Real.Root, Array.Empty<DomainCondition>());
            var imaginary = new Expression(value.Imaginary.Root, Array.Empty<DomainCondition>());
            var text = imaginary.TryGetRational(out var zero) && zero.IsZero ? Part(real) : "(" + Part(real) + ") + (" + Part(imaginary) + ")i";
            return text + ExpressionFormatter.ConditionSuffix(value.Real, tex);
        }
        if (expression.Kind == MathExpressionKind.Scalar) return Scalar(expression.Scalar!);
        if (expression.Kind == MathExpressionKind.Matrix)
        {
            var matrix = expression.Matrix!; var rows = new List<string>();
            for (int row = 0; row < matrix.Rows; row++)
            {
                var entries = new List<string>();
                for (int column = 0; column < matrix.Columns; column++) entries.Add(Scalar(matrix[row, column]));
                rows.Add(string.Join(tex ? " & " : ", ", entries));
            }
            return tex ? @"\begin{bmatrix}" + string.Join(@" \\ ", rows) + @"\end{bmatrix}" : "(" + string.Join("; ", rows) + ")";
        }
        string name = tex ? expression.Name.Replace("_", @"\_") : expression.Name;
        if (expression.Kind == MathExpressionKind.Symbol) return tex ? @"\mathrm{" + name + "}" : name;
        var args = string.Join(", ", expression.Arguments.Select(a => Render(a, tex)));
        return tex ? @"\operatorname{" + name + @"}\left(" + args + @"\right)" : name + "(" + args + ")";
    }
}
