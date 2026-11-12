using System;
using System.Linq;
using Naturplay.Tex;
using Naturplay.Tex.WinUI;
using NaturplayCalculator.Expressions;
using Windows.UI;
using Colors = Microsoft.UI.Colors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace NaturplayCalculator
{
    public sealed partial class MainPage
    {
        private static SolidColorBrush FormulaBrush() => new SolidColorBrush(Settings.White ? Colors.White : Colors.Black);

        private void AddInputPreview()
        {
            var preview = ExpressionPreview.Create(lastInput, ActiveMode == Model.CalculationMode.Symbolic, Settings.FoldingNum);
            AddComments(preview.Comments, "title:");
            inputC = CreateFormulaItem(preview.Formula, lastInput, false);
            addListViewItem(inputC);
            if (preview.IsLimited) addListViewItem(new TextBlock { Text = AppResources.GetString("calculation_limit"), TextWrapping = TextWrapping.Wrap });
        }

        // The host owns calculation input positions and copy data. The view receives
        // only standard TeX, style and colors, and can be reused without this app.
        private Canvas CreateFormulaItem(TexExpression message, string copyCode, bool answer)
        {
            var formula = new TexFormulaView { HostWindow = App.MainWindow, Foreground = FormulaBrush(), Tag = message, ShowEmptySlots = true,
                IsCaretActive = !answer && activeInputSurface == InputSurface.Formula };
            formula.SetFormula(message.Latex);
            if (!answer) BindFormulaCarets(message, formula.FormulaLayout);
            var row = new Canvas { Tag = copyCode };
            double baseline = formula.FormulaLayout.Baseline;
            double left = 0;
            if (answer)
            {
                var equal = new TexFormulaView { HostWindow = App.MainWindow, Foreground = FormulaBrush() };
                equal.SetFormula("=");
                baseline = Math.Max(baseline, equal.FormulaLayout.Baseline);
                Canvas.SetTop(equal, baseline - equal.FormulaLayout.Baseline);
                row.Children.Add(equal);
                left = equal.Width + 10;
                row.Height = baseline - equal.FormulaLayout.Baseline + equal.Height;
            }
            else
            {
                formula.CaretTapped += caret =>
                {
                    if (inputC == row) SelectFormulaCaret(formulaCaretMap.FromGeometry(caret));
                };
            }
            Canvas.SetLeft(formula, left);
            Canvas.SetTop(formula, baseline - formula.FormulaLayout.Baseline);
            row.Children.Add(formula);
            row.Width = left + formula.Width + 2;
            row.Height = Math.Max(double.IsNaN(row.Height) ? 0 : row.Height,
                baseline - formula.FormulaLayout.Baseline + formula.Height);
            return row;
        }

        private void UpdateFormulaCaret(int inputIndex)
        {
            inputBox.IsActiveInputSurface = activeInputSurface == InputSurface.Text;
            inputBox.IsCaretVisible = !caretsSuppressed;
            var view = inputC?.Children.OfType<TexFormulaView>().FirstOrDefault(v => v.Tag is TexExpression);
            if (view == null) return;
            view.IsCaretActive = activeInputSurface == InputSurface.Formula;
            if (activeInputSurface == InputSurface.Text) formulaCaret = formulaCaretMap.FromText(inputIndex);
            view.ShowSourceSelection(caretsSuppressed || formulaSelectionAnchor == null || formulaCaret == null ? (TexSpan?)null
                : new TexSpan(Math.Min(formulaSelectionAnchor.Geometry.Offset, formulaCaret.Geometry.Offset),
                    Math.Abs(formulaSelectionAnchor.Geometry.Offset - formulaCaret.Geometry.Offset)));
            view.ShowPositionedCaret(caretsSuppressed || _text.SelectionLength > 0 ? null : formulaCaret?.Geometry);
        }
    }
}
