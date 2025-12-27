namespace Sixel.Terminal.Models;

/// <summary>
/// The image protocols that exists.
/// not all protocols are supported.
/// </summary>
[Flags]
public enum ImageProtocol {
    Auto = 0,
    Blocks = 1,
    InlineImageProtocol = 2,
    Sixel = 4,
    KittyGraphicsProtocol = 8
}
