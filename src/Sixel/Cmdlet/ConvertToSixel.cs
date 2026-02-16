using System.Management.Automation;
using System.Net.Http;
using Sixel.Terminal;
using Sixel.Terminal.Models;


namespace Sixel.Cmdlet;

[Cmdlet(VerbsData.ConvertTo, "Sixel", DefaultParameterSetName = "Path")]
[Alias("cts")]
[OutputType(typeof(string))]
public sealed class ConvertSixelCmdlet : PSCmdlet {
    private static readonly HttpClient _httpClient = new();
    [Parameter(
        HelpMessage = "InputObject from Pipeline, can be filepath or base64 encoded image.",
        Mandatory = true,
        ValueFromPipeline = true,
        ParameterSetName = "InputObject"
    )]
    [ValidateNotNullOrEmpty]
    public string? InputObject { get; set; }
    [Parameter(
        HelpMessage = "A path to a local image to convert to sixel.",
        Mandatory = true,
        ValueFromPipelineByPropertyName = true,
        Position = 0,
        ParameterSetName = "Path"
    )]
    [ValidateNotNullOrEmpty]
    [Alias("FullName")]
    public string? Path { get; set; }

    [Parameter(
        HelpMessage = "A URL of the image to download and convert to sixel.",
        Mandatory = true,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        Position = 0,
        ParameterSetName = "Url"
    )]
    [ValidateNotNullOrEmpty]
    [Alias("Uri")]
    public Uri? Url { get; set; }

    [Parameter(
        HelpMessage = "A stream of the image to convert to sixel.",
        Mandatory = true,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        Position = 0,
        ParameterSetName = "Stream"
    )]
    [ValidateNotNullOrEmpty]
    [Alias("RawContentStream", "FileStream", "InputStream", "ContentStream")]
    public Stream? Stream { get; set; }

    [Parameter(
        HelpMessage = "The maximum number of colors to use in the image."
    )]
    [ValidateRange(1, 256)]
    public int MaxColors { get; set; } = 256;

    [Parameter(
        HelpMessage = "Width of the image in character cells, the height will be scaled to maintain aspect ratio."
    )]
    [ValidateTerminalWidth()]
    public int Width { get; set; }

    [Parameter(
        HelpMessage = "Height of the image in character cells, the width will be scaled to maintain aspect ratio."
    )]
    [ValidateTerminalHeight()]
    public int Height { get; set; }

    [Parameter(
        HelpMessage = "Force the command to attempt to output image data even if the terminal does not support the protocol selected."
    )]
    public SwitchParameter Force { get; set; }

    [Parameter(
        HelpMessage = "Choose ImageProtocol to output."
    )]
    public ImageProtocol Protocol { get; set; } = ImageProtocol.Auto;

    [Parameter(
        HelpMessage = "Timeout for web request",
        ParameterSetName = "Url"
    )]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    protected override void ProcessRecord() {
        Stream? imageStream = null;
        try {
            switch (ParameterSetName) {
                case "InputObject": {
                        if (InputObject?.Length > 512) {
                            // assume it's a base64 encoded image
                            InputObject = Compatibility.TrimBase64(InputObject);
                            imageStream = new MemoryStream(Convert.FromBase64String(InputObject));
                        }
                        else {
                            /// assume it's a path to a file
                            string resolvedPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(InputObject);
                            imageStream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read);
                        }
                        break;
                    }
                case "Path": {
                        string resolvedPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
                        imageStream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read);
                    }
                    break;
                case "Url": {
                        _httpClient.Timeout = Timeout;
                        HttpResponseMessage response = _httpClient.GetAsync(Url).GetAwaiter().GetResult();
                        _ = response.EnsureSuccessStatusCode();
                        imageStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                        break;
                    }
                case "Stream": {
                        if (Stream?.CanSeek is true && Stream.Position is not 0) {
                            Stream.Position = 0;
                        }
                        imageStream = Stream;
                        break;
                    }
                default:
                    // just to stop analyzer from complaining..
                    break;
            }
            if (imageStream is null) {
                return;
            }

            (ImageSize size, string image) = ConvertTo.ConsoleImage(
                Protocol,
                imageStream,
                MaxColors,
                Width,
                Height,
                Force.IsPresent
            );
            var wrappedImage = PSObject.AsPSObject(image);
            wrappedImage.Properties.Add(new PSNoteProperty("Width", size.Width));
            wrappedImage.Properties.Add(new PSNoteProperty("Height", size.Height));
            WriteObject(wrappedImage);
        }
        catch (Exception ex) {
            WriteError(new ErrorRecord(ex, "ConvertSixelCmdlet", ErrorCategory.NotSpecified, MyInvocation.BoundParameters));
        }
        finally {
            // if someone passes a Stream object, we should not dispose it.
            // that breaks Invoke-Webrequest etc. trackd/sixel#23
            if (ParameterSetName != "Stream" && imageStream is not null) {
                imageStream.Dispose();
            }
        }
    }
}
