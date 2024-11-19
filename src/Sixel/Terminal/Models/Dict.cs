using System.Collections.Generic;

namespace Sixel.Terminal.Models;

internal partial class Helpers
{
  /// <summary>
  ///  mapping of terminals to the image protocol they support.
  /// </summary>
  internal static readonly Dictionary<Terminals, ImageProtocol> SupportedProtocol = new Dictionary<Terminals, ImageProtocol>()
    {
      { Terminals.MicrosoftTerminal, ImageProtocol.Sixel },
      { Terminals.MicrosoftConhost, ImageProtocol.Sixel },
      { Terminals.Kitty, ImageProtocol.KittyGraphicsProtocol },
      { Terminals.Iterm2, ImageProtocol.InlineImageProtocol },
      { Terminals.WezTerm, ImageProtocol.InlineImageProtocol },
      { Terminals.Ghostty, ImageProtocol.KittyGraphicsProtocol },
      { Terminals.VSCode, ImageProtocol.InlineImageProtocol },
      { Terminals.Mintty, ImageProtocol.InlineImageProtocol },
      { Terminals.Alacritty, ImageProtocol.None },
      { Terminals.xterm, ImageProtocol.InlineImageProtocol },
      { Terminals.mlterm, ImageProtocol.Sixel },
      { Terminals.unknown, ImageProtocol.None }
  };
}
