using System.Runtime.InteropServices;

namespace Sixel.Terminal;
/// <summary>
/// Provides optimized writing to the terminal
/// handling platform-specific output streams for improved performance
/// used for rendering gifs on Windows.
/// </summary>
internal sealed class VTWriter : IDisposable {
    private readonly TextWriter? _writer;
    private readonly FileStream? _windowsStream;
    private readonly bool _customwriter;
    private bool _disposed;

    public VTWriter() {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        bool isRedirected = Console.IsOutputRedirected || Console.IsInputRedirected;

        // in my tests Console.Write has been much faster on Mac/Linux and does not need special handling here.
        if (isWindows && !isRedirected) {
            // Open the Windows stream to CONOUT$, for better performance..
            // Console.Write is too slow for gifs on Windows.
#if NET472
            // net472 can't access CONOUT$ directly, use pinvoke to solve that.
            _windowsStream = new FileStream(NativeMethods.OpenConOut(), FileAccess.Write);
            _writer = new StreamWriter(_windowsStream);
            _customwriter = true;
#else
            _windowsStream = File.OpenWrite("CONOUT$");
            _writer = new StreamWriter(_windowsStream);
            _customwriter = true;
#endif
        }
    }

    public void Write(string text) {
        if (_customwriter) {
            _writer?.Write(text);
        }
        else {
            Console.Write(text);
        }
    }

    public void WriteLine(string text) {
        if (_customwriter) {
            _writer?.WriteLine(text);
        }
        else {
            Console.WriteLine(text);
        }
    }

    private void Dispose(bool disposing) {
        if (!_disposed && _customwriter) {
            if (disposing) {
                _writer?.Dispose();
                _windowsStream?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
