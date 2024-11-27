﻿using Sixel.Terminal;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace Sixel.Protocols;
public static class Sixel
{
  /// <summary>
  /// Converts an image to a Sixel string.
  /// </summary>
  /// <param name="image">The image to convert.</param>
  /// <param name="cellWidth">The width of the cell in terminal cells.</param>
  /// <param name="maxColors">The Max colors of the image.</param>
  /// <param name="frame">The frame to convert.</param>
  /// <param name="returnCursorToTopLeft">Whether to return the cursor to the top left after rendering the image.</param>
  /// <returns>The Sixel string.</returns>
  public static string ImageToSixel(Image<Rgba32> image, int maxColors, int cellWidth, int frame = 0, bool returnCursorToTopLeft = false)
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
      ctx.Quantize(new OctreeQuantizer(new() {
        MaxColors = maxColors,
      }));
    });
    var targetFrame = image.Frames[frame];
    var cellHeight = Math.Ceiling((double)(targetFrame.Height / Compatibility.GetCellSize().PixelHeight));
    return FrameToSixelString(targetFrame, cellHeight, returnCursorToTopLeft);
  }
  internal static string FrameToSixelString(ImageFrame<Rgba32> frame, double cellHeight, bool returnCursorToTopLeft)
  {
    var sixelBuilder = new StringBuilder();
    var palette = new Dictionary<Rgba32, int>();
    var colorCounter = 1;
    sixelBuilder.StartSixel(frame.Width, frame.Height);
    frame.ProcessPixelRows(accessor =>
    {
      for (var y = 0; y < accessor.Height; y++)
      {
        var pixelRow = accessor.GetRowSpan(y);
        // The value of 1 left-shifted by the remainder of the current row divided by 6 gives the correct sixel character offset from the empty sixel char for each row.
        // See the description of s...s for more detail on the sixel format https://vt100.net/docs/vt3xx-gp/chapter14.html#S14.2.1
        var c = (char)(Constants.SixelTransparent + (1 << (y % 6)));
        var lastColor = -1;
        var repeatCounter = 0;
        foreach (ref var pixel in pixelRow)
        {

          // The colors can be added to the palette and interleaved with the sixel data so long as the color is defined before it is used.
          if (!palette.TryGetValue(pixel, out var colorIndex))
          {
            colorIndex = colorCounter++;
            palette[pixel] = colorIndex;
            sixelBuilder.AddColorToPalette(pixel, colorIndex);
          }

          // Transparency is a special color index of 0 that exists in our sixel palette.
          var colorId = pixel.A == 0 ? 0 : colorIndex;

          // Sixel data will use a repeat entry if the color is the same as the last one.
          // https://vt100.net/docs/vt3xx-gp/chapter14.html#S14.3.1
          if (colorId == lastColor || repeatCounter == 0)
          {
            // If the color was repeated go to the next loop iteration to check the next pixel.
            lastColor = colorId;
            repeatCounter++;
            continue;
          }

          // Every time the color is not repeated the previous color is written to the string.
          sixelBuilder.AppendRepeatEntry(lastColor, repeatCounter, c);

          // Remember the current color and reset the repeat counter.
          lastColor = colorId;
          repeatCounter = 1;
        }

        // Write the last color and repeat counter to the string for the current row.
        sixelBuilder.AppendRepeatEntry(lastColor, repeatCounter, c);

        // Add a carriage return at the end of each row and a new line every 6 pixel rows.
        sixelBuilder.AppendCarriageReturn();
        if (y % 6 == 5)
        {
          sixelBuilder.AppendNextLine();
        }
      }
    });

    // Add the exit sixel sequence and return the cursor to the top left if requested.
    sixelBuilder.AppendExitSixel();
    if (returnCursorToTopLeft)
    {

      // Move the cursor back to the top left of the image.
      sixelBuilder.Append($"{Constants.ESC}[{cellHeight}A");
    }

    return sixelBuilder.ToString();
  }

  private static void AddColorToPalette(this StringBuilder sixelBuilder, Rgba32 pixel, int colorIndex)
  {
    var r = (int)Math.Round(pixel.R / 255.0 * 100);
    var g = (int)Math.Round(pixel.G / 255.0 * 100);
    var b = (int)Math.Round(pixel.B / 255.0 * 100);

    sixelBuilder.Append(Constants.SixelColorStart)
                .Append(colorIndex)
                .Append(Constants.SixelColorParam)
                .Append(r)
                .Append(Constants.Divider)
                .Append(g)
                .Append(Constants.Divider)
                .Append(b);
  }
  private static void AppendRepeatEntry(this StringBuilder sixelBuilder, int colorIndex, int repeatCounter, char sixel)
  {
    if (repeatCounter <= 1)
    {
      sixelBuilder.AppendEntry(colorIndex, sixel);
    }
    else
    {
      // add the repeat entry
      sixelBuilder.Append(Constants.SixelColorStart)
                  .Append(colorIndex)
                  .Append(Constants.SixelRepeat)
                  .Append(repeatCounter)
                  .Append(colorIndex != 0 ? sixel : Constants.SixelTransparent);
    }
  }
  private static void AppendEntry(this StringBuilder sixelBuilder, int colorIndex, char sixel)
  {
    sixelBuilder.Append(Constants.SixelColorStart)
                .Append(colorIndex)
                .Append(colorIndex != 0 ? sixel : Constants.SixelTransparent);
  }
  private static void AppendCarriageReturn(this StringBuilder sixelBuilder)
  {
    sixelBuilder.Append(Constants.SixelDECGCR);
  }

  private static void AppendNextLine(this StringBuilder sixelBuilder)
  {
    sixelBuilder.Append(Constants.SixelDECGNL);
  }

  private static void AppendExitSixel(this StringBuilder sixelBuilder)
  {
    sixelBuilder.Append(Constants.SixelDECGNL)
                .Append(Constants.ST);
  }

  private static void StartSixel(this StringBuilder sixelBuilder, int width, int height)
  {
    sixelBuilder.Append(Constants.SixelStart)
                .Append(Constants.SixelRaster)
                .Append(width)
                .Append(Constants.Divider)
                .Append(height)
                .Append(Constants.SixelTransparentColor);
  }
}
