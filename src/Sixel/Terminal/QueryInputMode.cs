using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Sixel.Terminal;

internal interface IQueryInputMode {
    IDisposable EnterQueryMode();
}

/// <summary>Preserves APC/DCS responses through ConPTY while a query owns input.</summary>
internal sealed class QueryInputMode : IDisposable {
    private readonly IntPtr handle;
    private readonly uint originalMode;
    private bool restored;

    internal QueryInputMode() {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        handle = GetStdHandle(-10);
        if (!GetConsoleMode(handle, out originalMode) ||
            !SetConsoleMode(handle, (originalMode | 0x0200u) & ~0x0006u))
            throw new IOException("Unable to acquire VT input for terminal queries.", new Win32Exception(Marshal.GetLastWin32Error()));
        // ENABLE_VIRTUAL_TERMINAL_INPUT, without LINE_INPUT or ECHO_INPUT.
        // Legacy ConPTY input parsing otherwise eats APC replies, leaving ST's
        // backslash behind. Acquire this BEFORE emitting any query.
    }

    public void Dispose() {
        if (restored || handle == IntPtr.Zero) return;
        restored = true;
        if (!SetConsoleMode(handle, originalMode))
            throw new IOException("Unable to restore console input mode.", new Win32Exception(Marshal.GetLastWin32Error()));
    }

#pragma warning disable SYSLIB1054 // Also built for net472, without LibraryImport.
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr consoleHandle, out uint mode);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(IntPtr consoleHandle, uint mode);
#pragma warning restore SYSLIB1054
}
