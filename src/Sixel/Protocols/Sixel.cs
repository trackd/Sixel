using Sixel.Terminal;
using Sixel.Terminal.Models;
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
  /// <param name="MaxColors">The Max colors of the image.</param>
  /// <param name="frame">The frame to convert.</param>
  /// <param name="returnCursorToTopLeft">Whether to return the cursor to the top left after rendering the image.</param>
  /// <returns>The Sixel string.</returns>
  public static string ImageToSixel(Image<Rgba32> image, int MaxColors, int cellWidth, int frame = 0, bool returnCursorToTopLeft = false)
  {
    var imageClone = image.Clone();
    imageClone.Mutate(ctx =>
    {
      if (cellWidth > 0)
      {
        // We're going to resize the image when it's rendered, so use a copy to leave the original untouched.
        // Some math to get the target size in pixels and reverse it to cell height that it will consume.
        var pixelWidth = cellWidth * Compatibility.GetCellSize().PixelWidth;
        var pixelHeight = (int)Math.Round((double)imageClone.Height / imageClone.Width * pixelWidth);
        // Resize the image to the target size
        ctx.Resize(new ResizeOptions()
        {
          Sampler = KnownResamplers.Bicubic,
          Size = new SixLabors.ImageSharp.Size(pixelWidth, pixelHeight),
          PremultiplyAlpha = false,
        });
      }
      // Sixel supports 256 colors max
      ctx.Quantize(new OctreeQuantizer(new()
      {
        MaxColors = MaxColors,
      }));
    });
    var targetFrame = imageClone.Frames[frame];
    return FrameToSixelString(targetFrame, returnCursorToTopLeft);
  }
  private static string FrameToSixelString(ImageFrame<Rgba32> frame, bool returnCursorToTopLeft)
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
        // The way sixel works, this bitshift starting from the SIXELEMPTY constant
        // will give us the correct character to use for the current row.
        // Every six rows we swap back to the "empty character + 1" after adding a newline
        // character to the string.
        var c = (char)(Constants.SIXELEMPTY + (1 << (y % 6)));
        var lastColor = -1;
        var repeatCounter = 0;
        foreach (ref var pixel in pixelRow)
        {
          if (!palette.TryGetValue(pixel, out var colorIndex))
          {
            colorIndex = colorCounter++;
            palette[pixel] = colorIndex;
            sixelBuilder.AddColorToPalette(pixel, colorIndex);
          }
          var colorId = pixel.A == 0 ? 0 : colorIndex;
          if (colorId == lastColor || repeatCounter == 0)
          {
            lastColor = colorId;
            repeatCounter++;
            continue;
          }
          if (repeatCounter > 1)
          {
            sixelBuilder.AppendRepeatEntry(lastColor, repeatCounter, c);
          }
          else
          {
            sixelBuilder.AppendSixelEntry(lastColor, c);
          }
          lastColor = colorId;
          repeatCounter = 1;
        }
        if (repeatCounter > 1)
        {
          sixelBuilder.AppendRepeatEntry(lastColor, repeatCounter, c);
        }
        else
        {
          sixelBuilder.AppendSixelEntry(lastColor, c);
        }
        sixelBuilder.AppendCarriageReturn();
        if (y % 6 == 5)
        {
          sixelBuilder.AppendNextLine();
        }
      }
    });
    sixelBuilder.AppendExitSixel();
    if (returnCursorToTopLeft)
    {
      var cellHeight = Math.Ceiling((double)(frame.Height / Compatibility.GetCellSize().PixelHeight));
      sixelBuilder.Append($"{Constants.ESC}[{cellHeight}A");
    }
    return sixelBuilder.ToString();
  }

  private static void AddColorToPalette(this StringBuilder sixelBuilder, Rgba32 pixel, int colorIndex)
  {
    var r = (int)Math.Round(pixel.R / 255.0 * 100);
    var g = (int)Math.Round(pixel.G / 255.0 * 100);
    var b = (int)Math.Round(pixel.B / 255.0 * 100);

    sixelBuilder.Append(Constants.SIXELCOLORSTART)
                .Append(colorIndex)
                .Append(";2;")
                .Append(r)
                .Append(';')
                .Append(g)
                .Append(';')
                .Append(b);
  }

  private static void AppendRepeatEntry(this StringBuilder sixelBuilder, int color, int repeatCounter, char e)
  {
    sixelBuilder.Append(Constants.SIXELCOLORSTART)
                .Append(color)
                .Append(Constants.SIXELREPEAT)
                .Append(repeatCounter)
                .Append(color != 0 ? e : Constants.SIXELEMPTY);
  }

  private static void AppendSixelEntry(this StringBuilder sixelBuilder, int color, char e)
  {
    sixelBuilder.Append(Constants.SIXELCOLORSTART)
                .Append(color)
                .Append(color != 0 ? e : Constants.SIXELEMPTY);
  }

  private static void AppendCarriageReturn(this StringBuilder sixelBuilder)
  {
    sixelBuilder.Append(Constants.SIXELDECGCR);
  }

  private static void AppendNextLine(this StringBuilder sixelBuilder)
  {
    sixelBuilder.Append(Constants.SIXELDECGNL);
  }

  private static void AppendExitSixel(this StringBuilder sixelBuilder)
  {
    sixelBuilder.Append(Constants.SIXELEND);
  }

  private static void StartSixel(this StringBuilder sixelBuilder, int width, int height)
  {
    sixelBuilder.Append(Constants.SIXELSTART)
                .Append(Constants.SIXELRASTERATTRIBUTES)
                .Append(width)
                .Append(';')
                .Append(height)
                .Append(Constants.SIXELTRANSPARENTCOLOR);
  }
}
