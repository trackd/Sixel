#if NET472
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Sixel.Terminal;

internal static class NativeMethods
{
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint OPEN_EXISTING = 3;
    private const string CONOUT = "CONOUT$";
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile
    );

    internal static SafeFileHandle OpenConsole()
    {
        SafeFileHandle handle = CreateFileW(CONOUT, GENERIC_WRITE, 0, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw new IOException("Unable to open console output handle.");
        }
        return handle;
    }
}
#endif
