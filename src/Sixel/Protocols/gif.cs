﻿using Sixel.Terminal;
using Sixel.Terminal.Models;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace Sixel.Protocols;
public static class GifToSixel {
  public static SixelGif LoadGif(Stream imageStream, int maxColors, int cellWidth, int LoopCount)
  {
    var image = Image.Load<Rgba32>(imageStream);
    var gif = ConvertGifToSixel(image, maxColors, cellWidth, LoopCount);
    return gif;
  }
  private static SixelGif ConvertGifToSixel(Image<Rgba32> image, int maxColors, int cellWidth, int LoopCount)
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
      Delay = metadata?.FrameDelay * 10 ?? 1000,
      LoopCount = LoopCount,
      Height = (int)cellHeight
    };
    for (int i = 0; i < frameCount; i++)
    {
      var targetFrame = image.Frames[i];
      gif.Sixel.Add(Sixel.FrameToSixelString(targetFrame, false));
    }
    return gif;
  }
  public static void PlaySixelGif(SixelGif gif, int LoopCount = 0)
  {
    (int positionX, int positionY) = Console.GetCursorPosition();
    if (LoopCount > 0)
    {
      gif.LoopCount = LoopCount;
    }
    if (gif.Delay == 0)
    {
      gif.Delay = 1000;
    }
    Console.CursorVisible = false;
    for (int i = 0; i < gif.LoopCount; i++)
    {
      foreach (var sixel in gif.Sixel)
      {
        Console.SetCursorPosition(positionX, positionY);
        Console.Write(sixel);
        Thread.Sleep(gif.Delay);
      }
    }
    Console.CursorVisible = true;
    int endPositionY = positionY + gif.Height;
    Console.SetCursorPosition(positionX, positionY);
  }
}