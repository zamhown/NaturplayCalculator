using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace NaturplayCalculator.Desktop;

/// <summary>Small AppKit boundary. Uno owns the window and its NSVisualEffectView.</summary>
internal static class MacWindowIntegration
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    [DllImport(ObjC)] private static extern nint objc_getClass(string name);
    [DllImport(ObjC)] private static extern nint sel_registerName(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint Send(nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint Send(nint receiver, nint selector, nint argument);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern double SendDouble(nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendBool(nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendBool(nint receiver, nint selector, nint argument);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint StringFromUtf8(nint receiver, nint selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern int CGWindowLevelForKey(int key);

    // Public AppKit constants from NSWindow.h. These behavior groups are
    // mutually exclusive, so remove the old member before adding its replacement.
    private const long CanJoinAllSpaces = 1L << 0, MoveToActiveSpace = 1L << 1;
    private const long Managed = 1L << 2, Transient = 1L << 3, Stationary = 1L << 4;
    private const long FullScreenPrimary = 1L << 7, FullScreenAuxiliary = 1L << 8, FullScreenNone = 1L << 9;
    private const long FullScreenAllowsTiling = 1L << 11, FullScreenDisallowsTiling = 1L << 12;
    private const long Primary = 1L << 16, Auxiliary = 1L << 17, CanJoinAllApplications = 1L << 18;
    private const int FloatingWindowLevelKey = 5;
    private const long Miniaturizable = 1L << 2;
    private const int AccessoryActivationPolicy = 1;
    private static nint pinnedWindow, originalLevel, originalCollectionBehavior;
    private static nint originalActivationPolicy, originalStyleMask;
    private static bool originalHidesOnDeactivate;
    private static readonly string[] SystemColorSelectors =
    [
        "windowBackgroundColor", "controlBackgroundColor", "controlColor", "underPageBackgroundColor",
        "textBackgroundColor", "selectedContentBackgroundColor", "unemphasizedSelectedContentBackgroundColor",
        "labelColor", "secondaryLabelColor", "tertiaryLabelColor", "quaternaryLabelColor", "quinaryLabelColor",
        "separatorColor", "gridColor", "textColor", "controlTextColor", "disabledControlTextColor",
        "placeholderTextColor", "selectedTextColor", "selectedTextBackgroundColor", "selectedControlTextColor",
        "alternateSelectedControlTextColor", "keyboardFocusIndicatorColor", "controlAccentColor", "systemGrayColor",
        "systemFillColor", "secondarySystemFillColor", "tertiarySystemFillColor", "quaternarySystemFillColor", "quinarySystemFillColor",
        "clearColor", "whiteColor", "systemRedColor", "systemOrangeColor", "systemYellowColor", "systemBlueColor"
    ];

    public static bool IsAlwaysOnTop => pinnedWindow != 0;
    internal static nint CalculatorWindowHandle => OperatingSystem.IsMacOS() ? MainWindow : 0;
    public static bool IsOnActiveSpace => OperatingSystem.IsMacOS() && SendBool(MainWindow, sel_registerName("isOnActiveSpace"));
    public static bool IsVisible => OperatingSystem.IsMacOS() && SendBool(MainWindow, sel_registerName("isVisible"));
    public static long OcclusionState => OperatingSystem.IsMacOS() ? (long)Get(MainWindow, "occlusionState") : 0;

    private static nint Get(nint target, string selector) => Send(target, sel_registerName(selector));
    private static nint App => Get(objc_getClass("NSApplication"), "sharedApplication");
    public static string SystemAppearance => Marshal.PtrToStringUTF8(Get(Get(Get(App, "effectiveAppearance"), "name"), "UTF8String")) ?? "";
    public static bool IsSystemDark => OperatingSystem.IsMacOS() && SystemAppearance.Contains("Dark", StringComparison.OrdinalIgnoreCase);
    public static bool IsCommandDown => OperatingSystem.IsMacOS() && ((long)Get(Get(App, "currentEvent"), "modifierFlags") & (1L << 20)) != 0;
    public static bool IsShiftDown => OperatingSystem.IsMacOS() && ((long)Get(Get(App, "currentEvent"), "modifierFlags") & (1L << 17)) != 0;
    public static bool IsOptionDown => OperatingSystem.IsMacOS() && ((long)Get(Get(App, "currentEvent"), "modifierFlags") & (1L << 19)) != 0;
    public static string CurrentKeyText
    {
        get
        {
            if (!OperatingSystem.IsMacOS()) return "";
            nint currentEvent = Get(App, "currentEvent");
            // NSEventTypeKeyDown. Read the layout-resolved character, not a
            // physical key location shared by '/' and other keyboard layouts.
            if (currentEvent == 0 || (long)Get(currentEvent, "type") != 10) return "";
            return Marshal.PtrToStringUTF8(Get(Get(currentEvent, "characters"), "UTF8String")) ?? "";
        }
    }
    public static double DisplayScale => OperatingSystem.IsMacOS()
        ? Math.Max(1, SendDouble(Get(objc_getClass("NSScreen"), "mainScreen"), sel_registerName("backingScaleFactor"))) : 1;
    private static nint MainWindow
    {
        get
        {
            nint app = App;
            nint window = Get(app, "mainWindow");
            if (window != 0) return window;
            nint windows = Get(app, "windows");
            return Get(windows, "count") > 0 ? Send(windows, sel_registerName("objectAtIndex:"), 0) : 0;
        }
    }

    public static void Activate()
    {
        if (!OperatingSystem.IsMacOS()) return;
        Send(App, sel_registerName("activateIgnoringOtherApps:"), 1);
        Send(MainWindow, sel_registerName("makeKeyAndOrderFront:"), 0);
    }

    /// <summary>
    /// Float the existing editable Uno window across Spaces, including other
    /// apps' full-screen spaces. Call on the UI thread after window creation.
    /// This remains one movable window, not a duplicate on each physical display.
    /// </summary>
    public static bool SetAlwaysOnTop(bool enabled)
    {
        if (!OperatingSystem.IsMacOS()) return false;
        nint window = MainWindow;
        if (window == 0) return false;

        if (enabled)
        {
            if (pinnedWindow != window)
            {
                originalLevel = Get(window, "level");
                originalCollectionBehavior = Get(window, "collectionBehavior");
                originalHidesOnDeactivate = SendBool(window, sel_registerName("hidesOnDeactivate"));
                originalActivationPolicy = Get(App, "activationPolicy");
                originalStyleMask = Get(window, "styleMask");
            }

            // A regular app's NSWindow can remain assigned to its own Space
            // despite floating/fullScreenAuxiliary. While pinned, use AppKit's
            // accessory policy so this same editable Uno window can accompany
            // other apps in full screen. Restore the Dock/menu policy on unpin.
            if (Get(App, "activationPolicy") != AccessoryActivationPolicy &&
                !SendBool(App, sel_registerName("setActivationPolicy:"), AccessoryActivationPolicy)) return false;

            // Accessory apps have no Dock tile. Prevent minimizing the sole
            // calculator window into a state with no visible way to retrieve it.
            Send(window, sel_registerName("setStyleMask:"), (nint)((long)Get(window, "styleMask") & ~Miniaturizable));

            long behavior = (long)originalCollectionBehavior;
            behavior &= ~(MoveToActiveSpace | Managed | Stationary |
                FullScreenPrimary | FullScreenNone | FullScreenAllowsTiling |
                Primary | Auxiliary);
            behavior |= CanJoinAllSpaces | Transient | FullScreenAuxiliary | FullScreenDisallowsTiling;
            // Since macOS 13 this is the explicit floating-window policy for
            // Stage Manager and joining another application's full-screen Space.
            if (OperatingSystem.IsMacOSVersionAtLeast(13)) behavior |= CanJoinAllApplications;

            Send(window, sel_registerName("setCollectionBehavior:"), (nint)behavior);
            Send(window, sel_registerName("setHidesOnDeactivate:"), 0);
            Send(window, sel_registerName("setLevel:"), CGWindowLevelForKey(FloatingWindowLevelKey));
            Get(window, "orderFrontRegardless");
            pinnedWindow = window;
        }
        else if (pinnedWindow != 0)
        {
            if (Get(App, "activationPolicy") != originalActivationPolicy &&
                !SendBool(App, sel_registerName("setActivationPolicy:"), originalActivationPolicy)) return false;
            // A recreated native window has its own defaults; never apply a
            // previous window's saved settings to it or dereference a closed one.
            if (pinnedWindow == window)
            {
                Send(window, sel_registerName("setLevel:"), originalLevel);
                Send(window, sel_registerName("setCollectionBehavior:"), originalCollectionBehavior);
                Send(window, sel_registerName("setHidesOnDeactivate:"), originalHidesOnDeactivate ? 1 : 0);
                // Restore only the bit changed here; AppKit owns transient
                // style bits such as fullScreen during a window transition.
                long style = (long)Get(window, "styleMask");
                Send(window, sel_registerName("setStyleMask:"), (nint)((style & ~Miniaturizable) | ((long)originalStyleMask & Miniaturizable)));
            }
            pinnedWindow = 0;
        }
        return true;
    }

    public static string DescribeWindowBehavior()
    {
        if (!OperatingSystem.IsMacOS()) return "Not macOS";
        nint window = MainWindow;
        return window == 0 ? "No native window" :
            $"Pinned={IsAlwaysOnTop}; Level={Get(window, "level")}; CollectionBehavior=0x{(long)Get(window, "collectionBehavior"):X}; HidesOnDeactivate={SendBool(window, sel_registerName("hidesOnDeactivate"))}; OnActiveSpace={SendBool(window, sel_registerName("isOnActiveSpace"))}; Visible={SendBool(window, sel_registerName("isVisible"))}; OcclusionState=0x{(long)Get(window, "occlusionState"):X}; ActivationPolicy={Get(App, "activationPolicy")}; StyleMask=0x{(long)Get(window, "styleMask"):X}";
    }

    public static void SetAppearance(ElementTheme theme)
    {
        if (!OperatingSystem.IsMacOS()) return;
        Send(MainWindow, sel_registerName("setAppearance:"), theme == ElementTheme.Default ? 0 : ResolveAppearance(theme));
    }

    private static nint Workspace => Get(objc_getClass("NSWorkspace"), "sharedWorkspace");
    private static bool IncreaseContrast => SendBool(Workspace, sel_registerName("accessibilityDisplayShouldIncreaseContrast"));
    private static string AppearanceName(nint appearance) => Marshal.PtrToStringUTF8(Get(Get(appearance, "name"), "UTF8String")) ?? "";
    private static nint EffectiveAppearance
    {
        get
        {
            nint window = MainWindow;
            return Get(window != 0 ? window : App, "effectiveAppearance");
        }
    }

    private static nint ResolveAppearance(ElementTheme theme)
    {
        nint effective = EffectiveAppearance;
        bool contrast = IncreaseContrast;
        if (theme == ElementTheme.Default && !contrast) return effective;
        bool isDark = theme == ElementTheme.Dark ||
            (theme == ElementTheme.Default && AppearanceName(effective).Contains("Dark", StringComparison.OrdinalIgnoreCase));
        string name = contrast
            ? isDark ? "NSAppearanceNameAccessibilityDarkAqua" : "NSAppearanceNameAccessibilityAqua"
            : isDark ? "NSAppearanceNameDarkAqua" : "NSAppearanceNameAqua";
        nint nativeName = StringFromUtf8(objc_getClass("NSString"), sel_registerName("stringWithUTF8String:"), name);
        nint appearance = Send(objc_getClass("NSAppearance"), sel_registerName("appearanceNamed:"), nativeName);
        return appearance != 0 ? appearance : effective;
    }

    /// <summary>
    /// Snapshot an AppKit semantic color in the requested appearance. Invoke on
    /// the UI thread and refresh the cached palette when AppearanceFingerprint changes.
    /// System alpha is retained; no substitute RGB palette is embedded here.
    /// </summary>
    public static Color ResolveSystemColor(string selector, ElementTheme theme)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("AppKit system colors require macOS.");
        if (!SystemColorSelectors.Contains(selector, StringComparer.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(selector), selector, "Unsupported AppKit semantic color.");

        nint pool = Get(Get(objc_getClass("NSAutoreleasePool"), "alloc"), "init");
        nint appearanceClass = objc_getClass("NSAppearance");
        nint previous = Get(appearanceClass, "currentAppearance");
        try
        {
            // This public, thread-local API remains available on macOS. Its
            // synchronous try/finally scope avoids hand-marshalling an Objective-C
            // block for performAsCurrentDrawingAppearance and always restores the
            // caller's drawing appearance before returning to Uno.
            Send(appearanceClass, sel_registerName("setCurrentAppearance:"), ResolveAppearance(theme));
            nint colorClass = objc_getClass("NSColor");
            nint colorSelector = sel_registerName(selector);
            if (!SendBool(colorClass, sel_registerName("respondsToSelector:"), colorSelector))
            {
                // Fill roles were introduced in macOS 14. Older systems still
                // supply dynamic neutral label colors with their own alpha.
                string fallback = selector switch
                {
                    "systemFillColor" => "quaternaryLabelColor",
                    "secondarySystemFillColor" => "quaternaryLabelColor",
                    "tertiarySystemFillColor" or "quaternarySystemFillColor" or "quinarySystemFillColor" => "quinaryLabelColor",
                    "quinaryLabelColor" => "quaternaryLabelColor",
                    _ => throw new PlatformNotSupportedException($"AppKit does not provide {selector} on this system.")
                };
                if (fallback == "quinaryLabelColor" &&
                    !SendBool(colorClass, sel_registerName("respondsToSelector:"), sel_registerName(fallback))) fallback = "quaternaryLabelColor";
                colorSelector = sel_registerName(fallback);
            }
            nint color = Send(colorClass, colorSelector);
            nint colorSpace = Get(objc_getClass("NSColorSpace"), "sRGBColorSpace");
            nint rgb = Send(color, sel_registerName("colorUsingColorSpace:"), colorSpace);
            if (rgb == 0) throw new InvalidOperationException($"Cannot resolve AppKit color {selector} in sRGB.");
            // CGFloat is double on both supported 64-bit architectures. Reading
            // scalar components avoids NSColor/CGColor struct-return ABI differences.
            byte Component(string name) => (byte)Math.Clamp(Math.Round(SendDouble(rgb, sel_registerName(name)) * 255), 0, 255);
            return Color.FromArgb(Component("alphaComponent"), Component("redComponent"), Component("greenComponent"), Component("blueComponent"));
        }
        finally
        {
            Send(appearanceClass, sel_registerName("setCurrentAppearance:"), previous);
            Get(pool, "drain");
        }
    }

    public static string AppearanceFingerprint(ElementTheme theme)
    {
        if (!OperatingSystem.IsMacOS()) return "Not macOS";
        // Appearance names alone miss accessibility and accent changes that keep
        // the same light/dark scheme. Include representative native color values.
        string Swatch(string selector)
        {
            Color color = ResolveSystemColor(selector, theme);
            return $"{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        return $"{AppearanceName(EffectiveAppearance)}|Resolved={AppearanceName(ResolveAppearance(theme))}" +
            $"|Contrast={IncreaseContrast}|ReduceTransparency={SendBool(Workspace, sel_registerName("accessibilityDisplayShouldReduceTransparency"))}" +
            $"|Accent={Swatch("controlAccentColor")}|Window={Swatch("windowBackgroundColor")}" +
            $"|Control={Swatch("controlBackgroundColor")}|Label={Swatch("labelColor")}|Secondary={Swatch("secondaryLabelColor")}" +
            $"|Separator={Swatch("separatorColor")}|Fill={Swatch("secondarySystemFillColor")}" +
            $"|Red={Swatch("systemRedColor")}|Orange={Swatch("systemOrangeColor")}|Focus={Swatch("keyboardFocusIndicatorColor")}";
    }

    public static string DescribeBackdrop()
    {
        if (!OperatingSystem.IsMacOS()) return "Not macOS";
        nint effectClass = objc_getClass("NSVisualEffectView");
        var effects = new List<string>();
        void Visit(nint view)
        {
            if (Send(view, sel_registerName("isKindOfClass:"), effectClass) != 0)
                effects.Add($"NSVisualEffectView(material={Get(view, "material")}, blendingMode={Get(view, "blendingMode")}, state={Get(view, "state")})");
            nint children = Get(view, "subviews");
            for (nint i = 0, count = Get(children, "count"); i < count; i++) Visit(Send(children, sel_registerName("objectAtIndex:"), i));
        }
        Visit(Get(MainWindow, "contentView"));
        return SystemAppearance + "; " + string.Join("; ", effects);
    }
}
