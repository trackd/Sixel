using Sixel.Terminal.Models;

namespace Sixel.Terminal;
/// <summary>
/// Helper methods for selecting the best supported terminal image protocol.
/// not in use at the moment
/// </summary>
public static class ImageProtocolHelper {
    public static ImageProtocol GetBestSupported(ImageProtocol supported) {
        if ((supported & ImageProtocol.KittyGraphicsProtocol) != 0)
            return ImageProtocol.KittyGraphicsProtocol;
        if ((supported & ImageProtocol.Sixel) != 0)
            return ImageProtocol.Sixel;
        if ((supported & ImageProtocol.InlineImageProtocol) != 0)
            return ImageProtocol.InlineImageProtocol;
        if ((supported & ImageProtocol.Blocks) != 0)
            return ImageProtocol.Blocks;
        return ImageProtocol.Blocks; // fallback
    }
}
