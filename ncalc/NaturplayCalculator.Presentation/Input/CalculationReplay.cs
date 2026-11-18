using System.Linq;
using NaturplayCalculator.History;

namespace NaturplayCalculator.Presentation;

/// <summary>Casio-style result replay, without evaluating or changing global Ans.</summary>
public sealed class CalculationReplay
{
    private CalculatorWorkspace owner;
    private long revision;
    private int position;
    private HistoryRecord returnDraft;
    private object returnEditor;

    public bool Move(CalculatorWorkspace workspace, int direction)
    {
        if (workspace.IsBusy || direction == 0) return false;
        var session = workspace.Session;
        var records = session.History.Where(record => record.Kind == "math" && record.Answer != null).ToArray();
        if (records.Length == 0) return false;
        if (owner != workspace || revision != workspace.EditRevision)
        {
            // An unfinished expression uses vertical structure navigation.
            if (!session.DraftIsSolved && !string.IsNullOrWhiteSpace(session.Draft)) return false;
            owner = workspace;
            position = session.DraftIsSolved ? System.Array.FindLastIndex(records, record =>
                record.Input == session.Draft && record.Mode == session.DraftMode
                && record.UseRadians == session.DraftUseRadians
                && record.Answer?.Code == session.DraftAnswer?.Code
                && record.PreviousAnswer?.Code == session.DraftPreviousAnswer?.Code) : -1;
            returnDraft = null; returnEditor = null;
            if (position < 0)
            {
                position = records.Length;
                returnDraft = new HistoryRecord
                {
                    Input = session.Draft, Mode = session.DraftMode, UseRadians = session.DraftUseRadians,
                    PreviousAnswer = session.DraftPreviousAnswer?.Clone(), Answer = session.DraftAnswer?.Clone()
                };
                returnEditor = workspace.EditorState;
            }
            revision = workspace.EditRevision;
        }
        int next = position + (direction < 0 ? -1 : 1);
        if (next < 0 || next > records.Length || next == records.Length && returnDraft == null) return false;
        workspace.Recall(next == records.Length ? returnDraft : records[next]);
        workspace.EditorState = next == records.Length ? returnEditor : null;
        position = next;
        revision = workspace.EditRevision;
        return true;
    }
}
