# Explicit Windows PowerShell compatibility fixture for the net472 backend.
param([string]$ModulePath, [string]$ResultPath)
$ErrorActionPreference = 'Stop'
Import-Module $ModulePath -ErrorAction Stop
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

public static class QuantizerChecks {
    static Type raster;
    static ConstructorInfo constructor;
    static MethodInfo quantize;
    static PropertyInfo pixels;

    static int[] Run(Bitmap source, int count) {
        object buffer = constructor.Invoke(new object[] { source });
        quantize.Invoke(buffer, new object[] { count });
        return (int[])pixels.GetValue(buffer, null);
    }

    public static Dictionary<string, object> Check(Assembly assembly) {
        raster = assembly.GetType("Sixel.Terminal.DrawingRaster", true);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        constructor = raster.GetConstructor(flags, null, new[] { typeof(Bitmap) }, null);
        quantize = raster.GetMethod("Quantize", flags);
        pixels = raster.GetProperty("Pixels", flags);
        var result = new Dictionary<string, object>();
        using (var image = new Bitmap(3, 1, PixelFormat.Format32bppArgb)) {
            image.SetPixel(0, 0, Color.FromArgb(255, 100, 100, 100));
            image.SetPixel(1, 0, Color.FromArgb(255, 101, 101, 101));
            image.SetPixel(2, 0, Color.FromArgb(0, 200, 100, 50));
            int[] exact = Run(image, 2);
            result["ExactColors"] = exact[0] == image.GetPixel(0, 0).ToArgb() && exact[1] == image.GetPixel(1, 0).ToArgb() && exact[2] == 0;
            int before = image.GetPixel(0, 0).ToArgb();
            Run(image, 1);
            result["SourceUnchanged"] = image.GetPixel(0, 0).ToArgb() == before;
        }
        using (var empty = new Bitmap(3, 5, PixelFormat.Format32bppArgb)) {
            result["Transparent"] = Array.TrueForAll(Run(empty, 2), p => p == 0);
        }
        using (var weighted = new Bitmap(4, 1, PixelFormat.Format32bppArgb)) {
            for (int x = 0; x < 3; x++) weighted.SetPixel(x, 0, Color.Black);
            weighted.SetPixel(3, 0, Color.Red);
            result["WeightedMean"] = Array.TrueForAll(Run(weighted, 1), p => p == Color.FromArgb(255, 63, 0, 0).ToArgb());
        }
        using (var gradient = new Bitmap(64, 32, PixelFormat.Format32bppArgb)) {
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 64; x++) gradient.SetPixel(x, y, Color.FromArgb(x * 4, x * 4, x * 4));
            int[] first = Run(gradient, 2);
            int[] second = Run(gradient, 16); // An unrelated palette must not contaminate the next frame.
            second = Run(gradient, 2);
            bool identical = true;
            for (int i = 0; i < first.Length; i++) identical &= first[i] == second[i];
            result["Deterministic"] = identical;
            var column = new HashSet<int>();
            long center = 0;
            for (int y = 0; y < 32; y++) {
                column.Add(first[y * 64 + 32]);
                for (int x = 24; x < 40; x++) center += first[y * 64 + x] & 255;
            }
            result["DitherColumnColors"] = column.Count;
            result["DitherCenterMean"] = center / (32.0 * 16);
        }
        bool bounded = true, alpha = true;
        foreach (int width in new[] { 1, 3, 37 }) {
            using (var image = new Bitmap(width, 17, PixelFormat.Format32bppArgb)) {
                for (int y = 0; y < 17; y++)
                    for (int x = 0; x < width; x++)
                        image.SetPixel(x, y, Color.FromArgb((x + y) % 7 == 0 ? 0 : 200, (x * 41 + y * 13) % 256, y * 15, (x * 23 + y * 7) % 256));
                foreach (int limit in new[] { 1, 2, 16, 256 }) {
                    int[] output = Run(image, limit);
                    var colors = new HashSet<int>();
                    for (int i = 0; i < output.Length; i++) {
                        bool transparent = ((i % width) + (i / width)) % 7 == 0;
                        alpha &= transparent ? output[i] == 0 : (uint)output[i] >> 24 == 255;
                        if (!transparent) colors.Add(output[i]);
                    }
                    bounded &= colors.Count <= limit;
                }
            }
        }
        result["PaletteBounded"] = bounded;
        result["AlphaPreserved"] = alpha;

