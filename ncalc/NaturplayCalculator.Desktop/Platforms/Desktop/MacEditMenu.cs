using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NaturplayCalculator.Desktop;

/// <summary>Routes the native Edit menu to the calculator's shared undo history.</summary>
internal static class MacEditMenu
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const string TargetClassName = "NaturplayCalculatorEditMenuTarget";
    private const long CommandModifier = 1L << 20, ShiftModifier = 1L << 17;

    [DllImport(ObjC)] private static extern nint objc_getClass(string name);
    [DllImport(ObjC)] private static extern nint sel_registerName(string name);
    [DllImport(ObjC)] private static extern nint objc_allocateClassPair(nint superclass, string name, nuint extraBytes);
    [DllImport(ObjC)] private static extern void objc_registerClassPair(nint cls);
    [DllImport(ObjC)] private static extern void objc_disposeClassPair(nint cls);
    [DllImport(ObjC)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool class_addMethod(nint cls, nint selector, nint implementation, string types);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint Send(nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint Send(nint receiver, nint selector, nint argument);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector, nint title, nint action, nint keyEquivalent);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint StringFromUtf8(nint receiver, nint selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendBool(nint receiver, nint selector);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MenuAction(nint target, nint selector, nint sender);

    // Objective-C retains neither action delegates nor NSMenuItem.target. Keep
    // both managed delegates and our alloc/init-owned target alive for the app.
    private static readonly MenuAction UndoCallback = (_, _, _) => Invoke(undo, canUndo);
    private static readonly MenuAction RedoCallback = (_, _, _) => Invoke(redo, canRedo);
    private static Action? undo, redo;
    private static nint target, editMenu, editMenuItem, undoItem, redoItem;
    private static bool installed, canUndo, canRedo;

    /// <summary>Call on the UI thread after Uno has created its native main menu.</summary>
    public static bool Install(Action undoAction, Action redoAction)
    {
        if (!OperatingSystem.IsMacOS() || undoAction is null || redoAction is null) return false;
        nint createdTarget = 0, createdMenu = 0, createdMenuItem = 0, createdUndo = 0, createdRedo = 0;
        try
        {
            if (!OnMainThread) return false;
            if (installed)
            {
                undo = undoAction; redo = redoAction;
                return true;
            }

            nint app = Get(objc_getClass("NSApplication"), "sharedApplication");
            nint mainMenu = Get(app, "mainMenu");
            if (mainMenu == 0) return false;

            nint targetClass = GetTargetClass();
            if (targetClass == 0) return false;
            createdTarget = Get(Get(targetClass, "alloc"), "init");
            createdMenu = Send(Get(objc_getClass("NSMenu"), "alloc"), Sel("initWithTitle:"), String("编辑"));
            createdMenuItem = NewItem("编辑", 0, "");
            createdUndo = NewItem("撤销", Sel("naturplayUndo:"), "z");
            createdRedo = NewItem("重做", Sel("naturplayRedo:"), "z");
            if (createdTarget == 0 || createdMenu == 0 || createdMenuItem == 0 || createdUndo == 0 || createdRedo == 0)
                throw new InvalidOperationException("AppKit could not create the Edit menu.");

            // State comes from the same workspace as the on-screen controls,
            // rather than AppKit's responder-chain NSUndoManager.
            Send(createdMenu, Sel("setAutoenablesItems:"), 0);
            ConfigureItem(createdUndo, createdTarget, CommandModifier);
            ConfigureItem(createdRedo, createdTarget, CommandModifier | ShiftModifier);
            Send(createdMenu, Sel("addItem:"), createdUndo);
            Send(createdMenu, Sel("addItem:"), createdRedo);
            Send(createdMenuItem, Sel("setSubmenu:"), createdMenu);
            Send(mainMenu, Sel("addItem:"), createdMenuItem);

            target = createdTarget; editMenu = createdMenu; editMenuItem = createdMenuItem;
            undoItem = createdUndo; redoItem = createdRedo;
            undo = undoAction; redo = redoAction;
            installed = true;
            return true;
        }
        catch (Exception error)
        {
            Debug.WriteLine($"Native Edit menu unavailable: {error.Message}");
            Release(createdMenuItem); Release(createdMenu);
            Release(createdUndo); Release(createdRedo); Release(createdTarget);
            return false;
        }
    }

    /// <summary>Refresh availability on the UI thread whenever undo history changes.</summary>
    public static void Update(bool undoAvailable, bool redoAvailable)
    {
        if (!installed || !OperatingSystem.IsMacOS()) return;
        try
        {
            if (!OnMainThread) return;
            canUndo = undoAvailable; canRedo = redoAvailable;
            Send(undoItem, Sel("setEnabled:"), canUndo ? 1 : 0);
            Send(redoItem, Sel("setEnabled:"), canRedo ? 1 : 0);
        }
        catch (Exception error)
        {
            Debug.WriteLine($"Native Edit menu state unavailable: {error.Message}");
        }
    }

    private static bool OnMainThread => SendBool(objc_getClass("NSThread"), Sel("isMainThread"));
    private static nint Sel(string name) => sel_registerName(name);
    private static nint Get(nint receiver, string selector) => Send(receiver, Sel(selector));
    private static nint String(string value) => StringFromUtf8(objc_getClass("NSString"), Sel("stringWithUTF8String:"), value);
    private static nint NewItem(string title, nint action, string key) => Send(Get(objc_getClass("NSMenuItem"), "alloc"),
        Sel("initWithTitle:action:keyEquivalent:"), String(title), action, String(key));

    private static void ConfigureItem(nint item, nint commandTarget, long modifiers)
    {
        Send(item, Sel("setTarget:"), commandTarget);
        Send(item, Sel("setKeyEquivalentModifierMask:"), (nint)modifiers);
        Send(item, Sel("setEnabled:"), 0);
    }

    private static nint GetTargetClass()
    {
        nint cls = objc_getClass(TargetClassName);
        if (cls != 0) return cls;
        cls = objc_allocateClassPair(objc_getClass("NSObject"), TargetClassName, 0);
        if (cls == 0) return 0;
        if (!class_addMethod(cls, Sel("naturplayUndo:"), Marshal.GetFunctionPointerForDelegate(UndoCallback), "v@:@") ||
            !class_addMethod(cls, Sel("naturplayRedo:"), Marshal.GetFunctionPointerForDelegate(RedoCallback), "v@:@"))
        {
            objc_disposeClassPair(cls);
            return 0;
        }
        objc_registerClassPair(cls);
        return cls;
    }

    private static void Invoke(Action? action, bool enabled)
    {
        if (!enabled) return;
        // A managed exception must never cross an Objective-C callback boundary.
        try { action?.Invoke(); }
        catch (Exception error) { Debug.WriteLine($"Native Edit command failed: {error}"); }
    }

    private static void Release(nint value)
    {
        if (value == 0) return;
        try { Get(value, "release"); }
        catch (Exception error) { Debug.WriteLine($"Native Edit menu cleanup failed: {error.Message}"); }
    }
}
