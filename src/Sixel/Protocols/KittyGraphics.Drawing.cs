#if NET472
using Sixel.Terminal;
using Sixel.Terminal.Models;

namespace Sixel.Protocols;

public static class KittyGraphics {
    internal static string Encode(ImageBackend image, ImageSize cells) {
        bool raw = Compatibility.GetTerminalInfo().Terminal == Terminals.Contour;
        var controls = new List<string> { "a=T", "q=2", raw ? "f=32" : "f=100", "C=1",
            KittyImage.Number('c', cells.Width), KittyImage.Number('r', cells.Height) };
        if (raw) controls.AddRange([KittyImage.Number('s', image.Width), KittyImage.Number('v', image.Height)]);
        return KittyProtocol.Encode(controls, image.FrameBytes(0, raw ? 32 : 100)) + KittyImage.CursorRows(cells.Height);
    }

}
#endif
