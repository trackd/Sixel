using System.Management.Automation;
using System.Net.Http;
using System.Text.RegularExpressions;
using Sixel.Protocols;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;


namespace Sixel.Cmdlet;

[Cmdlet(VerbsData.ConvertTo, "Kitty", DefaultParameterSetName = "Path")]
[Alias("ctk", "kitty")]
[OutputType(typeof(string))]
public sealed class ConvertKittyCmdlet : PSCmdlet {
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

    // Kitty Graphics Protocol Advanced Options

    [Parameter(
        HelpMessage = "Image ID for referencing (0 = auto-assign)."
    )]
    [ValidateRange(0, uint.MaxValue)]
    public uint ImageId { get; set; }

    [Parameter(
        HelpMessage = "Placement ID for multiple instances of the same image."
    )]
    [ValidateRange(0, uint.MaxValue)]
    public uint PlacementId { get; set; }

    [Parameter(
        HelpMessage = "Use ZLIB compression for bandwidth optimization (default: true)."
    )]
    public SwitchParameter NoCompression { get; set; }

    [Parameter(
        HelpMessage = "Response suppression (0=allow, 1=suppress OK, 2=suppress errors)."
    )]
    [ValidateRange(0, 2)]
    public int SuppressResponses { get; set; }

    [Parameter(
        HelpMessage = "Z-index for layering (-1 = under text, 0 = default, >0 = above text)."
    )]
    public int ZIndex { get; set; }

    [Parameter(
        HelpMessage = "Pixel offset within first cell (X coordinate)."
    )]
    [ValidateRange(0, int.MaxValue)]
    public int XOffset { get; set; }

    [Parameter(
        HelpMessage = "Pixel offset within first cell (Y coordinate)."
    )]
    [ValidateRange(0, int.MaxValue)]
    public int YOffset { get; set; }

    [Parameter(
        HelpMessage = "Disable aspect ratio preservation when resizing."
    )]
    public SwitchParameter NoPreserveAspectRatio { get; set; }

    [Parameter(
        HelpMessage = "Use Unicode placeholders (U+10EEEE) instead of spaces. Official spec but may crash terminals without proper font support."
    )]
    public SwitchParameter UseUnicodePlaceholders { get; set; }

    [Parameter(
        HelpMessage = "Disable cursor positioning placeholders. Use if placeholders interfere with image rendering."
    )]
    public SwitchParameter NoPlaceholders { get; set; }

    [Parameter(
        HelpMessage = "Cursor movement policy (0 = move cursor after image, 1 = do not move cursor)."
    )]
    [ValidateRange(0, 1)]
    public int CursorPolicy { get; set; }

    [Parameter(
        HelpMessage = "Parent image id for relative placement."
    )]
    [ValidateRange(0, uint.MaxValue)]
    public uint ParentImageId { get; set; }

    [Parameter(
        HelpMessage = "Parent placement id for relative placement."
    )]
    [ValidateRange(0, uint.MaxValue)]
    public uint ParentPlacementId { get; set; }

    [Parameter(
        HelpMessage = "Relative horizontal offset in cells for relative placement."
    )]
    public int RelativeXCells { get; set; }

    [Parameter(
        HelpMessage = "Relative vertical offset in cells for relative placement."
    )]
    public int RelativeYCells { get; set; }

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
                        HttpResponseMessage response = _httpClient.GetAsync(Url).GetAwaiter().GetResult();
                        response.EnsureSuccessStatusCode();
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
                    break;
            }
            if (imageStream is null) {
                return;
            }

            // Check terminal support (only if Force is not specified)
            if (!Force.IsPresent) {
                ImageProtocol[] supportedProtocols = Compatibility.GetTerminalInfo().Protocol;
                bool kittySupported = supportedProtocols.Contains(ImageProtocol.KittyGraphicsProtocol);

                if (!kittySupported && !Compatibility.TerminalSupportsKitty()) {
                    WriteError(new ErrorRecord(
                        new InvalidOperationException($"Terminal does not support Kitty Graphics Protocol (detected: {string.Join(", ", supportedProtocols)}). Use -Force to override."),
                        "KittyNotSupported",
                        ErrorCategory.NotEnabled,
                        MyInvocation.BoundParameters
                    ));
                    return;
                }
            }

            // Load image
            using var loadedImage = Image.Load<Rgba32>(imageStream);

            // Calculate target size
            ImageSize targetSize = Width == 0 && Height == 0
                ? SizeHelper.GetDefaultTerminalImageSize(loadedImage)
                : SizeHelper.GetResizedCharacterCellSize(loadedImage, Width, Height);

            bool placeholdersRequested = UseUnicodePlaceholders.IsPresent;
            bool disablePlaceholders = !placeholdersRequested || NoPlaceholders.IsPresent;

            uint resolvedImageId = ImageId;
            if (placeholdersRequested && resolvedImageId == 0) {
                resolvedImageId = KittyDev.GenerateImageId();
            }

            var options = new KittyDev.KittyImageOptions {
                ImageId = resolvedImageId,
                PlacementId = PlacementId,
                UseCompression = !NoCompression.IsPresent,
                SuppressResponses = SuppressResponses,
                ZIndex = ZIndex,
                XOffset = XOffset,
                YOffset = YOffset,
                CellWidth = targetSize.Width,
                CellHeight = targetSize.Height,
                PreserveAspectRatio = !NoPreserveAspectRatio.IsPresent,
                UseUnicodePlaceholders = placeholdersRequested,
                DisablePlaceholders = disablePlaceholders,
                CursorPolicy = CursorPolicy,
                ParentImageId = ParentImageId,
                ParentPlacementId = ParentPlacementId,
                RelativeXCells = RelativeXCells,
                RelativeYCells = RelativeYCells
            };

            string kittyData = KittyDev.ImageToKitty(loadedImage, options);

            if (string.IsNullOrEmpty(kittyData)) {
                WriteError(new ErrorRecord(
                    new InvalidOperationException("Kitty conversion produced empty result. The image may be invalid or too small."),
                    "KittyConversionFailed",
                    ErrorCategory.InvalidResult,
                    MyInvocation.BoundParameters
                ));
                return;
            }

            // Wrap output with metadata
            var wrappedImage = PSObject.AsPSObject(kittyData);
            wrappedImage.Properties.Add(new PSNoteProperty("Width", targetSize.Width));
            wrappedImage.Properties.Add(new PSNoteProperty("Height", targetSize.Height));
            wrappedImage.Properties.Add(new PSNoteProperty("ImageId", resolvedImageId));
            wrappedImage.Properties.Add(new PSNoteProperty("PlacementId", PlacementId));
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
