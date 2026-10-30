using System.Globalization;
using Sixel.Protocols;
using Sixel.Terminal.Models;

namespace Sixel.Terminal;

/// <summary>Unknown is distinct from a negative response or a known missing feature.</summary>
public enum CapabilitySupport {
    Unknown,
    Supported,
    Unsupported
}

/// <summary>A snapshot of separately assessed Kitty features. Extension support is inferred, not proved by a=q.</summary>
public sealed class KittyCapabilities {
    public Terminals Terminal { get; internal set; }
    public string? Version { get; internal set; }
    public string Evidence { get; internal set; } = "No runtime response; terminal environment only.";
    public CapabilitySupport DirectTransmission { get; internal set; }
    public CapabilitySupport UnicodePlaceholders { get; internal set; }
    public CapabilitySupport Animation { get; internal set; }
    public CapabilitySupport FrameComposition { get; internal set; }
    public CapabilitySupport RelativePlacements { get; internal set; }
    public CapabilitySupport ImageNumbers { get; internal set; }
    public CapabilitySupport FileTransfer { get; internal set; }
    public CapabilitySupport SharedMemory { get; internal set; }

    internal static KittyCapabilities Detect(Terminals terminal, string? version, bool? direct) {
        var result = new KittyCapabilities {
            Terminal = terminal, Version = version,
            DirectTransmission = direct.HasValue ? (direct.Value ? CapabilitySupport.Supported : CapabilitySupport.Unsupported) :
                terminal is Terminals.Kitty or Terminals.Ghostty ? CapabilitySupport.Supported : CapabilitySupport.Unknown,
            Evidence = direct.HasValue ? "Direct transmission measured with a=q; extensions assessed separately from terminal identity/version." :
                "No runtime response; terminal identity/version only."
        };
        if (terminal == Terminals.WezTerm) {
            // https://github.com/wezterm/wezterm/issues/986 and /pull/7924.
            // Do not infer extensions from an OK reply to a basic transfer query.
            result.UnicodePlaceholders = CapabilitySupport.Unsupported;
        }
        if (terminal == Terminals.Kitty) {
            result.FileTransfer = CapabilitySupport.Supported;
            result.SharedMemory = CapabilitySupport.Supported;
            result.Animation = Since(version, new Version(0, 20, 0));
            result.FrameComposition = Since(version, new Version(0, 22, 0));
            result.UnicodePlaceholders = Since(version, new Version(0, 28, 0));
            result.RelativePlacements = Since(version, new Version(0, 31, 0));
            result.ImageNumbers = Since(version, new Version(0, 19, 3));
        }
        // Other implementations (and future versions) remain unknown until we
        // have specific evidence. They may be exercised explicitly with -Force.
        return result;
    }

    private static CapabilitySupport Since(string? text, Version minimum) {
        if (text is null) return CapabilitySupport.Unknown;
        string numeric = new(text.TakeWhile(character => char.IsDigit(character) || character == '.').ToArray());
        return System.Version.TryParse(numeric, out Version? version) ?
            version >= minimum ? CapabilitySupport.Supported : CapabilitySupport.Unsupported : CapabilitySupport.Unknown;
    }

    internal void Validate(IEnumerable<string> control, bool animate, bool placeholder, bool force) {
        Dictionary<char, string> keys = KittyProtocol.Parse(control);
        if (force) return;
        char action = keys.TryGetValue('a', out string? value) ? value[0] : 'T';
        // Queries test image formats/media; they do not prove extension support.
        if (action == 'q') return;
        if (DirectTransmission == CapabilitySupport.Unsupported)
            Require(DirectTransmission, "Kitty graphics", allowUnknown: false);
        if (placeholder || Nonzero(keys, 'U')) Require(UnicodePlaceholders, "Unicode placeholders");
        if (animate || action is 'f' or 'a' || (action == 'd' && keys.TryGetValue('d', out string? deletion) && deletion is "f" or "F"))
            Require(Animation, "native animation");
        if (action == 'c') Require(FrameComposition, "frame composition");
        if (Nonzero(keys, 'P')) Require(RelativePlacements, "relative placements");
        if (Nonzero(keys, 'I')) Require(ImageNumbers, "image numbers", allowUnknown: true);
        if (keys.TryGetValue('t', out string? medium)) {
            if (medium is "f" or "t") Require(FileTransfer, "file transfer", allowUnknown: true);
            if (medium == "s") Require(SharedMemory, "shared-memory transfer");
        }
    }

    private void Require(CapabilitySupport support, string feature, bool allowUnknown = false) {
        if (support == CapabilitySupport.Supported || (allowUnknown && support == CapabilitySupport.Unknown)) return;
        throw new NotSupportedException($"{Terminal} {Version}: {feature} support is {support}. " +
            "Use ordinary direct placement, inspect [Sixel.Terminal.Compatibility]::GetKittyCapabilities(), " +
            "or use -Force when deliberately testing a terminal build with this feature.");
    }

    private static bool Nonzero(Dictionary<char, string> values, char key) =>
        values.TryGetValue(key, out string? value) && uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out uint number) && number != 0;
}
