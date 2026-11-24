using System.Globalization;
using System.Text;

namespace Sixel.Terminal;

/// <summary>Reserves scrolling space before saving an image origin (issue #29).</summary>
internal static class ImagePlacement {
    // DECSDM reset allows inline scrolling; mode 8452 reset advances down.
    internal const string SixelModes = "\x1b[?80l\x1b[?8452l";
    // IND (ESC D) scrolls like a newline but keeps the cursor column, so an
    // image drawn after CUP or other text starts where the cursor was.
    internal static string Begin(int rows) {
        var result = new StringBuilder();
        for (int row = 0; row < rows; row++)
            result.Append("\x1b" + "D");
        return result.
            Append("\x1b[").
            Append(rows.ToString(CultureInfo.InvariantCulture)).
            Append("A\x1b" + "7").ToString();
    }

    internal static string End(int rows) =>
        "\x1b" + "8\x1b[" + rows.ToString(CultureInfo.InvariantCulture) + "B\r";

    internal static string Wrap(string payload, int rows) =>
        // An origin that has scrolled off-screen cannot be restored. Large
        // static images use the terminal's native scrolling instead.
        FitsViewport(rows) ? Begin(rows) + payload + End(rows) : payload + "\r\n";

    /// <summary>Whether <paramref name="rows"/> can be reserved below the cursor (unknown viewports fit).</summary>
    internal static bool FitsViewport(int rows) {
        int viewportHeight = GetViewportHeight();
        return viewportHeight <= 0 || rows < viewportHeight;
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
