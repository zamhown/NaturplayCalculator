using System;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Presentation
{
    /// <summary>Creates a decimal view of a stored exact value, never its original input.</summary>
    public static class ResultFormatting
    {
        public static bool TryCreateDecimal(CalculationValue value, bool useRadians, out CalculationValue result)
        {
            result = null;
            // A floating-point result has already lost its exact provenance.
            // Do not invent a rational approximation and label it exact.
            if (value == null || value.Numeric != null) return false;
            try
            {
                // NumericDomain evaluates the immutable value tree through Ans.
                // No variables, original expression, workspace, or history are used.
                var converted = new CalculationEngine().Evaluate("Ans", new CalculationOptions
                {
                    Mode = CalculationMode.Numeric,
                    PreviousAnswer = value,
                    UseRadians = useRadians,
                    CaptureSteps = false,
                    MaxOperations = 20000,
                    Timeout = TimeSpan.FromMilliseconds(250)
                }).Value;
                if (converted.Code == value.Code) return false;
                result = converted;
                return true;
            }
            catch (MathException)
            {
                // Unbound variables, domain errors, or an oversized conversion
                // leave the original exact result available without an error UI.
                return false;
            }
        }
    }
}
