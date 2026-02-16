namespace Sixel.Terminal.Models;

/// <summary>
/// Represents the size of the terminal window in pixels
/// not supported in all terminals.
/// like WezTerm, Alacritty
/// </summary>
public class WindowSizePixels {
    /// <summary>
    /// Gets the width of the terminal in pixels.
    /// </summary>
    public int PixelWidth { get; set; }

    /// <summary>
    /// Gets the height of the terminal in pixels.
    /// </summary>
    public int PixelHeight { get; set; }
}
