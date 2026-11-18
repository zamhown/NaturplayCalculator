using System.Text.Json;
using NaturplayMath.Symbolics;

namespace NaturplayMath.Serialization;

public static partial class MathJson
{
    public static string Serialize(MathExpression expression, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        context ??= ComputationContext.Default;
        var scoped = context.Bind(context.Start());
        return Write(writer =>
        {
            scoped.Start().Check();
            writer.WriteString("type", "term");
            writer.WriteString("kind", expression.Kind.ToString());
            if (expression.Kind == MathExpressionKind.Scalar)
            { writer.WritePropertyName("value"); writer.WriteRawValue(Serialize(expression.Scalar!, scoped)); }
            else if (expression.Kind == MathExpressionKind.Matrix)
            { writer.WritePropertyName("value"); writer.WriteRawValue(Serialize(expression.Matrix!, scoped)); }
            else
            {
                writer.WriteString("name", expression.Name);
                writer.WriteStartArray("arguments");
                foreach (var argument in expression.Arguments) writer.WriteRawValue(Serialize(argument, scoped));
                writer.WriteEndArray();
            }
        });
    }
    public static MathExpression DeserializeMathExpression(string json, ComputationContext? context = null)
    {
        context ??= ComputationContext.Default;
        var scoped = context.Bind(context.Start());
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 256 });
        var root = document.RootElement; CheckHeader(root, "term");
        return root.GetProperty("kind").GetString() switch
        {
            "Scalar" => MathExpression.From(DeserializeComplex(root.GetProperty("value").GetRawText(), scoped)),
            "Matrix" => MathExpression.From(DeserializeMatrix(root.GetProperty("value").GetRawText(), scoped)),
            "Symbol" => MathExpression.Symbol(root.GetProperty("name").GetString()!),
            "Call" => MathExpression.Call(root.GetProperty("name").GetString()!, root.GetProperty("arguments").EnumerateArray()
                .Select(arg => DeserializeMathExpression(arg.GetRawText(), scoped)).ToArray()),
            _ => throw new FormatException("Unknown mathematical term kind.")
        };
    }
}
