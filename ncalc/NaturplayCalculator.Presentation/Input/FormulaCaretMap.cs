using System;
using System.Collections.Generic;
using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.Model;

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
        public bool FollowsStructure { get; internal set; }
    }
    public sealed class FormulaCaretMap
    {
        private readonly TexExpression formula;
        private readonly TexRenderResult layout;
        private readonly string source;
        private readonly int textLength;
        public IReadOnlyList<FormulaCaretSlot> Slots { get; }
        public FormulaCaretMap(string source, TexExpression formula, TexRenderResult layout)
        {
            this.formula = formula; this.layout = layout; this.source = source; textLength = source.Length;
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
                        FollowsStructure = previous?.Kind == TexKind.Fraction || previous?.Kind == TexKind.Scripts,
                        TextIndex = index, Region = region };
                }).ToArray();
        }
        public FormulaCaretSlot FromText(int index, bool preferRootScope = false)
        {
            index = Math.Max(0, Math.Min(textLength, index));
            if (index == 0) return Slots.First(s => s.IsRootStart);
            if (index == textLength) return Slots.Last(s => s.IsRootEnd);
            if (preferRootScope)
            {
                // Leaving a fraction can share its source index with the end
                // of its denominator. Retain the parent-row identity when the
                // host rebinds a whole-expression buffer after rendering/undo.
                var root = Slots.Where(slot => slot.Region == null && slot.TextIndex == index)
                    .OrderByDescending(slot => slot.Geometry.Row.Length).FirstOrDefault();
                if (root != null) return root;
            }
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

        /// <summary>Leaves the current native edit buffer for its immediate enclosing
        /// formula scope. The returned stop is before or after the complete child
        /// structure in the parent row, not inside a sibling operand.</summary>
        public FormulaCaretSlot ParentScope(FormulaEditSession session, FormulaCaretSlot current = null, bool after = true)
        {
            if (session == null || !session.IsScoped || session.Source != source) return null;
            var matches = formula.EditRegions.Where(region =>
            {
                FormulaEditSession.GetContentRange(source, region, out int start, out int end);
                if (start != session.ContentStart || end < session.ContentEnd) return false;
                // Native composition excludes the closing delimiters generated
                // by FormulaEditSession. Its complete rendered scope may thus
                // be longer than Buffer, while still referring to that buffer.
                for (int i = session.ContentEnd; i < end; i++)
                    if (source[i] != ')' && source[i] != ']' && source[i] != '\\') return false;
                return true;
            }).ToArray();
            // Multiple TeX wrappers can describe the same native buffer. Leave
            // all of those together, rather than presenting a no-op parent step.
            var scope = matches.Where(region => current == null || Contains(region, current.Geometry.Row))
                .OrderByDescending(region => region.TexLength).FirstOrDefault()
                ?? matches.OrderByDescending(region => region.TexLength).FirstOrDefault();
            if (scope == null) return null;
            FormulaEditSession.GetContentRange(source, scope, out int scopeStart, out int scopeEnd);
            var parent = formula.EditRegions.Where(region => region != scope
                && region.TexStart <= scope.TexStart && region.TexStart + region.TexLength >= scope.TexStart + scope.TexLength)
                .Where(region =>
                {
                    FormulaEditSession.GetContentRange(source, region, out int start, out int end);
                    return start != scopeStart || end != scopeEnd;
                })
                .OrderBy(region => region.TexLength).FirstOrDefault();

            int endOffset = scope.TexStart + scope.TexLength;
            FormulaEditSession.GetContentRange(source, parent, out int parentStart, out int parentEnd);
            int boundary = Math.Max(parentStart, Math.Min(parentEnd, CompleteChildBoundary(scope, after)));
            var candidates = Slots.Where(slot => slot.Region == parent
                && slot.Geometry.Row.Start <= scope.TexStart && slot.Geometry.Row.End >= endOffset);
            return after
                ? candidates.Where(slot => slot.Geometry.Offset >= endOffset && slot.TextIndex >= boundary)
                    .OrderBy(slot => slot.Geometry.Offset).ThenByDescending(slot => slot.Geometry.Row.Length).FirstOrDefault()
                : candidates.Where(slot => slot.Geometry.Offset <= scope.TexStart && slot.TextIndex <= boundary)
                    .OrderByDescending(slot => slot.Geometry.Offset).ThenByDescending(slot => slot.Geometry.Row.Length).FirstOrDefault();
        }

        private int CompleteChildBoundary(TexEditRegion scope, bool after)
        {
            int boundary = scope.InputStart + (after ? scope.InputLength : 0);
            try
            {
                // Fractions and scripts are composite TeX atoms, whereas a
                // factorial is an ordinary trailing glyph in its parent's row.
                // Locate the calculator structure owning the operand so leaving
                // it also passes its suffix and any source closing delimiter.
                var owner = FindOwner(new ExpressionParser().ParsePreview(source).Root, scope);
                return owner == null ? boundary : owner.SourceStart + (after ? owner.SourceLength : 0);
            }
            catch (MathException) { return boundary; }
        }

        private static ExpressionNode FindOwner(ExpressionNode node, TexEditRegion scope)
        {
            bool structure = node.Kind == ExpressionKind.Function || node.Kind == ExpressionKind.Matrix
                || node.Kind == ExpressionKind.Binary && (node.Text == "/" || node.Text == "^" || node.Text == "yroot"
                    || node.Text == "nCr" || node.Text == "nPr");
            ExpressionNode result = structure && node.Children.Any(child => child.SourceStart == scope.InputStart
                && child.SourceLength == scope.InputLength) ? node : null;
            foreach (var child in node.Children)
            {
                var candidate = FindOwner(child, scope);
                if (candidate != null && (result == null || candidate.SourceLength < result.SourceLength)) result = candidate;
            }
            return result;
        }

        private static bool Contains(TexEditRegion region, TexSpan row) =>
            row.Start >= region.TexStart && row.End <= region.TexStart + region.TexLength;
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
            if (direction != -1 && direction != 1) throw new ArgumentOutOfRangeException(nameof(direction));
            // Row identity matters at shared source boundaries. A caret after
            // an entire fraction must not be treated as inside its denominator.
            foreach (var node in layout.Document.Descendants()
                .OrderBy(n => n.Span.Length))
            {
                TexNode target = null;
                if (node.Kind == TexKind.Fraction)
                {
                    int from = direction > 0 ? 0 : 1;
                    if (ContainsRow(node.Children[from], current)) target = node.Children[1 - from];
                }
                else if (node.Kind == TexKind.Scripts)
                {
                    // A power is a base and exponent, not two arbitrary rows
                    // that happen to sit near one another on the screen.
                    if (direction > 0 && ContainsRow(node.Children[1], current)
                        || direction < 0 && ContainsRow(node.Children[2], current)) target = node.Children[0];
                    else if (ContainsRow(node.Children[0], current)) target = node.Children[direction < 0 ? 1 : 2];
                }
                else if (node.Kind == TexKind.Radical && node.Children.Count == 2)
                {
                    int from = direction < 0 ? 0 : 1;
                    if (ContainsRow(node.Children[from], current)) target = node.Children[1 - from];
                }
                else if (node.Kind == TexKind.Matrix)
                {
                    for (int cell = 0; cell < node.Children.Count; cell++)
                    {
                        if (!ContainsRow(node.Children[cell], current)) continue;
                        int next = cell + direction * node.Columns;
                        if (next >= 0 && next < node.Children.Count) target = node.Children[next];
                        break;
                    }
                }
                if (target == null) continue;
                // Prefer the sibling's outer editable row. A fraction nested
                // inside that row should not steal a vertical move just because
                // one of its inner glyphs is closer to the current X coordinate.
                var candidates = Slots.Where(s => ContainsRow(target, s)).ToArray();
                if (candidates.Length == 0) continue;
                int rowLength = candidates.Max(s => s.Geometry.Row.Length);
                return candidates.Where(s => s.Geometry.Row.Length == rowLength)
                    .OrderBy(s => Math.Abs(s.Geometry.X - current.Geometry.X)).First();
            }
            // At a structural edge, stay put. Global nearest-Y navigation can
            // jump from an ordinary term into an unrelated neighbouring power.
            return current;
        }

        private static bool ContainsRow(TexNode node, FormulaCaretSlot slot) =>
            slot.Geometry.Row.Start >= node.Span.Start && slot.Geometry.Row.End <= node.Span.End;
    }
}
