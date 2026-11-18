using System;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.Input
{
    public static class ActivationInput
    {
        public static string Parse(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri || !string.Equals(uri.Scheme, "ncalc", StringComparison.OrdinalIgnoreCase)) return null;
            if (uri.OriginalString.Length > ExpressionLimits.MaxLength * 3 + 128) throw new FormatException("Activation expression is too long.");
            string result = null;
            foreach (string part in uri.Query.TrimStart('?').Split('&'))
            {
                int equals = part.IndexOf('=');
                if (equals >= 0 && part.Substring(0, equals).Equals("expression", StringComparison.OrdinalIgnoreCase))
                    result = Uri.UnescapeDataString(part.Substring(equals + 1));
            }
            if (result == null) result = Uri.UnescapeDataString(uri.OriginalString.Substring(uri.OriginalString.IndexOf(':') + 1).TrimStart('/'));
            if (result.Length > ExpressionLimits.MaxLength) throw new FormatException("Activation expression is too long.");
            return result;
        }
    }
}
