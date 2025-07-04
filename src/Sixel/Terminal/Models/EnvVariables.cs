﻿namespace Sixel.Terminal.Models;

using System.Collections.Generic;
public partial class Helpers
{
    /// <summary>
    /// mapping of environment variables to terminal.
    /// used for detecting the terminal.
    /// </summary>
    private static readonly Dictionary<Terminals, string> _lookup;
    private static readonly Dictionary<string, Terminals> _reverseLookup;
    static Helpers()
    {
        _lookup = new Dictionary<Terminals, string>
        {
            { Terminals.MicrosoftTerminal, "WT_SESSION" },
            // { Terminals.MicrosoftConhost, "SESSIONNAME" },
            { Terminals.Kitty, "KITTY_WINDOW_ID" },
            { Terminals.Iterm2, "ITERM_SESSION_ID" },
            { Terminals.WezTerm, "WEZTERM_CONFIG_FILE" },
            { Terminals.Ghostty, "GHOSTTY_RESOURCES_DIR" },
            { Terminals.VSCode, "VSCODE_GIT_ASKPASS_MAIN" },
            { Terminals.Mintty, "MINTTY" },
            { Terminals.Alacritty, "ALACRITTY_LOG" }
            // { Terminals.unknown, "TERM_PROGRAM" }
        };
        _reverseLookup = new Dictionary<string, Terminals>(StringComparer.OrdinalIgnoreCase);
        foreach (var (terminal, envVar) in _lookup)
        {
            if (!_reverseLookup.ContainsKey(envVar))
            {
                _reverseLookup[envVar] = terminal;
            }
        }
    }
    public static string[] GetEnvironmentVariables()
    {
        var envVars = new string[_lookup.Count];
        int i = 0;
        foreach (var envVar in _lookup.Values)
        {
            envVars[i++] = envVar;
        }
        return envVars;
    }
    public static Terminals GetTerminal(string str)
    {
        if (_reverseLookup.TryGetValue(str, out Terminals _terminal))
        {
            return _terminal;
        }
        if (Enum.TryParse(str, true, out _terminal))
        {
            return _terminal;
        }
        return Terminals.unknown;
    }
    public static string GetEnvironmentVariable(Terminals terminal)
    {
        if (_lookup.TryGetValue(terminal, out var _envVar))
        {
            return _envVar;
        }
        return "TERM_PROGRAM";
    }
}
