using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Expressions
{
    /// <summary>A calculator-owned edit boundary; never consumed by the TeX renderer.</summary>
    public sealed class TexEditRegion
    {
        public int TexStart { get; internal set; }
        public int TexLength { get; internal set; }
        public int InputStart { get; internal set; }
        public int InputLength { get; internal set; }
        public bool RequiresGrouping { get; internal set; }
    }
    public sealed class TexSourceRange
    {
        public int TexStart { get; }
        public int TexLength { get; }
        public int InputStart { get; }
        public int InputLength { get; }
        public int CaretStart { get; internal set; }
        public int CaretEnd { get; internal set; }
        internal TexSourceRange(int texStart, int texLength, int inputStart, int inputLength)
        { TexStart = CaretStart = texStart; TexLength = texLength; CaretEnd = texStart + texLength; InputStart = inputStart; InputLength = inputLength; }
    }
    /// <summary>The renderer receives Latex only. This optional sidecar belongs to the
    /// calculator host and maps existing input navigation to TeX source offsets.</summary>
    public sealed class TexExpression
    {
        public const string Protocol = "naturplay.math-tex/1";
        public string Latex { get; }
        public IReadOnlyList<TexSourceRange> SourceMap { get; }
        public IReadOnlyList<TexEditRegion> EditRegions { get; }
        internal TexExpression(string latex, IEnumerable<TexSourceRange> map, IEnumerable<TexEditRegion> regions)
        { Latex = latex; SourceMap = Array.AsReadOnly(map.ToArray()); EditRegions = Array.AsReadOnly(regions.ToArray()); }
        public TexEditRegion EditRegionAt(int offset) => EditRegions
            .Where(r => offset >= r.TexStart && offset <= r.TexStart + r.TexLength)
            .OrderBy(r => r.TexLength).FirstOrDefault();
        public int InputIndexAt(int texOffset)
        {
            var range = SourceMap.Where(r => texOffset >= r.TexStart && texOffset < r.TexStart + r.TexLength)
                .OrderBy(r => r.TexLength).FirstOrDefault();
            return range?.InputStart ?? -1;
        }
        public TexSourceRange RangeAtInput(int index) => SourceMap.Where(r => r.InputLength == 0 ? index == r.InputStart : index >= r.InputStart && index < r.InputStart + r.InputLength)
            .OrderBy(r => r.InputLength == 0 ? int.MaxValue : r.InputLength).FirstOrDefault();
        public int CaretOffsetAtInput(int index)
        {
            if (index < 0) return 0;
            var hole = SourceMap.FirstOrDefault(r => r.InputLength == 0 && r.InputStart == index);
            if (hole != null) return hole.CaretStart;
            var current = RangeAtInput(index);
            if (current != null) return index == current.InputStart ? current.CaretStart : current.CaretEnd;
            var previous = SourceMap.Where(r => r.InputStart + r.InputLength <= index)
                .OrderByDescending(r => r.InputStart + r.InputLength).ThenBy(r => r.InputLength).FirstOrDefault();
            var next = SourceMap.Where(r => r.InputStart > index).OrderBy(r => r.InputStart).FirstOrDefault();
            if (next != null && (previous == null || next.InputStart - index < index - previous.InputStart - previous.InputLength)) return next.CaretStart;
            return previous?.CaretEnd ?? next?.CaretStart ?? 0;
        }
        public int InputInsertionAt(int offset)
        {
            var hole = SourceMap.FirstOrDefault(r => r.InputLength == 0 && offset >= r.TexStart && offset <= r.TexStart + r.TexLength);
            if (hole != null) return hole.InputStart;
            // Source order is not display order for fractions, roots and scripts.
            // Match actual insertion boundaries before considering nearby syntax.
            var ends = SourceMap.Where(r => r.CaretEnd == offset && r.InputLength > 0).OrderBy(r => r.TexLength).FirstOrDefault();
            var starts = SourceMap.Where(r => r.CaretStart == offset && r.InputLength > 0).OrderBy(r => r.TexLength).FirstOrDefault();
            if (starts != null) return starts.InputStart;
            if (ends != null) return ends.InputStart + ends.InputLength;
            return SourceMap.SelectMany(r => new[] { new { Offset = r.CaretStart, Index = r.InputStart }, new { Offset = r.CaretEnd, Index = r.InputStart + r.InputLength } })
                .OrderBy(p => Math.Abs(p.Offset - offset)).Select(p => p.Index).DefaultIfEmpty(0).First();
        }
    }

    /// <summary>One-way calculation AST to standard math-TeX serialization.
    /// No drawing primitives, font metrics or renderer dependency cross this boundary.</summary>
    public sealed class ExpressionTexFormatter
    {
        private readonly StringBuilder text = new StringBuilder();
        private readonly List<TexSourceRange> map = new List<TexSourceRange>();
        private readonly List<TexEditRegion> editRegions = new List<TexEditRegion>();
        private bool implicitProducts, foldNumbers;
        private string source;
        public TexExpression Format(ExpressionNode expression, bool implicitProducts = false, bool foldNumbers = false, string inputSource = null)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            text.Clear(); map.Clear(); editRegions.Clear(); this.implicitProducts = implicitProducts; this.foldNumbers = foldNumbers; source = inputSource;
            Write(expression);
            if (text.Length > 32768) ExpressionLimits.Fail();
            return new TexExpression(text.ToString(), map, editRegions);
        }
        private TexSourceRange Token(string value, int input = -1, int length = 1)
        {
            TexSourceRange range = input >= 0 && value.Length > 0 ? new TexSourceRange(text.Length, value.Length, input, length) : null;
            if (range != null) map.Add(range);
            text.Append(value);
            return range;
        }
        private void Scope(ExpressionNode node, bool protect = true)
        {
            int start = text.Length;
            Write(node, true);
            if (source != null && node.SourceStart >= 0 && node.SourceStart + node.SourceLength <= source.Length)
                editRegions.Add(new TexEditRegion { TexStart = start, TexLength = text.Length - start,
                    InputStart = node.SourceStart, InputLength = node.SourceLength, RequiresGrouping = protect });
        }
        private void Group(ExpressionNode node, bool visible, bool scoped = false)
        {
            if (visible) Token(@"\left(");
            if (visible || scoped) Scope(node); else Write(node);
            if (visible) Token(@"\right)");
        }
        private void Argument(ExpressionNode node) { Token("{"); Scope(node); Token("}"); }
        private bool HasSourceGroup(ExpressionNode node)
        {
            int start = node.SourceStart, end = start + node.SourceLength;
            if (source == null || node.Kind == ExpressionKind.Matrix || start < 0 || start >= source.Length || end > source.Length || source[start] != '(') return false;
            int closing = ClosingAt(start);
            return closing < 0 || closing == end - 1;
        }
        private void SourceGroup(ExpressionNode node, bool visible)
        {
            int opening = node.SourceStart, closing = ClosingAt(opening);
            Token(visible ? @"\left(" : "{", opening);
            int start = text.Length;
            Write(node.Kind == ExpressionKind.DraftGroup ? node.Children[0] : node, true);
            editRegions.Add(new TexEditRegion { TexStart = start, TexLength = text.Length - start,
                InputStart = node.SourceStart, InputLength = node.SourceLength, RequiresGrouping = false });
            Token(visible ? @"\right)" : "}", closing);
        }
        private void Write(ExpressionNode node, bool insideScope = false)
        {
            // Retain the editing boundary of an explicit source group even when
            // its parentheses are redundant in mathematical presentation. TeX
            // braces provide a local row without drawing an extra pair of fences.
            if ((!insideScope || node.Kind == ExpressionKind.DraftGroup) && HasSourceGroup(node))
            { SourceGroup(node, node.Kind == ExpressionKind.DraftGroup); return; }
            if (node.Kind == ExpressionKind.Placeholder) { Token(@"\phantom{0}", node.SourceIndex, 0); return; }
            if (node.Kind == ExpressionKind.DraftGroup) { Group(node.Children[0], true); return; }
            if (node.Kind == ExpressionKind.DraftRow) { foreach (var child in node.Children) Write(child); return; }
            if (node.Kind == ExpressionKind.DraftLiteral)
            {
                Token(node.Text == "*" ? @"\times " : node.Text == "mod" ? @"\bmod " : node.Text == "…" ? @"\ldots "
                    : node.Text == "^" ? @"\text{^}" : Escape(node.Text), node.SourceIndex, node.Text.Length);
                return;
            }
            if (node.Kind == ExpressionKind.Number) { Number(node); return; }
            if (node.Kind == ExpressionKind.Symbol)
            {
                Token(node.Text == "π" ? @"\pi " : node.Text == "Ans" ? @"\operatorname{Ans}" : node.Text == "?" ? @"\phantom{0}" : Escape(node.Text), node.SourceIndex, SourceTokenLength(node));
                return;
            }
            if (node.Kind == ExpressionKind.Matrix) { Matrix(node, "pmatrix"); return; }
            if (node.Kind == ExpressionKind.Unary)
            { Token(node.Text, node.SourceIndex); Group(node.Children[0], NeedsUnaryGroup(node.Children[0])); return; }
            if (node.Kind == ExpressionKind.Function) { Function(node); return; }
            var left = node.Children[0]; var right = node.Children[1];
            switch (node.Text)
            {
                case "/":
                    var fraction = Token(@"\frac", node.SourceIndex);
                    Token("{"); Scope(left); int numeratorEnd = text.Length;
                    Token("}{"); int denominatorStart = text.Length; Scope(right); Token("}");
                    if (fraction != null) { fraction.CaretStart = numeratorEnd; fraction.CaretEnd = denominatorStart; }
                    break;
                case "yroot":
                    Token(@"\sqrt", node.SourceIndex); Token("["); Scope(right); Token("]"); Argument(left); break;
                case "^":
                    Group(left, NeedsPowerBaseGroup(left), true); Token("^", node.SourceIndex); Argument(right); break;
                case "nCr": case "nPr":
                    Token(node.Text == "nCr" ? @"\mathrm{C}" : @"\mathrm{A}", node.SourceIndex);
                    Token("_"); Argument(left); Token("^"); Argument(right); break;
                case "+": case "-":
                    Write(left); Token(node.Text, node.SourceIndex); Group(right, node.Text == "-" && IsSum(right) || IsNegative(right)); break;
                case "*":
                    bool omitMultiply = implicitProducts && ExpressionFormatter.CanOmitMultiply(left, right);
                    Group(left, IsSum(left) || omitMultiply && IsModulo(left));
                    if (!omitMultiply) Token(@"\times ", node.SourceIndex);
                    Group(right, IsSum(right) || IsNegative(right) || IsModulo(right)); break;
                case "mod":
                    Group(left, IsSum(left)); Token(@"\bmod ", node.SourceIndex);
                    Group(right, IsSum(right) || IsNegative(right) || right.Kind == ExpressionKind.Binary && Precedence(right) == 20); break;
                default: throw new ArgumentException("Unsupported calculation operator.");
            }
        }
        private void Function(ExpressionNode node)
        {
            var argument = node.Children[0];
            string name = ExpressionFormatter.DisplayFunctionName(node.Text);
            int tokenLength = SourceTokenLength(node), opening = FunctionOpening(node, tokenLength), closing = ClosingAt(opening);
            switch (name)
            {
                case "sqrt":
                    var radical = Token(@"\sqrt", node.SourceIndex, tokenLength);
                    Token("{", opening); if (radical != null) radical.CaretEnd = text.Length;
                    Scope(argument); Token("}", closing); break;
                case "Abs":
                    if (argument.Kind == ExpressionKind.Matrix) Matrix(argument, "vmatrix");
                    else { Token(@"\left|", node.SourceIndex); Scope(argument); Token(@"\right|"); }
                    break;
                case "transpose":
                    Group(argument, NeedsPowerBaseGroup(argument), true); Token(@"^{\mathrm{T}}", node.SourceIndex); break;
                case "fact": case "dfact":
                    Group(argument, NeedsUnaryGroup(argument) || IsScientificNumber(argument) || argument.Kind == ExpressionKind.Binary && argument.Text == "^"
                        || argument.Kind == ExpressionKind.Function && new[] { "transpose", "fact", "dfact" }.Contains(argument.Text), true);
                    Token(name == "fact" ? "!" : "!!", node.SourceIndex); break;
                default:
                    bool standard = new[] { "sin", "cos", "tan", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh", "ln", "lg" }.Contains(name);
                    string displayName = name == "E" ? "Ex" : name == "D" ? "Dx" : name == "Cov" ? "cov" : name;
                    Token(standard ? "\\" + name + " " : name == "rho" ? @"\rho " : @"\operatorname{" + Escape(displayName) + "}", node.SourceIndex, tokenLength);
                    if (argument.Kind == ExpressionKind.Matrix) Matrix(argument, "pmatrix", name == "Cov" || name == "rho");
                    else { Token(@"\left(", opening); Scope(argument); Token(@"\right)", closing); }
                    break;
            }
        }
        private void Matrix(ExpressionNode node, string environment, bool statistics = false)
        {
            Token(@"\begin{" + environment + "}", source == null ? -1 : node.SourceIndex);
            for (int row = 0; row < node.Rows; row++)
            {
                if (row > 0) Token(@"\\");
                if (statistics) Token(row % 2 == 0 ? @"\mathrm{X:}&" : @"\mathrm{Y:}&");
                for (int column = 0; column < node.Columns; column++)
                {
                    if (column > 0) Token("&");
                    Scope(node.Children[row * node.Columns + column], false);
                }
            }
            Token(@"\end{" + environment + "}", ClosingAt(node.SourceIndex));
        }
        private int SourceTokenLength(ExpressionNode node)
        {
            int index = node.SourceIndex;
            if (source == null || index < 0 || index >= source.Length) return 1;
            if (node.Kind == ExpressionKind.Function)
                foreach (string token in FunctionCatalog.Tokens)
                    if (index + token.Length <= source.Length && string.Compare(source, index, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) == 0) return token.Length;
            string name = node.Text == "π" ? "pi" : node.Text;
            return index + name.Length <= source.Length && string.Compare(source, index, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0 ? name.Length : 1;
        }
        private int FunctionOpening(ExpressionNode node, int tokenLength)
        {
            if (source == null || node.SourceIndex < 0) return -1;
            int index = node.SourceIndex + tokenLength;
            while (index < source.Length && char.IsWhiteSpace(source[index])) index++;
            return index < source.Length && source[index] == '(' ? index : -1;
        }
        private int ClosingAt(int opening)
        {
            if (source == null || opening < 0 || opening >= source.Length || source[opening] != '(') return -1;
            int depth = 0; bool comment = false, escaped = false;
            for (int i = opening; i < source.Length; i++)
            {
                char c = source[i];
                if (comment) { if (c == ']' && !escaped) comment = false; escaped = c == '\\' && !escaped; continue; }
                if (c == '[') { comment = true; continue; }
                if (c == '(') depth++;
                if (c == ')' && --depth == 0) return i;
            }
            return -1;
        }
        private void Number(ExpressionNode node)
        {
            string number = node.Text;
            int exponent = number.IndexOfAny(new[] { 'e', 'E' });
            int end = exponent >= 0 ? exponent : number.Length;
            int point = number.IndexOf('.');
            int folded = foldNumbers && point >= 0 && end - point > 4 ? point + 4 : end;
            for (int i = 0; i < folded; i++) Token(Escape(number[i].ToString()), node.SourceIndex < 0 ? -1 : node.SourceIndex + i);
            if (folded < end) Token(@"\ldots ", node.SourceIndex < 0 ? -1 : node.SourceIndex + folded, end - folded);
            if (exponent >= 0)
            {
                Token(@"\times 10^{", node.SourceIndex < 0 ? -1 : node.SourceIndex + exponent);
                for (int i = exponent + 1; i < number.Length; i++)
                    Token(number[i].ToString(), node.SourceIndex < 0 ? -1 : node.SourceIndex + i);
                if (exponent == number.Length - 1 || number.EndsWith("+", StringComparison.Ordinal) || number.EndsWith("-", StringComparison.Ordinal))
                    Token(@"\phantom{0}", node.SourceIndex < 0 ? -1 : node.SourceIndex + number.Length, 0);
                Token("}");
            }
        }
        private static string Escape(string value)
        {
            var result = new StringBuilder();
            foreach (char c in value)
                result.Append(c == '\\' ? @"\backslash " : "{}%_#&$".IndexOf(c) >= 0 ? "\\" + c : c == '∞' ? @"\infty " : c.ToString());
            return result.ToString();
        }
        private static bool IsSum(ExpressionNode n) => n.Kind == ExpressionKind.Binary && (n.Text == "+" || n.Text == "-") || n.Kind == ExpressionKind.DraftRow && n.Text == "sum";
        private static bool IsModulo(ExpressionNode n) => n.Kind == ExpressionKind.Binary && n.Text == "mod";
        private static bool IsNegative(ExpressionNode n) => n.Kind == ExpressionKind.Unary || n.Kind == ExpressionKind.Number && n.Text.StartsWith("-", StringComparison.Ordinal);
        private static bool IsScientificNumber(ExpressionNode n) => n.Kind == ExpressionKind.Number && n.Text.IndexOfAny(new[] { 'e', 'E' }) >= 0;
        private static int Precedence(ExpressionNode n) => n.Kind == ExpressionKind.Binary && (n.Text == "/" || n.Text == "yroot") ? 100 : ExpressionFormatter.Precedence(n);
        private static bool NeedsUnaryGroup(ExpressionNode n) => IsNegative(n) || Precedence(n) < 25;
        private static bool NeedsPowerBaseGroup(ExpressionNode n) => IsNegative(n) || IsScientificNumber(n) || n.Kind == ExpressionKind.DraftRow || n.Kind == ExpressionKind.Binary && n.Text != "yroot"
            || n.Kind == ExpressionKind.Function && n.Text == "transpose";
    }
}
