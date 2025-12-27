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

        ImageSize imageSize = cellWidth > 0 ? SizeHelper.GetResizedCharacterCellSize(image, cellWidth, 0) : SizeHelper.ConvertToCharacterCells(image);
        return ConvertGifToSixel(image, imageSize, maxColors, LoopCount);
    }

    private static SixelGif ConvertGifToSixel(Image<Rgba32> image, ImageSize imageSize, int maxColors, int LoopCount) {
        // Use Resizer to handle resizing and quantization
        Image<Rgba32> resizedImage = Resizer.ResizeToCharacterCells(image, imageSize, maxColors);
        GifFrameMetadata metadata = resizedImage.Frames.RootFrame.Metadata.GetGifMetadata();
        int frameCount = resizedImage.Frames.Count;

        // Derive final cell size from actual resized pixels to avoid drift vs. requested imageSize
        ImageSize finalSize = SizeHelper.GetCharacterCellSize(resizedImage);

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
        Console.CursorVisible = false;
        var writer = new VTWriter();

        try {
            // create space in the buffer for the image, so it doesn't scroll the terminal.
            for (int i = 0; i < gif.Height + 1; i++) {
                writer.Write(Environment.NewLine);
            }

            // Move cursor back up to starting position
            writer.Write($"{Constants.ESC}[{gif.Height}A");

            // DECSC - Save cursor position
            writer.Write($"{Constants.ESC}7");

            for (int i = 0; i < gif.LoopCount; i++) {
                foreach (string sixel in gif.Sixel) {
                    if (CT.IsCancellationRequested) {
                        return;
                    }
                    // DECRC - Restore cursor position
                    writer.Write($"{Constants.ESC}8");
                    writer.Write(sixel);
                    Thread.Sleep(gif.Delay);
                }
            }
        }
        finally {
            // DECRC - Restore to image start
            writer.Write($"{Constants.ESC}8");
            // Move down below image, subtract 1 line to compensate for powershell format engine.
            writer.Write($"{Constants.ESC}[{gif.Height - 1}B");
            writer?.Dispose();
            Console.CursorVisible = true;
        }
    }
}
