using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;


// WIP, not working yet..

namespace Sixel.Protocols;

internal static class KittyDev {
    /// <summary>
    /// Configuration options for Kitty graphics rendering
    /// </summary>
    public sealed class KittyImageOptions {
        /// <summary>Action for the graphics command (default: transmit+display).</summary>
        public char Action { get; set; } = 'T';

        /// <summary>Image ID for referencing (0 = auto-assign)</summary>
        public uint ImageId { get; set; }

        /// <summary>Placement ID for multiple instances</summary>
        public uint PlacementId { get; set; }

        /// <summary>Whether to use ZLIB compression for bandwidth optimization</summary>
        public bool UseCompression { get; set; } = true;

        /// <summary>Response suppression (0=allow, 1=suppress OK, 2=suppress errors)</summary>
        public int SuppressResponses { get; set; }

        /// <summary>Z-index for layering (-1 = under text, 0 = default, >0 = above text)</summary>
        public int ZIndex { get; set; }

        /// <summary>Pixel offset within first cell (X coordinate)</summary>
        public int XOffset { get; set; }

        /// <summary>Pixel offset within first cell (Y coordinate)</summary>
        public int YOffset { get; set; }

        /// <summary>Cursor movement policy (0 = move, 1 = no move)</summary>
        public int CursorPolicy { get; set; }

        /// <summary>Width in character cells (0 = natural size)</summary>
        public int CellWidth { get; set; }

        /// <summary>Height in character cells (0 = natural size)</summary>
        public int CellHeight { get; set; }

        /// <summary>Whether to preserve aspect ratio when resizing</summary>
        public bool PreserveAspectRatio { get; set; } = true;

        /// <summary>Whether to use Unicode placeholders (U+10EEEE) or simple spaces</summary>
        public bool UseUnicodePlaceholders { get; set; }

        /// <summary>Whether to disable placeholders entirely</summary>
        public bool DisablePlaceholders { get; set; }

        /// <summary>Parent image id for relative placement</summary>
        public uint ParentImageId { get; set; }

        /// <summary>Parent placement id for relative placement</summary>
        public uint ParentPlacementId { get; set; }

        /// <summary>Relative horizontal offset in cells</summary>
        public int RelativeXCells { get; set; }

        /// <summary>Relative vertical offset in cells</summary>
        public int RelativeYCells { get; set; }

        /// <summary>Uncompressed data size for PNG when compression is enabled</summary>
        public int SourceDataSize { get; set; }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ImageToKitty(string File, KittyImageOptions? options) {
        using var stream = new FileStream(File, FileMode.Open, FileAccess.Read);
        return options is not null ? ImageToKitty(stream, options) : ImageToKitty(stream);
    }
    /// <summary>
    /// Converts an image stream to Kitty Graphics Protocol format with default options.
    /// </summary>
    /// <param name="imageStream">The image stream to convert</param>
    /// <returns>The Kitty Graphics Protocol formatted string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ImageToKitty(Stream imageStream) =>
        ImageToKitty(imageStream, new KittyImageOptions());

    /// <summary>
    /// Converts an image stream to Kitty Graphics Protocol format with specified options.
    /// </summary>
    /// <param name="imageStream">The image stream to convert</param>
    /// <param name="options">Configuration options for the conversion</param>
    /// <returns>The Kitty Graphics Protocol formatted string</returns>
    /// <exception cref="ArgumentNullException">Thrown when imageStream or options is null</exception>
    private static string ImageToKitty(Stream imageStream, KittyImageOptions options) {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(imageStream);
        ArgumentNullException.ThrowIfNull(options);
#else
        if (imageStream is null)
            throw new ArgumentNullException(nameof(imageStream));
        if (options is null)
            throw new ArgumentNullException(nameof(options));
#endif
        // Read image data efficiently
        using var ms = new MemoryStream();
        imageStream.CopyTo(ms);
        byte[] imageBytes = ms.ToArray();

        return ProcessImageData(imageBytes, options);
    }

