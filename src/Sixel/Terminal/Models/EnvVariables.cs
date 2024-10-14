namespace Sixel.Terminal.Models;

internal partial class Helpers {
    /// <summary>
    /// mapping of environment variables to terminal.
    /// used for detecting the terminal.
    /// </summary>
    internal static readonly Dictionary<Terminals, string> EnvVars = new Dictionary<Terminals, string>()
        {
            { Terminals.MicrosoftTerminal, "WT_SESSION" },
            { Terminals.MicrosoftTerminalPreview, "WT_SESSION" },
            { Terminals.MicrosoftTerminalDev, "WT_SESSION" },
            { Terminals.MicrosoftTerminalCanary, "WT_SESSION" },
            { Terminals.MicrosoftConhost, "WT_SESSION" },
            { Terminals.Kitty, "KITTY_WINDOW_ID" },
            { Terminals.Iterm2, "ITERM_SESSION_ID" },
            { Terminals.WezTerm, "WEZTERM_CONFIG_FILE" },
            { Terminals.Ghostty, "GHOSTTY_RESOURCES_DIR" },
            { Terminals.VSCode, "TERM_PROGRAM" },
            { Terminals.Mintty, "MINTTY" },
            { Terminals.Apple, "TERM_PROGRAM" },
            { Terminals.Alacritty, "ALACRITTY_LOG" }
        };
}
