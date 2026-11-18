using System;
using System.Collections.Generic;
using System.Linq;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.History
{
    public sealed class HistoryRecord
    {
        public string Input { get; set; } = "";
        public string Kind { get; set; } = "math";
        public CalculationMode Mode { get; set; } = CalculationMode.Numeric;
        public CalculationValue Answer { get; set; }
        public CalculationValue PreviousAnswer { get; set; }
        public bool UseRadians { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public bool HasCalculationContext => PreviousAnswer != null && Answer != null;
    }

    public sealed class CalculatorSession
    {
        public const int MaxHistory = 100;
        public List<HistoryRecord> History { get; } = new List<HistoryRecord>();
        public string Draft { get; set; } = "";
        public CalculationMode Mode { get; set; } = CalculationMode.Numeric;
        public CalculationMode DraftMode { get; set; } = CalculationMode.Numeric;
        public CalculationValue Answer { get; set; } = new MathValue(0);
        public CalculationValue DraftPreviousAnswer { get; set; } = new MathValue(0);
        public bool DraftUseRadians { get; set; } = true;
        public bool DraftIsSolved { get; set; }
        public CalculationValue DraftAnswer { get; set; }

        public void Trim()
        {
            int characters = History.Sum(item => item.Input.Length);
            while (History.Count > MaxHistory || (characters > 256 * 1024 && History.Count > 1))
            {
                characters -= History[0].Input.Length;
                History.RemoveAt(0);
            }
        }
    }
}
