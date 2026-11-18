using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Windows.ApplicationModel.Resources;

namespace NaturplayCalculator
{
    public static class AppResources
    {
        private static ResourceLoader CurrentResourceLoader
        {
            get { return _loader ?? (_loader = new ResourceLoader()); }
        }

        private static ResourceLoader _loader;
        private static readonly Dictionary<string, string> ResourceCache = new Dictionary<string, string>();

        public static string GetString(string key)
        {
            string s;
            if (ResourceCache.TryGetValue(key, out s))
            {
                return s;
            }
            else
            {
                s = CurrentResourceLoader.GetString(key);
                ResourceCache[key] = s;
                return s;
            }
        }



        public static void ConfigureCalculator()
        {
            var messages = new Dictionary<string, string>();
            for (int i = 0; i <= 34; i++) messages["error_" + i] = GetString("error_" + i);
            foreach (var key in new[] { "calculation_limit", "integer_required", "non_finite", "division_by_zero", "invalid_expression", "unbound_variable", "symbolic_numeric_required" }) messages[key] = GetString(key);
            NaturplayCalculator.Model.CalculationResources.Configure(messages);
        }

        /// <summary>
        /// AppName
        /// </summary>
        public static string AppName
        {
            get
            {
                return CurrentResourceLoader.GetString("AppName");
            }
        }
    }
}
