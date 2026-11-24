using System.Globalization;

namespace Sixel.Protocols;

// The net472 uint parser has no span overload.
#pragma warning disable CA1846

/// <summary>A graphics acknowledgement. Parsing never reads console input.</summary>
public sealed class KittyResponse {
    private static readonly IReadOnlyDictionary<char, string> s_noKeys = new Dictionary<char, string>();

    public uint ImageId { get; private set; }
    public uint ImageNumber { get; private set; }
    public uint PlacementId { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public bool Success => Message == "OK";
    public string? ErrorCode => Success ? null : Message.Split(':')[0];
    /// <summary>The action a feature query (a=q with e=) asked about, when the reply names it; otherwise null.</summary>
    public char? FeatureAction { get; private set; }
    /// <summary>The keys a feature query reply echoes as supported for FeatureAction, with their echoed values. Empty for other replies.</summary>
    public IReadOnlyDictionary<char, string> FeatureKeys { get; private set; } = s_noKeys;

    /// <summary>Parses one complete ESC _ G ... ESC backslash response.</summary>
    public static bool TryParse(string? packet, out KittyResponse? response) {
        response = null;
        if (packet is null || !packet.StartsWith("\u001b_G", StringComparison.Ordinal) ||
            !packet.EndsWith("\u001b\\", StringComparison.Ordinal)) return false;
        int separator = packet.IndexOf(';');
        if (separator < 3 || separator >= packet.Length - 3) return false;
        string message = packet.Substring(separator + 1, packet.Length - separator - 3);
        if (message.Any(character => character < ' ' || character > '~')) return false;
        var parsed = new KittyResponse { Message = message };
        var seen = new HashSet<char>();
        string[] entries = packet.Substring(3, separator - 3).Split(',');
        // A feature query reply (Contour's a=q,e=<action>) echoes the supported keys with their values.
        bool feature = entries.Any(entry => entry.StartsWith("e=", StringComparison.Ordinal));
        Dictionary<char, string>? keys = feature ? new Dictionary<char, string>() : null;
        foreach (string entry in entries) {
            if (entry.Length < 3 || entry[1] != '=' || !seen.Add(entry[0])) return false;
            string text = entry.Substring(2);
            if (entry[0] == 'e') {
                if (text.Length != 1 || !IsAsciiLetter(text[0])) return false;
                parsed.FeatureAction = text[0];
                continue;
            }
            if (entry[0] is 'i' or 'I' || !feature) {
                if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value)) return false;
                switch (entry[0]) {
                    case 'i': parsed.ImageId = value; continue;
                    case 'I': parsed.ImageNumber = value; continue;
                    case 'p': parsed.PlacementId = value; continue;
                    default: return false;
                }
            }
            if (!IsAsciiLetter(entry[0]) || text.Any(c => !IsAsciiLetter(c) && (c < '0' || c > '9') && c != '-')) return false;
            if (entry[0] == 'p' && uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint placement))
                parsed.PlacementId = placement;
            keys!.Add(entry[0], text);
        }
        if (!seen.Contains('i') && !seen.Contains('I')) return false;
        if (keys is not null) parsed.FeatureKeys = keys;
        response = parsed;
        return true;
    }

    private static bool IsAsciiLetter(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');
}
