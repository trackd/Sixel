using System.Diagnostics;
using System.Runtime.InteropServices;
namespace Sixel.Terminal.Native;

internal partial class ProcessHelper {
  /// <summary>
  /// helper class to get the parent process id.
  /// </summary>
  /// <param name="dwFlags"></param>
  /// <param name="th32ProcessID"></param>
  /// <returns></returns>

  [DllImport("kernel32.dll", SetLastError = true)]
  internal static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

  [DllImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

  [DllImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

  [DllImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  internal static extern bool CloseHandle(IntPtr hObject);

  internal const uint TH32CS_SNAPPROCESS = 0x00000002;

  [StructLayout(LayoutKind.Sequential)]
  internal struct PROCESSENTRY32
  {
    public uint dwSize;
    public uint cntUsage;
    public uint th32ProcessID;
    public IntPtr th32DefaultHeapID;
    public uint th32ModuleID;
    public uint cntThreads;
    public uint th32ParentProcessID;
    public int pcPriClassBase;
    public uint dwFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string szExeFile;
  }
  internal static int GetParentProcessId(Process process)
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      return GetParentProcessIdWindows(process);
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      return GetParentProcessIdUnix(process);
    }
    throw new PlatformNotSupportedException("Unsupported OS platform");
  }

  internal static int GetParentProcessIdWindows(Process process)
  {
    var parentId = 0;
    var handle = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (handle == IntPtr.Zero)
    {
      return parentId;
    }

    var processEntry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32)) };
    if (Process32First(handle, ref processEntry))
    {
      do
      {
        if (processEntry.th32ProcessID == process.Id)
        {
          parentId = (int)processEntry.th32ParentProcessID;
          break;
        }
      } while (Process32Next(handle, ref processEntry));
    }
    CloseHandle(handle);
    return parentId;
  }

  internal static int GetParentProcessIdUnix(Process process)
  {
    var parentId = 0;
    try
    {
      var path = $"/proc/{process.Id}/stat";
      var stat = System.IO.File.ReadAllText(path);
      var parts = stat.Split(' ');
      parentId = int.Parse(parts[3]);
    }
    catch
    {
      // Handle exceptions if needed
    }
    return parentId;
  }
}
