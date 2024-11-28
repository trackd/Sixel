using Sixel.Terminal;
using Sixel.Terminal.Models;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace Sixel.Protocols;

// ignore this, nothing to see here.. move along.
// not finished..
public static class AsciiGenerator
{
  private static Rgba32 _backgroundColor;
  public static List<Frame> LoadImage(string Path, int cellWidth = 0)
  {
    _backgroundColor = GetConsoleBackgroundColor();
    var image = Image.Load<Rgba32>(Path);
    var frames = ProcessImage(image, cellWidth);
    return frames;
  }
  private static List<Frame> ProcessImage(Image<Rgba32> image, int cellWidth)
  {
    // _backgroundColor = GetConsoleBackgroundColor();

    var cellSize = Compatibility.GetCellSize();
    var maxWidth = Console.WindowWidth - 2;
    var characterWidth = (int)Math.Ceiling((double)image.Width / cellSize.PixelWidth);
    var characterHeight = (int)Math.Ceiling((double)image.Height / cellSize.PixelHeight);

    if (cellWidth <= 0 || cellWidth > maxWidth)
    {
      cellWidth = Math.Min(characterWidth, maxWidth);
    }
    image.Mutate(ctx =>
    {
      // Calculate target size in pixels based on character dimensions
      var pixelHeight = (int)Math.Round((double)image.Height / image.Width * cellWidth);
      ctx.Resize(new ResizeOptions
      {
        Size = new Size(cellWidth, pixelHeight),
        Sampler = KnownResamplers.Bicubic,
        PremultiplyAlpha = false
      });

      // ctx.Quantize(new OctreeQuantizer(new()
      // {
      //   MaxColors = 256
      // }));
    });

    var frames = new List<Frame>();

    foreach (var frame in image.Frames)
    {
      var frameDelay = 1000;
      var metadata = frame.Metadata.GetGifMetadata();
      if (metadata?.FrameDelay > 0) {
        frameDelay = metadata.FrameDelay * 10;
      }

      var buffer = ProcessFrame(frame, image.Width, image.Height);
      frames.Add(new Frame
      {
        Delay = frameDelay,
        Content = buffer
      });
    }

    return frames;
  }

  private static string ProcessFrame(ImageFrame<Rgba32> frame, int width, int height)
  {
    var _buffer = new StringBuilder();

    for (int y = 0; y < height; y += 2)
    {
      if (y + 1 >= height)
      {
        _buffer.AppendLine();
        break;
      }

      for (int x = 0; x < width; x++)
      {
        var topPixel = frame[x, y];
        var bottomPixel = frame[x, y + 1];

        _buffer.ProcessPixelPair(topPixel, bottomPixel);
      }
      _buffer.AppendLine();
    }
    return _buffer.ToString();
  }
  private static void ProcessPixelPair(this StringBuilder _buffer, Rgba32 top, Rgba32 bottom)
  {
    var topRgb = BlendPixel(top);
    var bottomRgb = BlendPixel(bottom);

    if (IsTransparent(bottom))
      _buffer.Append($"{Constants.ESC}[0m");
    else
      _buffer.Append($"{Constants.ESC}[38;2;{bottomRgb.R};{bottomRgb.G};{bottomRgb.B}m");

    if (IsTransparent(top))
      _buffer.Append($"{Constants.ESC}[0m ");
    else
      _buffer.Append($"{Constants.ESC}[48;2;{topRgb.R};{topRgb.G};{topRgb.B}m{Constants.LowerHalfBlock}{Constants.ESC}[0m");
  }

  private static (byte R, byte G, byte B) BlendPixel(Rgba32 pixel)
  {
    if (pixel.A == 0) return (pixel.R, pixel.G, pixel.B);

    var foregroundMultiplier = pixel.A / 255f;
    var backgroundMultiplier = (255 - pixel.A) / 255f;

    return (
        (byte)Math.Min(255, (pixel.R * foregroundMultiplier + _backgroundColor.R * backgroundMultiplier)),
        (byte)Math.Min(255, (pixel.G * foregroundMultiplier + _backgroundColor.G * backgroundMultiplier)),
        (byte)Math.Min(255, (pixel.B * foregroundMultiplier + _backgroundColor.B * backgroundMultiplier))
    );
  }

  private static bool IsTransparent(Rgba32 pixel) => pixel.A < 5;

  private static Rgba32 GetConsoleBackgroundColor()
  {
    var color = Console.BackgroundColor switch
    {
      ConsoleColor.Black => Color.FromRgb(0, 0, 0),
      ConsoleColor.Blue => Color.FromRgb(0, 0, 170),
      ConsoleColor.Cyan => Color.FromRgb(0, 170, 170),
      ConsoleColor.DarkBlue => Color.FromRgb(0, 0, 85),
      ConsoleColor.DarkCyan => Color.FromRgb(0, 85, 85),
      ConsoleColor.DarkGray => Color.FromRgb(85, 85, 85),
      ConsoleColor.DarkGreen => Color.FromRgb(0, 85, 0),
      ConsoleColor.DarkMagenta => Color.FromRgb(85, 0, 85),
      ConsoleColor.DarkRed => Color.FromRgb(85, 0, 0),
      ConsoleColor.DarkYellow => Color.FromRgb(85, 85, 0),
      ConsoleColor.Gray => Color.FromRgb(170, 170, 170),
      ConsoleColor.Green => Color.FromRgb(0, 170, 0),
      ConsoleColor.Magenta => Color.FromRgb(170, 0, 170),
      ConsoleColor.Red => Color.FromRgb(170, 0, 0),
      ConsoleColor.White => Color.FromRgb(255, 255, 255),
      ConsoleColor.Yellow => Color.FromRgb(170, 170, 0),
      _ => Color.FromRgb(0, 0, 0)
    };
    return color.ToPixel<Rgba32>();
  }
}

public class Frame
{
  public int? Delay { get; set; }
  public string? Content { get; set; }
}
