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
    // if (AudioFile != null)
    // {
    //   return ConvertGifToSixel(image, maxColors, cellWidth, LoopCount, AudioFile);
    // }
    // Fix: Don't pass maxCellHeight: 0, let it calculate proper height based on width constraint
    var imageSize = SizeHelper.GetResizedCharacterCellSize(image, cellWidth, maxCellHeight: int.MaxValue);

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
    // Create a new VTWriter instance, Console.Write is slow..
    var writer = new VTWriter();
    // add 1 row padding before the gif.
    writer.Write(Environment.NewLine);
    int height;
    // GifAudio? audio = null;
    // this is pretty annoying and hacky, need to find a better solution..
    var termInfo = Compatibility.GetTerminalInfo();
    if (termInfo.Terminal == Terminals.VSCode || termInfo.Terminal == Terminals.WezTerm)
    {
      // VSCode and WezTerm need 1 less row for the gif to display correctly.
      // temporary workaround..
      // gif.height is used for replacing each frame.
      // height is used for moving the cursor back to the bottom of the gif after playback.
      // 5.1 formatter adds more lines than 7.4+.
      gif.Height--;
#if NET472
      height = gif.Height - 2;
#else
      height = gif.Height - 1;
#endif
    }
    else
    {
#if NET472
      height = gif.Height - 3;
#else
      height = gif.Height - 2;
#endif
    }
    try
    {
      // if (gif.Audio != null)
      // {
      //   audio = new GifAudio(gif.Audio);
      //   audio.Play();
      // }
      for (int i = 0; i < gif.LoopCount; i++)
      {
        // if (audio != null && !audio.IsPlaying)
        // {
        //   // restart the audio if it's not playing.
        //   audio.Play();
        // }
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
      // if (audio != null)
      // {
      //   audio.Stop();
      //   audio.Dispose();
      // }
      if (writer != null)
      {
        writer.Dispose();
      }
      Console.CursorVisible = true;
    }
  }
}
