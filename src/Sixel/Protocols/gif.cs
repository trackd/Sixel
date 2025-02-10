using Sixel.Terminal;
using Sixel.Terminal.Models;
using System;
using System.Text;
using System.Threading;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace Sixel.Protocols;

public static class GifToSixel {

  public static SixelGif LoadGif(Stream imageStream, int maxColors, int cellWidth, int LoopCount, string? AudioFile = null)
  {
    using var image = Image.Load<Rgba32>(imageStream);
    if (AudioFile != null)
    {
      return ConvertGifToSixel(image, maxColors, cellWidth, LoopCount, AudioFile);
    }
    return ConvertGifToSixel(image, maxColors, cellWidth, LoopCount);
  }

  private static SixelGif ConvertGifToSixel(Image<Rgba32> image, int maxColors, int cellWidth, int LoopCount, string? AudioPath = null)
  {
    var cellSize = Compatibility.GetCellSize();
    var targetSize = SizeHelper.GetTerminalImageSize(image.Width, image.Height, cellWidth);

    image.Mutate(ctx =>
    {
      var targetPixelWidth = targetSize.Width * cellSize.PixelWidth;
      var targetPixelHeight = targetSize.Height * cellSize.PixelHeight;

      if (image.Width != targetPixelWidth || image.Height != targetPixelHeight)
      {
        ctx.Resize(new ResizeOptions
        {
          Size = new Size(targetPixelWidth, targetPixelHeight),
          Sampler = KnownResamplers.Bicubic,
          PremultiplyAlpha = false
        });
      }

      ctx.Quantize(new OctreeQuantizer(new QuantizerOptions
      {
        MaxColors = maxColors,
      }));
    });

    var metadata = image.Frames.RootFrame.Metadata.GetGifMetadata();
    int frameCount = image.Frames.Count;

    var gif = new SixelGif()
    {
        Sixel = new List<string>(),
        Delay = metadata?.FrameDelay * 10 ?? 1000,
        LoopCount = LoopCount,
        Height = targetSize.Height,
        Width = targetSize.Width,
        Audio = AudioPath
    };

    for (int i = 0; i < frameCount; i++)
    {
        var targetFrame = image.Frames[i];
        gif.Sixel.Add(Sixel.FrameToSixelString(targetFrame));
    }
    return gif;
  }
  public static void PlaySixelGif(SixelGif gif, CancellationToken CT = default)
  {
    Console.CursorVisible = false;
    // Create a new VTWriter instance, Console.Write is slow..
    var writer = new VTWriter();
    // add 1 row padding before the gif.
    writer.Write(Environment.NewLine);
    // hack to remove the padding from the formatter
    // the formatter adds 2 lines of padding at the end.
    int height = gif.Height - 1;
    GifAudio? audio = null;

    try
    {
      if (gif.Audio != null)
      {
        audio = new GifAudio(gif.Audio);
        audio.Play();
      }
      for (int i = 0; i < gif.LoopCount; i++)
      {
        if (audio != null && !audio.IsPlaying)
        {
          // restart the audio if it's not playing.
          audio.Play();
        }
        foreach (var sixel in gif.Sixel)
        {
          if (CT.IsCancellationRequested)
          {
            // bail if cancellation is requested.
            return;
          }
          writer.Write(sixel);
          Thread.Sleep(gif.Delay);
          writer.Write($"{Constants.ESC}[{gif.Height}A");
        }
      }
    }
    finally
    {
      // move the cursor below the gif.
      writer.Write($"{Constants.ESC}[{height}B");
      if (audio != null)
      {
        audio.Stop();
        audio.Dispose();
      }
      if (writer != null)
      {
        writer.Dispose();
      }
      Console.CursorVisible = true;
    }
  }
}
