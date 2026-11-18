using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace NaturplayCalculator.Desktop.Controls;

internal enum EditorSymbolKind { Menu, Undo, Redo, ChevronDown, ChevronUp, Previous, Next, ParentScope, Clear }

/// <summary>Small vector controls sized by their visible strokes, not a text em box.</summary>
internal sealed class EditorSymbol : Viewbox
{
    public EditorSymbol(EditorSymbolKind kind, Brush ink)
    {
        bool menu = kind == EditorSymbolKind.Menu;
        var canvas = new Canvas { Width = 20, Height = menu ? 16 : 20 };
        var geometry = new PathGeometry();
        void Line(params Point[] points)
        {
            var figure = new PathFigure { StartPoint = points[0], IsClosed = false, IsFilled = false };
            foreach (var point in points.Skip(1)) figure.Segments.Add(new LineSegment { Point = point });
            geometry.Figures.Add(figure);
        }
        if (menu)
            foreach (double y in new[] { 2.0, 8, 14 }) Line(new(1, y), new(19, y));
        else if (kind is EditorSymbolKind.ChevronDown or EditorSymbolKind.ChevronUp)
            Line(new(4, 7), new(10, 13), new(16, 7));
        else if (kind is EditorSymbolKind.Previous or EditorSymbolKind.Next)
            Line(new(12, 4), new(6, 10), new(12, 16));
        else if (kind == EditorSymbolKind.ParentScope)
        {
            Line(new(4, 16), new(4, 7), new(17, 7));
            Line(new(12, 2), new(17, 7), new(12, 12));
        }
        else if (kind == EditorSymbolKind.Clear)
        {
            Line(new(5, 5), new(15, 15));
            Line(new(15, 5), new(5, 15));
        }
        else
        {
            Line(new(7, 2.5), new(2, 7.5), new(7, 12.5));
            var curve = new PathFigure { StartPoint = new(2, 7.5), IsFilled = false };
            curve.Segments.Add(new LineSegment { Point = new(11, 7.5) });
            curve.Segments.Add(new BezierSegment { Point1 = new(20, 7.5), Point2 = new(20, 17.5), Point3 = new(11, 17.5) });
            geometry.Figures.Add(curve);
        }
        var path = new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = geometry, Stroke = ink, StrokeThickness = 1.6,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
        };
        if (kind is EditorSymbolKind.Redo or EditorSymbolKind.Next)
            path.RenderTransform = new ScaleTransform { ScaleX = -1, CenterX = 10 };
        else if (kind == EditorSymbolKind.ChevronUp)
            path.RenderTransform = new ScaleTransform { ScaleY = -1, CenterY = 10 };
        canvas.Children.Add(path); Child = canvas; Stretch = Stretch.Uniform;
        HorizontalAlignment = HorizontalAlignment.Center; VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;
    }
}