    /// <summary>
    /// Enhanced image conversion with placement control and advanced features.
    /// </summary>
    /// <param name="image">The image to convert</param>
    /// <param name="options">Configuration options for the conversion</param>
    /// <returns>The Kitty Graphics Protocol formatted string</returns>
    /// <exception cref="ArgumentNullException">Thrown when image or options is null</exception>
    public static string ImageToKitty(Image<Rgba32> image, KittyImageOptions options) {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(options);
#else
        if (image is null)
            throw new ArgumentNullException(nameof(image));
        if (options is null)
            throw new ArgumentNullException(nameof(options));
#endif

        EnsureUnicodePlaceholderRequirements(options);

        // Store original cell dimensions for placeholder calculation
        var cellSize = new ImageSize(options.CellWidth, options.CellHeight);
        bool hasCellSize = cellSize.Width > 0 && cellSize.Height > 0;

        // Apply resizing if dimensions are specified
        if (options.CellWidth > 0 || options.CellHeight > 0) {
            ApplyResizing(image, options);
        }

        // Convert to PNG format efficiently
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);

        string kittyData = ProcessImageData(ms.ToArray(), options);

        if (!options.DisablePlaceholders && options.UseUnicodePlaceholders && hasCellSize) {
            string placement = BuildUnicodePlacementCommand(options, cellSize);
            kittyData += placement;
        }

        // Add placeholders to occupy screen space for correct cursor positioning
        if (!options.DisablePlaceholders && hasCellSize) {
            string placeholders = KittyPlaceholder.GeneratePlaceholders(cellSize, options.ImageId, true, options.UseUnicodePlaceholders);
            return kittyData + placeholders;
        }

        return hasCellSize && options.CursorPolicy == 0 ? kittyData + KittyPlaceholder.GenerateSimplePlaceholders(cellSize) : kittyData;
    }

    /// <summary>
    /// Creates a Kitty graphics command for deleting images with proper culture handling.
    /// </summary>
    /// <param name="deleteMode">What to delete (a=all, i=by image id, etc.)</param>
    /// <param name="imageId">Image ID for targeted deletion</param>
    /// <param name="placementId">Placement ID for targeted deletion</param>
    /// <param name="freeMemory">Whether to free memory (uppercase) or just hide (lowercase)</param>
    /// <returns>Kitty delete command</returns>
    public static string CreateDeleteCommand(char deleteMode = 'a', uint imageId = 0,
        uint placementId = 0, bool freeMemory = true) {
        StringBuilder sb = new StringBuilder(128)
            .Append('\u001b') // ESC as char for better performance
            .Append("_G")
            .Append("a=d");

        if (deleteMode != 'a') {
            sb.Append(",d=")
                .Append(freeMemory
                    ? char.ToUpper(deleteMode, CultureInfo.InvariantCulture)
                    : char.ToLower(deleteMode, CultureInfo.InvariantCulture));
        }

        if (imageId > 0) {
            sb.Append(",i=").Append(imageId);
        }

        if (placementId > 0) {
            sb.Append(",p=").Append(placementId);
        }

        return sb.Append(Constants.ST).ToString();
    }

