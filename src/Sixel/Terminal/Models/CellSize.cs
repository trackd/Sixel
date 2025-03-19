namespace Sixel.Terminal.Models;

/// <summary>
/// Represents the size of a cell in pixels for sixel rendering.
/// </summary>
public class CellSize
{
  /// <summary>
  /// Gets the width of a cell in pixels.
  /// </summary>
  public int PixelWidth { get; set; }

  /// <summary>
  /// Gets the height of a cell in pixels.
  /// This isn't used for anything yet but this would be required for something like spectre console that needs to work around the size of the rendered sixel image.
  /// </summary>
  public int PixelHeight { get; set; }
}
