using System.Globalization;
using System.Text;
using Sixel.Terminal;
using Sixel.Terminal.Models;



namespace Sixel.Protocols;

/// <summary>
/// Provides methods to convert GIF images to Sixel format for terminal display, including resizing and optional audio support.
/// </summary>
public static class GifToSixel {
    internal const string LeadingBlankLine = "\r\n";

    public static SixelGif ConvertGif(Stream imageStream, int maxColors, int cellWidth, int LoopCount) {
        using var image = ImageBackend.Load(imageStream);
        ImageLayout layout = ImageLayout.Calculate(image.Width, image.Height, cellWidth, 0,
            Compatibility.GetCellSize(), sixel: true);
        image.Resize(layout.Pixels.Width, layout.Pixels.Height, maxColors);
        var result = new SixelGif {
            Width = layout.Cells.Width,
            Height = layout.Cells.Height,
            LoopCount = LoopCount,
            Delay = image.GifDelay(0),
            Sixel = new List<string>(image.FrameCount),
            Delays = new List<int>(image.FrameCount)
        };
        for (int index = 0; index < image.FrameCount; index++) {
            string frame = image.SixelFrame(index);
            result.Sixel.Add(frame);
            result.Delays.Add(image.GifDelay(index));
            result.HasTransparency |= HasTransparentPixels(frame);
        }
        return result;
    }

    // Opaque sixel data characters are '@'..'~' (at least one bit set). The encoder
    // writes '?' (no bits) only for transparent runs, and the header never contains it.
#if NET472
    internal static bool HasTransparentPixels(string sixelFrame) => sixelFrame.IndexOf('?') >= 0;
#else
    internal static bool HasTransparentPixels(string sixelFrame) => sixelFrame.Contains('?');
#endif

    public static void PlaySixelGif(SixelGif gif, CancellationToken CT = default) =>
        PlaySixelGif(gif, leadingBlankLine: true, CT);

    /// <param name="leadingBlankLine">
    /// False when this GIF directly follows another one, which already left a blank row below itself.
    /// </param>
    internal static void PlaySixelGif(SixelGif gif, bool leadingBlankLine, CancellationToken CT) {
        using var writer = new VTWriter();
        PlaySixelGif(gif, writer, leadingBlankLine, CT);
    }

    internal static void PlaySixelGif(SixelGif gif, VTWriter writer, CancellationToken CT) =>
        PlaySixelGif(gif, writer, leadingBlankLine: true, CT);

    internal static void PlaySixelGif(SixelGif gif, VTWriter writer, bool leadingBlankLine, CancellationToken CT) {
        if (gif.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(gif), "The GIF height must be positive.");
        int viewportHeight = ImagePlacement.GetViewportHeight();
        if (viewportHeight > 0 && gif.Height >= viewportHeight)
            throw new ArgumentException("The GIF must fit in the viewport with one row below it. Convert it with a smaller width.", nameof(gif));
        // Sixel transparency leaves the previous frame visible, so a transparent
        // GIF clears its cells before each frame (in the same flush, so no flicker).
        string frameStart = gif.HasTransparency ? EraseCells(gif.Width, gif.Height) : RestoreCursor;
        bool hasDelays = gif.Delays.Count == gif.Sixel.Count;
        try {
            writer.Write(Constants.HideCursor);
            writer.Write(ImagePlacement.SixelModes);
            // Blank row above. The one below comes from the single line the OutOfBand
            // format entry writes after playback, since the cursor is left below the image.
            if (leadingBlankLine)
                writer.Write(LeadingBlankLine);
            writer.Write(ImagePlacement.Begin(gif.Height));

            // LoopCount 0 loops until cancelled (Ctrl+C).
            for (int i = 0; gif.LoopCount == 0 || i < gif.LoopCount; i++) {
                for (int frame = 0; frame < gif.Sixel.Count; frame++) {
                    if (CT.IsCancellationRequested) {
                        return;
                    }
                    writer.Write(frameStart);
                    writer.Write(gif.Sixel[frame]);
                    writer.Write(RestoreCursor);
                    writer.Flush();
                    int delay = hasDelays ? gif.Delays[frame] : gif.Delay;
                    if (CT.WaitHandle.WaitOne(Math.Max(0, delay)))
                        return;
                }
            }
        }
        finally {
            writer.Write(ImagePlacement.End(gif.Height));
            writer.Write(Constants.ShowCursor);
            writer.Flush();
        }
    }

    // DECRC - Restore cursor position (the saved image origin).
    private const string RestoreCursor = "\x1b" + "8";

    /// <summary>Erases the image's cells row by row (ECH, CUD) and returns to the origin.</summary>
    internal static string EraseCells(int columns, int rows) {
        string erase = "\x1b[" + columns.ToString(CultureInfo.InvariantCulture) + "X";
        var result = new StringBuilder(RestoreCursor, 2 + rows * (erase.Length + 3));
        for (int row = 0; row < rows; row++) {
            if (row > 0) result.Append("\x1b[B");
            result.Append(erase);
        }
        return result.Append(RestoreCursor).ToString();
    }
}
