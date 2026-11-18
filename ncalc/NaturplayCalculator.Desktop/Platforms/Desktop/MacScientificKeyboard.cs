using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.System;

namespace NaturplayCalculator.Desktop;

/// <summary>Consumes calculator keys before Uno's unconditional TextBox post-key processing.</summary>
internal static class MacScientificKeyboard
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    [DllImport(ObjC)] private static extern nint objc_getClass(string name);
    [DllImport(ObjC)] private static extern nint sel_registerName(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint Send(nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint Send(nint receiver, nint selector, nint argument);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint AddMonitor(nint receiver, nint selector, nuint mask, nint handler);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendBool(nint receiver, nint selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendBool(nint receiver, nint selector, nint argument);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint MonitorCallback(nint block, nint nativeEvent);
    [StructLayout(LayoutKind.Sequential)]
    private struct BlockLiteral
    {
        public nint Isa;
        public int Flags, Reserved;
        public nint Invoke, Descriptor;
    }

    internal readonly record struct Key(VirtualKey Code, string Text, bool Command, bool Shift, bool Option);
    private static readonly MonitorCallback Callback = Dispatch;
    private static Func<Key, bool>? handler;
    private static nint monitor, block, descriptor, signature, window;
    public static bool IsInstalled => monitor != 0;

    public static bool Install(nint calculatorWindow, Func<Key, bool> callback)
    {
        if (!OperatingSystem.IsMacOS() || calculatorWindow == 0) return false;
        try
        {
            window = calculatorWindow; handler = callback;
            if (monitor != 0) return true;
            if (block == 0)
            {
                // Apple Blocks ABI: an immutable global block with a retained
                // managed trampoline. AppKit copies it without stack captures.
                // https://clang.llvm.org/docs/Block-ABI-Apple.html
                nint system = NativeLibrary.Load("/usr/lib/libSystem.B.dylib");
                signature = Marshal.StringToHGlobalAnsi("@16@?0@8");
                descriptor = Marshal.AllocHGlobal(IntPtr.Size * 3);
                Marshal.WriteIntPtr(descriptor, 0, 0);
                Marshal.WriteIntPtr(descriptor, IntPtr.Size, Marshal.SizeOf<BlockLiteral>());
                Marshal.WriteIntPtr(descriptor, IntPtr.Size * 2, signature);
                block = Marshal.AllocHGlobal(Marshal.SizeOf<BlockLiteral>());
                Marshal.StructureToPtr(new BlockLiteral
                {
                    Isa = NativeLibrary.GetExport(system, "_NSConcreteGlobalBlock"),
                    Flags = (1 << 28) | (1 << 30),
                    Invoke = Marshal.GetFunctionPointerForDelegate(Callback), Descriptor = descriptor
                }, block, false);
            }
            monitor = AddMonitor(objc_getClass("NSEvent"), sel_registerName("addLocalMonitorForEventsMatchingMask:handler:"), 1u << 10, block);
            return monitor != 0;
        }
        catch (Exception error)
        {
            Debug.WriteLine($"Native calculator keyboard unavailable: {error}");
            return false;
        }
    }

    private static nint Dispatch(nint _, nint nativeEvent)
    {
        try
        {
            // This is a local monitor for this one NSWindow. File panels,
            // other windows, native shortcuts and composition keep AppKit's path.
            if (nativeEvent == 0 || Get(nativeEvent, "window") != window) return nativeEvent;
            nint responder = Get(window, "firstResponder"), markedText = sel_registerName("hasMarkedText");
            if (responder != 0 && SendBool(responder, sel_registerName("respondsToSelector:"), markedText)
                && SendBool(responder, markedText)) return nativeEvent;
            long modifiers = (long)Get(nativeEvent, "modifierFlags");
            int scanCode = (int)Get(nativeEvent, "keyCode");
            string text = Marshal.PtrToStringUTF8(Get(Get(nativeEvent, "characters"), "UTF8String")) ?? "";
            bool shift = (modifiers & (1L << 17)) != 0;
            if (text.Length == 0 && !shift && scanCode is 44 or 75) text = "/";
            var key = new Key(scanCode switch
            {
                123 => VirtualKey.Left, 124 => VirtualKey.Right, 125 => VirtualKey.Down, 126 => VirtualKey.Up,
                51 => VirtualKey.Back, 117 => VirtualKey.Delete, 36 or 76 => VirtualKey.Enter,
                53 => VirtualKey.Escape, _ => VirtualKey.None
            }, text, (modifiers & ((1L << 18) | (1L << 20))) != 0, shift, (modifiers & (1L << 19)) != 0);
            return handler?.Invoke(key) == true ? 0 : nativeEvent;
        }
        catch (Exception error)
        {
            // Never unwind a managed exception through AppKit's event loop.
            Debug.WriteLine($"Native calculator key failed: {error}");
            return nativeEvent;
        }
    }

    private static nint Get(nint receiver, string selector) => Send(receiver, sel_registerName(selector));
}