        int[] memory = { Color.Blue.ToArgb(), Color.White.ToArgb(), Color.Red.ToArgb(), Color.Lime.ToArgb() };
        GCHandle pin = GCHandle.Alloc(memory, GCHandleType.Pinned);
        try {
            using (var image = new Bitmap(2, 2, -8, PixelFormat.Format32bppArgb, IntPtr.Add(pin.AddrOfPinnedObject(), 8))) {
                int[] output = Run(image, 4);
                result["NegativeStride"] = output[0] == Color.Red.ToArgb() && output[1] == Color.Lime.ToArgb()
                    && output[2] == Color.Blue.ToArgb() && output[3] == Color.White.ToArgb();
            }
        }
        finally { pin.Free(); }

        Type mapper = assembly.GetType("Sixel.Terminal.PaletteDither", true);
        var random = new Random(12345);
        var palette = new int[256];
        for (int i = 0; i < palette.Length; i++) palette[i] = unchecked((int)0xff000000) | random.Next(1 << 24);
        object instance = Activator.CreateInstance(mapper, flags, null, new object[] { palette }, null);
        var nearest = (Func<int, int, int, int>)Delegate.CreateDelegate(typeof(Func<int, int, int, int>), instance, mapper.GetMethod("Nearest", flags));
        bool matches = true;
        for (int i = 0; i < 4096; i++) {
            int r = random.Next(256), g = random.Next(256), b = random.Next(256);
            int expected = palette[0], best = int.MaxValue;
            foreach (int color in palette) {
                int dr = r - ((color >> 16) & 255), dg = g - ((color >> 8) & 255), db = b - (color & 255);
                int distance = dr * dr + dg * dg + db * db;
                if (distance < best) { best = distance; expected = color; }
            }
            matches &= nearest(r, g, b) == expected;
            matches &= nearest(r, g, b) == expected; // Exercise the exact-key cache hit.
        }
        result["NearestMatchesBruteForce"] = matches;
        instance = Activator.CreateInstance(mapper, flags, null, new object[] { new[] { Color.Black.ToArgb(), Color.FromArgb(2, 0, 0).ToArgb() } }, null);
        nearest = (Func<int, int, int, int>)Delegate.CreateDelegate(typeof(Func<int, int, int, int>), instance, mapper.GetMethod("Nearest", flags));
        result["StableTie"] = nearest(1, 0, 0) == Color.Black.ToArgb();
        return result;
    }
}
'@
$checks = [QuantizerChecks]::Check([Sixel.Protocols.Sixel].Assembly)
# Three independently colored 4x4 frames, 16 colors each, with disposal-to-background.
$gifBytes = [Convert]::FromBase64String('R0lGODlhBAAEAIMAAMDAloDAeMCAgoCAZEDAWkCARgDAPACAKMBAboBAUEBAMgBAFMAAWoAAPEAAHgAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQIAwAAACwAAAAABAAEAAAIFQAfOGjAYIGCBAgOFBggwACBAAACAgAh+QQIBwAAACwAAAAABAAEAIPjxtyjxr7jhsijhqpjxqBjhowjxoIjhm7jRrSjRpZjRngjRlrjBvCjBtJjBrQjBpYIFQAXKEiA4ECBAQIMEAgA4IGDBgwCAgAh+QQICQAAACwAAAAABAAEAIOGzObGzATGjPCGjNJGzMhGjLQGzCIGjA7GTCyGTA5GTPAGTEqGDPrGDBhGDNwGDDYIFQALDBBwgACAAAYcMGjwQEECBAsCAgA7')
[Sixel.Terminal.Compatibility]::SetCellSize(1, 1)
$stream = New-Object IO.MemoryStream (,$gifBytes)
$referenceStream = New-Object IO.MemoryStream (,$gifBytes)
$reference = [Drawing.Image]::FromStream($referenceStream)
try {
    $gif = [Sixel.Protocols.GifToSixel]::ConvertGif($stream, 4, 4, 1)
    $frames = $gif.GetType().GetProperty('Sixel', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($gif)
    $independent = $gif.FrameCount -eq 3
    for ($frame = 0; $frame -lt 3; $frame++) {
        $reference.SelectActiveFrame([Drawing.Imaging.FrameDimension]::Time, $frame) | Out-Null
        $single = [Sixel.Protocols.Sixel]::ImageToSixel($reference, [Sixel.Terminal.Models.ImageSize]::new(4, 0), 4)
        $independent = $independent -and ($frames[$frame] -ceq $single)
    }
    $checks['GifFramesIndependent'] = $independent
}
finally { $reference.Dispose(); $referenceStream.Dispose(); $stream.Dispose() }
$checks | ConvertTo-Json | Set-Content -LiteralPath $ResultPath -Encoding UTF8
