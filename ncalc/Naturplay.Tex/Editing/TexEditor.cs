using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Naturplay.Tex
{
    /// <summary>Opt-in visual editing model. All offsets refer to TeX source boundaries,
    /// not calculator input. Edits are atomic and keep commands/groups syntactically valid.</summary>
    public sealed class TexEditor
    {
        private readonly TexLayoutEngine engine;
        private readonly double fontSize;
        private readonly List<Snapshot> undo = new List<Snapshot>(), redo = new List<Snapshot>();
        private sealed class Snapshot { public string Source; public int Anchor, Focus; public TexSpan? FocusRow; }
        private TexSpan? focusRow;
        public string Latex { get; private set; }
        public int Anchor { get; private set; }
        public int Focus { get; private set; }
        public TexSpan Selection => new TexSpan(Math.Min(Anchor, Focus), Math.Abs(Focus - Anchor));
        public TexRenderResult Layout { get; private set; }
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public TexCaretStop Caret => Layout.Carets.Where(c => c.Offset == Focus).OrderBy(c => focusRow.HasValue && c.Row.Equals(focusRow.Value) ? 0 : 1).ThenBy(c => c.Row.Length).FirstOrDefault()
            ?? Layout.Carets.OrderBy(c => Math.Abs(c.Offset - Focus)).First();

        public TexEditor(string latex = "", ITexFontMetrics metrics = null, double fontSize = 24)
        {
            engine = new TexLayoutEngine(metrics); this.fontSize = fontSize;
            Layout = engine.Render(latex, fontSize); Latex = latex;
        }
        public void SetSelection(int anchor, int focus)
        {
            if (anchor < 0 || focus < 0 || anchor > Latex.Length || focus > Latex.Length) throw new ArgumentOutOfRangeException();
            Anchor = NearestOffset(anchor); Focus = NearestOffset(focus); focusRow = null;
        }
        public void HitTest(double x, double y, bool extend = false)
        { var stop = Layout.HitTest(x, y); MoveTo(stop.Offset, extend, stop.Row); }
        public void MoveLeft(bool extend = false)
        {
            if (!extend && Anchor != Focus) { MoveTo(Selection.Start, false); return; }
            if (MoveAcrossSharedBoundary(-1, extend)) return;
            MoveTo(Layout.Carets.Where(c => c.Offset < Focus).Select(c => c.Offset).DefaultIfEmpty(0).Max(), extend);
        }
        public void MoveRight(bool extend = false)
        {
            if (!extend && Anchor != Focus) { MoveTo(Selection.End, false); return; }
            if (MoveAcrossSharedBoundary(1, extend)) return;
            MoveTo(Layout.Carets.Where(c => c.Offset > Focus).Select(c => c.Offset).DefaultIfEmpty(Latex.Length).Min(), extend);
        }
        public void MoveHome(bool extend = false) => MoveTo(Caret.Row.Start, extend, Caret.Row);
        public void MoveEnd(bool extend = false) => MoveTo(Caret.Row.End, extend, Caret.Row);
        private bool MoveAcrossSharedBoundary(int direction, bool extend)
        {
            var current = Caret;
            // Unbraced scripts can share a source offset with the outer row while
            // occupying a different visual position. Retain that caret affinity.
            var target = Layout.Carets.Where(c => c.Offset == Focus && !c.Row.Equals(current.Row)
                && (Math.Abs(c.X - current.X) > 0.01 || Math.Abs(c.Top - current.Top) > 0.01)
                && (direction > 0
                    ? current.Row.End == Focus && c.Row.Length > current.Row.Length || c.Row.Start == Focus && c.Row.Length < current.Row.Length
                    : current.Row.Start == Focus && c.Row.Length > current.Row.Length || c.Row.End == Focus && c.Row.Length < current.Row.Length))
                .OrderBy(c => Math.Abs(c.Row.Length - current.Row.Length)).FirstOrDefault();
            if (target == null) return false;
            MoveTo(target.Offset, extend, target.Row); return true;
        }
        public void MoveVertical(int direction, bool extend = false)
        {
            if (direction != -1 && direction != 1) throw new ArgumentOutOfRangeException(nameof(direction));
            var current = Caret;
            foreach (var node in Layout.Document.Descendants().Where(n => n.Span.Contains(Focus)).OrderBy(n => n.Span.Length))
            {
                TexNode target = null;
                if (node.Kind == TexKind.Fraction)
                {
                    int origin = direction > 0 ? 0 : 1;
                    if (node.Children[origin].Span.Contains(Focus)) target = node.Children[1 - origin];
                }
                else if (node.Kind == TexKind.Scripts)
                {
                    if (direction < 0 && node.Children[2].Span.Contains(Focus)) target = node.Children[0];
                    else if (direction > 0 && node.Children[1].Span.Contains(Focus)) target = node.Children[0];
                    else if (node.Children[0].Span.Contains(Focus)) target = node.Children[direction < 0 ? 1 : 2];
                }
                else if (node.Kind == TexKind.Radical && node.Children.Count == 2)
                {
                    if (node.Children[direction < 0 ? 0 : 1].Span.Contains(Focus)) target = node.Children[direction < 0 ? 1 : 0];
                }
                else if (node.Kind == TexKind.Matrix)
                {
                    for (int i = 0; i < node.Children.Count; i++)
                        if (node.Children[i].Span.Contains(Focus))
                        {
                            int next = i + direction * node.Columns;
                            if (next >= 0 && next < node.Children.Count) target = node.Children[next];
                            break;
                        }
                }
                if (target != null && target.Span.Length > 0)
                {
                    var stop = Layout.Carets.Where(c => target.Span.Contains(c.Offset))
                        .OrderBy(c => Math.Abs(c.X - current.X)).ThenBy(c => c.Row.Length).FirstOrDefault();
                    if (stop != null) { MoveTo(stop.Offset, extend, stop.Row); return; }
                }
            }
            var candidate = Layout.Carets.Where(c => direction * (c.Top - current.Top) > current.Height * 0.25)
                .OrderBy(c => Math.Abs(c.X - current.X) * 2 + Math.Abs(c.Top - current.Top)).FirstOrDefault();
            if (candidate != null) MoveTo(candidate.Offset, extend, candidate.Row);
        }
        public void InsertLatex(string fragment)
        {
            new TexParser().Parse(fragment); // Validation happens before any state is changed.
            Replace(Selection, fragment, fragment.Length);
        }
        public void InsertFraction()
        {
            var range = WholeRange(Selection);
            string selected = Latex.Substring(range.Start, range.Length);
            Replace(range, "\\frac{" + selected + "}{}", 8 + selected.Length);
        }
        public void InsertRoot()
        {
            var range = WholeRange(Selection);
            string selected = Latex.Substring(range.Start, range.Length);
            Replace(range, "\\sqrt{" + selected + "}", 6 + selected.Length);
        }
        public void InsertSuperscript() => InsertScript('^');
        public void InsertSubscript() => InsertScript('_');
        private void InsertScript(char kind)
        {
            var span = WholeRange(Selection);
            if (span.Length == 0)
            {
                var previous = Layout.Document.Descendants().Where(n => n.Kind != TexKind.Row && n.Span.End == Focus)
                    .Where(n => Caret.Row.Contains(n.Span.Start) && Caret.Row.Contains(n.Span.End))
                    .OrderByDescending(n => n.Span.Length).FirstOrDefault();
                if (previous != null) span = previous.Span;
            }
            span = WholeRange(span);
            string selected = Latex.Substring(span.Start, span.Length);
            Replace(span, "{" + selected + "}" + kind + "{}", selected.Length + 4);
        }
        public void DeleteBackward() => Delete(-1);
        public void DeleteForward() => Delete(1);
        private void Delete(int direction)
        {
            var argument = UnbracedArguments().FirstOrDefault(n => n.Span.Equals(Caret.Row)
                && (Selection.Length > 0 ? n.Span.Equals(Selection) : direction < 0 ? n.Span.End == Focus : n.Span.Start == Focus));
            if (argument != null) { Commit(argument.Span, "{}", 1); return; }
            if (Selection.Length > 0) { Replace(Selection, "", 0); return; }
            var candidates = Layout.Document.Descendants().Where(n => n.Kind != TexKind.Row && n.Span.Length > 0
                && (direction < 0 ? n.Span.End == Focus : n.Span.Start == Focus));
            var target = candidates.OrderByDescending(n => n.Span.Length).FirstOrDefault();
            if (target != null)
            {
                if (UnbracedArguments().Any(n => n.Span.Equals(target.Span))) Commit(target.Span, "{}", 1);
                else Replace(target.Span, "", 0);
            }
        }
        private void Replace(TexSpan selection, string fragment, int caretInFragment)
        {
            var range = WholeRange(selection);
            var argument = UnbracedArguments().FirstOrDefault(n => n.Span.Equals(Caret.Row)
                && n.Span.Contains(range.Start) && n.Span.Contains(range.End));
            if (argument != null)
            {
                string before = Latex.Substring(argument.Span.Start, range.Start - argument.Span.Start);
                string after = Latex.Substring(range.End, argument.Span.End - range.End);
                Commit(argument.Span, "{" + before + fragment + after + "}", 1 + before.Length + caretInFragment);
            }
            else Commit(range, fragment, caretInFragment);
        }
        private void Commit(TexSpan range, string fragment, int caretInFragment)
        {
            string prefix = Latex.Substring(0, range.Start), suffix = Latex.Substring(range.End);
            string leftSpace = fragment.Length > 0 && char.IsLetter(fragment[0]) && Regex.IsMatch(prefix, @"\\[A-Za-z]+$") ? " " : "";
            string rightSpace = suffix.Length > 0 && char.IsLetter(suffix[0]) && Regex.IsMatch(fragment, @"\\[A-Za-z]+$") ? " " : "";
            string next = prefix + leftSpace + fragment + rightSpace + suffix;
            var rendered = engine.Render(next, fontSize);
            Remember(undo, Current()); redo.Clear();
            Latex = next; Layout = rendered;
            Focus = Anchor = NearestOffset(range.Start + leftSpace.Length + caretInFragment); focusRow = null;
        }
        private IEnumerable<TexNode> UnbracedArguments() => Layout.Document.Descendants()
            .Where(n => n.Kind == TexKind.Fraction || n.Kind == TexKind.Radical || n.Kind == TexKind.Scripts || n.Kind == TexKind.Styled || n.Kind == TexKind.Accent)
            .SelectMany(n => n.Kind == TexKind.Scripts ? n.Children.Skip(1) : n.Children)
            .Where(n => n.Kind != TexKind.Row && n.Span.Length > 0);
        private TexSpan WholeRange(TexSpan selection)
        {
            if (selection.Length == 0) return selection;
            var argument = UnbracedArguments().Where(n => n.Span.Contains(selection.Start) && n.Span.Contains(selection.End))
                .OrderBy(n => n.Span.Length).FirstOrDefault();
            if (argument != null) return argument.Span;
            // Normalize before editing: merely reparsing a cut such as a}{b can
            // otherwise steal the following '+' as a new fraction argument.
            var row = Layout.Document.Descendants().Where(n => n.Kind == TexKind.Row
                && n.ContentSpan.Contains(selection.Start) && n.ContentSpan.Contains(selection.End))
                .OrderBy(n => n.ContentSpan.Length).First();
            int start = selection.Start, end = selection.End;
            foreach (var child in row.Children.Where(n => n.Span.Intersects(selection)))
            { start = Math.Min(start, child.Span.Start); end = Math.Max(end, child.Span.End); }
            return new TexSpan(start, end - start);
        }
        public bool Undo() => Restore(undo, redo);
        public bool Redo() => Restore(redo, undo);
        private bool Restore(List<Snapshot> from, List<Snapshot> to)
        {
            if (from.Count == 0) return false;
            var previous = from[from.Count - 1];
            var rendered = engine.Render(previous.Source, fontSize);
            Remember(to, Current()); from.RemoveAt(from.Count - 1);
            Latex = previous.Source; Layout = rendered; Anchor = previous.Anchor; Focus = previous.Focus; focusRow = previous.FocusRow; return true;
        }
        private Snapshot Current() => new Snapshot { Source = Latex, Anchor = Anchor, Focus = Focus, FocusRow = focusRow };
        private static void Remember(List<Snapshot> stack, Snapshot value) { stack.Add(value); if (stack.Count > 128) stack.RemoveAt(0); }
        private int NearestOffset(int offset) => Layout.Carets.OrderBy(c => Math.Abs(c.Offset - offset)).ThenBy(c => c.Row.Length).First().Offset;
        private void MoveTo(int offset, bool extend, TexSpan? row = null) { Focus = offset; focusRow = row; if (!extend) Anchor = Focus; }
    }
}
