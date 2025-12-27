namespace Sixel.Terminal.Models;

/// <summary>
/// Represents the size of an image in character cells.
/// </summary>
public readonly struct ImageSize(int Width, int Height) {
    /// <summary>
    /// Gets the width of an image in character cells.
    /// </summary>
    public readonly int Width { get; } = Width;

    /// <summary>
    /// Gets the height of an image in character cells.
    /// </summary>
    public readonly int Height { get; } = Height;

}
