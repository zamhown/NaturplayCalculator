using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NaturplayCalculator.History;
using NaturplayCalculator.Model;
using NaturplayCalculator.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NaturplayCalculator
{
    public sealed partial class MainPage
    {
        private readonly SessionStore sessionStore = new SessionStore();
        private bool sessionLoaded;
        private CancellationTokenSource draftSaveCancellation;
        private Task pendingSessionSave = Task.CompletedTask;
        private bool storageNoticeShown;
        private Uri pendingActivation;

        public void HandleActivation(object parameter)
        {
            if (!(parameter is Uri uri)) return;
            if (!sessionLoaded) { pendingActivation = uri; return; }
            try
            {
                string input = NaturplayCalculator.Input.ActivationInput.Parse(uri);
                if (string.IsNullOrWhiteSpace(input)) return;
                clear(); insertNew(input); solve();
            }
            catch (Exception ex) { Settings.ReportError("Activation", ex); ShowCalculationError(ex.Message); }
        }

        private async Task RestoreSessionAsync()
        {
            if (sessionLoaded || restoringSession) return;
            restoringSession = true;
            try
            {
                var session = await sessionStore.LoadAsync() ?? ReadLegacySession();
                Settings.Ans = session.Answer.Clone();
                Settings.CalculationMode = session.Mode;
                inputMode = session.DraftMode;
                inputPreviousAnswer = session.DraftPreviousAnswer.Clone();
                inputUseRadians = session.DraftUseRadians;
                UpdateModeControls();
                foreach (var record in session.History) historyManager.Add(record);
                if (!TClearLast.IsChecked)
                {
                    quickSetInputTextWithoutInvokingChange(session.Draft);
                    NowS = new InputString(session.Draft); NowS.GiveIndex();
                    textPtr = session.Draft.Length;
                    TextChanged();
                    setThePtr(textPtr);
                    if (session.DraftIsSolved && session.DraftAnswer != null)
                    {
                        presentAnswer = session.DraftAnswer.Clone(); AddExpression(presentAnswer.Expression, true); AddResultNotes(presentAnswer);
                    }
                    solved = session.DraftIsSolved;
                }
                else { inputPreviousAnswer = null; inputUseRadians = null; inputMode = null; UpdateModeControls(); }
                updateRecordMenu();
                if (sessionStore.HadReadError) ShowStorageNotice("session_recovered");
            }
            catch (Exception ex)
            {
                Settings.ReportError("Restore session", ex);
                ShowStorageNotice("session_recovered");
            }
            finally { restoringSession = false; sessionLoaded = true; }
            if (pendingActivation != null)
            {
                var activation = pendingActivation; pendingActivation = null; HandleActivation(activation);
            }
        }
        private CalculatorSession ReadLegacySession()
        {
            var result = new CalculatorSession { DraftUseRadians = Settings.UseRadians, Mode = Settings.CalculationMode, DraftMode = Settings.CalculationMode };
            if (localSettings.Values.TryGetValue("laststr", out var draft) && draft is string text && text.Length <= JsonParser.JSON.MaxInputLength) result.Draft = text;
            if (localSettings.Values.TryGetValue("htyn", out var total) && total is int count && count > 0)
            {
                for (int i = Math.Max(0, count - CalculatorSession.MaxHistory); i < count; i++)
                    if (localSettings.Values.TryGetValue("hty" + i, out var entry) && entry is string input && input.Length <= JsonParser.JSON.MaxInputLength)
                        result.History.Add(new HistoryRecord { Input = input, Kind = "legacy", UseRadians = Settings.UseRadians });
            }
            result.Trim();
            return result;
        }
        public Task SaveSessionAsync()
        {
            if (!sessionLoaded || restoringSession) return pendingSessionSave;
            draftSaveCancellation?.Cancel();
            var snapshot = new CalculatorSession
            {
                Draft = lastInput, Answer = Settings.Ans.Clone(),
                Mode = Settings.CalculationMode, DraftMode = ActiveMode,
                DraftPreviousAnswer = (inputPreviousAnswer ?? Settings.Ans).Clone(),
                DraftUseRadians = inputUseRadians ?? Settings.UseRadians,
                DraftIsSolved = solved,
                DraftAnswer = solved ? presentAnswer?.Clone() : null
            };
            snapshot.History.AddRange(historyManager.Items.Select(item => item.Record));
            pendingSessionSave = PersistSessionAsync(pendingSessionSave, snapshot);
            return pendingSessionSave;
        }
        private async Task PersistSessionAsync(Task previous, CalculatorSession snapshot)
        {
            try
            {
                await previous;
                string json = await Task.Run(() => SessionCodec.Write(snapshot));
                await sessionStore.SaveAsync(json);
            }
            catch (Exception ex)
            {
                Settings.ReportError("Save session", ex);
                ShowStorageNotice("session_save_failed");
            }
        }
        private void ScheduleDraftSave()
        {
            if (!sessionLoaded || restoringSession) return;
            draftSaveCancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            draftSaveCancellation = cancellation;
            _ = SaveDraftAfterDelayAsync(cancellation);
        }
        private async Task SaveDraftAfterDelayAsync(CancellationTokenSource cancellation)
        {
            try { await Task.Delay(400, cancellation.Token); await SaveSessionAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Settings.ReportError("Save draft", ex); }
            finally
            {
                if (ReferenceEquals(draftSaveCancellation, cancellation)) draftSaveCancellation = null;
                cancellation.Dispose();
            }
        }
        public Task SuspendAsync()
        {
            CancelPendingCalculation();
            return SaveSessionAsync();
        }
        private void ShowStorageNotice(string resource)
        {
            if (storageNoticeShown) return;
            storageNoticeShown = true;
            addListViewItem(new TextBlock { Text = AppResources.GetString(resource), TextWrapping = TextWrapping.Wrap, MaxWidth = 1000 });
        }
        private void showHistory()
        {
            if (historyManager.Ptr < 0 || historyManager.Ptr >= historyManager.Items.Count) return;
            CancelPendingCalculation();
            var record = historyManager.Items[historyManager.Ptr].Record;
            inputPreviousAnswer = record.PreviousAnswer?.Clone(); inputUseRadians = record.Kind == "legacy" ? (bool?)null : record.UseRadians;
            inputMode = record.Mode; UpdateModeControls();
            restoringSession = true;
            try
            {
                quickSetInputTextWithoutInvokingChange(record.Input);
                NowS = new InputString(record.Input); NowS.GiveIndex(); textPtr = record.Input.Length;
                TextChanged(); setThePtr(textPtr);
                if (record.Answer != null)
                {
                    presentAnswer = record.Answer.Clone(); AddExpression(presentAnswer.Expression, true); AddResultNotes(presentAnswer);
                }
                else if (record.Kind == "legacy")
                    addListViewItem(new TextBlock { Text = AppResources.GetString("legacy_history"), TextWrapping = TextWrapping.Wrap });
                solved = true;
            }
            finally { restoringSession = false; }
            if (record.Kind == "json" || record.Kind == "xml") _ = CalculateAsync(false, record);
            updateRecordMenu(); ScheduleDraftSave();
        }
        private void mdelHis_Click(object sender, RoutedEventArgs e)
        {
            if (historyManager.Ptr < 0 || historyManager.Ptr >= historyManager.Items.Count) return;
            historyManager.Items.RemoveAt(historyManager.Ptr);
            if (historyManager.Items.Count == 0) clear();
            else { historyManager.Ptr = Math.Min(historyManager.Ptr, historyManager.Items.Count - 1); showHistory(); }
            _ = SaveSessionAsync();
        }
    }
}
