using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NaturplayCalculator.History;
using NaturplayCalculator.Expressions;
using System.Collections.Generic;
using NaturplayCalculator.Model;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace NaturplayCalculator
{
    public sealed partial class MainPage
    {
        private CancellationTokenSource calculationCancellation;
        private int calculationRevision;
        private CalculationValue inputPreviousAnswer;
        private bool? inputUseRadians;
        private bool restoringSession;

        private void CancelPendingCalculation()
        {
            calculationRevision++;
            calculationCancellation?.Cancel();
            bequal.IsEnabled = true;
        }
        private CalculationOptions InputOptions(bool captureSteps)
        {
            return new CalculationOptions
            {
                Mode = ActiveMode,
                PreviousAnswer = (inputPreviousAnswer ?? Settings.Ans).Clone(),
                UseRadians = inputUseRadians ?? Settings.UseRadians,
                CaptureSteps = captureSteps,
                MaxSteps = 32
            };
        }
        private async Task PreviewAsync(string input)
        {
            CancelPendingCalculation();
            var cancellation = new CancellationTokenSource();
            calculationCancellation = cancellation;
            int revision = calculationRevision;
            var options = InputOptions(false);
            try
            {
                await Task.Delay(120, cancellation.Token);
                var result = await Task.Run(() => new CalculationEngine().Evaluate(input, options, cancellation.Token), cancellation.Token);
                if (revision != calculationRevision || input != lastInput) return;
                presentAnswer = result.Value;
                int answerIndex = listView.Items.Count;
                AddExpression(result.Value.Expression, true);
                HighlightPreviewResult(answerIndex);
                AddResultNotes(result.Value);
                AddComments(result.Comments, "comment:");
                RefreshResultTile(result.Value);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                // Incomplete expressions are normal while typing; explicit evaluation
                // presents the error, while diagnostics preserve its original cause.
                Settings.ReportError("Preview", ex);
            }
            finally
            {
                if (ReferenceEquals(calculationCancellation, cancellation)) calculationCancellation = null;
                cancellation.Dispose();
            }
        }

        private async Task CalculateAsync(bool recordHistory = true, HistoryRecord record = null)
        {
            string input = lastInput;
            string command = input.Trim().ToLowerInvariant();
            if (recordHistory && ActiveMode == CalculationMode.Numeric && commandDic.TryGetValue(command, out var handler))
            {
                try { clear(); handler(null, null); }
                catch (Exception ex) { Settings.ReportError("Command", ex); ShowCalculationError(ex.Message); }
                return;
            }
            CancelPendingCalculation();
            var cancellation = new CancellationTokenSource();
            calculationCancellation = cancellation;
            int revision = calculationRevision;
            var options = record == null ? InputOptions(ShowCalculationSteps) : new CalculationOptions
            {
                Mode = record.Mode,
                PreviousAnswer = record.PreviousAnswer?.Clone() ?? new MathValue(0),
                UseRadians = record.UseRadians, CaptureSteps = ShowCalculationSteps, MaxSteps = 32
            };
            inputPreviousAnswer = options.PreviousAnswer.Clone();
            inputUseRadians = options.UseRadians;
            inputMode = options.Mode;
            bequal.IsEnabled = false;
            try
            {
                string complete = tryToSaveTheWrongExpression(new InputString(input)).ToString();
                var result = await Task.Run(() => new ContentEvaluator().Evaluate(complete, options, cancellation.Token), cancellation.Token);
                if (revision != calculationRevision || input != lastInput) return;
                TreeViewState = result.Calculation == null;
                inputC = null;
                if (result.Calculation != null)
                {
                    RenderCalculation(result.Calculation);
                    if (recordHistory) Settings.Ans = result.Calculation.Value.Clone();
                    presentAnswer = result.Calculation.Value.Clone();
                    RefreshResultTile(presentAnswer);
                    self.Title = AppResources.GetString("tileText") + " : " + presentAnswer;
                }
                else
                {
                    presentAnswer = null;
                    var root = new TreeViewNode { Content = result.Kind.ToUpperInvariant() };
                    root.Children.Add(result.Json != null ? makeTree(result.Json) : makeTree(result.Xml.DocumentElement));
                    treeView.RootNodes.Add(root);
                }
                formulaEdit = null; RefreshInputPresentation();
                NowS = new InputString(input); NowS.GiveIndex();
                setThePtr(input.Length, resumeEditing: false);
                solved = true;
                if (recordHistory)
                {
                    historyManager.Add(new HistoryRecord
                    {
                        Input = input, Kind = result.Kind, Mode = options.Mode, Answer = result.Calculation?.Value.Clone(),
                        PreviousAnswer = options.PreviousAnswer.Clone(), UseRadians = options.UseRadians
                    });
                    updateRecordMenu();
                    await SaveSessionAsync();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Settings.ReportError("Calculate", ex);
                if (revision == calculationRevision) ShowCalculationError(ex.Message);
            }
            finally
            {
                if (revision == calculationRevision) bequal.IsEnabled = true;
                if (ReferenceEquals(calculationCancellation, cancellation)) calculationCancellation = null;
                cancellation.Dispose();
            }
        }
        private void RenderCalculation(CalculationResult result)
        {
            TreeViewState = false;
            AddComments(result.Comments, "title:");
            inputC = AddExpression(result.Expression, false) as Canvas;
            foreach (var step in CalculationPresentation.ResultSequence(result.Expression, result.Steps, result.Value.Expression))
                AddExpression(step, true);
            AddResultNotes(result.Value);
            AddComments(result.Comments, "comment:");
        }
        private void HighlightPreviewResult(int answerIndex)
        {
            for (int i = 0; i < listView.Items.Count; i++)
                if (listView.Items[i] is Controls.FormulaListItem row) row.IsPreviewResult = i == answerIndex;
        }
        private UIElement AddExpression(ExpressionNode expression, bool answer)
        {
            string code = expression.ToCode();
            UIElement item = CreateFormulaItem(new ExpressionTexFormatter().Format(expression, ActiveMode == CalculationMode.Symbolic, !answer && Settings.FoldingNum, answer ? null : lastInput), code, answer);
            addListViewItem(item);
            return item;
        }
        private void AddResultNotes(CalculationValue value)
        {
            if (value.UsesFixedRadians)
                addListViewItem(new TextBlock { Text = AppResources.GetString("result_angles_radians"), FontSize = 12, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, MaxWidth = 1000 });
            if (value.Conditions.Count > 0)
                addListViewItem(new TextBlock { Text = AppResources.GetString("symbolic_conditions") + " " + string.Join(", ", value.DisplayConditions), FontSize = 16, TextWrapping = TextWrapping.Wrap, MaxWidth = 1000 });
        }
        private void AddComments(IReadOnlyList<string> comments, string prefix)
        {
            foreach (string comment in comments)
            {
                string trimmed = comment.Trim();
                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    addListViewItem(new TextBlock { Text = trimmed.Substring(prefix.Length), TextWrapping = TextWrapping.Wrap });
            }
        }
        private void ShowCalculationError(string message)
        {
            TreeViewState = false; inputC = null; presentAnswer = null; solved = false;
            try
            {
                if (ExpressionPreview.IsMathCandidate(lastInput)) AddInputPreview();
                else addListViewItem(new TextBlock { Text = lastInput, TextWrapping = TextWrapping.Wrap, MaxWidth = 1000 });
            }
            catch (Exception renderError) { Settings.ReportError("Render invalid input", renderError); }
            addListViewItem(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 1000 });
        }
        private void RefreshResultTile(CalculationValue answer)
        {
            try { refreshTile(answer); }
            catch (Exception ex) { Settings.ReportError("Refresh tile", ex); }
        }
    }
}
