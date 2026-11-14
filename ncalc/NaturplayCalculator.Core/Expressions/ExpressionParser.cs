using System;
using System.Collections.Generic;
using System.Linq;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Expressions
{
    public sealed class ExpressionParser
    {
        private enum TokenKind { End, Number, Symbol, Function, Operator, Left, Right, Comma, Literal }
        private sealed class Token
        {
            internal TokenKind Kind; internal string Text; internal int Index, End;
            internal Token(TokenKind kind, string text, int index, int length = -1)
            { Kind = kind; Text = text; Index = index; End = index + (length < 0 ? text.Length : length); }
        }
        private readonly List<Token> tokens = new List<Token>();
        private readonly List<string> comments = new List<string>();
        private int position, depth;
        private bool preview;
        private Token Current => tokens[position];
        private int ConsumedEnd => position == 0 ? 0 : tokens[position - 1].End;

        public ParsedExpression Parse(string input) => Parse(input, false);
        public ParsedExpression ParsePreview(string input) => Parse(input, false, true);
        internal ParsedExpression ParseStoredResult(string input) => Parse(input, true);
        private ParsedExpression Parse(string input, bool stored, bool preview = false)
        {
            ExpressionLimits.Validate(input, stored ? ExpressionLimits.MaxResultLength : ExpressionLimits.MaxLength, stored ? 100 : ExpressionLimits.MaxDepth);
            tokens.Clear(); comments.Clear(); position = 0; depth = 0; this.preview = preview;
            Tokenize(input);
            var root = preview ? ParseDraftRow() : ParseExpression(0);
            if (Current.Kind != TokenKind.End) Fail();
            return new ParsedExpression { Root = root, Comments = new List<string>(comments).AsReadOnly() };
        }

        private ExpressionNode ParseDraftRow()
        {
            var items = new List<ExpressionNode> { ParseExpression(0) };
            while (Current.Kind != TokenKind.End)
            {
                // Keep unexpected punctuation visible inside a formula, without
                // feeding repaired syntax back into the calculation input.
                // An unmatched ')' is source punctuation, not evidence that the
                // entire preceding expression owns an invented opening group.
                if (Current.Kind == TokenKind.Comma || Current.Kind == TokenKind.Right)
                { items.Add(ExpressionNode.DraftLiteral(Current.Text, Current.Index)); position++; }
                else items.Add(ParseExpression(0));
            }
            return items.Count == 1 ? items[0] : ExpressionNode.DraftRow(items);
        }

        private ExpressionNode ParseExpression(int minimum)
        {
            CalculationContext.Check();
            if (++depth > 100) ExpressionLimits.Fail();
            try
            {
                var token = Current;
                int start = token.Index, firstToken = position;
                bool missing = preview && (token.Kind == TokenKind.End || token.Kind == TokenKind.Right || token.Kind == TokenKind.Comma
                    || token.Kind == TokenKind.Operator && token.Text != "-" && token.Text != "+");
                if (!missing) position++;
                ExpressionNode left;
                if (missing) left = ExpressionNode.Missing(token.Index);
                else if (token.Kind == TokenKind.Literal && preview) left = ExpressionNode.DraftLiteral(token.Text, token.Index);
                else if (token.Kind == TokenKind.Number) left = ExpressionNode.Number(token.Text, token.Index);
                else if (token.Kind == TokenKind.Symbol) left = ExpressionNode.Symbol(token.Text, token.Index);
                else if (token.Kind == TokenKind.Operator && (token.Text == "-" || token.Text == "+"))
                    left = token.Text == "+" && !preview ? ParseExpression(25) : ExpressionNode.Unary(token.Text, ParseExpression(25), token.Index);
                else if (token.Kind == TokenKind.Function)
                {
                    ExpressionNode argument;
                    if (Current.Kind == TokenKind.Left) { int index = Current.Index; position++; argument = ParseGroup(index, false); }
                    else argument = ParseExpression(30);
                    left = ExpressionNode.Function(token.Text, argument, token.Index);
                }
                else if (token.Kind == TokenKind.Left) left = ParseGroup(token.Index, true);
                else { Fail(); return null; }

                left = left.WithSourceRange(start, position == firstToken ? start : ConsumedEnd);

                while (Current.Kind != TokenKind.End)
                {
                    CalculationContext.Check();
                    token = Current;
                    if (token.Kind == TokenKind.Operator && (token.Text == "!" || token.Text == "!!"))
                    {
                        if (40 < minimum) break;
                        position++; left = ExpressionNode.Function(token.Text == "!" ? "fact" : "dfact", left, token.Index)
                            .WithSourceRange(start, ConsumedEnd); continue;
                    }
                    bool implicitProduct = token.Kind == TokenKind.Left || token.Kind == TokenKind.Symbol || token.Kind == TokenKind.Function || token.Kind == TokenKind.Number;
                    if (preview && implicitProduct && left.Kind == ExpressionKind.DraftLiteral) break;
                    string op = implicitProduct ? "*" : token.Kind == TokenKind.Operator ? token.Text : "";
                    // Juxtaposition forms one factor: a/2b means a/(2*b), while
                    // a/2*b keeps the explicit left-associative multiply/divide rule.
                    int precedence = implicitProduct ? 25 : op == "+" || op == "-" ? 10 : op == "^" || op == "yroot" ? 30 : op == "*" || op == "/" || op == "mod" || op == "nCr" || op == "nPr" ? 20 : -1;
                    if (precedence < minimum) break;
                    if (!implicitProduct) position++;
                    var right = ParseExpression(precedence + (op == "^" ? 0 : 1));
                    left = op == "^" && right.Kind == ExpressionKind.Symbol && right.Text == "T"
                        ? ExpressionNode.Function("transpose", left, token.Index)
                        : MakeBinary(op, left, right, token.Index);
                    left = left.WithSourceRange(start, ConsumedEnd);
                }
                return left;
            }
            finally { depth--; }
        }

        private ExpressionNode MakeBinary(string op, ExpressionNode left, ExpressionNode right, int index)
        {
            bool sum = op == "+" || op == "-";
            bool product = op == "*" || op == "mod";
            string category = sum ? "sum" : "product";
            if (!preview || !sum && !product || left.Depth < 32 && !(left.Kind == ExpressionKind.DraftRow && left.Text == category))
                return ExpressionNode.Binary(op, left, right, index);
            var items = new List<ExpressionNode>();
            FlattenDraft(left, category, items);
            items.Add(ExpressionNode.DraftLiteral(op, index));
            items.Add(GroupDraftOperand(right, sum ? 10 : 20));
            return ExpressionNode.DraftRow(items, category);
        }
        private static void FlattenDraft(ExpressionNode node, string category, List<ExpressionNode> items)
        {
            if (node.Kind == ExpressionKind.DraftRow && node.Text == category) { items.AddRange(node.Children); return; }
            if (node.Kind == ExpressionKind.Binary && (category == "sum" ? node.Text == "+" || node.Text == "-" : node.Text == "*" || node.Text == "mod"))
            {
                FlattenDraft(node.Children[0], category, items);
                items.Add(ExpressionNode.DraftLiteral(node.Text, node.SourceIndex));
                items.Add(GroupDraftOperand(node.Children[1], category == "sum" ? 10 : 20));
            }
            else items.Add(GroupDraftOperand(node, category == "sum" ? 9 : 19));
        }
        private static ExpressionNode GroupDraftOperand(ExpressionNode node, int precedence) =>
            ExpressionFormatter.Precedence(node) <= precedence ? ExpressionNode.DraftGroup(node, node.SourceIndex) : node;

        private ExpressionNode ParseGroup(int index, bool showEmptyBoundary)
        {
            if (preview) return ParseDraftGroup(index, showEmptyBoundary).WithSourceRange(index, ConsumedEnd);
            var cells = new List<ExpressionNode> { ParseExpression(0) };
            int rows = 1, columns = 0, currentColumns = 1;
            bool matrix = false;
            while (Current.Kind == TokenKind.Comma)
            {
                matrix = true; position++;
                // A trailing comma distinguishes a one-cell matrix from a scalar group.
                if (Current.Kind == TokenKind.Right && cells.Count == 1 && rows == 1) break;
                if (Current.Kind == TokenKind.Comma)
                {
                    position++;
                    if (columns == 0) columns = currentColumns;
                    if (currentColumns != columns) Fail();
                    rows++; currentColumns = 0;
                }
                cells.Add(ParseExpression(0)); currentColumns++;
                if (rows > ExpressionLimits.MaxMatrixDimension || currentColumns > ExpressionLimits.MaxMatrixDimension) ExpressionLimits.Fail();
            }
            if (Current.Kind != TokenKind.Right) Fail();
            position++;
            if (!matrix) return cells[0].WithSourceRange(index, ConsumedEnd);
            if (columns == 0) columns = currentColumns;
            if (columns != currentColumns) Fail();
            return ExpressionNode.Matrix(rows, columns, cells, index).WithSourceRange(index, ConsumedEnd);
        }

        private ExpressionNode ParseDraftGroup(int index, bool showEmptyBoundary)
        {
            var rows = new List<List<ExpressionNode>> { new List<ExpressionNode> { ParseExpression(0) } };
            var ends = new List<int>();
            bool matrix = false;
            while (Current.Kind != TokenKind.End && Current.Kind != TokenKind.Right)
            {
                if (Current.Kind != TokenKind.Comma)
                {
                    // Recovery within the current cell must not swallow its row
                    // separator, e.g. a relation typed while editing a matrix.
                    var last = rows[rows.Count - 1];
                    var parts = new List<ExpressionNode> { last[last.Count - 1], ParseExpression(0) };
                    last[last.Count - 1] = ExpressionNode.DraftRow(parts);
                    continue;
                }
                matrix = true;
                int boundary = Current.Index; position++;
                // Preserve the established closed one-cell-matrix notation (2,).
                if (Current.Kind == TokenKind.Right && rows.Count == 1 && rows[0].Count == 1) break;
                if (Current.Kind == TokenKind.Comma)
                {
                    ends.Add(boundary); position++;
                    rows.Add(new List<ExpressionNode>());
                }
                rows[rows.Count - 1].Add(ParseExpression(0));
                if (rows.Count > ExpressionLimits.MaxMatrixDimension || rows[rows.Count - 1].Count > ExpressionLimits.MaxMatrixDimension) ExpressionLimits.Fail();
            }
            ends.Add(Current.Index);
            if (Current.Kind == TokenKind.Right) position++;
            if (!matrix)
            {
                var body = rows[0][0];
                return showEmptyBoundary && body.Kind == ExpressionKind.Placeholder ? ExpressionNode.DraftGroup(body, index) : body;
            }
            int columns = rows.Max(row => row.Count);
            for (int row = 0; row < rows.Count; row++)
                while (rows[row].Count < columns) rows[row].Add(ExpressionNode.Missing(ends[row]));
            return ExpressionNode.Matrix(rows.Count, columns, rows.SelectMany(row => row), index);
        }

        private void Tokenize(string text)
        {
            for (int i = 0; i < text.Length;)
            {
                CalculationContext.Check();
                char c = text[i]; int start = i;
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (c == '[')
                {
                    i++; var comment = new System.Text.StringBuilder(); bool closed = false;
                    while (i < text.Length)
                    {
                        char next = text[i++];
                        if (next == ']') { closed = true; break; }
                        if (next == '\\' && i < text.Length) next = text[i++];
                        comment.Append(next);
                    }
                    if (!closed && !preview) Fail();
                    comments.Add(comment.ToString()); continue;
                }
                if (char.IsDigit(c) || c == '.')
                {
                    bool digit = false, point = false;
                    while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
                    {
                        if (text[i] == '.') { if (point) { if (preview) break; Fail(); } point = true; } else digit = true;
                        i++;
                    }
                    if (!digit && !preview) Fail();
                    if (i + 1 < text.Length && (text[i] == 'e' || text[i] == 'E'))
                    {
                        int exponent = i + 1;
                        if (text[exponent] == '+' || text[exponent] == '-') exponent++;
                        if (preview && exponent == text.Length && exponent > i + 1) i = exponent;
                        else if (exponent < text.Length && char.IsDigit(text[exponent]))
                        {
                            i = exponent + 1; while (i < text.Length && char.IsDigit(text[i])) i++;
                        }
                    }
                    tokens.Add(new Token(TokenKind.Number, text.Substring(start, i - start), start)); continue;
                }
                if (c == '(' || c == ')' || c == ',')
                {
                    tokens.Add(new Token(c == '(' ? TokenKind.Left : c == ')' ? TokenKind.Right : TokenKind.Comma, c.ToString(), i++)); continue;
                }
                if ("+-*/^!×÷−".IndexOf(c) >= 0)
                {
                    string op = c == '×' ? "*" : c == '÷' ? "/" : c == '−' ? "-" : c.ToString(); i++;
                    if (c == '!' && i < text.Length && text[i] == '!') { op = "!!"; i++; }
                    tokens.Add(new Token(TokenKind.Operator, op, start)); continue;
                }
                if (c == '√' || c == 'ρ') { tokens.Add(new Token(TokenKind.Function, c == '√' ? "sqrt" : "rho", i++, 1)); continue; }
                string matched = null;
                foreach (var op in new[] { "yroot", "nCr", "nPr", "mod" })
                    if (StartsWith(text, i, op)) { matched = op; break; }
                if (matched != null) { tokens.Add(new Token(TokenKind.Operator, matched, i)); i += matched.Length; continue; }
                foreach (var name in new[] { "Ans", "pi" })
                    if (StartsWith(text, i, name)) { matched = name; break; }
                if (matched != null) { tokens.Add(new Token(TokenKind.Symbol, matched == "pi" ? "π" : "Ans", i, matched.Length)); i += matched.Length; continue; }
                foreach (var name in FunctionCatalog.Tokens)
                    if (StartsWith(text, i, name)) { matched = name; break; }
                if (matched != null) { tokens.Add(new Token(TokenKind.Function, FunctionCatalog.Canonical(matched), i, matched.Length)); i += matched.Length; continue; }
                int nextIndex = i + 1;
                while (nextIndex < text.Length && char.IsWhiteSpace(text[nextIndex])) nextIndex++;
                if ((c == 'E' || c == 'D') && nextIndex < text.Length && text[nextIndex] == '(')
                { tokens.Add(new Token(TokenKind.Function, c.ToString(), i++)); continue; }
                if (char.IsLetter(c) || c == 'π' || c == '☺' || c == '?')
                { tokens.Add(new Token(TokenKind.Symbol, c == '☺' ? "Ans" : c.ToString(), i++, 1)); continue; }
                if (preview) { tokens.Add(new Token(TokenKind.Literal, c.ToString(), i++)); continue; }
                Fail();
            }
            tokens.Add(new Token(TokenKind.End, "", text.Length));
        }
        private static bool StartsWith(string text, int index, string token) => index + token.Length <= text.Length && string.Compare(text, index, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) == 0;
        private static void Fail() => throw new MathException(CalculationResources.GetString("invalid_expression"));
    }
}
