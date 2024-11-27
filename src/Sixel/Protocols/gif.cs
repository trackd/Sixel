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
    image.Mutate(ctx =>
    {
      if (cellWidth > 0)
      {
        // Some math to get the target size in pixels and reverse it to cell height that it will consume.
        var pixelWidth = cellWidth * Compatibility.GetCellSize().PixelWidth;
        var pixelHeight = (int)Math.Round((double)image.Height / image.Width * pixelWidth);
        // Resize the image to the target size
        ctx.Resize(new ResizeOptions()
        {
          Sampler = KnownResamplers.Bicubic,
          Size = new(pixelWidth, pixelHeight),
          PremultiplyAlpha = false,
        });
      }
      // Sixel supports 256 colors max
      ctx.Quantize(new OctreeQuantizer(new()
      {
        MaxColors = maxColors,
      }));
    });
    var metadata = image.Frames.RootFrame.Metadata.GetGifMetadata();
    int frameCount = image.Frames.Count;
    var cellHeight = Math.Ceiling((double)(image.Height / Compatibility.GetCellSize().PixelHeight));
    var gif = new SixelGif()
    {
      Sixel = new List<string>(),
      // Delay = metadata?.FrameDelay ?? 1000,
      Delay = metadata?.FrameDelay * 10 ?? 1000,
      LoopCount = LoopCount,
      Height = (int)cellHeight,
      Audio = AudioPath ?? null
    };
    for (int i = 0; i < frameCount; i++)
    {
      var targetFrame = image.Frames[i];
      gif.Sixel.Add(Sixel.FrameToSixelString(targetFrame, cellHeight, true));
    }
    return gif;
  }
  public static void PlaySixelGif(SixelGif gif, CancellationToken CT = default)
  {
    Console.CursorVisible = false;
    // Create a new VTWriter instance, Console.Write is slow..
    var writer = new VTWriter();
    // if its the last cursor position, we need to add an empty row.
    if (Console.CursorTop == Console.WindowHeight - 1)
    {
      // can't move the cursor ahead of the buffer.. so we add a empty line.
      writer.Write("\r\n");
    }
    else {
      // add 1 row padding before the gif.
      writer.Write($"{Constants.ESC}[1B");
    }
    // hack to remove the padding from the formatter
    // the formatter adds 2 lines of padding at the end.
    // because we dont really use the formatter.
    int height = gif.Height - 2;
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
