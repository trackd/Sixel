using System.Runtime.InteropServices;

namespace Sixel.Terminal;
internal class VTWriter : IDisposable
{
  private readonly TextWriter? _writer;
  private readonly FileStream? _windowsStream;
  private readonly bool _customwriter;
  private bool _disposed;

  public VTWriter()
  {
    bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    bool isRedirected = Console.IsOutputRedirected;
    if (isWindows && !isRedirected)
    {
      // Open the Windows stream to CONOUT$, for better performance..
      // Console.Write is too slow for gifs.
      _windowsStream = File.OpenWrite("CONOUT$");
      _writer = new StreamWriter(_windowsStream);
      _customwriter = true;
    }
  }

  public void Write(string text)
  {
    if (_customwriter)
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
    if (_customwriter)
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
    if (!_disposed && _customwriter)
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
