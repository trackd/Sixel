using System.Management.Automation;
using System.Net.Http;
using Sixel.Protocols;
using Sixel.Terminal;
using Sixel.Terminal.Models;

namespace Sixel.Cmdlet;

/// <summary>Preview Good Image Protocol converter (DCS ! g). The specification is alpha.</summary>
[Cmdlet(VerbsData.ConvertTo, "GoodImage", DefaultParameterSetName = "Path")]
[OutputType(typeof(string))]
public sealed class ConvertGoodImageCmdlet : PSCmdlet {
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

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(Mandatory = true, ParameterSetName = "Render")]
    [Parameter(Mandatory = true, ParameterSetName = "Release")]
    [ValidatePattern("^[A-Za-z0-9_]{1,512}$")]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Render")]
    public SwitchParameter Render { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Release")]
    public SwitchParameter Release { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Query")]
    public SwitchParameter QueryLimits { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Command")]
    [ValidateNotNullOrEmpty]
    public string[] Command { get; set; } = [];

    [Parameter(ParameterSetName = "Command")]
    public byte[]? Payload { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [ValidateSet("Display", "Upload")]
    public string Action { get; set; } = "Display";

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [ValidateSet("Png", "Rgb", "Rgba")]
    public string Format { get; set; } = "Png";

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Render")]
    [ValidateRange(0, int.MaxValue)]
    public int Width { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Render")]
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
    [Parameter(ParameterSetName = "Render")]
    public GoodImageAlignment Alignment { get; set; } = GoodImageAlignment.TopStart;

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Render")]
    public GoodImageResize Resize { get; set; } = GoodImageResize.ResizeToFit;

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Render")]
    public GoodImageLayer Layer { get; set; } = GoodImageLayer.Replace;

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Render")]
    [ValidateCount(4, 4)]
    [ValidateRange(0, int.MaxValue)]
    public int[]? Crop { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    public SwitchParameter Passthrough { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Render")]
    public SwitchParameter UpdateCursor { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Render")]
    public SwitchParameter AutoScroll { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Render")]
    public SwitchParameter NoCursorMove { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Render")]
    public SwitchParameter RequestStatus { get; set; }

    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Stream")]
    [Parameter(ParameterSetName = "Url")]
    [Parameter(ParameterSetName = "Base64")]
    [Parameter(ParameterSetName = "Render")]
    public string[] Header { get; set; } = [];

    [Parameter(HelpMessage = "Skip the DA1 90 check and automatic terminal probing.")]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord() {
        Stream? source = null;
        try {
            TerminalExecution.Prepare(this, Force.IsPresent);
            if (ParameterSetName == "Query") {
                // The query is how a client learns more; it never needs the DA1 announcement.
                WriteObject(GoodImageProtocol.QueryLimits);
                return;
            }
            if (!Force.IsPresent && !Compatibility.TerminalSupportsGoodImageProtocol())
                throw new InvalidOperationException("Terminal did not announce the Good Image Protocol (DA1 90), override with -Force");
            switch (ParameterSetName) {
                case "Command":
                    WriteObject(GoodImageProtocol.Encode(Command, Payload));
                    return;
                case "Release":
                    WriteObject(GoodImageProtocol.Release(Name!));
                    return;
                case "Render":
                    WriteObject(GoodImage.Render(Options(Compatibility.GetCellSize())));
                    return;
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
            CellSize cached = Compatibility.GetCellSize();
            GoodImageOptions options = Options(new CellSize {
                PixelWidth = CellWidth == 0 ? cached.PixelWidth : CellWidth,
                PixelHeight = CellHeight == 0 ? cached.PixelHeight : CellHeight,
            });
            var encoded = GoodImage.Encode(source, options);
            var output = PSObject.AsPSObject(encoded.Text);
            output.Properties.Add(new PSNoteProperty("Width", encoded.Cells.Width));
            output.Properties.Add(new PSNoteProperty("Height", encoded.Cells.Height));
            output.Properties.Add(new PSNoteProperty("PixelWidth", encoded.Pixels.Width));
            output.Properties.Add(new PSNoteProperty("PixelHeight", encoded.Pixels.Height));
            output.Properties.Add(new PSNoteProperty("Name", Name));
            WriteObject(output);
        }
        catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "ConvertGoodImageCmdlet", ErrorCategory.InvalidData, MyInvocation.BoundParameters));
        }
        finally {
            if (ParameterSetName != "Stream") source?.Dispose();
        }
    }

    private GoodImageOptions Options(CellSize cell) => new() {
        Width = Width,
        Height = Height,
        Cell = cell,
        Name = Name,
        Action = Action.Equals("Upload", StringComparison.OrdinalIgnoreCase) ? GoodImageAction.Upload : GoodImageAction.Display,
        Format = Format.ToUpperInvariant() switch { "RGB" => GoodImageFormat.Rgb, "RGBA" => GoodImageFormat.Rgba, _ => GoodImageFormat.Png },
        Passthrough = Passthrough.IsPresent,
        RequestStatus = RequestStatus.IsPresent,
        Headers = Header,
        Place = !NoCursorMove.IsPresent,
        Render = new GoodImageRender {
            Alignment = Alignment,
            Resize = Resize,
            Layer = Layer,
            UpdateCursor = UpdateCursor.IsPresent,
            AutoScroll = AutoScroll.IsPresent,
            Crop = Crop is { Length: 4 } c ? (c[0], c[1], c[2], c[3]) : null,
        },
    };
}
