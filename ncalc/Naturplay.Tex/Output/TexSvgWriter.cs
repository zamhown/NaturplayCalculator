using System;
using System.Globalization;
using System.Text;
using System.Xml;

namespace Naturplay.Tex
{
    /// <summary>Headless export of exactly the same display list used by the native view.</summary>
    public static class TexSvgWriter
    {
        public static string Write(TexRenderResult formula, string foreground = "#111111")
        {
            if (formula == null) throw new ArgumentNullException(nameof(formula));
            var text = new StringBuilder();
            var settings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = false };
            using (var writer = XmlWriter.Create(text, settings))
            {
                writer.WriteStartElement("svg", "http://www.w3.org/2000/svg");
                writer.WriteAttributeString("width", F(formula.Width + 4)); writer.WriteAttributeString("height", F(formula.Height + 4));
                writer.WriteAttributeString("viewBox", "-2 -2 " + F(formula.Width + 4) + " " + F(formula.Height + 4));
                writer.WriteAttributeString("role", "img");
                writer.WriteStartElement("title"); writer.WriteString(formula.Document.Source); writer.WriteEndElement();
                foreach (var command in formula.Commands)
                {
                    if (command.Kind == TexDrawKind.Glyph)
                    {
                        writer.WriteStartElement("text");
                        writer.WriteAttributeString("x", F(command.OriginX)); writer.WriteAttributeString("y", F(command.Baseline));
                        writer.WriteAttributeString("font-family", "Cambria Math, Cambria, serif");
                        writer.WriteAttributeString("font-size", F(command.FontSize));
                        writer.WriteAttributeString("font-style", command.Font == TexFont.MathItalic ? "italic" : "normal");
                        writer.WriteAttributeString("font-weight", command.Font == TexFont.Bold ? "bold" : "normal");
                        writer.WriteAttributeString("fill", foreground); writer.WriteString(command.Text); writer.WriteEndElement();
                    }
                    else if (command.Kind == TexDrawKind.Rule)
                    {
                        writer.WriteStartElement("rect"); writer.WriteAttributeString("x", F(command.Bounds.X)); writer.WriteAttributeString("y", F(command.Bounds.Y));
                        writer.WriteAttributeString("width", F(command.Bounds.Width)); writer.WriteAttributeString("height", F(command.Bounds.Height));
                        writer.WriteAttributeString("fill", foreground); writer.WriteEndElement();
                    }
                    else
                    {
                        var path = new StringBuilder();
                        for (int i = 0; i < command.Points.Count; i++)
                        {
                            path.Append(i == 0 ? "M" : command.Kind == TexDrawKind.CubicCurve ? (i % 3 == 1 ? "C" : " ") : "L");
                            path.Append(F(command.Points[i].X)).Append(',').Append(F(command.Points[i].Y));
                        }
                        writer.WriteStartElement("path"); writer.WriteAttributeString("d", path.ToString());
                        writer.WriteAttributeString("fill", "none"); writer.WriteAttributeString("stroke", foreground);
                        writer.WriteAttributeString("stroke-width", F(command.Thickness)); writer.WriteEndElement();
                    }
                }
                writer.WriteEndElement();
            }
            return text.ToString();
        }
        private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
