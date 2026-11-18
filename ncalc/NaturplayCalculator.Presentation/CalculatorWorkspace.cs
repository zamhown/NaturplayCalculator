using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NaturplayCalculator.History;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Presentation
{
    /// <summary>Chooses what Ans means in the expression being edited.</summary>
    public enum WorkspaceEditContext
    {
        /// <summary>Start from global Ans after a solved expression; otherwise continue the draft.</summary>
        Automatic,
        /// <summary>Continue the original expression, including its captured meaning of Ans.</summary>
        Preserve,
        /// <summary>Start a fresh expression whose Ans is the latest committed answer.</summary>
        NewCalculation,
        /// <summary>Continue from the visible result, which may have been recalled from history.</summary>
        DisplayedResult
    }

    /// <summary>UI-thread-owned workspace. The worker receives only immutable snapshots.</summary>
    public sealed class CalculatorWorkspace : IDisposable
    {
        private CancellationTokenSource pending;
        private long revision;
        internal long EditRevision => revision;
        private const int MaxEditHistory = 100;
        // This is a retained-text budget (about 4 MiB of UTF-16), in addition to
        // the entry limit. A long JSON/XML draft must not cost 100 full copies.
        private const long MaxEditCharacters = 2 * 1024 * 1024;
        private readonly LinkedList<EditSnapshot> undo = new LinkedList<EditSnapshot>();
        private readonly LinkedList<EditSnapshot> redo = new LinkedList<EditSnapshot>();
        private long editCharacters;
        private bool repeatWithLatestAnswer;
        private bool showDecimalResult;
        private CalculationValue formattedResultSource, decimalResult;
        public CalculatorSession Session { get; }
        public ContentResult Content { get; private set; }
        public CalculationValue Result => Session.DraftAnswer;
        /// <summary>The displayed representation. Ans and stored results retain their exact value.</summary>
        public CalculationValue DisplayResult
        {
            get
            {
                if (!showDecimalResult) return Result;
                EnsureResultFormats();
                return decimalResult ?? Result;
            }
        }
        public bool IsDecimalResult => Result?.Numeric != null || showDecimalResult;
        public bool CanToggleResultFormat
        {
            get
            {
                if (IsBusy || !Session.DraftIsSolved || Error != null) return false;
                EnsureResultFormats();
                return decimalResult != null;
            }
        }
        public string Error { get; private set; }
        public int ErrorStart { get; private set; } = -1;
        public int ErrorLength { get; private set; }
        public bool IsBusy { get; private set; }
        public bool CanUndo => undo.Count != 0;
        public bool CanRedo => redo.Count != 0;
        /// <summary>Host-owned immutable editing context (caret, scope, selection).
        /// Assigning it does not create an undo step. Existing edit snapshots retain
        /// the reference, while saved calculator sessions contain no host state.</summary>
        public object EditorState { get; set; }

        public CalculatorWorkspace(CalculatorSession session = null)
        {
            Session = session ?? new CalculatorSession();
            if (string.IsNullOrWhiteSpace(Session.Draft) || Session.DraftPreviousAnswer == null)
                Session.DraftPreviousAnswer = Session.Answer.Clone();
            // A saved live result can resume repetition after reopening. Older
            // recalled entries keep their own captured Ans for the first '='.
            var latest = Session.History.LastOrDefault();
            repeatWithLatestAnswer = Session.DraftIsSolved && latest != null
                && Session.Draft == latest.Input && Session.DraftMode == latest.Mode
                && Session.DraftUseRadians == latest.UseRadians
                && SameValue(Result, Session.Answer) && SameValue(Result, latest.Answer)
                && SameValue(Session.DraftPreviousAnswer, latest.PreviousAnswer);
        }

        public bool ToggleResultFormat()
        {
            if (!CanToggleResultFormat) return false;
            // A presentation change is not a new calculation or an input edit.
            showDecimalResult = !showDecimalResult;
            return true;
        }

        private void EnsureResultFormats()
        {
            if (ReferenceEquals(formattedResultSource, Result)) return;
            formattedResultSource = Result;
            decimalResult = null;
            ResultFormatting.TryCreateDecimal(Result, Session.DraftUseRadians, out decimalResult);
        }

        private void ResetResultFormat()
        {
            showDecimalResult = false;
            formattedResultSource = decimalResult = null;
        }

        private void ClearError()
        {
            Error = null;
            ErrorStart = -1;
            ErrorLength = 0;
        }

        public void Edit(string text, WorkspaceEditContext context = WorkspaceEditContext.Automatic)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            CalculationValue previousAnswer;
            switch (context)
            {
                case WorkspaceEditContext.Automatic:
                    previousAnswer = Session.DraftIsSolved ? Session.Answer : Session.DraftPreviousAnswer;
                    break;
                case WorkspaceEditContext.Preserve:
                    previousAnswer = Session.DraftPreviousAnswer;
                    break;
                case WorkspaceEditContext.NewCalculation:
                    previousAnswer = Session.Answer;
                    break;
                case WorkspaceEditContext.DisplayedResult:
                    previousAnswer = Result ?? Session.Answer;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(context));
            }
            previousAnswer = previousAnswer ?? Session.Answer;
            // Re-entering a solved expression is an actual state transition:
            // the UI uses it to resume editing without changing the text yet.
            if (text == Session.Draft && !Session.DraftIsSolved && Session.DraftMode == Session.Mode
                && Session.DraftAnswer == null && Content == null && Error == null
                && SameValue(Session.DraftPreviousAnswer, previousAnswer)) return;
            RememberEdit();
            ApplyEdit(text, Session.Mode, Session.DraftUseRadians, previousAnswer);
        }

        private void ApplyEdit(string text, CalculationMode mode, bool radians, CalculationValue previousAnswer)
        {
            Cancel();
            Session.DraftPreviousAnswer = (previousAnswer ?? Session.Answer).Clone();
            Session.Draft = text;
            Session.Mode = Session.DraftMode = mode;
            Session.DraftUseRadians = radians;
            Session.DraftIsSolved = false;
            Session.DraftAnswer = null;
            repeatWithLatestAnswer = false;
            ResetResultFormat();
            Content = null;
            ClearError();
        }

        public void SetMode(CalculationMode mode)
        {
            if (Session.Mode == mode && Session.DraftMode == mode) return;
            RememberEdit();
            ApplyEdit(Session.Draft, mode, Session.DraftUseRadians, Session.DraftPreviousAnswer);
        }

        public void SetRadians(bool radians)
        {
            if (Session.DraftUseRadians == radians) return;
            RememberEdit();
            ApplyEdit(Session.Draft, Session.Mode, radians, Session.DraftPreviousAnswer);
        }

        public void Recall(HistoryRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            var previousAnswer = record.PreviousAnswer ?? Session.Answer;
            if (Session.Draft == record.Input && Session.Mode == record.Mode && Session.DraftMode == record.Mode
                && Session.DraftUseRadians == record.UseRadians && Session.DraftIsSolved == (record.Answer != null)
                && SameValue(Session.DraftPreviousAnswer, previousAnswer) && SameValue(Session.DraftAnswer, record.Answer)
                && Content == null && Error == null && !repeatWithLatestAnswer) return;
            RememberEdit();
            Cancel();
            Session.Draft = record.Input;
            Session.Mode = Session.DraftMode = record.Mode;
            Session.DraftUseRadians = record.UseRadians;
            Session.DraftPreviousAnswer = previousAnswer.Clone();
            Session.DraftAnswer = record.Answer?.Clone();
            Session.DraftIsSolved = record.Answer != null;
            repeatWithLatestAnswer = false;
            ResetResultFormat();
            Content = null;
            ClearError();
            // Browsing never changes Ans or recalculates a historical expression.
        }

        public bool Undo() => RestoreEdit(undo, redo);
        public bool Redo() => RestoreEdit(redo, undo);

        private void RememberEdit()
        {
            ClearEdits(redo);
            PushEdit(undo, new EditSnapshot(this), redo);
        }

        private bool RestoreEdit(LinkedList<EditSnapshot> source, LinkedList<EditSnapshot> destination)
        {
            if (source.Last == null) return false;
            var target = PopEdit(source);
            var current = new EditSnapshot(this);
            // Invalidate the worker before restoring anything; a late completion
            // must not overwrite the restored draft or append to global history.
            Cancel();
            PushEdit(destination, current, source);
            Session.Draft = target.Draft;
            Session.Mode = target.Mode;
            Session.DraftMode = target.DraftMode;
            Session.DraftUseRadians = target.UseRadians;
            Session.DraftIsSolved = target.IsSolved;
            Session.DraftPreviousAnswer = target.PreviousAnswer?.Clone();
            Session.DraftAnswer = target.Answer?.Clone();
            Content = target.Content;
            Error = target.Error;
            ErrorStart = target.ErrorStart;
            ErrorLength = target.ErrorLength;
            repeatWithLatestAnswer = target.RepeatWithLatestAnswer;
            ResetResultFormat();
            showDecimalResult = target.ShowDecimalResult;
            EditorState = target.EditorState;
            // Ans and the calculation history describe committed calculations,
            // not editing state, and deliberately are not part of the snapshot.
            return true;
        }

        private void PushEdit(LinkedList<EditSnapshot> destination, EditSnapshot snapshot, LinkedList<EditSnapshot> other)
        {
            if (snapshot.Characters > MaxEditCharacters)
            {
                // Keep the chain contiguous instead of silently skipping an
                // oversized state and undoing an unrelated, older edit.
                ClearEdits(destination);
                return;
            }
            destination.AddLast(snapshot);
            editCharacters += snapshot.Characters;
            while (undo.Count + redo.Count > MaxEditHistory || editCharacters > MaxEditCharacters)
            {
                // Prefer the nearest edits, including the action just reversed.
                var oldest = other.Count != 0 ? other : destination;
                editCharacters -= oldest.First.Value.Characters;
                oldest.RemoveFirst();
            }
        }

        private EditSnapshot PopEdit(LinkedList<EditSnapshot> stack)
        {
            var snapshot = stack.Last.Value;
            stack.RemoveLast();
            editCharacters -= snapshot.Characters;
            return snapshot;
        }

        private void ClearEdits(LinkedList<EditSnapshot> stack)
        {
            foreach (var snapshot in stack) editCharacters -= snapshot.Characters;
            stack.Clear();
        }

        private static bool SameValue(CalculationValue left, CalculationValue right) => ReferenceEquals(left, right)
            || (left != null && right != null && left.Mode == right.Mode && left.Code == right.Code
                && left.Conditions.SequenceEqual(right.Conditions, StringComparer.Ordinal));

        private sealed class EditSnapshot
        {
            public readonly string Draft, Error;
            public readonly CalculationMode Mode, DraftMode;
            public readonly bool UseRadians, IsSolved;
            public readonly bool RepeatWithLatestAnswer, ShowDecimalResult;
            public readonly int ErrorStart, ErrorLength;
            public readonly CalculationValue PreviousAnswer, Answer;
            public readonly ContentResult Content;
            public readonly object EditorState;
            public readonly long Characters;

            public EditSnapshot(CalculatorWorkspace workspace)
            {
                var session = workspace.Session;
                Draft = session.Draft;
                Mode = session.Mode;
                DraftMode = session.DraftMode;
                UseRadians = session.DraftUseRadians;
                IsSolved = session.DraftIsSolved;
                PreviousAnswer = session.DraftPreviousAnswer?.Clone();
                Answer = session.DraftAnswer?.Clone();
                // Evaluation content is published once and read by the UI; keep
                // the exact tree/steps instead of reevaluating during an undo.
                Content = workspace.Content;
                Error = workspace.Error;
                ErrorStart = workspace.ErrorStart;
                ErrorLength = workspace.ErrorLength;
                RepeatWithLatestAnswer = workspace.repeatWithLatestAnswer;
                ShowDecimalResult = workspace.showDecimalResult;
                EditorState = workspace.EditorState;
                Characters = (long)(Draft?.Length ?? 0) + (Error?.Length ?? 0)
                    + ValueCharacters(PreviousAnswer) + ValueCharacters(Answer);
                if (Content?.Calculation is CalculationResult calculation)
                {
                    Characters += calculation.Expression?.ToCode().Length ?? 0;
                    Characters += calculation.Steps?.Sum(step => (long)step.ToCode().Length) ?? 0;
                    Characters += calculation.Comments?.Sum(comment => (long)comment.Length) ?? 0;
                }
                else if (Content != null) Characters += Draft?.Length ?? 0;
            }

            private static long ValueCharacters(CalculationValue value) => value == null ? 0
                : (long)value.Code.Length + value.Conditions.Sum(condition => (long)condition.Length);
        }

        public async Task<bool> EvaluateAsync()
        {
            Cancel();
            if (string.IsNullOrWhiteSpace(Session.Draft)) return false;
            var source = new CancellationTokenSource();
            pending = source;
            long current = revision;
            string input = Session.Draft;
            var options = new CalculationOptions
            {
                Mode = Session.Mode,
                PreviousAnswer = (Session.DraftIsSolved && repeatWithLatestAnswer
                    ? Session.Answer : Session.DraftPreviousAnswer ?? Session.Answer).Clone(),
                UseRadians = Session.DraftUseRadians,
                CaptureSteps = true,
                MaxSteps = 32
            };
            IsBusy = true;
            ClearError();
            try
            {
                var result = await Task.Run(() => new ContentEvaluator().Evaluate(input, options, source.Token), source.Token);
                if (current != revision || source.IsCancellationRequested) return false;
                Content = result;
                Session.DraftPreviousAnswer = options.PreviousAnswer;
                Session.DraftMode = options.Mode;
                Session.DraftAnswer = result.Calculation?.Value;
                Session.DraftIsSolved = true;
                repeatWithLatestAnswer = result.Calculation != null;
                ResetResultFormat();
                if (result.Calculation != null) Session.Answer = result.Calculation.Value.Clone();
                Session.History.Add(new HistoryRecord
                {
                    Input = input, Kind = result.Kind, Mode = options.Mode,
                    UseRadians = options.UseRadians, PreviousAnswer = options.PreviousAnswer,
                    Answer = result.Calculation?.Value.Clone()
                });
                Session.Trim();
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception ex)
            {
                if (current == revision)
                {
                    Error = ex.Message;
                    if (ex is MathException math && math.SourceStart >= 0)
                    {
                        ErrorStart = Math.Min(math.SourceStart, input.Length);
                        ErrorLength = Math.Min(math.SourceLength, input.Length - ErrorStart);
                    }
                }
                return false;
            }
            finally
            {
                if (current == revision) { IsBusy = false; pending = null; }
                source.Dispose();
            }
        }

        public void Cancel()
        {
            revision++;
            pending?.Cancel();
            pending = null;
            IsBusy = false;
        }

        public string Snapshot() => SessionCodec.Write(Session);
        public void Dispose() => Cancel();
    }
}