    /// <summary>
    /// Applies resizing logic to the image based on the specified options.
    /// Uses efficient calculations with proper aspect ratio handling.
    /// Accounts for both image aspect ratio and cell aspect ratio (font ratio).
    /// </summary>
    private static void ApplyResizing(Image<Rgba32> image, KittyImageOptions options) {
        CellSize cellSize = Compatibility.GetCellSize();
        double cellAspect = cellSize.AspectRatio;
        double imageAspect = (double)image.Width / image.Height;
        int targetPixelWidth, targetPixelHeight;

        if (options.CellWidth > 0 && options.CellHeight > 0) {
            // Both specified - use exact pixel dimensions
            targetPixelWidth = options.CellWidth * cellSize.PixelWidth;
            targetPixelHeight = options.CellHeight * cellSize.PixelHeight;
        }
        else if (options.CellWidth > 0) {
            // Width specified - calculate height maintaining aspect ratio
            // Account for both image aspect and cell aspect (font ratio)
            targetPixelWidth = options.CellWidth * cellSize.PixelWidth;
            targetPixelHeight = options.PreserveAspectRatio
                ? (int)Math.Round(targetPixelWidth / imageAspect * cellAspect)
                : image.Height;
        }
        else if (options.CellHeight > 0) {
            // Height specified - calculate width maintaining aspect ratio
            // Account for both image aspect and cell aspect (font ratio)
            targetPixelHeight = options.CellHeight * cellSize.PixelHeight;
            targetPixelWidth = options.PreserveAspectRatio
                ? (int)Math.Round(targetPixelHeight * imageAspect / cellAspect)
                : image.Width;
        }
        else {
            // Fallback to original dimensions
            targetPixelWidth = image.Width;
            targetPixelHeight = image.Height;
        }

        image.Mutate(ctx => ctx.Resize(new ResizeOptions {
            Sampler = KnownResamplers.Bicubic,
            Size = new(targetPixelWidth, targetPixelHeight),
            PremultiplyAlpha = false,
        }));
    }

    /// <summary>
    /// Processes image data with compression and encoding using efficient memory management.
    /// </summary>
    private static string ProcessImageData(byte[] imageData, KittyImageOptions options) {
        if (options.SourceDataSize <= 0) {
            options.SourceDataSize = imageData.Length;
        }

        byte[] dataToEncode = imageData;

        if (options.UseCompression) {
            dataToEncode = CompressDataEfficiently(imageData);
        }

        // Use efficient base64 encoding
        string base64Image = Convert.ToBase64String(dataToEncode);
        return BuildKittyCommand(base64Image, options);
    }

