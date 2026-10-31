using System.Management.Automation;
using System.Net.Http;
using System.Text;
using Sixel.Protocols;
using Sixel.Terminal;
using Sixel.Terminal.Models;

namespace Sixel.Cmdlet;

/// <summary>Preview Kitty graphics converter; existing ConvertTo-Sixel behavior is independent.</summary>
[Cmdlet(VerbsData.ConvertTo, "Kitty", DefaultParameterSetName = "Path")]
[OutputType(typeof(string))]
public sealed class ConvertKittyCmdlet : PSCmdlet {
    private static readonly HttpClient Client = new();

    [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true, ParameterSetName = "Path")]
    [Alias("FullName")]
    [ValidateNotNullOrEmpty]
    public string? Path { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Stream")]
    [ValidateNotNull]
    public Stream? Stream { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Url")]
    [Alias("Uri")]
    [ValidateNotNull]
    public Uri? Url { get; set; }

    [Parameter(ParameterSetName = "Url")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    [Parameter(Mandatory = true, ParameterSetName = "Base64")]
    [ValidateNotNullOrEmpty]
    public string? Base64 { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Command")]
    [ValidateNotNullOrEmpty]
    public string[] Command { get; set; } = [];

    [Parameter(ParameterSetName = "Command")]
    public byte[]? Payload { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Reference")]
    [ValidateNotNullOrEmpty]
    public string? Reference { get; set; }

    [Parameter(ParameterSetName = "Reference")]
    [ValidateSet("File", "TemporaryFile", "SharedMemory")]
    public string Medium { get; set; } = "File";

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Reference")]
    [ValidateSet("TransmitAndDisplay", "Transmit", "Query", "Frame")]
    public string Action { get; set; } = "TransmitAndDisplay";

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Reference")]
    [ValidateSet("Png", "Rgb", "Rgba")]
    public string Format { get; set; } = "Png";

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Reference")]
    [ValidateRange(0, int.MaxValue)]
    public int Width { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Reference")]
    [ValidateRange(0, int.MaxValue)]
    public int Height { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [ValidateRange(1, int.MaxValue)]
    public int CellWidth { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [ValidateRange(1, int.MaxValue)]
    public int CellHeight { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Reference")]
    public uint ImageId { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Reference")]
    public uint ImageNumber { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Reference")]
    public uint PlacementId { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Reference")]
    [ValidateRange(0, 2)]
    public int Quiet { get; set; } = 2;

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    public SwitchParameter Compress { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    public SwitchParameter Animate { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    public SwitchParameter UnicodePlaceholder { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Reference")]
    public SwitchParameter NoCursorMove { get; set; }

    [Parameter]
    public SwitchParameter Tmux { get; set; }

    [Parameter(HelpMessage = "Maximum base64 characters per direct packet; a multiple of four from 4 to 4096.")]
    [ValidateRange(4, 4096)]
    public int ChunkSize { get; set; } = 4096;

    [Parameter(HelpMessage = "Bypass terminal feature checks when deliberately testing unsupported or unknown features.")]
    public SwitchParameter Force { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Reference")]
    public string[] Control { get; set; } = [];

    protected override void ProcessRecord() {
        Stream? source = null;
        try {
            if (ChunkSize % 4 != 0) throw new ArgumentException("ChunkSize must be a multiple of four.");
            TerminalExecution.Prepare(this, Force.IsPresent);
            KittyCapabilities capabilities = Compatibility.GetKittyCapabilities();
            if (ParameterSetName == "Command") {
                capabilities.Validate(Command, animate: false, placeholder: false, Force.IsPresent);
                WriteObject(KittyProtocol.Encode(Command, Payload, Tmux.IsPresent, ChunkSize));
                return;
            }
            int format = Format.ToUpperInvariant() switch { "RGB" => 24, "RGBA" => 32, _ => 100 };
            // Contour's current Kitty PNG path stores encoded bytes without
            // decoding them. Prefer raw RGBA; an explicit -Format remains authoritative.
            if (ParameterSetName != "Reference" && !MyInvocation.BoundParameters.ContainsKey(nameof(Format)) &&
                capabilities.Terminal == Terminals.Contour)
                format = 32;
            char action = Action.ToUpperInvariant() switch { "TRANSMIT" => 't', "QUERY" => 'q', "FRAME" => 'f', _ => 'T' };
            if (ParameterSetName == "Reference") {
                if (action != 'T' && (Width != 0 || Height != 0))
                    throw new ArgumentException("Reference Width/Height apply only to TransmitAndDisplay. Use -Control for frame coordinates.");
                var controls = new List<string> { "a=" + action, "t=" + (Medium.ToUpperInvariant() switch { "TEMPORARYFILE" => 't', "SHAREDMEMORY" => 's', _ => 'f' }), KittyImage.Number('f', format), KittyImage.Number('q', Quiet) };
                if (ImageId != 0) controls.Add(KittyImage.Number('i', ImageId));
                if (ImageNumber != 0) controls.Add(KittyImage.Number('I', ImageNumber));
                if (PlacementId != 0) controls.Add(KittyImage.Number('p', PlacementId));
                if (Width != 0) controls.Add(KittyImage.Number('c', Width));
                if (Height != 0) controls.Add(KittyImage.Number('r', Height));
                if (NoCursorMove.IsPresent) controls.Add("C=1");
                controls.AddRange(Control);
                capabilities.Validate(controls, animate: false, placeholder: false, Force.IsPresent);
                WriteObject(KittyProtocol.Encode(controls, Encoding.UTF8.GetBytes(Reference!), Tmux.IsPresent));
                return;
            }
            var featureControls = new List<string>(Control) { "a=" + action };
            if (ImageNumber != 0) featureControls.Add(KittyImage.Number('I', ImageNumber));
            capabilities.Validate(featureControls, Animate.IsPresent, UnicodePlaceholder.IsPresent, Force.IsPresent);
            switch (ParameterSetName) {
                case "Path": source = File.OpenRead(SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path)); break;
                case "Stream": source = Stream; break;
                case "Base64": source = new MemoryStream(Convert.FromBase64String(Compatibility.TrimBase64(Base64!))); break;
                case "Url":
                    using (var cancellation = new CancellationTokenSource(Timeout)) {
                        using HttpResponseMessage response = Client.GetAsync(Url, cancellation.Token).GetAwaiter().GetResult();
                        response.EnsureSuccessStatusCode();
                        source = new MemoryStream(response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult());
                    }
                    break;
            }
            if (source is null) throw new ArgumentException("An image source is required.");
            if (source.CanSeek) source.Position = 0;
            CellSize cached = Compatibility.GetCellSize();
            var options = new KittyImageOptions {
                Width = Width, Height = Height, Format = format, Action = action,
                ImageId = ImageId, ImageNumber = ImageNumber, PlacementId = PlacementId, Quiet = Quiet,
                Compress = Compress.IsPresent, Animate = Animate.IsPresent, Placeholder = UnicodePlaceholder.IsPresent,
                NoCursorMove = NoCursorMove.IsPresent, Tmux = Tmux.IsPresent, ChunkSize = ChunkSize, Control = Control,
                ExplicitCursorMove = capabilities.Terminal == Terminals.Contour,
                Cell = new CellSize { PixelWidth = CellWidth == 0 ? cached.PixelWidth : CellWidth, PixelHeight = CellHeight == 0 ? cached.PixelHeight : CellHeight }
            };
            var encoded = KittyImage.Encode(source, options);
            var output = PSObject.AsPSObject(encoded.Text);
            output.Properties.Add(new PSNoteProperty("Width", encoded.Cells.Width));
            output.Properties.Add(new PSNoteProperty("Height", encoded.Cells.Height));
            output.Properties.Add(new PSNoteProperty("PixelWidth", encoded.Pixels.Width));
            output.Properties.Add(new PSNoteProperty("PixelHeight", encoded.Pixels.Height));
            output.Properties.Add(new PSNoteProperty("ImageId", ImageId));
            WriteObject(output);
        }
        catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "ConvertKittyCmdlet", ErrorCategory.InvalidData, MyInvocation.BoundParameters));
        }
        finally {
            if (ParameterSetName != "Stream") source?.Dispose();
        }
    }
}
