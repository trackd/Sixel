using Sixel.Terminal;
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
  /// <returns>The Sixel string.</returns>
  public static string ImageToSixel(Image<Rgba32> image, int maxColors, int cellWidth, int frame = 0)
  {
    // get image size in characters
    var cellSize = Compatibility.GetCellSize();
    // get the image size in console characters
    var imageSize = SizeHelper.GetTerminalImageSize(image.Width, image.Height, cellWidth);

    image.Mutate(ctx =>
    {
      // Some math to get the target size in pixels and reverse it to cell height that it will consume.
      var targetPixelWidth = imageSize.Width * cellSize.PixelWidth;
      var targetPixelHeight = imageSize.Height * cellSize.PixelHeight;

        if (image.Width != targetPixelWidth || image.Height != targetPixelHeight)
        {
        // Resize the image to the target size
        ctx.Resize(new ResizeOptions()
        {
          // https://en.wikipedia.org/wiki/Bicubic_interpolation
          // quality goes Bicubic > Bilinear > NearestNeighbor
          Sampler = KnownResamplers.Bicubic,
          Size = new(targetPixelWidth, targetPixelHeight),
          PremultiplyAlpha = false,
        });
      }
      // Sixel supports 256 colors max
      ctx.Quantize(new OctreeQuantizer(new() {
        MaxColors = maxColors,
      }));
    });
    var targetFrame = image.Frames[frame];
    return FrameToSixelString(targetFrame);
  }
  internal static string FrameToSixelString(ImageFrame<Rgba32> frame)
  {
    var sixelBuilder = new StringBuilder();
    var sixel = new StringBuilder();
    var palette = new Dictionary<Rgba32, int>();
    var colorCounter = 1;
    sixel.StartSixel(frame.Width, frame.Height);
    frame.ProcessPixelRows(accessor =>
    {
      for (var y = 0; y < accessor.Height; y++)
      {
        var pixelRow = accessor.GetRowSpan(y);
        // The value of 1 left-shifted by the remainder of the current row divided by 6 gives the correct sixel character offset from the empty sixel char for each row.
        // See the description of s...s for more detail on the sixel format https://vt100.net/docs/vt3xx-gp/chapter14.html#S14.2.1

        // modulus trick from https://github.com/sxyazi/yazi/blob/main/yazi-adapter/src/sixel.rs (MIT)
        var c = (char)(Constants.SixelTransparent + (1 << (y % 6)));
        var lastColor = -1;
        var repeatCounter = 0;
        foreach (ref var pixel in pixelRow)
        {
          if (!palette.TryGetValue(pixel, out var colorIndex))
          {
            // The colors can be added to the palette and interleaved with the sixel data so long as the color is defined before it is used.
            // for compatibility testing im not doing this at the moment.
            colorIndex = colorCounter++;
            palette[pixel] = colorIndex;
            sixel.AddColorToPalette(pixel, colorIndex);
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
          sixelBuilder.AppendSixel(lastColor, repeatCounter, c);

          // Remember the current color and reset the repeat counter.
          lastColor = colorId;
          repeatCounter = 1;
        }

        // Write the last color and repeat counter to the string for the current row.
        sixelBuilder.AppendSixel(lastColor, repeatCounter, c);

        // Add a carriage return at the end of each row and a new line every 6 pixel rows.
        sixelBuilder.AppendCarriageReturn();
        if (y % 6 == 5)
        {
          sixelBuilder.AppendNextLine();
        }
      }
    });
    sixelBuilder.AppendNextLine();
    sixelBuilder.AppendExitSixel();

    return sixel.Append(sixelBuilder).ToString();
  }

  private static void AddColorToPalette(this StringBuilder sixelBuilder, Rgba32 pixel, int colorIndex)
  {
    // rgb 0-255 needs to be translated to 0-100 for sixel.
    var (r, g, b) = (
        (int)pixel.R * 100 / 255,
        (int)pixel.G * 100 / 255,
        (int)pixel.B * 100 / 255
    );

    sixelBuilder.Append(Constants.SixelColorStart)
                .Append(colorIndex)
                .Append(Constants.SixelColorParam)
                .Append(r)
                .Append(Constants.Divider)
                .Append(g)
                .Append(Constants.Divider)
                .Append(b);
  }
  private static void AppendSixel(this StringBuilder sixelBuilder, int colorIndex, int repeatCounter, char sixel)
  {
    if (colorIndex == 0)
    {
      // Transparent pixels are a special case and are always 0 in the palette.
      sixel = Constants.SixelTransparent;
    }
    if (repeatCounter <= 1)
    {
      // single entry
      sixelBuilder.Append(Constants.SixelColorStart)
                  .Append(colorIndex)
                  .Append(sixel);
    }
    else
    {
      // add repeats
      sixelBuilder.Append(Constants.SixelColorStart)
                  .Append(colorIndex)
                  .Append(Constants.SixelRepeat)
                  .Append(repeatCounter)
                  .Append(sixel);
    }
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
    sixelBuilder.Append(Constants.ST);
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