    /// <summary>
    /// Efficiently compresses data using ZLIB with proper RFC 1950 compliance.
    /// </summary>
    private static byte[] CompressDataEfficiently(byte[] data) {
        using var output = new MemoryStream();

        // Write ZLIB header (RFC 1950)
        output.WriteByte(0x78); // CMF: CM=8 (deflate), CINFO=7 (32K window)
        output.WriteByte(0x9C); // FLG: FCHECK=28, FDICT=0, FLEVEL=2

        // Compress the data using DeflateStream
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true)) {
            deflate.Write(data, 0, data.Length);
        }

        // Write Adler-32 checksum
        uint checksum = ComputeAdler32(data);
        output.WriteByte((byte)((checksum >> 24) & 0xFF));
        output.WriteByte((byte)((checksum >> 16) & 0xFF));
        output.WriteByte((byte)((checksum >> 8) & 0xFF));
        output.WriteByte((byte)(checksum & 0xFF));

        return output.ToArray();
    }

    /// <summary>
    /// Efficiently computes Adler-32 checksum using optimized patterns.
    /// </summary>
    private static uint ComputeAdler32(byte[] data) {
        const uint MOD_ADLER = 65521;
        uint a = 1, b = 0;

        foreach (byte dataByte in data) {
            a = (a + dataByte) % MOD_ADLER;
            b = (b + a) % MOD_ADLER;
        }

        return (b << 16) | a;
    }

    /// <summary>
    /// Builds the complete Kitty command string using efficient string building.
    /// </summary>
    private static string BuildKittyCommand(string base64Data, KittyImageOptions options) {
        // Pre-calculate approximate size to minimize allocations
        int estimatedSize = base64Data.Length + 200;
        var sb = new StringBuilder(estimatedSize);

        int pos = 0;
        bool isFirstChunk = true;

        while (pos < base64Data.Length) {
            sb.Append('\u001b') // ESC as char for better performance
                .Append("_G");

            if (isFirstChunk) {
                BuildFirstChunkParameters(sb, options);
            }

            int remaining = base64Data.Length - pos;
            int chunkSize = Math.Min(Constants.KittychunkSize, remaining);
            string chunk = base64Data.Substring(pos, chunkSize);
            pos += chunkSize;

            // Add chunk continuation flag
            string continuationFlag = pos < base64Data.Length ? "m=1" : "m=0";
            if (isFirstChunk) {
                sb.Append(',').Append(continuationFlag);
                isFirstChunk = false;
            }
            else {
                sb.Append(continuationFlag);
            }

            if (chunk.Length > 0) {
                sb.Append(';').Append(chunk);
            }

            sb.Append(Constants.ST);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the first chunk parameters efficiently.
    /// </summary>
    private static void BuildFirstChunkParameters(StringBuilder sb, KittyImageOptions options) {
        sb.Append("a=")
            .Append(options.Action)
            .Append(",f=100"); // PNG

        if (options.UseCompression) {
            sb.Append(",o=z");
            if (options.SourceDataSize > 0) {
                sb.Append(",S=").Append(options.SourceDataSize);
            }
        }

        if (options.ImageId > 0) {
            sb.Append(",i=").Append(options.ImageId);
        }

        if (options.PlacementId > 0) {
            sb.Append(",p=").Append(options.PlacementId);
        }

        if (options.SuppressResponses > 0) {
            sb.Append(",q=").Append(options.SuppressResponses);
        }

        if (options.UseUnicodePlaceholders) {
            sb.Append(",U=1");
        }

        if (options.ZIndex != 0) {
            sb.Append(",z=").Append(options.ZIndex);
        }

        if (options.XOffset > 0) {
            sb.Append(",X=").Append(options.XOffset);
        }

        if (options.YOffset > 0) {
            sb.Append(",Y=").Append(options.YOffset);
        }

        if (options.CursorPolicy > 0) {
            sb.Append(",C=").Append(options.CursorPolicy);
        }

        if (options.CellWidth > 0) {
            sb.Append(",c=").Append(options.CellWidth);
        }

        if (options.CellHeight > 0) {
            sb.Append(",r=").Append(options.CellHeight);
        }

        if (options.ParentImageId > 0) {
            sb.Append(",P=").Append(options.ParentImageId);
        }

        if (options.ParentPlacementId > 0) {
            sb.Append(",Q=").Append(options.ParentPlacementId);
        }

        if (options.RelativeXCells != 0) {
            sb.Append(",H=").Append(options.RelativeXCells);
        }

        if (options.RelativeYCells != 0) {
            sb.Append(",V=").Append(options.RelativeYCells);
        }
    }

    private static void EnsureUnicodePlaceholderRequirements(KittyImageOptions options) {
        if (!options.UseUnicodePlaceholders) {
            return;
        }

        if (options.SuppressResponses < 2) {
            options.SuppressResponses = 2;
        }

        options.Action = 't';

        if (options.ImageId == 0) {
            options.ImageId = GenerateImageId();
        }
    }

    internal static uint GenerateImageId() {
        uint value = unchecked((uint)Environment.TickCount);
        return value == 0 ? 1u : value;
    }

    private static string BuildUnicodePlacementCommand(KittyImageOptions options, ImageSize cellSize) {
        StringBuilder sb = new StringBuilder(128)
        .Append('\u001b')
        .Append("_G")
        .Append("a=p,U=1")
        .Append(",i=")
        .Append(options.ImageId);

        if (options.SuppressResponses > 0) {
            sb.Append(",q=").Append(options.SuppressResponses);
        }

        if (cellSize.Width > 0) {
            sb.Append(",c=").Append(cellSize.Width);
        }

        if (cellSize.Height > 0) {
            sb.Append(",r=").Append(cellSize.Height);
        }
        return sb.Append(Constants.ST).ToString();
    }
}
