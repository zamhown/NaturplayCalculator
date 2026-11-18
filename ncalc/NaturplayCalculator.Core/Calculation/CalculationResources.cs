using System;
using System.Collections.Generic;

namespace NaturplayCalculator.Model
{
    // The host supplies a snapshot of localized messages before starting workers.
    // Core calculations never access a view, ResourceLoader, or application state.
    public static class CalculationResources
    {
        private static IReadOnlyDictionary<string, string> messages = new Dictionary<string, string>();

        public static void Configure(IDictionary<string, string> values)
        {
            messages = new Dictionary<string, string>(values ?? throw new ArgumentNullException(nameof(values)));
        }

        public static string GetString(string key)
        {
            string value;
            if (messages.TryGetValue(key, out value) && !string.IsNullOrEmpty(value)) return value;
            switch (key)
            {
                case "calculation_limit": return "This expression exceeds the calculation limit. Simplify it and try again.";
                case "integer_required": return "This operation requires integers in its supported range.";
                case "non_finite": return "The result is undefined or outside the supported numeric range.";
                case "division_by_zero": return "Division by zero is undefined.";
                case "invalid_expression": return "The expression is incomplete or invalid.";
                case "unbound_variable": return "Variable {0} has no numeric value. Use symbolic mode to simplify expressions with variables.";
                case "symbolic_numeric_required": return "An exact symbolic expression has no numeric value until all variables are known.";
                default: return key;
            }
        }
    }
}
