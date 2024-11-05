namespace Sixel.Terminal;

/// <summary>
/// Sixel terminal compatibility helpers.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The character to use when entering a terminal escape code sequence.
    /// </summary>
    public const string ESC = "\u001b";

    /// <summary>
    /// The character to indicate the start of a sixel color palette entry or to switch to a new color.
    /// </summary>
    public const char SIXELCOLORSTART = '#';

    /// <summary>
    /// The character to use when a sixel is empty/transparent.
    /// </summary>
    public const char SIXELEMPTY = '?';

    /// <summary>
    /// The character to use when entering a repeated sequence of a color in a sixel.
    /// </summary>
    public const char SIXELREPEAT = '!';

    /// <summary>
    /// The character to use when moving to the next line in a sixel.
    /// </summary>
    public const char SIXELDECGNL = '-';

    /// <summary>
    /// The character to use when going back to the start of the current line in a sixel to write more data over it.
    /// </summary>
    public const char SIXELDECGCR = '$';

    /// <summary>
    /// The start of a sixel sequence.
    /// </summary>
    public const string SIXELSTART = $"{ESC}P0;1q";

    /// <summary>
    /// The raster settings for setting the sixel pixel ratio to 1:1 so images are square when rendered instead of the 2:1 double height default.
    /// </summary>
    public const string SIXELRASTERATTRIBUTES = "\"1;1;";

    /// <summary>
    /// The end of a sixel sequence.
    /// </summary>
    public const string SIXELEND = $"{ESC}\\";

    /// <summary>
    /// The transparent color for the sixel, this is red but the sixel should be transparent so this is not visible.
    /// </summary>
    public const string SIXELTRANSPARENTCOLOR = "#0;2;0;0;0";

    /// <summary>
    /// inline image protocol start
    /// </summary>
    public const string INLINEIMAGESTART = $"{ESC}]";
    /// <summary>
    /// inline image protocol end
    /// </summary>
    public const char INLINEIMAGEEND = (char)7;
    /// <summary>
    /// newline
    /// </summary>
    public const char NEWLINE = (char)10;
}
