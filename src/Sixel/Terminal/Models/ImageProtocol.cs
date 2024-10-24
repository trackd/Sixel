namespace Sixel.Terminal.Models;

/// <summary>
/// The image protocols that exists.
/// not all protocols are supported.
/// </summary>
[Flags]
public enum ImageProtocol
{
  None = 0,
  Sixel = 1,
  InlineImageProtocol = 2,
  KittyGraphicsProtocol = 4
};
