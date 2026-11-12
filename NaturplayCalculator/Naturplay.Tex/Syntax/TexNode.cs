using System;
using System.Collections.Generic;
using System.Linq;

namespace Naturplay.Tex
{
    public enum TexKind { Row, Glyph, Space, Fraction, Radical, Scripts, Delimited, Matrix, Styled, Accent, Phantom }
    public enum TexAtomClass { Ordinary, Operator, Binary, Relation, Open, Close, Punctuation, Inner }
    public enum TexFont { MathItalic, Roman, Bold }
    public enum TexMathStyle { Display, Text, Script, ScriptScript }

    public struct TexSpan
    {
        public int Start { get; }
        public int Length { get; }
        public int End => Start + Length;
        public TexSpan(int start, int length)
        {
            if (start < 0 || length < 0) throw new ArgumentOutOfRangeException();
            Start = start; Length = length;
        }
        public bool Contains(int offset) => offset >= Start && offset <= End;
        public bool Intersects(TexSpan other) => Start < other.End && other.Start < End;
        public override string ToString() => Start + ":" + Length;
    }

    /// <summary>TeX syntax only. No calculator AST, arithmetic or platform types.</summary>
    public sealed class TexNode
    {
        public TexKind Kind { get; }
        public TexSpan Span { get; }
        public TexSpan ContentSpan { get; }
        public string Text { get; }
        public string Closing { get; }
        public TexAtomClass AtomClass { get; }
        public TexFont Font { get; }
        public IReadOnlyList<TexNode> Children { get; }
        public int Rows { get; }
        public int Columns { get; }
        public double SpaceEm { get; }
        public bool Limits { get; }

        internal TexNode(TexKind kind, TexSpan span, string text = "", IEnumerable<TexNode> children = null,
            TexAtomClass atomClass = TexAtomClass.Ordinary, TexFont font = TexFont.Roman,
            string closing = "", int rows = 0, int columns = 0, double spaceEm = 0,
            bool limits = false, TexSpan? contentSpan = null)
        {
            Kind = kind; Span = span; ContentSpan = contentSpan ?? span; Text = text; Closing = closing;
            Children = Array.AsReadOnly((children ?? Enumerable.Empty<TexNode>()).ToArray());
            AtomClass = atomClass; Font = font; Rows = rows; Columns = columns; SpaceEm = spaceEm; Limits = limits;
        }
    }

    public sealed class TexDocument
    {
        public string Source { get; }
        public TexNode Root { get; }
        internal TexDocument(string source, TexNode root) { Source = source; Root = root; }
        public IEnumerable<TexNode> Descendants()
        {
            var stack = new Stack<TexNode>(); stack.Push(Root);
            while (stack.Count > 0)
            {
                var item = stack.Pop(); yield return item;
                for (int i = item.Children.Count - 1; i >= 0; i--) stack.Push(item.Children[i]);
            }
        }
    }

    public sealed class TexParseException : FormatException
    {
        public int Position { get; }
        public TexParseException(string message, int position) : base(message + " (TeX offset " + position + ")") { Position = position; }
    }

    public static class TexLimits
    {
        public const int MaxSourceLength = 32768;
        // TeX groups and arguments add multiple parser levels per calculation
        // node; keep the 48-level calculator input budget renderable as TeX.
        public const int MaxDepth = 256;
        public const int MaxNodes = 8192;
        public const int MaxMatrixDimension = 32;
    }
}
