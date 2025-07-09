using System;
using System.IO;
using System.Text;
using System.Threading;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;



namespace Sixel.Protocols;

/// <summary>
/// Provides methods to convert GIF images to Sixel format for terminal display, including resizing and optional audio support.
/// </summary>
public static class GifToSixel
{

  public static SixelGif ConvertGif(Stream imageStream, int maxColors, int cellWidth, int LoopCount, string? AudioFile = null)
  {
    using var image = Image.Load<Rgba32>(imageStream);

    ImageSize imageSize;
    if (cellWidth > 0)
    {
      // User specified width - constrain to that width, let height scale naturally
      imageSize = SizeHelper.GetResizedCharacterCellSize(image, cellWidth, 0);
    }
    else
    {
      // No width specified - use natural image size converted to cells (no upscaling)
      imageSize = SizeHelper.ConvertToCharacterCells(image);
    }

    return ConvertGifToSixel(image, imageSize, maxColors, LoopCount);
  }

  // old resizing logic, evaluating new..
  private static SixelGif ConvertGifToSixelv1(Image<Rgba32> image, int cellWidth, int maxColors, int LoopCount, string? AudioPath = null)
  {
    var cellSize = Compatibility.GetCellSize();
    var targetSize = SizeOld.GetTerminalImageSize(image.Width, image.Height, cellWidth);

    image.Mutate(ctx => {
      var targetPixelWidth = targetSize.Width * cellSize.PixelWidth;
      var targetPixelHeight = targetSize.Height * cellSize.PixelHeight;

      if (image.Width != targetPixelWidth || image.Height != targetPixelHeight)
      {
        ctx.Resize(new ResizeOptions {
          Size = new Size(targetPixelWidth, targetPixelHeight),
          Sampler = KnownResamplers.Bicubic,
          PremultiplyAlpha = false
        });
      }

      ctx.Quantize(new OctreeQuantizer(new QuantizerOptions {
        MaxColors = maxColors,
      }));
    });
    var metadata = image.Frames.RootFrame.Metadata.GetGifMetadata();
    int frameCount = image.Frames.Count;

    var gif = new SixelGif() {
      Sixel = [],
      Delay = metadata?.FrameDelay * 10 ?? 1000,
      LoopCount = LoopCount,
      Height = targetSize.Height,
      Width = targetSize.Width,
      // Audio = AudioPath
    };

    for (int i = 0; i < frameCount; i++)
    {
      var targetFrame = image.Frames[i];
      gif.Sixel.Add(Sixel.FrameToSixelString(targetFrame));
    }
    return gif;
  }

  // current resizing logic, using Resizer
  private static SixelGif ConvertGifToSixel(Image<Rgba32> image, ImageSize imageSize, int maxColors, int LoopCount, string? AudioPath = null)
  {
    // Use Resizer to handle resizing and quantization
    var resizedImage = Resizer.ResizeToCharacterCells(image, imageSize, maxColors);
    var metadata = resizedImage.Frames.RootFrame.Metadata.GetGifMetadata();
    int frameCount = resizedImage.Frames.Count;

    var gif = new SixelGif() {
      Sixel = [],
      Delay = metadata?.FrameDelay * 10 ?? 1000,
      LoopCount = LoopCount,
      Height = imageSize.Height,
      Width = imageSize.Width,
      // Audio = AudioPath
    };

    for (int i = 0; i < frameCount; i++)
    {
      var targetFrame = resizedImage.Frames[i];
      gif.Sixel.Add(Sixel.FrameToSixelString(targetFrame));
    }
    return gif;
  }
  public static void PlaySixelGif(SixelGif gif, CancellationToken CT = default)
  {
    Console.CursorVisible = false;
    var writer = new VTWriter();

    try
    {
      // create space in the buffer for the image, so it doesn't scroll the terminal.
      for (int i = 0; i < gif.Height + 1; i++)
      {
        writer.Write(Environment.NewLine);
      }

      // Move cursor back up to starting position
      writer.Write($"{Constants.ESC}[{gif.Height}A");

      // DECSC - Save cursor position
      writer.Write($"{Constants.ESC}7");

      for (int i = 0; i < gif.LoopCount; i++)
      {
        foreach (string sixel in gif.Sixel)
        {
          if (CT.IsCancellationRequested)
          {
            return;
          }
          // DECRC - Restore cursor position
          writer.Write($"{Constants.ESC}8");
          writer.Write(sixel);
          Thread.Sleep(gif.Delay);
        }
      }
    }
    finally
    {
      // DECRC - Restore to image start
      writer.Write($"{Constants.ESC}8");
      // Move down below image, subtract 1 line to compensate for powershell format engine.
      writer.Write($"{Constants.ESC}[{gif.Height - 1}B");
      writer?.Dispose();
      Console.CursorVisible = true;
    }
  }
}
