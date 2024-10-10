namespace Sixel.Terminal.Models;

public class CellSize
{
  /// <summary>
  /// The width of a cell in pixels.
  /// </summary>
  public int PixelWidth { get; set; }

  /// <summary>
  /// The height of a cell in pixels.
  /// This isn't used for anything yet but this would be required for something like spectre console that needs to work around the size of the rendered sixel image.
  /// </summary>
  public int PixelHeight { get; set; }
}