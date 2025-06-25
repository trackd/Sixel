using System.Collections.Generic;

namespace Sixel.Terminal.Models;

/// <summary>
/// Provides mappings and helper methods for associating terminal types with supported image protocols.
/// </summary>
public sealed partial class Helpers
{
  /// <summary>
  ///  mapping of terminals to the image protocol they support.
  /// </summary>
  public static readonly Dictionary<Terminals, ImageProtocol[]> SupportedProtocol = new Dictionary<Terminals, ImageProtocol[]>()
  {
        { Terminals.MicrosoftTerminal, new[] { ImageProtocol.Sixel } },
        { Terminals.MicrosoftConhost, new[] { ImageProtocol.Sixel } },
        { Terminals.Kitty, new[] { ImageProtocol.KittyGraphicsProtocol } },
        { Terminals.Iterm2, new[] { ImageProtocol.InlineImageProtocol } },
        { Terminals.WezTerm, new[] { ImageProtocol.InlineImageProtocol } },
        { Terminals.Ghostty, new[] { ImageProtocol.KittyGraphicsProtocol } },
        { Terminals.VSCode, new[] { ImageProtocol.InlineImageProtocol } },
        { Terminals.Mintty, new[] { ImageProtocol.InlineImageProtocol } },
        { Terminals.Alacritty, new[] { ImageProtocol.Blocks } },
        { Terminals.xterm, new[] { ImageProtocol.InlineImageProtocol } },
        { Terminals.mlterm, new[] { ImageProtocol.Sixel } },
        { Terminals.unknown, new[] { ImageProtocol.Blocks } }
    };
}
