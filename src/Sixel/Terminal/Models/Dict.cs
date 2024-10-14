using System.Collections.Generic;

namespace Sixel.Terminal.Models;

internal partial class Helpers
{
  /// <summary>
  ///  mapping of terminals to the image protocol they support.
  /// </summary>
  internal static readonly Dictionary<Terminals, ImageProtocol> SupportedProtocol = new Dictionary<Terminals, ImageProtocol>()
    {
      { Terminals.MicrosoftTerminal, ImageProtocol.Unsupported },
      { Terminals.MicrosoftTerminalPreview, ImageProtocol.Sixel },
      { Terminals.MicrosoftTerminalDev, ImageProtocol.Sixel },
      { Terminals.MicrosoftTerminalCanary, ImageProtocol.Sixel },
      { Terminals.MicrosoftConhost, ImageProtocol.Sixel },
      { Terminals.Kitty, ImageProtocol.KittyGraphicsProtocol },
      { Terminals.Iterm2, ImageProtocol.InlineImageProtocol },
      { Terminals.WezTerm, ImageProtocol.InlineImageProtocol },
      { Terminals.Ghostty, ImageProtocol.KittyGraphicsProtocol },
      { Terminals.VSCode, ImageProtocol.Sixel },
      { Terminals.Mintty, ImageProtocol.InlineImageProtocol },
      { Terminals.Apple, ImageProtocol.Sixel },
      { Terminals.Alacritty, ImageProtocol.Unsupported },
      { Terminals.xterm, ImageProtocol.Sixel },
      { Terminals.mlterm, ImageProtocol.Sixel }
  };
}
