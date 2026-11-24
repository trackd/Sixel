#if NET472
using Sixel.Terminal;
using Sixel.Terminal.Models;

namespace Sixel.Protocols;

public static class KittyGraphics {
    internal static string Encode(ImageBackend image, ImageSize cells) {
        var controls = new List<string> { "a=T", "q=2", "f=100", "C=1",
            KittyImage.Number('c', cells.Width), KittyImage.Number('r', cells.Height) };
        return KittyProtocol.Encode(controls, image.FrameBytes(0, 100)) + KittyImage.CursorRows(cells.Height);
    }

}
#endif
