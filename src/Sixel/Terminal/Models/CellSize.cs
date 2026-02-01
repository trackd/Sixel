namespace Sixel.Terminal.Models;


/// <summary>
/// Represents the size of a terminal cell in pixels for Sixel rendering.
/// </summary>
public class CellSize {
    /// <summary>
    /// Gets the width of a cell in pixels.
    /// </summary>
    public int PixelWidth { get; set; }

    /// <summary>
    /// Gets the height of a cell in pixels.
    /// This isn't used for anything yet but this would be required for something like spectre console that needs to work around the size of the rendered sixel image.
    /// </summary>
    public int PixelHeight { get; set; }

    /// <summary>
    /// Gets the aspect ratio of the cell (width / height).
    /// Typically ~0.5 for standard terminals (10x20 pixels).
    /// This is the "font ratio" used to maintain proper image aspect ratios.
    /// </summary>
    public double AspectRatio => PixelHeight > 0 ? (double)PixelWidth / PixelHeight : 0.5;
}
