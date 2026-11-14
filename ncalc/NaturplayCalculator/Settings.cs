using NaturplayCalculator.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace NaturplayCalculator
{
    public static class Settings
    {
        public static bool White = true;
        public static bool UseRadians = true;
        public static bool FoldingNum = true;
        public static CalculationValue Ans = new MathValue(0);
        public static CalculationMode CalculationMode = CalculationMode.Numeric;

        public static string GetVersion()
        {
            return $"{Package.Current.Id.Version.Major}.{Package.Current.Id.Version.Minor}.{Package.Current.Id.Version.Build}.{Package.Current.Id.Version.Revision}";
        }

        public static void Log(string message) { System.Diagnostics.Debug.WriteLine(message); }
        public static void ReportError(string operation, Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(operation + ": " + exception);
            try
            {
                var path = System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "diagnostics.log");
                if (System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length > 256 * 1024) System.IO.File.WriteAllText(path, "");
                System.IO.File.AppendAllText(path, DateTimeOffset.Now + " " + operation + ": " + exception + Environment.NewLine);
            }
            catch { /* Diagnostics must not replace the original failure. */ }
        }
    }
}
