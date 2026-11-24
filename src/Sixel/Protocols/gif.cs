using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;


namespace Sixel.Protocols;

/// <summary>
/// Provides methods to convert GIF images to Sixel format for terminal display, including resizing and optional audio support.
/// </summary>
public static class GifToSixel {
    public static SixelGif ConvertGif(Stream imageStream, int maxColors, int cellWidth, int LoopCount) {
        using var image = Image.Load<Rgba32>(imageStream);

        ImageLayout layout = ImageLayout.Calculate(image.Width, image.Height, cellWidth, 0,
            Compatibility.GetCellSize(), sixel: true);
        return ConvertGifToSixel(image, layout, maxColors, LoopCount);
    }

    private static SixelGif ConvertGifToSixel(Image<Rgba32> image, ImageLayout layout, int maxColors, int LoopCount) {
        // Use Resizer to handle resizing and quantization
        Image<Rgba32> resizedImage = Resizer.ResizeToPixels(image, layout.Pixels, maxColors);
        GifFrameMetadata metadata = resizedImage.Frames.RootFrame.Metadata.GetGifMetadata();
        int frameCount = resizedImage.Frames.Count;

        // Keep the occupancy calculated from the same pixel dimensions as every frame.
        ImageSize finalSize = layout.Cells;

        var gif = new SixelGif() {
            Sixel = new List<string>(frameCount), // Pre-allocate capacity for better performance
            Delay = (metadata?.FrameDelay * 10) ?? 1000,
            LoopCount = LoopCount,
            Height = finalSize.Height,
            Width = finalSize.Width,
        };

        // Pre-allocate and process frames efficiently
        for (int i = 0; i < frameCount; i++) {
            ImageFrame<Rgba32> targetFrame = resizedImage.Frames[i];
            gif.Sixel.Add(Sixel.FrameToSixelString(targetFrame));
        }
        return gif;
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
