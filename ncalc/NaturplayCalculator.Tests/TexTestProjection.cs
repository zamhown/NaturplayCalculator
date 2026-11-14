using System;
using System.Linq;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;

namespace NaturplayCalculator.Tests
{
    // An independent reader of the emitted TeX. It reconstructs calculator input
    // from visible grouping and spatial containers for numeric equivalence checks.
    internal static class TexTestProjection
    {
        public static TexDocument Document(ExpressionNode expression, bool implicitProducts = false) =>
            new TexParser().Parse(new ExpressionTexFormatter().Format(expression, implicitProducts).Latex);
        public static string Snapshot(TexNode n) => Read(n, false);
        public static string Linearize(TexNode n) => Read(n, true);
        private static string Read(TexNode n, bool numeric)
        {
            var c = n.Children.Select(child => Read(child, numeric)).ToArray();
            switch (n.Kind)
            {
                case TexKind.Glyph:
                    string text = n.Text == "×" ? "*" : n.Text == "−" ? "-" : n.Text;
                    return numeric && n.AtomClass == TexAtomClass.Binary ? " " + text + " " : text;
                case TexKind.Space: return "";
                case TexKind.Row: case TexKind.Styled: return string.Concat(c);
                case TexKind.Delimited:
                    if (n.Text == "|" && n.Closing == "|") return numeric ? "Abs(" + c[0] + ")" : "|" + c[0] + "|";
                    return n.Text + c[0] + n.Closing;
                case TexKind.Fraction:
                    return numeric ? "((" + c[0] + ")/(" + c[1] + "))" : "{" + c[0] + "}/{" + c[1] + "}";
                case TexKind.Radical:
                    if (c.Length == 1) return numeric ? "sqrt(" + c[0] + ")" : "√{" + c[0] + "}";
                    return numeric ? "((" + c[0] + ")yroot(" + c[1] + "))" : "root{" + c[1] + "}{" + c[0] + "}";
                case TexKind.Scripts:
                    if ((c[0] == "C" || c[0] == "A") && c[2].Length > 0)
                        return numeric ? "((" + c[2] + ")" + (c[0] == "C" ? "nCr" : "nPr") + "(" + c[1] + "))"
                            : c[0] + "{" + c[2] + "}{" + c[1] + "}";
                    if (c[2].Length > 0) throw new InvalidOperationException("Unexpected calculation subscript.");
                    return c[0] + (numeric ? "^(" + c[1] + ")" : "^{" + c[1] + "}");
                case TexKind.Matrix:
                    string body = string.Join(numeric ? ",," : ";", Enumerable.Range(0, n.Rows)
                        .Select(row => string.Join(",", c.Skip(row * n.Columns).Take(n.Columns))));
                    if (numeric) return "(" + body + (c.Length == 1 ? ",)" : ")");
                    return n.Text == "vmatrix" ? "|" + body + "|" : "[" + body + "]";
                default: throw new InvalidOperationException("Unexpected calculation TeX: " + n.Kind);
            }
        }
    }
}
