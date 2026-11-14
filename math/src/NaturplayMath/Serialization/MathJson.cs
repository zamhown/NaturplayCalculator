using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using NaturplayMath.Computation;
using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
namespace NaturplayMath.Serialization;

/// <summary>Versioned lossless storage. Integers are strings; display text is never used for persistence.</summary>
public static class MathJson
{
    public static string Serialize(ComplexExpression expression, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        context ??= ComputationContext.Default;
        var scoped = context.Bind(context.Start());
        return Write(writer =>
        {
            writer.WriteString("type", "complex");
            writer.WritePropertyName("real"); writer.WriteRawValue(Serialize(expression.Real, scoped));
            writer.WritePropertyName("imaginary"); writer.WriteRawValue(Serialize(expression.Imaginary, scoped));
        });
    }
    public static ComplexExpression DeserializeComplex(string json, ComputationContext? context = null)
    {
        context ??= ComputationContext.Default;
        var scoped = context.Bind(context.Start());
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 256 });
        var root = document.RootElement; CheckHeader(root, "complex");
        return new(DeserializeExpression(root.GetProperty("real").GetRawText(), scoped), DeserializeExpression(root.GetProperty("imaginary").GetRawText(), scoped));
    }
    public static string Serialize(ExpressionMatrix matrix, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        context ??= ComputationContext.Default;
        var scoped = context.Bind(context.Start());
        return Write(writer =>
        {
            writer.WriteString("type", "matrix"); writer.WriteNumber("rows", matrix.Rows); writer.WriteNumber("columns", matrix.Columns);
            writer.WriteStartArray("entries");
            for (int row = 0; row < matrix.Rows; row++) for (int column = 0; column < matrix.Columns; column++) writer.WriteRawValue(Serialize(matrix[row, column], scoped));
            writer.WriteEndArray();
        });
    }
    public static ExpressionMatrix DeserializeMatrix(string json, ComputationContext? context = null)
    {
        context ??= ComputationContext.Default;
        var scoped = context.Bind(context.Start());
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 256 });
        var root = document.RootElement; CheckHeader(root, "matrix");
        int rows = root.GetProperty("rows").GetInt32(), columns = root.GetProperty("columns").GetInt32();
        var entries = root.GetProperty("entries");
        if (rows < 0 || columns < 0 || (long)rows * columns != entries.GetArrayLength()) throw new FormatException("Matrix dimensions do not match the entries.");
        var values = new ComplexExpression[rows, columns];
        int index = 0;
        for (int row = 0; row < rows; row++) for (int column = 0; column < columns; column++) values[row, column] = DeserializeComplex(entries[index++].GetRawText(), scoped);
        return new(values);
    }
    public static string Serialize(Expression expression, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var session = (context ?? ComputationContext.Default).Start();
        return Write(writer =>
        {
            writer.WriteString("type", "expression");
            writer.WritePropertyName("root"); WriteNode(writer, expression.Root, session);
            writer.WriteStartArray("conditions");
            foreach (var condition in expression.Conditions)
            {
                session.Check(); writer.WriteStartObject(); writer.WriteString("requirement", condition.Requirement.ToString());
                writer.WritePropertyName("argument"); WriteNode(writer, condition.ArgumentNode, session); writer.WriteEndObject();
            }
            writer.WriteEndArray();
        });
    }
    public static string Serialize(RealInterval interval) => Write(writer =>
    {
        writer.WriteString("type", "interval"); writer.WriteString("lower", interval.Lower.ToString()); writer.WriteString("upper", interval.Upper.ToString());
    });
    private static string Write(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { MaxDepth = 256 }))
        {
            writer.WriteStartObject(); writer.WriteNumber("version", 1); write(writer); writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void WriteNode(Utf8JsonWriter writer, Node node, ComputationSession session)
    {
        session.Check(); writer.WriteStartObject(); writer.WriteString("kind", node.Kind.ToString());
        if (node.Kind == NodeKind.Constant) writer.WriteString("value", node.Value.ToString());
        else if (node.Kind == NodeKind.Symbol) writer.WriteString("name", node.Name);
        else
        {
            if (node.Kind is NodeKind.Power or NodeKind.Root) writer.WriteString("exponent", node.Exponent.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteStartArray("arguments");
            foreach (var child in node.Children) WriteNode(writer, child, session);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }
    public static Expression DeserializeExpression(string json, ComputationContext? context = null)
    {
        var session = (context ?? ComputationContext.Default).Start();
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 256 });
        var root = document.RootElement; CheckHeader(root, "expression");
        var expression = ReadNode(root.GetProperty("root"), session);
        var conditions = new List<DomainCondition>(expression.Conditions);
        foreach (var condition in root.GetProperty("conditions").EnumerateArray())
        {
            session.Check();
            if (!Enum.TryParse<DomainRequirement>((condition.GetProperty("requirement").GetString() ?? throw new FormatException("Expected a string value.")), out var requirement) || !Enum.IsDefined(requirement))
                throw new FormatException("Unknown domain requirement.");
            var argument = ReadNode(condition.GetProperty("argument"), session);
            conditions.AddRange(argument.Conditions);
            conditions.Add(new DomainCondition(argument.Root, requirement));
        }
        return new Expression(expression.Root, conditions);
    }
    public static RealInterval DeserializeInterval(string json, ComputationContext? context = null)
    {
        var session = (context ?? ComputationContext.Default).Start();
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 256 });
        var root = document.RootElement; CheckHeader(root, "interval");
        return new(ParseRational((root.GetProperty("lower").GetString() ?? throw new FormatException("Expected a string value.")), session), ParseRational((root.GetProperty("upper").GetString() ?? throw new FormatException("Expected a string value.")), session));
    }
    private static void CheckHeader(JsonElement root, string type)
    {
        if (root.GetProperty("version").GetInt32() != 1 || (root.GetProperty("type").GetString() ?? throw new FormatException("Expected a string value.")) != type)
            throw new FormatException("Unsupported mathematics JSON version or type.");
    }
    private static Rational ParseRational(string text, ComputationSession session)
    {
        var parts = text.Split('/');
        if (parts.Length is < 1 or > 2) throw new FormatException("Invalid rational.");
        return new(ParseInteger(parts[0], session), parts.Length == 1 ? BigInteger.One : ParseInteger(parts[1], session));
    }
    private static BigInteger ParseInteger(string text, ComputationSession session)
    {
        // Charge per digit before using BigInteger; storage accepts decimal integers only.
        if (string.IsNullOrEmpty(text)) throw new FormatException("An integer is required.");
        int first = text[0] == '-' ? 1 : 0;
        if (first == text.Length) throw new FormatException("An integer requires digits.");
        for (int i = first; i < text.Length; i++)
        {
            session.Check();
            if (text[i] < '0' || text[i] > '9') throw new FormatException("Invalid integer digit.");
        }
        return Text.NumberParser.Parse(text, 10, session).Numerator;
    }
    private static Expression ReadNode(JsonElement json, ComputationSession session)
    {
        session.Check();
        if (!Enum.TryParse<NodeKind>((json.GetProperty("kind").GetString() ?? throw new FormatException("Expected a string value.")), out var kind) || !Enum.IsDefined(kind)) throw new FormatException("Unknown expression node.");
        if (kind == NodeKind.Constant) return Expression.Constant(ParseRational((json.GetProperty("value").GetString() ?? throw new FormatException("Expected a string value.")), session));
        if (kind == NodeKind.Symbol) return Expression.Symbol((json.GetProperty("name").GetString() ?? throw new FormatException("Expected a string value.")));
        if (kind == NodeKind.Pi)
        {
            if (json.GetProperty("arguments").GetArrayLength() != 0) throw new FormatException("Incorrect constant arity.");
            return Expression.Pi;
        }
        var args = json.GetProperty("arguments").EnumerateArray().Select(a => ReadNode(a, session)).ToArray();
        if (kind is NodeKind.Sum or NodeKind.Product ? args.Length < 2 : args.Length != (kind is NodeKind.ComplexPowerReal or NodeKind.ComplexPowerImag ? 4 : kind is NodeKind.GeneralPower or NodeKind.Choose or NodeKind.Permute or NodeKind.Atan2 or NodeKind.ComplexRootReal or NodeKind.ComplexRootImag ? 2 : 1)) throw new FormatException("Incorrect expression arity.");
        var nodes = args.Select(a => a.Root).ToArray();
        var conditions = args.SelectMany(a => a.Conditions).ToList();
        Node node;
        switch (kind)
        {
            case NodeKind.Sum: node = ExpressionNormalizer.Add(nodes, session); break;
            case NodeKind.Product: node = ExpressionNormalizer.Multiply(nodes, session); break;
            case NodeKind.Power:
                var exponent = ParseInteger((json.GetProperty("exponent").GetString() ?? throw new FormatException("Expected a string value.")), session);
                if (exponent.Sign <= 0) conditions.Add(new DomainCondition(nodes[0], DomainRequirement.NonZero));
                node = ExpressionNormalizer.Pow(nodes[0], exponent, session); break;
            case NodeKind.Sqrt:
                conditions.Add(new DomainCondition(nodes[0], DomainRequirement.Nonnegative)); node = ExpressionNormalizer.Sqrt(nodes[0], session); break;
            case NodeKind.Root:
                var degree = ParseInteger((json.GetProperty("exponent").GetString() ?? throw new FormatException("Expected a root degree.")), session);
                if (degree.Sign <= 0) throw new FormatException("A root degree must be positive.");
                if (degree.IsEven) conditions.Add(new DomainCondition(nodes[0], DomainRequirement.Nonnegative));
                node = ExpressionNormalizer.Root(nodes[0], degree, session); break;
            case NodeKind.GeneralPower:
                var power = args[0].Pow(args[1]);
                conditions.AddRange(power.Conditions); node = power.Root; break;
            case NodeKind.Ln:
                conditions.Add(new DomainCondition(nodes[0], DomainRequirement.Positive)); node = ExpressionNormalizer.Ln(nodes[0]); break;
            case NodeKind.Exp: node = ExpressionNormalizer.Exp(nodes[0]); break;
            case NodeKind.Abs: node = ExpressionNormalizer.Abs(nodes[0]); break;
            case NodeKind.Sin: node = ExpressionNormalizer.Trig(nodes[0], false); break;
            case NodeKind.Cos: node = ExpressionNormalizer.Trig(nodes[0], true); break;
            case NodeKind.Asin:
                conditions.Add(new DomainCondition(ExpressionNormalizer.Add(new[] { Node.Constant(Rational.One),
                    ExpressionNormalizer.Multiply(new[] { Node.Constant(new Rational(-1)), ExpressionNormalizer.Pow(nodes[0], 2, session) }, session) }, session), DomainRequirement.Nonnegative));
                node = ExpressionNormalizer.Inverse(nodes[0], kind); break;
            case NodeKind.Atan:
            case NodeKind.Asinh:
            case NodeKind.Tanh: node = ExpressionNormalizer.Inverse(nodes[0], kind); break;
            case NodeKind.Floor:
            case NodeKind.Ceiling:
            case NodeKind.Truncate: node = ExpressionNormalizer.Discrete(kind, nodes, session); break;
            case NodeKind.Factorial:
            case NodeKind.DoubleFactorial:
            case NodeKind.Choose:
            case NodeKind.Permute:
                node = ExpressionNormalizer.Discrete(kind, nodes, session);
                conditions.Add(new DomainCondition(node, DomainRequirement.Defined)); break;
            case NodeKind.ComplexPowerReal:
            case NodeKind.ComplexPowerImag:
                node = ExpressionNormalizer.ComplexPower(kind, nodes, session);
                conditions.Add(new DomainCondition(node, DomainRequirement.Defined)); break;
            case NodeKind.Atan2:
            case NodeKind.ComplexRootReal:
            case NodeKind.ComplexRootImag:
                node = ExpressionNormalizer.ComplexPart(kind, nodes[0], nodes[1], session);
                if (kind == NodeKind.Atan2) conditions.Add(new DomainCondition(node, DomainRequirement.Defined)); break;
            default: throw new FormatException("Unsupported expression node.");
        }
        return new Expression(node, conditions);
    }
}
