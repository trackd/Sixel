using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Sixel.Protocols;

public static class KittyDev {
    /// <summary>
    /// Configuration options for Kitty graphics rendering
    /// </summary>
    public sealed class KittyImageOptions {
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

        /// <summary>Width in character cells (0 = natural size)</summary>
        public int CellWidth { get; set; }

        /// <summary>Height in character cells (0 = natural size)</summary>
        public int CellHeight { get; set; }

        /// <summary>Whether to preserve aspect ratio when resizing</summary>
        public bool PreserveAspectRatio { get; set; } = true;
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
    private static string ImageToKitty(Image<Rgba32> image, KittyImageOptions options) {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(options);
#else
        if (image is null)
            throw new ArgumentNullException(nameof(image));
        if (options is null)
            throw new ArgumentNullException(nameof(options));
#endif

        // Apply resizing if dimensions are specified
        if (options.CellWidth > 0 || options.CellHeight > 0) {
            ApplyResizing(image, options);
        }

        // Convert to PNG format efficiently
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);

        return ProcessImageData(ms.ToArray(), options);
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
        var sb = new StringBuilder(128);

        sb.Append('\u001b'); // ESC as char for better performance
        sb.Append("_G");
        sb.Append("a=d");

        if (deleteMode != 'a') {
            sb.Append(",d=");
            sb.Append(freeMemory
                ? char.ToUpper(deleteMode, CultureInfo.InvariantCulture)
                : char.ToLower(deleteMode, CultureInfo.InvariantCulture));
        }

        if (imageId > 0) {
            sb.Append(",i=");
            sb.Append(imageId);
        }

        if (placementId > 0) {
            sb.Append(",p=");
            sb.Append(placementId);
        }

        sb.Append(Constants.ST);
        return sb.ToString();
    }

    /// <summary>
    /// Applies resizing logic to the image based on the specified options.
    /// Uses efficient calculations with proper aspect ratio handling.
    /// </summary>
    private static void ApplyResizing(Image<Rgba32> image, KittyImageOptions options) {
        CellSize cellSize = Compatibility.GetCellSize();
        int targetPixelWidth, targetPixelHeight;

        if (options.CellWidth > 0 && options.CellHeight > 0) {
            // Both specified - use exact pixel dimensions
            targetPixelWidth = options.CellWidth * cellSize.PixelWidth;
            targetPixelHeight = options.CellHeight * cellSize.PixelHeight;
        }
        else if (options.CellWidth > 0) {
            // Width specified - calculate height maintaining aspect ratio
            targetPixelWidth = options.CellWidth * cellSize.PixelWidth;
            targetPixelHeight = options.PreserveAspectRatio ? (int)Math.Round((double)image.Height / image.Width * targetPixelWidth) : image.Height;
        }
        else if (options.CellHeight > 0) {
            // Height specified - calculate width maintaining aspect ratio
            targetPixelHeight = options.CellHeight * cellSize.PixelHeight;
            targetPixelWidth = options.PreserveAspectRatio ? (int)Math.Round((double)image.Width / image.Height * targetPixelHeight) : image.Width;
        }
        else {
            // Fallback to original dimensions
            targetPixelWidth = image.Width;
            targetPixelHeight = options.CellHeight * cellSize.PixelHeight;
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
            sb.Append('\u001b'); // ESC as char for better performance
            sb.Append("_G");

            if (isFirstChunk) {
                BuildFirstChunkParameters(sb, options);
                isFirstChunk = false;
            }

            int remaining = base64Data.Length - pos;
            int chunkSize = Math.Min(Constants.KittychunkSize, remaining);
            string chunk = base64Data.Substring(pos, chunkSize);
            pos += chunkSize;

            // Add chunk continuation flag
            sb.Append(pos < base64Data.Length ? ",m=1" : ",m=0");

            if (chunk.Length > 0) {
                sb.Append(';');
                sb.Append(chunk);
            }

            sb.Append(Constants.ST);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the first chunk parameters efficiently.
    /// </summary>
    private static void BuildFirstChunkParameters(StringBuilder sb, KittyImageOptions options) {
        sb.Append("a=T,f=100"); // Transmit and display PNG

        if (options.UseCompression)
            sb.Append(",o=z");

        if (options.ImageId > 0) {
            sb.Append(",i=");
            sb.Append(options.ImageId);
        }

        if (options.PlacementId > 0) {
            sb.Append(",p=");
            sb.Append(options.PlacementId);
        }

        if (options.SuppressResponses > 0) {
            sb.Append(",q=");
            sb.Append(options.SuppressResponses);
        }

        if (options.ZIndex != 0) {
            sb.Append(",z=");
            sb.Append(options.ZIndex);
        }

        if (options.XOffset > 0) {
            sb.Append(",X=");
            sb.Append(options.XOffset);
        }

        if (options.YOffset > 0) {
            sb.Append(",Y=");
            sb.Append(options.YOffset);
        }

        if (options.CellWidth > 0) {
            sb.Append(",c=");
            sb.Append(options.CellWidth);
        }

        if (options.CellHeight > 0) {
            sb.Append(",r=");
            sb.Append(options.CellHeight);
        }
    }
}
