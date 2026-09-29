using System.Globalization;
using System.Text;

namespace Sixel.Terminal;

/// <summary>Reserves scrolling space before saving an image origin (issue #29).</summary>
internal static class ImagePlacement {
    // DECSDM reset allows inline scrolling; mode 8452 reset advances down.
    internal const string SixelModes = "\x1b[?80l\x1b[?8452l";
    internal static string Begin(int rows) {
        var result = new StringBuilder();
        for (int row = 0; row < rows; row++)
            result.Append("\r\n");
        return result.
            Append("\x1b[").
            Append(rows.ToString(CultureInfo.InvariantCulture)).
            Append("A\r\x1b" + "7").ToString();
    }

    internal static string End(int rows) =>
        "\x1b" + "8\x1b[" + rows.ToString(CultureInfo.InvariantCulture) + "B\r";

    internal static string Wrap(string payload, int rows) {
        int viewportHeight = GetViewportHeight();
        // An origin that has scrolled off-screen cannot be restored. Large
        // static images use the terminal's native scrolling instead.
        return viewportHeight > 0 && rows >= viewportHeight
            ? payload + "\r\n"
            : Begin(rows) + payload + End(rows);
    }

    internal static int GetViewportHeight() {
        if (Console.IsOutputRedirected)
            return 0;
        try {
            return Console.WindowHeight;
        }
        catch (IOException) {
            return 0;
        }
        catch (PlatformNotSupportedException) {
            return 0;
        }
    }
}
