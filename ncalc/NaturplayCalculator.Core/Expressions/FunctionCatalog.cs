using System;
using System.Collections.Generic;
using System.Linq;

namespace NaturplayCalculator.Expressions
{
    internal static class FunctionCatalog
    {
        private static readonly Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sqrt"]="sqrt", ["sin"]="sin", ["cos"]="cos", ["tan"]="tan",
            ["sinrad"]="sinrad", ["cosrad"]="cosrad", ["tanrad"]="tanrad",
            ["arcsinrad"]="arcsinrad", ["arccosrad"]="arccosrad", ["arctanrad"]="arctanrad",
            ["arcsin"]="arcsin", ["asin"]="arcsin", ["arccos"]="arccos", ["acos"]="arccos", ["arctan"]="arctan", ["atan"]="arctan",
            ["sinh"]="sinh", ["cosh"]="cosh", ["tanh"]="tanh", ["arsh"]="arsh", ["asinh"]="arsh", ["arch"]="arch", ["acosh"]="arch", ["arth"]="arth", ["atanh"]="arth",
            ["abs"]="Abs", ["ln"]="ln", ["lg"]="lg", ["fact"]="fact", ["dfact"]="dfact",
            ["Ex"]="E", ["Dx"]="D", ["Cov"]="Cov", ["rho"]="rho", ["rank"]="rank"
        };
        internal static readonly string[] Tokens = names.Keys.OrderByDescending(name => name.Length).ToArray();
        internal static string Canonical(string name) => names[name];
    }
}
