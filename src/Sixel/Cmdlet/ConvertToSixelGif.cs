using System.Management.Automation;
using System.Net.Http;
using System.Text.RegularExpressions;
using Sixel.Protocols;
using Sixel.Terminal;
using Sixel.Terminal.Models;


namespace Sixel.Cmdlet;

[Cmdlet(VerbsData.ConvertTo, "SixelGif", DefaultParameterSetName = "Path")]
[Alias("gif")]
[OutputType(typeof(SixelGif))]
public sealed class ConvertSixelGifCmdlet : PSCmdlet {
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    [Parameter(
        HelpMessage = "InputObject from Pipeline, can be filepath or base64 encoded image.",
        Mandatory = true,
        ValueFromPipeline = true,
        ParameterSetName = "InputObject"
    )]
    [ValidateNotNullOrEmpty]
    public string? InputObject { get; set; }
    [Parameter(
        HelpMessage = "A path to a local gif to convert to sixelgif.",
        Mandatory = true,
        ValueFromPipelineByPropertyName = true,
        Position = 0,
        ParameterSetName = "Path"
    )]
    [ValidateNotNullOrEmpty]
    [Alias("FullName")]
    public string? Path { get; set; }

    [Parameter(
        HelpMessage = "A URL of the gif to download and convert to sixelgif.",
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
        HelpMessage = "A stream of the gif to convert to sixelgif.",
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
        HelpMessage = "Force the command to attempt to output sixel data even if the terminal does not support sixel."
    )]
    public SwitchParameter Force { get; set; }

    [Parameter(
        HelpMessage = "The number of times to loop the gif. Use 0 for infinite loop."
    )]
    public int LoopCount { get; set; } = 3;
    protected override void ProcessRecord() {
        Stream? imageStream = null;
        try {
            switch (ParameterSetName) {
                case "InputObject": {
                        if (InputObject?.Length > 512) {
                            // assume it's a base64 encoded image
                            InputObject = Regex.Replace(
                                InputObject,
                                @"^data:image/\w+;base64,",
                                string.Empty,
                                RegexOptions.IgnoreCase,
                                TimeSpan.FromSeconds(1)
                            );
                            imageStream = new MemoryStream(Convert.FromBase64String(InputObject));
                        }
                        else {
                            // assume it's a path to a file
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
                        HttpResponseMessage response = _httpClient.GetAsync(Url).GetAwaiter().GetResult();
                        response.EnsureSuccessStatusCode();
                        imageStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                        break;
                    }
                case "Stream": {
                        if (Stream is not null && Stream.CanSeek && Stream.Position != 0) {
                            Stream.Position = 0;
                        }
                        imageStream = Stream;
                        break;
                    }

                default:
                    break;
            }
            if (imageStream is null) {
                return;
            }
            WriteObject(GifToSixel.ConvertGif(imageStream, MaxColors, Width, LoopCount));
        }
        catch (Exception ex) {
            WriteError(new ErrorRecord(ex, "ConvertSixelGifCmdlet", ErrorCategory.NotSpecified, MyInvocation.BoundParameters));
        }
        finally {
            if (ParameterSetName != "Stream" && imageStream is not null) {
                imageStream.Dispose();
            }
        }
    }
}
