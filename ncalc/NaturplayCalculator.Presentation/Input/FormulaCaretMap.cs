using System;
using System.Collections.Generic;
using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;

namespace NaturplayCalculator.Input
{
    /// <summary>The host bridge between two different caret spaces. A formula slot
    /// retains its TeX row and offset even when several slots map to the same text index.</summary>
    public sealed class FormulaCaretSlot
    {
        public TexCaretStop Geometry { get; internal set; }
        public int TextIndex { get; internal set; }
        public TexEditRegion Region { get; internal set; }
        public bool IsRootStart { get; internal set; }
        public bool IsRootEnd { get; internal set; }
        public bool FollowsFraction { get; internal set; }
    }
    public sealed class FormulaCaretMap
    {
        private readonly TexExpression formula;
        private readonly TexRenderResult layout;
        private readonly int textLength;
        public IReadOnlyList<FormulaCaretSlot> Slots { get; }
        public FormulaCaretMap(string source, TexExpression formula, TexRenderResult layout)
        {
            this.formula = formula; this.layout = layout; textLength = source.Length;
            var root = layout.Document.Root.ContentSpan;
            var rows = layout.Document.Descendants().Where(n => n.Kind == TexKind.Row).ToArray();
            Slots = layout.Carets.GroupBy(c => new { c.Offset, c.Row.Start, c.Row.Length })
                .Select(g => g.First()).OrderBy(c => c.Offset)
                // At a shared boundary enter an outer row before its child;
                // leave a child before the enclosing row.
                .ThenBy(c => c.Offset == c.Row.Start ? -c.Row.Length : c.Row.Length)
                .Select(c => {
                    bool start = c.Row.Equals(root) && c.Offset == root.Start;
                    bool end = c.Row.Equals(root) && c.Offset == root.End;
                    var region = start || end ? null : formula.EditRegions
                            .Where(r => c.Offset >= r.TexStart && c.Offset <= r.TexStart + r.TexLength
                                && c.Row.Start >= r.TexStart && c.Row.End <= r.TexStart + r.TexLength)
                            .OrderBy(r => r.TexLength).FirstOrDefault();
                    int index = start ? 0 : end ? textLength : formula.InputInsertionAt(c.Offset);
                    FormulaEditSession.GetContentRange(source, region, out int contentStart, out int contentEnd);
                    if (region != null) index = Math.Max(contentStart, Math.Min(contentEnd, index));
                    var row = rows.FirstOrDefault(n => n.ContentSpan.Equals(c.Row));
                    var previous = row?.Children.LastOrDefault(n => n.Kind != TexKind.Space && n.Span.End <= c.Offset);
                    return new FormulaCaretSlot { Geometry = c, IsRootStart = start, IsRootEnd = end, FollowsFraction = previous?.Kind == TexKind.Fraction,
                        TextIndex = index, Region = region };
                }).ToArray();
        }
        public FormulaCaretSlot FromText(int index)
        {
            index = Math.Max(0, Math.Min(textLength, index));
            if (index == 0) return Slots.First(s => s.IsRootStart);
            if (index == textLength) return Slots.Last(s => s.IsRootEnd);
            int offset = formula.CaretOffsetAtInput(index);
            var exact = Slots.Where(s => s.Geometry.Offset == offset).OrderBy(s => s.Geometry.Row.Length).FirstOrDefault();
            // A source delimiter or command terminator need not own a formula
            // stop. Resolve it in input space, not by distance through TeX syntax.
            return exact ?? Slots.OrderBy(s => Math.Abs(s.TextIndex - index))
                .ThenBy(s => Math.Abs(s.Geometry.Offset - offset)).ThenBy(s => s.Geometry.Row.Length).First();
        }
        public FormulaCaretSlot FromGeometry(TexCaretStop caret) => Slots.FirstOrDefault(s =>
            s.Geometry.Offset == caret.Offset && s.Geometry.Row.Equals(caret.Row)) ??
            Slots.OrderBy(s => Math.Abs(s.Geometry.X - caret.X) + 3 * Math.Abs(s.Geometry.Top - caret.Top)).First();
        public FormulaCaretSlot AfterEdit(int textIndex, int? contextStart = null, int? contextEnd = null)
        {
            var candidates = Slots.Where(s => s.TextIndex == textIndex).ToArray();
            if (candidates.Length == 0) return FromText(textIndex);
            // An edit within a numerator/denominator must not jump to the outer
            // slot simply because that slot has the same linear source index.
            if (contextStart.HasValue)
            {
                var inside = candidates.Where(s => s.Region != null && s.Region.InputStart <= contextEnd.Value
                    && s.Region.InputStart + s.Region.InputLength >= contextStart.Value)
                    .OrderBy(s => s.Geometry.Row.Length).FirstOrDefault();
                if (inside != null) return inside;
            }
            return candidates.OrderBy(s => s.Geometry.Row.Length).First();
        }
        public FormulaCaretSlot Move(FormulaCaretSlot current, int delta)
        {
            int index = Slots.ToList().IndexOf(current);
            if (index < 0) index = Slots.ToList().IndexOf(FromGeometry(current.Geometry));
            return Slots[Math.Max(0, Math.Min(Slots.Count - 1, index + delta))];
        }
        public FormulaCaretSlot RowBoundary(FormulaCaretSlot current, bool end) =>
            (end ? Slots.Reverse() : Slots).First(s => s.Geometry.Row.Equals(current.Geometry.Row));
        public FormulaCaretSlot MoveVertical(FormulaCaretSlot current, int direction)
        {
            foreach (var node in layout.Document.Descendants().Where(n => n.Kind == TexKind.Fraction)
                .OrderBy(n => n.Span.Length))
            {
                int from = direction > 0 ? 0 : 1;
                if (!node.Children[from].Span.Contains(current.Geometry.Offset)) continue;
                var target = node.Children[1 - from].Span;
                var sibling = Slots.Where(s => s.Geometry.Row.Start >= target.Start && s.Geometry.Row.End <= target.End)
                    .OrderBy(s => Math.Abs(s.Geometry.X - current.Geometry.X)).FirstOrDefault();
                if (sibling != null) return sibling;
            }
            double y = current.Geometry.Top + current.Geometry.Height / 2;
            return Slots.Where(s => (s.Geometry.Top + s.Geometry.Height / 2 - y) * direction > 1)
                .OrderBy(s => 3 * Math.Abs(s.Geometry.Top + s.Geometry.Height / 2 - y) + Math.Abs(s.Geometry.X - current.Geometry.X))
                .FirstOrDefault() ?? current;
        }
    }
}
