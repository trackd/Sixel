using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Sixel.Protocols;

// Preserve support for .NET Framework, which lacks these throw helpers.
#pragma warning disable CA1510, CA1513

/// <summary>Owns a local transfer resource. Keep alive until the terminal acknowledges reading it.</summary>
public sealed class KittyTransfer : IDisposable {
    private readonly MemoryMappedFile? mapping;
    private bool disposed;
    public string Reference { get; }
    public char Medium { get; }
    public int Length { get; }
    public int UncompressedLength { get; }
    public bool Compressed { get; }

    private KittyTransfer(string reference, char medium, int length, int uncompressedLength, bool compressed, MemoryMappedFile? mapping = null) {
        Reference = reference;
        Medium = medium;
        Length = length;
        UncompressedLength = uncompressedLength;
        Compressed = compressed;
        this.mapping = mapping;
    }

    /// <summary>Creates a protocol-named temporary file, deleted by the terminal or Dispose.</summary>
    public static KittyTransfer CreateTemporaryFile(byte[] payload, bool compress = false) {
        ValidatePayload(payload);
        int uncompressedLength = payload.Length;
        if (compress) payload = KittyProtocol.Compress(payload);
        string path = Path.Combine(Path.GetTempPath(), "tty-graphics-protocol-" + Guid.NewGuid().ToString("N"));
        bool created = false;
        try {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) {
                created = true;
#if !NET472
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
#endif
                stream.Write(payload, 0, payload.Length);
            }
            return new KittyTransfer(path, 't', payload.Length, uncompressedLength, compress);
        }
        catch {
            if (created) File.Delete(path);
            throw;
        }
    }

    /// <summary>Creates Windows named memory or a POSIX shm object on Linux/macOS.</summary>
    public static KittyTransfer CreateSharedMemory(byte[] payload, bool compress = false) {
        ValidatePayload(payload);
        int uncompressedLength = payload.Length;
        if (compress) payload = KittyProtocol.Compress(payload);
        string name = "tty-graphics-protocol-" + Guid.NewGuid().ToString("N");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            MemoryMappedFile memory = MemoryMappedFile.CreateNew(name, payload.Length);
            try {
                using (MemoryMappedViewAccessor view = memory.CreateViewAccessor())
                    view.WriteArray(0, payload, 0, payload.Length);
                return new KittyTransfer(name, 's', payload.Length, uncompressedLength, compress, memory);
            }
            catch { memory.Dispose(); throw; }
        }
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            throw new PlatformNotSupportedException("Shared memory is implemented for Windows, Linux, and macOS.");
        name = "/" + name;
        bool mac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        // O_RDWR | O_CREAT | O_EXCL, mode 0600. Never open an existing object.
        int descriptor = mac ? Native.MacOpen(name, 0xA02, 0x180) : Native.LinuxOpen(name, 0xC2, 0x180);
        if (descriptor < 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        try {
            using var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
            using var stream = new FileStream(handle, FileAccess.ReadWrite);
            stream.SetLength(payload.Length);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
            return new KittyTransfer(name, 's', payload.Length, uncompressedLength, compress);
        }
        catch { Unlink(name); throw; }
    }

    /// <summary>Encodes a reference to this resource. Bytes are already in their final wire format.</summary>
    public string Encode(IEnumerable<string> control, bool tmux = false) {
        if (disposed) throw new ObjectDisposedException(nameof(KittyTransfer));
        Dictionary<char, string> values = KittyProtocol.Parse(control);
        if (values.ContainsKey('t') || values.ContainsKey('S') || values.ContainsKey('O') || values.ContainsKey('o'))
            throw new ArgumentException("The resource owns t/S/O/o. Request compression when creating the resource.", nameof(control));
        var entries = values.Select(pair => pair.Key + "=" + pair.Value).ToList();
        entries.Add("t=" + Medium);
        entries.Add(KittyImage.Number('S', Compressed && values.TryGetValue('f', out string? format) && format == "100" ? UncompressedLength : Length));
        if (Compressed) entries.Add("o=z");
        return KittyProtocol.Encode(entries, Encoding.UTF8.GetBytes(Reference), tmux);
    }

    public void Dispose() {
        if (disposed) return;
        if (Medium == 't') File.Delete(Reference);
        else if (mapping is not null) mapping.Dispose();
        else Unlink(Reference);
        disposed = true;
    }

    private static void ValidatePayload(byte[] payload) {
        if (payload is null) throw new ArgumentNullException(nameof(payload));
        if (payload.Length == 0) throw new ArgumentException("A nonempty image payload is required.", nameof(payload));
    }

    private static void Unlink(string name) {
        int result = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Native.MacUnlink(name) : Native.LinuxUnlink(name);
        int error = Marshal.GetLastWin32Error();
        if (result != 0 && error != 2) throw new Win32Exception(error); // ENOENT: terminal already consumed it.
    }

    private static class Native {
        // LibraryImport is unavailable in the net472 target. Names are generated ASCII.
#pragma warning disable SYSLIB1054, CA2101
        [DllImport("librt.so.1", EntryPoint = "shm_open", SetLastError = true)]
        internal static extern int LinuxOpen(string name, int flags, uint mode);
        [DllImport("librt.so.1", EntryPoint = "shm_unlink", SetLastError = true)]
        internal static extern int LinuxUnlink(string name);
        [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "shm_open", SetLastError = true)]
        internal static extern int MacOpen(string name, int flags, uint mode);
        [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "shm_unlink", SetLastError = true)]
        internal static extern int MacUnlink(string name);
#pragma warning restore SYSLIB1054, CA2101
    }
}
