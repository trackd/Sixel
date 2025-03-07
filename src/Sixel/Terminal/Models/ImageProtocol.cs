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
  // Alias for InlineImageProtocol
  iTerm2 = 4,
  KittyGraphicsProtocol = 8
};
