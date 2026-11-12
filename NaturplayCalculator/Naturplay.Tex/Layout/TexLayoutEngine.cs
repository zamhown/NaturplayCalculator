using System;
using System.Collections.Generic;
using System.Linq;

namespace Naturplay.Tex
{
    public sealed class TexLayoutEngine
    {
        private readonly ITexFontMetrics metrics;
        private double baseSize;
        private sealed class Placement { public Box Box; public double X, Baseline; }
        private sealed class Box
        {
            public TexNode Node;
            public double Width, Ascent, Descent, Size;
            public double CaretAscent, CaretDescent;
            public double VisualAscent, VisualDescent;
            public double InkLeft, InkRight;
            public readonly List<Placement> Children = new List<Placement>();
            public readonly List<TexDrawCommand> Drawing = new List<TexDrawCommand>();
        }
        public TexLayoutEngine(ITexFontMetrics metrics = null) { this.metrics = metrics ?? new ApproximateTexFontMetrics(); }
        public TexRenderResult Render(string latex, double fontSize = 24, TexMathStyle style = TexMathStyle.Display) =>
            Render(new TexParser().Parse(latex), fontSize, style);
        public TexRenderResult Render(TexDocument document, double fontSize = 24, TexMathStyle style = TexMathStyle.Display)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (double.IsNaN(fontSize) || double.IsInfinity(fontSize) || fontSize < 4 || fontSize > 256) throw new ArgumentOutOfRangeException(nameof(fontSize));
            baseSize = fontSize;
            var root = Build(document.Root, style, null);
            var drawing = new List<TexDrawCommand>();
            var regions = new List<TexHitRegion>();
            var carets = new List<TexCaretStop>();
            Flatten(root, 0, root.Ascent, 0, drawing, regions, carets, false);
            if (drawing.Count > 32768) throw new InvalidOperationException("Too many drawing operations.");
            // Stroke extents and editable empty slots belong to the public bounds,
            // so hosts can clip/export the returned rectangle without losing ink.
            double minX = 0, minY = 0, maxX = root.Width, maxY = root.Ascent + root.Descent;
            foreach (var d in drawing)
            {
                if (d.Points.Count == 0)
                {
                    minX = Math.Min(minX, d.Bounds.X); minY = Math.Min(minY, d.Bounds.Y);
                    maxX = Math.Max(maxX, d.Bounds.Right); maxY = Math.Max(maxY, d.Bounds.Bottom);
                }
                else foreach (var p in d.Points)
                {
                    minX = Math.Min(minX, p.X - d.Thickness / 2); minY = Math.Min(minY, p.Y - d.Thickness / 2);
                    maxX = Math.Max(maxX, p.X + d.Thickness / 2); maxY = Math.Max(maxY, p.Y + d.Thickness / 2);
                }
            }
            foreach (var c in carets)
            {
                minX = Math.Min(minX, c.X); maxX = Math.Max(maxX, c.X);
                minY = Math.Min(minY, c.Top); maxY = Math.Max(maxY, c.Top + c.Height);
            }
            foreach (var d in drawing)
            {
                d.Bounds = Translate(d.Bounds, -minX, -minY);
                d.Baseline -= minY;
                d.OriginX -= minX;
                d.Points = d.Points.Select(p => new TexPoint(p.X - minX, p.Y - minY)).ToArray();
            }
            foreach (var r in regions) r.Bounds = Translate(r.Bounds, -minX, -minY);
            foreach (var c in carets) { c.X -= minX; c.Top -= minY; }
            return new TexRenderResult
            {
                Document = document, Width = maxX - minX, Height = maxY - minY, Baseline = root.Ascent - minY,
                Commands = drawing.AsReadOnly(), Regions = regions.AsReadOnly(),
                Carets = carets.OrderBy(c => c.Offset).ThenBy(c => c.Row.Length).ToArray()
            };
        }
        private static TexRect Translate(TexRect rect, double x, double y) => new TexRect(rect.X + x, rect.Y + y, rect.Width, rect.Height);
        private static TexMathStyle Script(TexMathStyle style) => style < TexMathStyle.Script ? TexMathStyle.Script : TexMathStyle.ScriptScript;
        private double Size(TexMathStyle style) => baseSize * (style == TexMathStyle.Script ? 0.7 : style == TexMathStyle.ScriptScript ? 0.5 : 1);
        private Box Build(TexNode node, TexMathStyle style, TexFont? font)
        {
            double size = Size(style);
            var line = metrics.Measure("0", size, font ?? TexFont.Roman);
            var box = new Box { Node = node, Size = size, CaretAscent = line.Ascent, CaretDescent = line.Descent };
            switch (node.Kind)
            {
                case TexKind.Phantom:
                    var phantom = Build(node.Children[0], style, font);
                    box.Width = phantom.Width; box.Ascent = phantom.Ascent; box.Descent = phantom.Descent;
                    break;
                case TexKind.Glyph:
                    double glyphSize = size * (style == TexMathStyle.Display && "∑∏∫∬∮".Contains(node.Text) && node.Text.Length == 1 ? 1.45 : 1);
                    string glyphText = node.Text == "-" ? "−" : node.Text;
                    var m = metrics.Measure(glyphText, glyphSize, font ?? node.Font);
                    box.Width = m.Width; box.Ascent = m.Ascent; box.Descent = m.Descent;
                    box.Drawing.Add(new TexDrawCommand { Kind = TexDrawKind.Glyph, Text = glyphText,
                        Font = font ?? node.Font, FontSize = glyphSize, Bounds = new TexRect(m.InkLeft, -m.Ascent, m.InkRight - m.InkLeft, m.Ascent + m.Descent) });
                    break;
                case TexKind.Space:
                    box.Width = node.SpaceEm * size;
                    break;
                case TexKind.Row:
                    var children = node.Children.Select(n => Build(n, style, font)).ToArray();
                    var classes = node.Children.Select(n => n.AtomClass).ToArray();
                    for (int i = 0; i < classes.Length; i++)
                        if (classes[i] == TexAtomClass.Binary && (i == 0 || i == classes.Length - 1
                            || new[] { TexAtomClass.Binary, TexAtomClass.Relation, TexAtomClass.Open, TexAtomClass.Punctuation }.Contains(classes[i - 1])
                            || new[] { TexAtomClass.Relation, TexAtomClass.Close, TexAtomClass.Punctuation }.Contains(classes[i + 1])))
                            classes[i] = TexAtomClass.Ordinary;
                    double x = 0;
                    for (int i = 0; i < children.Length; i++)
                    {
                        if (i > 0 && node.Children[i - 1].Kind != TexKind.Space && node.Children[i].Kind != TexKind.Space)
                            x += Spacing(classes[i - 1], classes[i], style) * size;
                        if (i > 0 && node.Children[i - 1].Kind == TexKind.Glyph && node.Children[i].Kind == TexKind.Glyph)
                            x = Math.Max(x, box.Children[i - 1].X + children[i - 1].InkRight - children[i].InkLeft + Math.Max(2.5, size * .06));
                        Place(box, children[i], Math.Max(0, x), 0); x += children[i].Width;
                    }
                    box.Width = Math.Max(box.Width, x);
                    if (children.Length == 0) { box.Width = size * 0.35; box.Ascent = line.Ascent; box.Descent = line.Descent; }
                    // The baseline text supplies the minimum caret height. Each
                    // insertion boundary additionally includes its adjacent atoms.
                    foreach (var child in children) IncludeBaselineGlyphs(child, ref box.CaretAscent, ref box.CaretDescent);
                    break;
                case TexKind.Fraction:
                    var fractionStyle = node.Text == "dfrac" ? TexMathStyle.Display : node.Text == "tfrac" ? TexMathStyle.Text : style;
                    double fractionSize = Size(fractionStyle);
                    box.Size = fractionSize;
                    var childStyle = fractionStyle == TexMathStyle.Display ? TexMathStyle.Text : Script(fractionStyle);
                    var top = Build(node.Children[0], childStyle, font);
                    var bottom = Build(node.Children[1], childStyle, font);
                    double thickness = fractionSize * 0.045, axis = fractionSize * 0.25, gap = fractionSize * (fractionStyle == TexMathStyle.Display ? 0.2 : 0.12);
                    box.Width = Math.Max(top.Width, bottom.Width) + fractionSize * 0.3;
                    Place(box, top, (box.Width - top.Width) / 2, -axis - thickness / 2 - gap - top.Descent);
                    Place(box, bottom, (box.Width - bottom.Width) / 2, -axis + thickness / 2 + gap + bottom.Ascent);
                    if (node.Text == "binom") box = Fence(box, "(", ")", fractionSize);
                    else Rule(box, 0, -axis - thickness / 2, box.Width, thickness);
                    break;
                case TexKind.Radical:
                    var radicand = Build(node.Children[0], style, font);
                    var degree = node.Children.Count == 2 ? Build(node.Children[1], TexMathStyle.ScriptScript, font) : null;
                    double offset = degree == null ? 0 : Math.Max(0, degree.Width - size * 0.22);
                    double rootWidth = size * 0.6, ruleY = -radicand.Ascent - size * 0.12;
                    Place(box, radicand, offset + rootWidth + size * 0.06, 0);
                    box.Width = offset + rootWidth + radicand.Width + size * 0.16;
                    box.Ascent = -ruleY + size * 0.045;
                    box.Descent = Math.Max(radicand.Descent, size * 0.05);
                    Path(box, TexDrawKind.Polyline, size * 0.05, new[]
                    {
                        new TexPoint(offset, -size * 0.08), new TexPoint(offset + size * 0.12, -size * 0.16),
                        new TexPoint(offset + size * 0.26, box.Descent), new TexPoint(offset + rootWidth, ruleY),
                        new TexPoint(box.Width, ruleY)
                    });
                    if (degree != null) Place(box, degree, 0, ruleY + degree.Ascent * 0.55);
                    break;
                case TexKind.Scripts:
                    var basis = Build(node.Children[0], style, font);
                    var sup = node.Children[1].Span.Length == 0 && node.Children[1].Children.Count == 0 ? null : Build(node.Children[1], Script(style), font);
                    var sub = node.Children[2].Span.Length == 0 && node.Children[2].Children.Count == 0 ? null : Build(node.Children[2], Script(style), font);
                    if (node.Limits && style == TexMathStyle.Display)
                    {
                        box.Width = Math.Max(basis.Width, Math.Max(sup?.Width ?? 0, sub?.Width ?? 0));
                        Place(box, basis, (box.Width - basis.Width) / 2, 0);
                        if (sup != null) Place(box, sup, (box.Width - sup.Width) / 2, -basis.Ascent - size * 0.15 - sup.Descent);
                        if (sub != null) Place(box, sub, (box.Width - sub.Width) / 2, basis.Descent + size * 0.15 + sub.Ascent);
                    }
                    else
                    {
                        Place(box, basis, 0, 0);
                        double upper = -Math.Max(size * 0.5, basis.Ascent - size * 0.25 + (sup?.Descent ?? 0));
                        double lower = Math.Max(size * 0.25, basis.Descent + size * 0.12 + (sub?.Ascent ?? 0) * 0.5);
                        if (sup != null && sub != null)
                            lower = Math.Max(lower, upper + sup.Descent + sub.Ascent + size * 0.2);
                        double scriptX = basis.Width + size * 0.05;
                        if (sup != null) Place(box, sup, scriptX, upper);
                        if (sub != null) Place(box, sub, scriptX, lower);
                        box.Width = scriptX + Math.Max(sup?.Width ?? 0, sub?.Width ?? 0);
                    }
                    break;
                case TexKind.Delimited:
                    box = Fence(Build(node.Children[0], style, font), node.Text, node.Closing, size);
                    box.Node = node;
                    break;
                case TexKind.Matrix:
                    box = Matrix(node, style, font);
                    break;
                case TexKind.Styled:
                    if (node.Text.EndsWith("style", StringComparison.Ordinal))
                    {
                        var selected = node.Text == "displaystyle" ? TexMathStyle.Display : node.Text == "textstyle" ? TexMathStyle.Text
                            : node.Text == "scriptstyle" ? TexMathStyle.Script : TexMathStyle.ScriptScript;
                        Place(box, Build(node.Children[0], selected, font), 0, 0);
                    }
                    else Place(box, Build(node.Children[0], style, node.Text == "mathnormal" ? (TexFont?)null : node.Font), 0, 0);
                    break;
                case TexKind.Accent:
                    var body = Build(node.Children[0], style, font);
                    Place(box, body, 0, 0);
                    double accentY = node.Text == "underline" ? body.Descent + size * 0.12 : -body.Ascent - size * 0.12;
                    Rule(box, 0, accentY, body.Width, size * 0.045);
                    if (node.Text == "underline") box.Descent += size * 0.2;
                    else
                    {
                        box.Ascent += size * 0.25;
                        if (node.Text == "hat")
                        {
                            box.Drawing.Clear();
                            Path(box, TexDrawKind.Polyline, size * 0.04, new[] { new TexPoint(0, accentY), new TexPoint(body.Width / 2, accentY - size * 0.12), new TexPoint(body.Width, accentY) });
                        }
                        if (node.Text == "vec")
                            Path(box, TexDrawKind.Polyline, size * 0.04, new[] { new TexPoint(body.Width - size * 0.18, accentY - size * 0.1), new TexPoint(body.Width, accentY), new TexPoint(body.Width - size * 0.18, accentY + size * 0.1) });
                    }
                    break;
                default: throw new InvalidOperationException("Unknown TeX box.");
            }
            box.Ascent = Math.Max(box.Ascent, size * 0.1);
            box.VisualAscent = box.Ascent; box.VisualDescent = box.Descent;
            var leftInk = new List<double>(); var rightInk = new List<double>();
            foreach (var command in box.Drawing)
            {
                leftInk.Add(command.Points.Count == 0 ? command.Bounds.X : command.Points.Min(p => p.X) - command.Thickness / 2);
                rightInk.Add(command.Points.Count == 0 ? command.Bounds.Right : command.Points.Max(p => p.X) + command.Thickness / 2);
                double topInk = command.Points.Count == 0 ? command.Bounds.Y : command.Points.Min(p => p.Y) - command.Thickness / 2;
                double bottomInk = command.Points.Count == 0 ? command.Bounds.Bottom : command.Points.Max(p => p.Y) + command.Thickness / 2;
                box.VisualAscent = Math.Max(box.VisualAscent, -topInk);
                box.VisualDescent = Math.Max(box.VisualDescent, bottomInk);
            }
            foreach (var child in box.Children)
            {
                leftInk.Add(child.X + child.Box.InkLeft); rightInk.Add(child.X + child.Box.InkRight);
                box.VisualAscent = Math.Max(box.VisualAscent, Math.Max(child.Box.Ascent, child.Box.VisualAscent) - child.Baseline);
                box.VisualDescent = Math.Max(box.VisualDescent, Math.Max(child.Box.Descent, child.Box.VisualDescent) + child.Baseline);
            }
            box.InkLeft = leftInk.Count == 0 ? 0 : leftInk.Min();
            box.InkRight = rightInk.Count == 0 ? box.Width : rightInk.Max();
            return box;
        }
        private static void Place(Box parent, Box child, double x, double baseline)
        {
            parent.Children.Add(new Placement { Box = child, X = x, Baseline = baseline });
            parent.Width = Math.Max(parent.Width, x + child.Width);
            parent.Ascent = Math.Max(parent.Ascent, child.Ascent - baseline);
            parent.Descent = Math.Max(parent.Descent, child.Descent + baseline);
        }
        private static void IncludeBaselineGlyphs(Box box, ref double ascent, ref double descent)
        {
            if (box.Node.Kind == TexKind.Glyph)
            { ascent = Math.Max(ascent, box.Ascent); descent = Math.Max(descent, box.Descent); }
            foreach (var child in box.Children)
                if (Math.Abs(child.Baseline) < .001) IncludeBaselineGlyphs(child.Box, ref ascent, ref descent);
        }
        private static double Spacing(TexAtomClass left, TexAtomClass right, TexMathStyle style)
        {
            if (left == TexAtomClass.Open || right == TexAtomClass.Close) return 0;
            if (left == TexAtomClass.Relation || right == TexAtomClass.Relation) return style < TexMathStyle.Script ? 5.0 / 18 : 0;
            if (left == TexAtomClass.Binary || right == TexAtomClass.Binary) return style < TexMathStyle.Script ? 2.0 / 9 : 0;
            if (left == TexAtomClass.Punctuation || left == TexAtomClass.Operator && right != TexAtomClass.Open
                || right == TexAtomClass.Operator && left != TexAtomClass.Open || left == TexAtomClass.Inner || right == TexAtomClass.Inner) return 1.0 / 6;
            return 0;
        }
        private static void Rule(Box box, double x, double y, double width, double height) =>
            box.Drawing.Add(new TexDrawCommand { Kind = TexDrawKind.Rule, Bounds = new TexRect(x, y, width, height) });
        private static void Path(Box box, TexDrawKind kind, double thickness, IEnumerable<TexPoint> points) =>
            box.Drawing.Add(new TexDrawCommand { Kind = kind, Thickness = thickness, Points = points.ToArray() });
        private static Box Fence(Box body, string left, string right, double size)
        {
            double axis = size * 0.25;
            double half = Math.Max(body.Ascent - axis, body.Descent + axis) + size * 0.04;
            var result = new Box { Node = body.Node, Size = size, Ascent = half + axis, Descent = Math.Max(0, half - axis) };
            double width = Math.Max(size * 0.25, Math.Min(size * 0.6, half * 0.3));
            double padding = size * 0.06;
            double leftWidth = left.Length == 0 ? 0 : width + padding;
            Place(result, body, leftWidth, 0);
            result.Width = leftWidth + body.Width + (right.Length == 0 ? 0 : width + padding);
            if (left.Length > 0) Delimiter(result, left, 0, width);
            if (right.Length > 0) Delimiter(result, right, leftWidth + body.Width + padding, width);
            return result;
        }
        private static void Delimiter(Box box, string text, double x, double width)
        {
            double top = -box.Ascent, bottom = box.Descent, mid = (top + bottom) / 2, stroke = box.Size * 0.045;
            bool open = text == "(" || text == "[" || text == "{" || text == "⟨" || text == "⌊" || text == "⌈";
            double outer = open ? x : x + width, inner = open ? x + width : x;
            if (text == "(" || text == ")")
                Path(box, TexDrawKind.CubicCurve, stroke, new[] { new TexPoint(inner, top), new TexPoint(outer, top + (bottom - top) * 0.15),
                    new TexPoint(outer, bottom - (bottom - top) * 0.15), new TexPoint(inner, bottom) });
            else if (text == "{" || text == "}")
                Path(box, TexDrawKind.CubicCurve, stroke, new[] { new TexPoint(inner, top), new TexPoint(outer, top), new TexPoint(inner, mid), new TexPoint(outer, mid),
                    new TexPoint(inner, mid), new TexPoint(outer, bottom), new TexPoint(inner, bottom) });
            else if (text == "|" || text == "‖")
            {
                Rule(box, x + width * 0.45, top, stroke, bottom - top);
                if (text == "‖") Rule(box, x + width * 0.8, top, stroke, bottom - top);
            }
            else if (text == "⟨" || text == "⟩" || text == "/")
                Path(box, TexDrawKind.Polyline, stroke, text == "/" ? new[] { new TexPoint(x + width, top), new TexPoint(x, bottom) }
                    : new[] { new TexPoint(inner, top), new TexPoint(outer, mid), new TexPoint(inner, bottom) });
            else
            {
                var points = new List<TexPoint>();
                if (text != "⌊" && text != "⌋") points.Add(new TexPoint(inner, top));
                points.Add(new TexPoint(outer, top)); points.Add(new TexPoint(outer, bottom));
                if (text != "⌈" && text != "⌉") points.Add(new TexPoint(inner, bottom));
                Path(box, TexDrawKind.Polyline, stroke, points);
            }
        }
        private Box Matrix(TexNode node, TexMathStyle style, TexFont? font)
        {
            double size = Size(style);
            var cells = node.Children.Select(c => Build(c, style == TexMathStyle.Display ? TexMathStyle.Text : style, font)).ToArray();
            var widths = new double[node.Columns]; var ascents = new double[node.Rows]; var descents = new double[node.Rows];
            for (int r = 0; r < node.Rows; r++) for (int c = 0; c < node.Columns; c++)
            {
                var cell = cells[r * node.Columns + c];
                widths[c] = Math.Max(widths[c], cell.Width); ascents[r] = Math.Max(ascents[r], cell.Ascent); descents[r] = Math.Max(descents[r], cell.Descent);
            }
            double rowGap = size * 0.35, columnGap = size * 0.75;
            var bars = new int[node.Columns + 1];
            if (node.Text == "array")
            {
                int boundary = 0;
                foreach (char token in node.Closing)
                    if (token == '|') bars[boundary]++; else boundary++;
            }
            double leftPad = bars[0] > 0 ? size * (0.2 + bars[0] * 0.12) : 0;
            double rightPad = bars[node.Columns] > 0 ? size * (0.2 + bars[node.Columns] * 0.12) : 0;
            double height = ascents.Sum() + descents.Sum() + rowGap * (node.Rows - 1);
            var box = new Box { Node = node, Size = size, Ascent = height / 2 + size * 0.25, Descent = Math.Max(0, height / 2 - size * 0.25) };
            double y = -box.Ascent;
            for (int r = 0; r < node.Rows; r++)
            {
                double x = leftPad;
                for (int c = 0; c < node.Columns; c++)
                {
                    var cell = cells[r * node.Columns + c];
                    double align = (widths[c] - cell.Width) / 2;
                    if (node.Text == "cases") align = 0;
                    if (node.Text == "aligned") align = c % 2 == 0 ? widths[c] - cell.Width : 0;
                    string specification = node.Closing.Replace("|", "").Replace(" ", "");
                    if (node.Text == "array" && c < specification.Length)
                        align = specification[c] == 'l' ? 0 : specification[c] == 'r' ? widths[c] - cell.Width : align;
                    Place(box, cell, x + align, y + ascents[r]);
                    x += widths[c] + columnGap;
                }
                y += ascents[r] + descents[r] + rowGap;
            }
            box.Width = leftPad + widths.Sum() + columnGap * (node.Columns - 1) + rightPad;
            double boundaryX = leftPad / 2;
            for (int c = 0; c <= node.Columns; c++)
            {
                if (c == node.Columns) boundaryX = box.Width - rightPad / 2;
                for (int j = 0; j < bars[c]; j++)
                    Rule(box, boundaryX + (j - (bars[c] - 1) / 2.0) * size * 0.12, -box.Ascent, size * 0.04, height);
                boundaryX = c == 0 ? leftPad + widths[0] + columnGap / 2 : boundaryX + (c < node.Columns ? widths[c] + columnGap : 0);
            }
            if (node.Text == "pmatrix") return Fence(box, "(", ")", size);
            if (node.Text == "bmatrix") return Fence(box, "[", "]", size);
            if (node.Text == "Bmatrix") return Fence(box, "{", "}", size);
            if (node.Text == "vmatrix") return Fence(box, "|", "|", size);
            if (node.Text == "Vmatrix") return Fence(box, "‖", "‖", size);
            if (node.Text == "cases") return Fence(box, "{", "", size);
            return box;
        }
        private static void Flatten(Box box, double x, double baseline, int depth, List<TexDrawCommand> drawing,
            List<TexHitRegion> regions, List<TexCaretStop> carets, bool insideRow)
        {
            bool glyph = box.Node.Kind == TexKind.Glyph;
            regions.Add(new TexHitRegion { Span = box.Node.Span, Bounds = new TexRect(x + (glyph ? box.InkLeft : 0), baseline - box.Ascent,
                Math.Max(0, glyph ? box.InkRight - box.InkLeft : box.Width), box.Ascent + box.Descent),
                IsGlyph = box.Node.Kind == TexKind.Glyph, IsEmptySlot = box.Node.Kind == TexKind.Phantom, Depth = depth });
            foreach (var d in box.Drawing)
                drawing.Add(new TexDrawCommand { Kind = d.Kind, Text = d.Text, Font = d.Font, FontSize = d.FontSize, Thickness = d.Thickness, Baseline = baseline + d.Baseline, OriginX = x + d.OriginX,
                    Bounds = new TexRect(x + d.Bounds.X, baseline + d.Bounds.Y, d.Bounds.Width, d.Bounds.Height),
                    Points = d.Points.Select(p => new TexPoint(x + p.X, baseline + p.Y)).ToArray() });
            if (box.Node.Kind == TexKind.Row)
            {
                Action<int, int, int> add = (offset, leftIndex, rightIndex) =>
                {
                    double top = baseline - box.CaretAscent, bottom = baseline + box.CaretDescent;
                    // Height belongs to this slot, not the entire surrounding row:
                    // a fraction-side slot spans the fraction, whereas numerator,
                    // denominator, script and matrix-cell slots stay in their row.
                    int argumentIndex = rightIndex < 0 ? -1 : Enumerable.Range(rightIndex + 1, box.Children.Count - rightIndex - 1)
                        .Where(i => box.Children[i].Box.Node.Kind != TexKind.Space).DefaultIfEmpty(-1).First();
                    if (argumentIndex >= 0 && !IsFunctionArgument(box.Children[rightIndex].Box.Node, box.Children[argumentIndex].Box.Node)) argumentIndex = -1;
                    foreach (int i in new[] { leftIndex, rightIndex, argumentIndex }.Where(i => i >= 0))
                    {
                        var child = box.Children[i];
                        top = Math.Min(top, baseline + child.Baseline - Math.Max(child.Box.Ascent, child.Box.VisualAscent));
                        bottom = Math.Max(bottom, baseline + child.Baseline + Math.Max(child.Box.Descent, child.Box.VisualDescent));
                    }
                    var previous = leftIndex < 0 ? null : box.Children[leftIndex];
                    var next = rightIndex < 0 ? null : box.Children[rightIndex];
                    double clearance = Math.Max(1.5, box.Size * .06);
                    double at = previous == null && next == null ? 0
                        : previous == null ? (next.Box.Node.Kind == TexKind.Phantom ? next.X : Math.Min(0, next.X + next.Box.InkLeft - clearance))
                        : next == null ? (previous.Box.Node.Kind == TexKind.Phantom ? previous.X + previous.Box.Width : Math.Max(box.Width, previous.X + previous.Box.InkRight + clearance))
                        : next.Box.Node.Kind == TexKind.Phantom ? next.X
                        : previous.Box.Node.Kind == TexKind.Phantom ? previous.X + previous.Box.Width
                        : (previous.X + previous.Box.InkRight + next.X + next.Box.InkLeft) / 2;
                    carets.Add(new TexCaretStop { Offset = offset, X = x + at,
                        Top = top, Height = bottom - top, Row = box.Node.ContentSpan });
                };
                // One stop per visible boundary. Command-terminating whitespace
                // is syntax, so a glyph end and the next atom start are not two
                // distinct stops at the same point (e.g. after \\pi or \\sin).
                var atoms = Enumerable.Range(0, box.Children.Count).Where(i => box.Children[i].Box.Node.Kind != TexKind.Space).ToArray();
                add(box.Node.ContentSpan.Start, -1, atoms.Length == 0 ? -1 : atoms[0]);
                for (int i = 1; i < atoms.Length; i++)
                {
                    // A named operator and its fenced argument form a function
                    // prefix. Enter the argument directly instead of stopping in
                    // the gap between the name and its opening delimiter.
                    if (!IsFunctionArgument(box.Children[atoms[i - 1]].Box.Node, box.Children[atoms[i]].Box.Node))
                        add(box.Children[atoms[i]].Box.Node.Span.Start, atoms[i - 1], atoms[i]);
                }
                if (atoms.Length > 0) add(box.Node.ContentSpan.End, atoms[atoms.Length - 1], -1);
            }
            else if (box.Node.Kind == TexKind.Glyph && !insideRow)
            {
                double clearance = Math.Max(1.5, box.Size * .06);
                carets.Add(new TexCaretStop { Offset = box.Node.Span.Start, X = x + box.InkLeft - clearance, Top = baseline - box.Ascent, Height = box.Ascent + box.Descent, Row = box.Node.Span });
                carets.Add(new TexCaretStop { Offset = box.Node.Span.End, X = x + box.InkRight + clearance, Top = baseline - box.Ascent, Height = box.Ascent + box.Descent, Row = box.Node.Span });
            }
            foreach (var child in box.Children) Flatten(child.Box, x + child.X, baseline + child.Baseline, depth + 1, drawing, regions, carets, box.Node.Kind == TexKind.Row);
        }
        private static bool IsFunctionArgument(TexNode name, TexNode argument)
        {
            if (name.AtomClass != TexAtomClass.Operator) return false;
            while (argument.Kind == TexKind.Scripts || argument.Kind == TexKind.Styled) argument = argument.Children[0];
            return argument.Kind == TexKind.Delimited || argument.Kind == TexKind.Matrix
                || argument.Kind == TexKind.Glyph && argument.AtomClass == TexAtomClass.Open;
        }
    }
}
