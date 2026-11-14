using NaturplayCalculator.Model;
using Microsoft.UI.Xaml;

namespace NaturplayCalculator
{
    public sealed partial class MainPage
    {
        private CalculationMode? inputMode;
        private CalculationMode ActiveMode => inputMode ?? Settings.CalculationMode;

        private void UpdateModeControls()
        {
            TNumericMode.IsChecked = ActiveMode == CalculationMode.Numeric;
            TSymbolicMode.IsChecked = ActiveMode == CalculationMode.Symbolic;
            Thudu.IsChecked = inputUseRadians ?? Settings.UseRadians;
            Tnohudu.IsChecked = !Thudu.IsChecked;
        }

        private void NumericMode_Click(object sender, RoutedEventArgs e) => SetCalculationMode(CalculationMode.Numeric);
        private void SymbolicMode_Click(object sender, RoutedEventArgs e) => SetCalculationMode(CalculationMode.Symbolic);

        private void SetCalculationMode(CalculationMode mode)
        {
            bool changed = mode != ActiveMode;
            Settings.CalculationMode = mode;
            inputMode = mode;
            localSettings.Values["calculationMode"] = (int)mode;
            // Clicking the checked item must not leave both modes unchecked.
            UpdateModeControls();
            if (changed) TextChanged(true);
            ScheduleDraftSave();
        }
    }
}
