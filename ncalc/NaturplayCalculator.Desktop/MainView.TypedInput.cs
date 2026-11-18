using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace NaturplayCalculator.Desktop;

public sealed partial class MainView
{
    private void InstallNativeScientificKeyboard() => MacScientificKeyboard.Install(MacWindowIntegration.CalculatorWindowHandle, TryHandleNativeScientificKey);

    private bool TryHandleNativeScientificKey(MacScientificKeyboard.Key key)
    {
        if (!loaded || dialogOpen || nativePickerOpen || !IsEnabled || key.Command || !HasScientificKeyboardFocus()) return false;
        if (!key.Shift && key.Option && key.Code is VirtualKey.Left or VirtualKey.Right)
        {
            MoveToParentScope(key.Code == VirtualKey.Right); ConsumeShift(); return true;
        }
        if (!key.Shift && !key.Option && key.Code is VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down)
        {
            NavigateFormula(key.Code); return true;
        }
        if (!key.Shift && !key.Option && key.Code is VirtualKey.Back or VirtualKey.Delete)
        {
            DeleteScientific(key.Code == VirtualKey.Back); ConsumeShift(); return true;
        }
        if (!key.Option && !key.Shift && key.Code == VirtualKey.Enter)
        {
            _ = CalculateAsync(); return true;
        }
        if (!key.Option && !key.Shift && key.Code == VirtualKey.Escape)
        {
            if (workspace.IsBusy) { workspace.Cancel(); UpdateStatus(); } else Clear();
            return true;
        }
        return !key.Option && TryApplyScientificCharacter(key.Text);
    }

    private bool HasScientificKeyboardFocus()
    {
        // XAML flyouts live in a separate visual tree. Their arrows and text
        // belong to the menu, not to the calculator behind it.
        if (XamlRoot == null) return false;
        for (var element = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            element != null; element = VisualTreeHelper.GetParent(element))
            if (ReferenceEquals(element, this)) return true;
        return false;
    }

    private bool TryHandleScientificKey(KeyRoutedEventArgs e, bool command, bool shift, bool option)
    {
        // Clipboard insertion and IME composition stay with the native editor.
        // This path handles a physical key event, never a TextChanged rewrite.
        if (command || option) return false;
        string character = OperatingSystem.IsMacOS() ? MacWindowIntegration.CurrentKeyText
            : !shift && e.Key == VirtualKey.Divide ? "/"
            : !shift && e.Key == VirtualKey.Subtract ? "-" : "";
        // CurrentEvent can be empty when Uno forwards a queued key. Only use
        // the US slash location as fallback when no layout text is available.
        if (character.Length == 0 && !shift && (e.Key == VirtualKey.Divide || (int)e.Key == 191)) character = "/";
        if (!TryApplyScientificCharacter(character)) return false;
        e.Handled = true;
        return true;
    }

    private bool TryApplyScientificCharacter(string character)
    {
        if (character is not "/" and not "-") return false;

        var field = formulaEdit == null ? input : formulaInput;
        string source = field.Text;
        int caret = Math.Clamp(field.SelectionStart, 0, source.Length);
        if (IsLiteralTypingContext(source, caret)) return false;

        // Uno 7.0.65 maps Mac scan code 44 ('/') to unnamed VirtualKey 191,
        // while the numeric keypad uses Divide. Key 191 also names a different
        // key on ISO layouts; AppKit's actual character disambiguates them and
        // lets Shift-'?' remain text instead of becoming division.
        string operation = character == "/" ? "÷"
            : IsUnaryTypedMinus(source, caret, field.SelectionLength) ? "(-)" : "-";
        Key(operation);
        return true;
    }

    private bool IsUnaryTypedMinus(string source, int caret, int selectionLength)
    {
        // The sole physical minus serves both common typing conventions:
        // a result followed by '-' subtracts; 2*-3 starts a negative operand.
        if (formulaEdit == null && workspace.Session.DraftIsSolved && selectionLength == 0) return false;
        while (caret > 0 && char.IsWhiteSpace(source[caret - 1])) caret--;
        if (caret == 0 || "(,+-*/÷×−^".Contains(source[caret - 1])) return true;
        string prefix = source.Substring(0, caret);
        foreach (string operation in new[] { "mod", "nCr", "nPr", "yroot" })
            if (prefix.EndsWith(operation, StringComparison.Ordinal)
                && (prefix.Length == operation.Length || !char.IsLetter(prefix[prefix.Length - operation.Length - 1]))) return true;
        return false;
    }

    private static bool IsLiteralTypingContext(string source, int caret)
    {
        string leading = source.TrimStart();
        if (leading.StartsWith('{') || leading.StartsWith('<')) return true;
        bool comment = false;
        for (int i = 0; i < caret; i++)
        {
            if (comment && source[i] == '\\') { i++; continue; }
            if (source[i] == '[') comment = true;
            else if (source[i] == ']') comment = false;
        }
        return comment;
    }
}
