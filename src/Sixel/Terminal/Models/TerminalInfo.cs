namespace Sixel.Terminal.Models;

public class TerminalInfo
{
  public Terminals Terminal { get; set; }
  public ImageProtocol[] Protocol { get; set; } = [ImageProtocol.Blocks];
  public override string ToString()
  {
    return $"{Terminal} ({string.Join(", ", Protocol)})";
  }
}
