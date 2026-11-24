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

/// <summary>
/// A snapshot of separately assessed Kitty features. Extensions come from the terminal's answers to
/// feature queries (a=q,e=&lt;action&gt;) when it gives them, otherwise from its identity and version.
/// A plain a=q only proves direct transmission.
/// </summary>
public sealed class KittyCapabilities {
    public Terminals Terminal { get; internal set; }
    public string? Version { get; internal set; }
    public string Evidence { get; internal set; } = "No runtime response; terminal environment only.";
    public CapabilitySupport DirectTransmission { get; internal set; }
    public CapabilitySupport UnicodePlaceholders { get; internal set; }
    public CapabilitySupport Animation { get; internal set; }
    public CapabilitySupport FrameComposition { get; internal set; }
    public CapabilitySupport RelativePlacements { get; internal set; }
    /// <summary>Source rectangles (x, y, w, h) that crop the image on a=p and a=T.</summary>
    public CapabilitySupport SourceRectangle { get; internal set; }
    public CapabilitySupport ImageNumbers { get; internal set; }
    public CapabilitySupport FileTransfer { get; internal set; }
    public CapabilitySupport SharedMemory { get; internal set; }

    /// <summary>One feature query of the runtime probe: its own image id, the action it asks about, and its keys.</summary>
    internal sealed class FeatureQuery {
        internal FeatureQuery(uint id, char action, params string[] keys) {
            Id = id;
            Action = action;
            Control = new[] { "a=q", "i=" + id.ToString(CultureInfo.InvariantCulture), "e=" + action }.Concat(keys).ToArray();
        }
        internal uint Id { get; }
        internal char Action { get; }
        // Every value must be valid for its key: Contour drops a whole command on a parse error.
        // Values are nonzero because a terminal echoes only keys whose value differs from the default.
        internal IReadOnlyList<string> Control { get; }
    }

    // Placement geometry and placeholders in one query; relative placement keys in another, since a
    // terminal may refuse U together with P (virtual placements cannot be relative).
    internal static readonly FeatureQuery PlacementQuery = new(32, 'p', "p=1", "x=1", "y=1", "w=1", "h=1", "X=1", "Y=1", "c=1", "r=1", "C=1", "U=1", "z=1");
    internal static readonly FeatureQuery RelativeQuery = new(33, 'p', "P=1", "Q=1", "H=1", "V=1");
    internal static readonly FeatureQuery TransmitQuery = new(34, 'T', "f=100", "t=d", "o=z", "s=1", "v=1", "U=1");
    internal static readonly FeatureQuery AnimationQuery = new(35, 'a');
    internal static readonly FeatureQuery FrameQuery = new(36, 'f');
    internal static readonly FeatureQuery CompositionQuery = new(37, 'c');
    internal static readonly FeatureQuery[] FeatureQueries = [PlacementQuery, RelativeQuery, TransmitQuery, AnimationQuery, FrameQuery, CompositionQuery];

    internal static bool IsFeatureQueryId(uint id) => FeatureQueries.Any(query => query.Id == id);

    internal static KittyCapabilities Detect(Terminals terminal, string? version, bool? direct, IReadOnlyDictionary<uint, KittyResponse>? features = null) {
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
            result.SourceRectangle = CapabilitySupport.Supported;
            result.Animation = Since(version, new Version(0, 20, 0));
            result.FrameComposition = Since(version, new Version(0, 22, 0));
            result.UnicodePlaceholders = Since(version, new Version(0, 28, 0));
            result.RelativePlacements = Since(version, new Version(0, 31, 0));
            result.ImageNumbers = Since(version, new Version(0, 19, 3));
        }
        // Other implementations (and future versions) remain unknown until they answer
        // feature queries. They may be exercised explicitly with -Force.
        if (features is not null) result.ApplyFeatureReplies(features);
        return result;
    }

    /// <summary>
    /// The terminal's own answer overrides the version table. Only a successful reply naming the queried
    /// action (e=) is evidence; any other reply, or none, leaves the capability as the table has it.
    /// </summary>
    private void ApplyFeatureReplies(IReadOnlyDictionary<uint, KittyResponse> replies) {
        var used = new List<string>();
        IReadOnlyDictionary<char, string>? Echoed(FeatureQuery query) {
            if (!replies.TryGetValue(query.Id, out KittyResponse? reply) || !reply.Success || reply.FeatureAction != query.Action)
                return null;
            used.Add("e=" + query.Action + (reply.FeatureKeys.Count == 0 ? string.Empty : "{" + string.Join(",", reply.FeatureKeys.Keys) + "}"));
            return reply.FeatureKeys;
        }
        static CapabilitySupport All(IReadOnlyDictionary<char, string> keys, string wanted) =>
            wanted.All(keys.ContainsKey) ? CapabilitySupport.Supported : CapabilitySupport.Unsupported;

        CapabilitySupport? placeholders = null;
        if (Echoed(PlacementQuery) is { } placement) {
            SourceRectangle = All(placement, "xywh");
            placeholders = All(placement, "U");
        }
        if (Echoed(RelativeQuery) is { } relative)
            RelativePlacements = All(relative, "PQHV");
        if (Echoed(TransmitQuery) is { } transmit && placeholders != CapabilitySupport.Supported)
            placeholders = All(transmit, "U");
        if (placeholders.HasValue) UnicodePlaceholders = placeholders.Value;
        // An echoed e=<action> means the terminal implements that action; native animation needs a and f.
        bool animation = Echoed(AnimationQuery) is not null;
        bool frames = Echoed(FrameQuery) is not null;
        if (animation && frames) Animation = CapabilitySupport.Supported;
        if (Echoed(CompositionQuery) is not null) FrameComposition = CapabilitySupport.Supported;
        if (used.Count > 0)
            Evidence += " Feature query replies (" + string.Join(" ", used) + ") override the version table.";
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
        // Cropping is part of the base protocol and widely implemented: only a known lack of it blocks.
        if (action is 'p' or 'T' && (Nonzero(keys, 'x') || Nonzero(keys, 'y') || Nonzero(keys, 'w') || Nonzero(keys, 'h')))
            Require(SourceRectangle, "source rectangles (x, y, w, h)", allowUnknown: true);
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
