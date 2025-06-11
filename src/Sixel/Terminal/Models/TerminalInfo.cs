namespace Sixel.Terminal.Models;

public class TerminalInfo
{
  public Terminals Terminal { get; set; }
  public ImageProtocol[] Protocol { get; set; } = new ImageProtocol[] { ImageProtocol.Blocks };
}
