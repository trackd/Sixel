using Sixel.Terminal;
using Sixel.Terminal.Models;



namespace Sixel.Protocols;

/// <summary>
/// Provides methods to convert GIF images to Sixel format for terminal display, including resizing and optional audio support.
/// </summary>
public static class GifToSixel {
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
            Sixel = new List<string>(image.FrameCount)
        };
        for (int index = 0; index < image.FrameCount; index++)
            result.Sixel.Add(image.SixelFrame(index));
        return result;
    }
    public static void PlaySixelGif(SixelGif gif, CancellationToken CT = default) {
        using var writer = new VTWriter();
        PlaySixelGif(gif, writer, CT);
    }

    internal static void PlaySixelGif(SixelGif gif, VTWriter writer, CancellationToken CT) {
        if (gif.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(gif), "The GIF height must be positive.");
        int viewportHeight = ImagePlacement.GetViewportHeight();
        if (viewportHeight > 0 && gif.Height >= viewportHeight)
            throw new ArgumentException("The GIF must fit in the viewport with one row below it. Convert it with a smaller width.", nameof(gif));
        try {
            writer.Write(Constants.HideCursor);
            writer.Write(ImagePlacement.SixelModes);
            writer.Write(ImagePlacement.Begin(gif.Height));

            for (int i = 0; i < gif.LoopCount; i++) {
                foreach (string sixel in gif.Sixel) {
                    if (CT.IsCancellationRequested) {
                        return;
                    }
                    // DECRC - Restore cursor position
                    writer.Write($"{Constants.ESC}8");
                    writer.Write(sixel);
                    writer.Write($"{Constants.ESC}8");
                    writer.Flush();
                    if (CT.WaitHandle.WaitOne(Math.Max(0, gif.Delay)))
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
}
