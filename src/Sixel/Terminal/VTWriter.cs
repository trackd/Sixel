using System.Runtime.InteropServices;

namespace Sixel.Terminal;
internal class VTWriter : IDisposable
{
  private readonly TextWriter? _writer;
  private readonly FileStream? _windowsStream;
  internal readonly bool isWindows;
  private readonly bool _isRedirected;
  private bool _disposed;

  public VTWriter()
  {
    // Check if the OS is Windows
    isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    // Check if the output is redirected
    _isRedirected = Console.IsOutputRedirected;

    if (isWindows && !_isRedirected)
    {
      // Open the Windows stream to CONOUT$, for better performance..
      // Console.Write is too slow for gifs.
      _windowsStream = File.OpenWrite("CONOUT$");
      _writer = new StreamWriter(_windowsStream);
    }
  }
  public void Write(string text)
  {
    if (isWindows && !_isRedirected)
    {
      _writer?.Write(text);
    }
    else
    {
      Console.Write(text);
    }
  }
  public void WriteLine(string text)
  {
    if (isWindows && !_isRedirected)
    {
      _writer?.WriteLine(text);
    }
    else
    {
      Console.WriteLine(text);
    }
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!_disposed)
    {
      if (disposing)
      {
        _writer?.Dispose();
        _windowsStream?.Dispose();
      }
      _disposed = true;
    }
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }
}
