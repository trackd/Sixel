namespace Sixel.Terminal.Models;

/// <summary>
/// The image protocols that exists.
/// not all protocols are supported.
/// </summary>
[Flags]
public enum ImageProtocol
{
  Blocks = 0,
  Sixel = 1,
  InlineImageProtocol = 2,
  iTerm2 = 4, // Alias for InlineImageProtocol
  KittyGraphicsProtocol = 8
};
