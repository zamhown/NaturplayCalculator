using System;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;

namespace NaturplayCalculator.Model
{
    internal sealed class CalculationContext : IDisposable
    {
        private static readonly AsyncLocal<CalculationContext> slot = new AsyncLocal<CalculationContext>();
        private readonly CalculationContext previous;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly CancellationToken cancellation;
        private readonly TimeSpan timeout;
        private int remainingOperations;

        internal static CalculationContext Current => slot.Value;
        internal MathValue PreviousAnswer { get; }
        internal bool UseRadians { get; }
        internal Action OnStep { get; set; }

        internal CalculationContext(CalculationOptions options, CancellationToken cancellation)
        {
            previous = slot.Value;
            PreviousAnswer = options.PreviousAnswer?.Numeric?.Clone() ?? new MathValue(0);
            UseRadians = options.UseRadians;
            remainingOperations = options.MaxOperations;
            timeout = options.Timeout;
            this.cancellation = cancellation;
            slot.Value = this;
        }

        internal static void Check()
        {
            var current = slot.Value;
            if (current == null) return;
            current.cancellation.ThrowIfCancellationRequested();
            if (--current.remainingOperations < 0 || current.clock.Elapsed > current.timeout)
                throw new MathException(CalculationResources.GetString("calculation_limit"));
        }

        public void Dispose() { slot.Value = previous; }
    }

    public sealed class CalculationOptions
    {
        public CalculationMode Mode { get; set; } = CalculationMode.Numeric;
        public CalculationValue PreviousAnswer { get; set; } = new MathValue(0);
        public IReadOnlyDictionary<string, MathValue> Variables { get; set; }
        public bool UseRadians { get; set; } = true;
        public bool CaptureSteps { get; set; }
        public int MaxSteps { get; set; } = 64;
        public int MaxOperations { get; set; } = 250000;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);
    }
}
