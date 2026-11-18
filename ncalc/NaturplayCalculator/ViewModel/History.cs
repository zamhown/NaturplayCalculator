using NaturplayCalculator.History;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.ViewModel
{
    public sealed class History
    {
        public HistoryRecord Record { get; }
        public InputString InputExpression => new InputString(Record.Input);
        public History(HistoryRecord record) { Record = record; }
    }
}
