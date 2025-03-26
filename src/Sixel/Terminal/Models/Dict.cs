using System.Collections.Generic;

namespace Sixel.Terminal.Models;

internal partial class Helpers
{
  /// <summary>
  ///  mapping of terminals to the image protocol they support.
  /// </summary>
  internal static readonly Dictionary<Terminals, ImageProtocol[]> SupportedProtocol = new Dictionary<Terminals, ImageProtocol[]>()
  {
        { Terminals.MicrosoftTerminal, new[] { ImageProtocol.Sixel } },
        { Terminals.MicrosoftConhost, new[] { ImageProtocol.Sixel } },
        { Terminals.Kitty, new[] { ImageProtocol.KittyGraphicsProtocol } },
        { Terminals.Iterm2, new[] { ImageProtocol.InlineImageProtocol } },
        { Terminals.WezTerm, new[] { ImageProtocol.KittyGraphicsProtocol, ImageProtocol.Sixel, ImageProtocol.InlineImageProtocol } },
        { Terminals.Ghostty, new[] { ImageProtocol.KittyGraphicsProtocol } },
        { Terminals.VSCode, new[] { ImageProtocol.Sixel, ImageProtocol.InlineImageProtocol } },
        { Terminals.Mintty, new[] { ImageProtocol.InlineImageProtocol } },
        { Terminals.Alacritty, new[] { ImageProtocol.Blocks } },
        { Terminals.xterm, new[] { ImageProtocol.InlineImageProtocol } },
        { Terminals.mlterm, new[] { ImageProtocol.Sixel } },
        { Terminals.unknown, new[] { ImageProtocol.Blocks } }
    };
}
