using System.Collections.Generic;
using NaturplayCalculator.History;

namespace NaturplayCalculator.ViewModel
{
    public sealed class HistoryManager
    {
        public List<History> Items { get; } = new List<History>();
        public int Ptr { get; set; } = -1;
        public void Add(HistoryRecord record)
        {
            Items.Add(new History(record));
            while (Items.Count > CalculatorSession.MaxHistory) Items.RemoveAt(0);
            int characters = 0;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                characters += Items[i].Record.Input.Length;
                if (characters > 256 * 1024 && i < Items.Count - 1) { Items.RemoveRange(0, i + 1); break; }
            }
            Ptr = Items.Count;
        }
    }
}
