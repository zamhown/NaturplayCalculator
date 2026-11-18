using System;
using System.Collections.Generic;
using System.Linq;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Expressions
{
    public enum ExpressionKind { Number, Symbol, Unary, Binary, Function, Matrix, Placeholder, DraftGroup, DraftRow, DraftLiteral }

    // Immutable syntax shared by parsing, both evaluation domains, history and rendering.
    public sealed class ExpressionNode
    {
        public ExpressionKind Kind { get; }
        public string Text { get; }
        public IReadOnlyList<ExpressionNode> Children { get; }
        public int Rows { get; }
        public int Columns { get; }
        public int SourceIndex { get; }
        public int SourceStart { get; }
        public int SourceLength { get; }
        public int Depth { get; }
        public string Key { get; }

        private ExpressionNode(ExpressionKind kind, string text, IEnumerable<ExpressionNode> children, int index, int rows = 0, int columns = 0)
        {
            Kind = kind; Text = text; SourceIndex = SourceStart = index; Rows = rows; Columns = columns;
            SourceLength = kind == ExpressionKind.Placeholder ? 0 : index < 0 ? 0 : text.Length;
            var items = (children ?? Enumerable.Empty<ExpressionNode>()).ToArray();
            Children = Array.AsReadOnly(items);
            Depth = items.Length == 0 ? 1 : 1 + items.Max(item => item.Depth);
            if (Depth > 128) ExpressionLimits.Fail();
            Key = ExpressionFormatter.Format(this, false);
            if (Key.Length > ExpressionLimits.MaxResultLength) ExpressionLimits.Fail();
        }

        private ExpressionNode(ExpressionNode node, int start, int length)
        {
            Kind = node.Kind; Text = node.Text; Children = node.Children; Rows = node.Rows; Columns = node.Columns;
            SourceIndex = node.SourceIndex; Depth = node.Depth; Key = node.Key;
            SourceStart = start; SourceLength = Math.Max(0, length);
        }
        internal ExpressionNode WithSourceRange(int start, int end) => new ExpressionNode(this, start, end - start);

        public static ExpressionNode Number(string value, int index = -1) => new ExpressionNode(ExpressionKind.Number, value, null, index);
        public static ExpressionNode Symbol(string name, int index = -1) => new ExpressionNode(ExpressionKind.Symbol, name, null, index);
        public static ExpressionNode Unary(string op, ExpressionNode value, int index = -1) => new ExpressionNode(ExpressionKind.Unary, op, new[] { value }, index);
        public static ExpressionNode Binary(string op, ExpressionNode left, ExpressionNode right, int index = -1) => new ExpressionNode(ExpressionKind.Binary, op, new[] { left, right }, index);
        public static ExpressionNode Function(string name, ExpressionNode argument, int index = -1) => new ExpressionNode(ExpressionKind.Function, name, new[] { argument }, index);
        // Recovery-only syntax. The calculation entry point never uses ParsePreview.
        internal static ExpressionNode Missing(int index) => new ExpressionNode(ExpressionKind.Placeholder, "?", null, index);
        internal static ExpressionNode DraftGroup(ExpressionNode body, int index) => new ExpressionNode(ExpressionKind.DraftGroup, "", new[] { body }, index);
        internal static ExpressionNode DraftRow(IEnumerable<ExpressionNode> items, string precedence = "") => new ExpressionNode(ExpressionKind.DraftRow, precedence, items, -1);
        internal static ExpressionNode DraftLiteral(string text, int index) => new ExpressionNode(ExpressionKind.DraftLiteral, text, null, index);
        public static ExpressionNode Matrix(int rows, int columns, IEnumerable<ExpressionNode> cells, int index = -1)
        {
            if (rows < 1 || columns < 1 || rows > ExpressionLimits.MaxMatrixDimension || columns > ExpressionLimits.MaxMatrixDimension) ExpressionLimits.Fail();
            var values = cells.ToArray();
            if (values.Length != rows * columns) throw new ArgumentException("Invalid matrix shape.");
            return new ExpressionNode(ExpressionKind.Matrix, "", values, index, rows, columns);
        }
        internal ExpressionNode WithChildren(IEnumerable<ExpressionNode> children) => new ExpressionNode(Kind, Text, children, SourceIndex, Rows, Columns);
        public string ToCode() => Key;
        public string ToDisplayString() => ExpressionFormatter.Format(this, true);
        public override string ToString() => ToDisplayString();
    }

    public sealed class ParsedExpression
    {
        public ExpressionNode Root { get; internal set; }
        public IReadOnlyList<string> Comments { get; internal set; }
    }
}
