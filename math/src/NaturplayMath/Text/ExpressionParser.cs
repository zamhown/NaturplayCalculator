using System.Numerics;
using NaturplayMath.Numbers;
using NaturplayMath.Symbolics;
namespace NaturplayMath.Text;

/// <summary>Exact literals and calculator precedence: implicit multiplication binds more tightly than explicit division.</summary>
public static class ExpressionParser
{
    internal static bool IsScalarFunction(string name) => Parser<Expression>.Functions.Contains(name);
    internal static int ScalarArity(string name) => name is "root" or "pow" or "choose" or "permute" or "ncr" or "npr" or "mod" or "log" ? 2 : 1;
    public static Expression Parse(string text, ExpressionParseOptions? options = null, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        context ??= ComputationContext.Default;
        var session = context.Start();
        var effective = options ?? new(); var scoped = context.Bind(session);
        return new Parser<Expression>(text, effective, session, new RealExpressionDomain(effective, scoped)).Parse();
    }
    /// <summary>Complex expressions use principal branches; unbound symbols are real unless supplied in variables.</summary>
    public static ComplexExpression ParseComplex(string text, ExpressionParseOptions? options = null,
        IReadOnlyDictionary<string, ComplexExpression>? variables = null, ComputationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        context ??= ComputationContext.Default;
        var session = context.Start(); var effective = options ?? new(); var scoped = context.Bind(session);
        return new Parser<ComplexExpression>(text, effective, session, new ComplexExpressionDomain(effective, scoped, variables)).Parse();
    }
    internal static MathDocument ParseDocument(string text, ExpressionParseOptions options, InputSyntax syntax, ComputationContext context)
    {
        ArgumentNullException.ThrowIfNull(text);
        var comments = new List<string>();
        var term = new Parser<MathExpression>(text, options, context.Start(), new MathExpressionDomain(options), syntax, comments).Parse();
        return new(text, term, comments.AsReadOnly(), options);
    }
    private enum Kind { End, Number, Identifier, Operator, Open, Close, Comma, String }
    private readonly record struct Token(Kind Kind, string Text, int Position, Rational Value = default);
    private sealed class Parser<T>(string source, ExpressionParseOptions options, ComputationSession session, IExpressionDomain<T> domain,
        InputSyntax? syntax = null, List<string>? comments = null)
    {
        internal static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
        {
            "sqrt", "root", "pow", "exp", "ln", "lg", "log", "abs", "sin", "cos", "tan", "sinrad", "cosrad", "tanrad",
            "asin", "acos", "atan", "arcsin", "arccos", "arctan", "asinrad", "acosrad", "atanrad", "arcsinrad", "arccosrad", "arctanrad",
            "sinh", "cosh", "tanh", "asinh", "acosh", "atanh", "arsh", "arch", "arth", "fact", "factorial", "dfact", "doublefactorial",
            "floor", "ceil", "ceiling", "trunc", "truncate", "choose", "permute", "ncr", "npr", "mod", "conj", "arg", "re", "im"
        };
        private int offset, depth;
        private Token token;
        private static readonly HashSet<string> DocumentFunctions = new(new[] { "transpose", "adjoint", "inverse", "matrix", "variance", "samplevariance", "mean", "covariance", "correlation", "radix", "rank", "det", "row", "expand", "radicals", "Ex", "Dx", "Cov", "rho" }, StringComparer.OrdinalIgnoreCase);
        private static readonly string[] CalculatorNames = Functions.Concat(DocumentFunctions).Concat(new[] { "Ans", "pi", "E", "D" })
            .OrderByDescending(n => n.Length).ToArray();
        internal T Parse()
        {
            session.Report(ComputationStage.Preparing, "parsing exact expression", force: true);
            Next();
            var result = Read(0);
            if (token.Kind != Kind.End) throw Error("Unexpected token");
            return result;
        }
        private ExpressionSyntaxException Error(string message) => new(message, token.Position);
        private void Next() => token = Lex();
        private T Read(int minimum)
        {
            session.Check();
            if (++depth > options.MaxDepth) throw Error("Expression nesting exceeds MaxDepth");
            try
            {
                T left;
                if (token.Kind == Kind.Operator && token.Text is "+" or "-")
                {
                    bool negative = token.Text == "-"; Next(); left = Read(30);
                    if (negative) left = domain.Negate(left);
                }
                else if (token.Kind == Kind.Number) { left = domain.Number(token.Value); Next(); }
                else if (token.Kind == Kind.Open)
                {
                    Next(); left = Read(0);
                    if (syntax == InputSyntax.Calculator && token.Kind == Kind.Comma)
                    {
                        var rows = new List<T>(); var cells = new List<T> { left };
                        while (token.Kind == Kind.Comma)
                        {
                            Next();
                            if (token.Kind == Kind.Comma) { rows.Add(domain.Function("row", cells, token.Position)); cells = new(); Next(); }
                            cells.Add(Read(0));
                        }
                        rows.Add(domain.Function("row", cells, token.Position));
                        left = domain.Function("matrix", rows, token.Position);
                    }
                    Expect(Kind.Close, "Expected closing parenthesis"); Next();
                }
                else if (token.Kind == Kind.Identifier || token.Kind == Kind.Operator && Functions.Contains(token.Text))
                {
                    var name = token.Text; var position = token.Position; Next();
                    if (syntax is not null && name.Equals("radix", StringComparison.OrdinalIgnoreCase))
                    {
                        Expect(Kind.Open, "Expected radix(base, quoted digits)"); Next();
                        Expect(Kind.Number, "Expected an integer base"); var radix = token.Value; Next();
                        if (!radix.IsInteger) throw Error("Expected an integer base");
                        Expect(Kind.Comma, "Expected comma"); Next(); Expect(Kind.String, "Expected quoted digits");
                        left = domain.Number(NumberParser.Parse(token.Text, radix.Numerator, session)); Next();
                        Expect(Kind.Close, "Expected closing parenthesis"); Next();
                    }
                    else if (Functions.Contains(name) || syntax is not null && (DocumentFunctions.Contains(name) || name is "E" or "D")
                        || syntax == InputSyntax.Canonical && token.Kind == Kind.Open)
                    {
                        var args = new List<T>();
                        if (syntax == InputSyntax.Calculator && token.Kind != Kind.Open) args.Add(Read(30));
                        else
                        {
                            Expect(Kind.Open, "Function arguments require parentheses"); Next();
                            if (token.Kind != Kind.Close)
                            {
                                args.Add(Read(0));
                                while (token.Kind == Kind.Comma) { Next(); args.Add(Read(0)); }
                            }
                            Expect(Kind.Close, "Expected closing parenthesis"); Next();
                        }
                        left = domain.Function(syntax is null ? name.ToLowerInvariant() : name, args, position);
                    }
                    else left = domain.Symbol(name);
                }
                else throw Error("Expected a number, symbol, function, or parenthesized expression");
                while (true)
                {
                    session.Check();
                    if (syntax == InputSyntax.Calculator && token.Kind == Kind.Operator && token.Text == "^T")
                    { if (30 < minimum) break; left = domain.Function("transpose", new[] { left }, token.Position); Next(); continue; }
                    if (token.Kind == Kind.Operator && token.Text is "!" or "!!")
                    {
                        if (40 < minimum) break;
                        left = domain.Function(token.Text == "!" ? "fact" : "dfact", new[] { left }, token.Position); Next(); continue;
                    }
                    bool implicitProduct = token.Kind is Kind.Number or Kind.Identifier or Kind.Open;
                    if (implicitProduct && syntax == InputSyntax.Canonical) throw Error("Use an explicit multiplication operator");
                    string op = implicitProduct ? "*" : token.Kind == Kind.Operator ? token.Text : "";
                    int precedence = implicitProduct ? 25 : op switch { "+" or "-" => 10, "*" or "/" or "mod" or "ncr" or "npr" => 20, "^" or "yroot" => 30, _ => -1 };
                    if (precedence < minimum) break;
                    if (!implicitProduct) Next();
                    var right = Read(op == "^" ? precedence : precedence + 1);
                    left = domain.Binary(op, left, right);
                }
                return left;
            }
            finally { depth--; }
        }
        private void Expect(Kind kind, string message) { if (token.Kind != kind) throw Error(message); }
        private Token Lex()
        {
            while (offset < source.Length)
            {
                session.Check();
                if (char.IsWhiteSpace(source[offset])) { offset++; continue; }
                if (syntax == InputSyntax.Calculator && source[offset] == '[')
                {
                    int startComment = ++offset;
                    while (offset < source.Length && source[offset] != ']') { session.Check(); offset++; }
                    if (offset == source.Length) throw new ExpressionSyntaxException("Unclosed comment", startComment - 1);
                    comments!.Add(source[startComment..offset]); offset++; continue;
                }
                if (source[offset] == '/' && offset + 1 < source.Length && source[offset + 1] == '/')
                { int startComment = offset + 2; while (offset < source.Length && source[offset] != '\n') { session.Check(); offset++; } comments?.Add(source[startComment..offset]); continue; }
                break;
            }
            int start = offset;
            if (offset == source.Length) return new(Kind.End, "", offset);
            char c = source[offset++];
            if (syntax is not null && c == '"')
            {
                while (offset < source.Length && source[offset] != '"') { session.Check(); offset++; }
                if (offset == source.Length) throw new ExpressionSyntaxException("Unclosed quoted digits", start);
                return new(Kind.String, source[(start + 1)..offset++], start);
            }
            if (syntax == InputSyntax.Calculator && c == '√') return new(Kind.Identifier, "sqrt", start);
            if (c is '(' or ')') return new(c == '(' ? Kind.Open : Kind.Close, c.ToString(), start);
            if (c == ',') return new(Kind.Comma, ",", start);
            if (c is '+' or '-' or '*' or '/' or '^' or '%' or '!' or '×' or '÷' or '−')
            {
                string op = c switch { '×' => "*", '÷' => "/", '−' => "-", '%' => "mod", _ => c.ToString() };
                if (syntax == InputSyntax.Calculator && c == '^' && offset < source.Length && source[offset] == 'T') { offset++; op = "^T"; }
                if (c == '!' && offset < source.Length && source[offset] == '!') { offset++; op = "!!"; }
                return new(Kind.Operator, op, start);
            }
            if (c is >= '0' and <= '9' || c is '.' or '[')
            {
                if (syntax is not null && c == '[') throw new ExpressionSyntaxException("Use radix(base, quoted digits) for bracket digits", start);
                offset = start;
                var radix = options.InputRadix;
                if (offset + 1 < source.Length && source[offset] == '0')
                {
                    var prefix = char.ToLowerInvariant(source[offset + 1]);
                    if (prefix is 'x' or 'b' or 'o') { radix = prefix == 'x' ? 16 : prefix == 'b' ? 2 : 8; offset += 2; }
                }
                int digitStart = offset;
                while (offset < source.Length)
                {
                    session.Check();
                    char digit = source[offset];
                    if (digit == '[')
                    {
                        if (syntax is not null) break;
                        offset++;
                        while (offset < source.Length && source[offset] is >= '0' and <= '9') offset++;
                        if (offset == source.Length || source[offset] != ']') throw new ExpressionSyntaxException("Expected closing digit bracket", offset);
                        offset++; continue;
                    }
                    int value = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".IndexOf(char.ToUpperInvariant(digit));
                    if (digit == '.' || value >= 0 && value < radix && radix <= 36) { offset++; continue; }
                    break;
                }
                Rational number;
                try { number = NumberParser.Parse(source[digitStart..offset], radix, session); }
                catch (FormatException e) { throw new ExpressionSyntaxException(e.Message, start); }
                if (radix == 10 && offset < source.Length && source[offset] is 'e' or 'E')
                {
                    int expStart = offset + 1, end = expStart;
                    if (end < source.Length && source[end] is '+' or '-') end++;
                    int firstDigit = end;
                    while (end < source.Length && source[end] is >= '0' and <= '9') { session.Check(); end++; }
                    if (end > firstDigit)
                    {
                        var exponent = NumberParser.Parse(source[expStart..end], 10, session).Numerator;
                        number *= new Rational(10).Pow(exponent, session); offset = end;
                    }
                }
                return new(Kind.Number, source[start..offset], start, number);
            }
            if (char.IsLetter(c) || c == '_')
            {
                foreach (var namedOperator in new[] { "ncr", "npr", "mod", "yroot" })
                {
                    int end = start + namedOperator.Length;
                    if (end <= source.Length && source.AsSpan(start, namedOperator.Length).Equals(namedOperator, StringComparison.OrdinalIgnoreCase)
                        && (end == source.Length || !char.IsLetter(source[end]) && source[end] != '_'))
                    { offset = end; return new(Kind.Operator, namedOperator, start); }
                }
                if (syntax == InputSyntax.Calculator)
                {
                    foreach (var known in CalculatorNames)
                        if (source.AsSpan(start).StartsWith(known, StringComparison.OrdinalIgnoreCase))
                        { offset = start + known.Length; return new(Kind.Identifier, source[start..offset], start); }
                    if (char.IsLetter(c)) return new(Kind.Identifier, c.ToString(), start);
                }
                while (offset < source.Length && (char.IsLetterOrDigit(source[offset]) || source[offset] == '_')) { session.Check(); offset++; }
                var name = source[start..offset];
                var lower = name.ToLowerInvariant();
                if (options.InputRadix > 10 && options.InputRadix <= 36 && lower is not "e" and not "pi" && !Functions.Contains(name)
                    && name.All(d => "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".IndexOf(char.ToUpperInvariant(d)) is int v && v >= 0 && v < options.InputRadix))
                    return new(Kind.Number, name, start, NumberParser.Parse(name, options.InputRadix, session));
                return new(Kind.Identifier, name, start);
            }
            throw new ExpressionSyntaxException("Invalid character", start);
        }
    }
}
