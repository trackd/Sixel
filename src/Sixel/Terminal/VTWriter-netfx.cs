#if NET472
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Sixel.Terminal;

/// <summary>
/// Contains P/Invoke methods for accessing native Windows console handles and related operations (NET472 only).
/// used for rendering gifs on Windows in net472.
/// </summary>
internal static class NativeMethods {
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile
    );

    internal static SafeFileHandle OpenConOut() {
        SafeFileHandle handle = CreateFileW("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (handle.IsInvalid) {
            int errorCode = Marshal.GetLastWin32Error();
            throw new IOException("Unable to open console output handle, error code: " + errorCode);
        }
        return handle;
    }
    // [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    // internal static extern uint GetConsoleMode(SafeFileHandle consoleHandle, out uint mode);
    // maybe this can be used to read input from the console..
    // internal static SafeFileHandle OpenConIn()
    // {
    //     SafeFileHandle fileHandle = CreateFileW("CONIN$", GENERIC_READ, FILE_SHARE_READ, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
    //     if (fileHandle.IsInvalid)
    //     {
    //         throw new System.ComponentModel.Win32Exception();
    //     }
    //     return fileHandle;
    // }
}
#endif
