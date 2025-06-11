namespace Sixel.Terminal.Models;

/// <summary>
/// Represents the size of an image in character cells.
/// </summary>
/// <remarks>
/// Creates a new ImageSize instance with the specified width and height.
/// </remarks>
/// <param name="width">Width in character cells.</param>
/// <param name="height">Height in character cells.</param>
public readonly struct ImageSize(int CellWidth, int CellHeight, int PixelWidth, int PixelHeight)
{
    /// <summary>
    /// Gets the width of an image in character cells.
    /// </summary>
    public readonly int CellWidth { get; } = CellWidth;

    /// <summary>
    /// Gets the height of an image in character cells.
    /// </summary>
    public readonly int CellHeight { get; } = CellHeight;

    /// <summary>
    /// Gets the width of an image in pixels.
    /// </summary>
    public readonly int PixelWidth { get; } = PixelWidth;

    /// <summary>
    /// Gets the height of an image in pixels.
    /// </summary>
    public readonly int PixelHeight { get; } = PixelHeight;

    public int Width => CellWidth;
    public int Height => CellHeight;
}
