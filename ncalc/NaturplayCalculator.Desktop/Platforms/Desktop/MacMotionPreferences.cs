using System.Runtime.InteropServices;

namespace NaturplayCalculator.Desktop;

/// <summary>Read the live AppKit preference at the start of each expression edit.</summary>
internal static class MacMotionPreferences
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    [DllImport(ObjC)] private static extern nint objc_getClass(string name);
    [DllImport(ObjC)] private static extern nint sel_registerName(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint Send(nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendBool(nint receiver, nint selector);

    public static bool ReduceMotion => OperatingSystem.IsMacOS() && SendBool(
        Send(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace")),
        sel_registerName("accessibilityDisplayShouldReduceMotion"));
}
