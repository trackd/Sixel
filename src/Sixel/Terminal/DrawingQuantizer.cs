#if NET472
namespace Sixel.Terminal;

/// <summary>Bounded RGB histogram and weighted median cut over packed ARGB pixels.</summary>
internal sealed class DrawingQuantizer {
    private ColorBin[]? histogram;

    internal void Quantize(int[] pixels, int width, int maxColors) {
        if (maxColors <= 0) return;
        // Preserve exact colors when reduction is unnecessary, including low-color
        // art whose distinct colors would otherwise share a histogram bin.
        var exact = new HashSet<int>();
        foreach (int pixel in pixels) {
            if ((pixel & unchecked((int)0xff000000)) == 0) continue;
            exact.Add(pixel & 0xffffff);
            if (exact.Count > maxColors) break;
        }
        if (exact.Count <= maxColors) {
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = (pixels[i] & unchecked((int)0xff000000)) == 0 ? 0 : pixels[i] | unchecked((int)0xff000000);
            return;
        }

        // Five bits/channel bounds histogram memory and sort work independently
        // of image size. Full-precision sums retain the actual mean of each bin.
        if (histogram is null) histogram = new ColorBin[32768];
        else Array.Clear(histogram, 0, histogram.Length);
        ColorBin[] bins = histogram;
        var occupied = new List<int>();
        foreach (int pixel in pixels) {
            if ((pixel & unchecked((int)0xff000000)) == 0) continue;
            int index = ((pixel >> 9) & 0x7c00) | ((pixel >> 6) & 0x3e0) | ((pixel >> 3) & 31);
            ref ColorBin bin = ref bins[index];
            if (bin.Count == 0) occupied.Add(index);
            bin.Count++;
            bin.Red += (pixel >> 16) & 255;
            bin.Green += (pixel >> 8) & 255;
            bin.Blue += pixel & 255;
        }
        foreach (int index in occupied) {
            ref ColorBin bin = ref bins[index];
            bin.Color = (int)(bin.Red / bin.Count) << 16 | (int)(bin.Green / bin.Count) << 8 | (int)(bin.Blue / bin.Count);
        }
        var boxes = new List<ColorBox>(Math.Min(maxColors, occupied.Count)) { new(occupied, bins) };
        IComparer<int>[] comparers = [new ChannelComparer(bins, 16), new ChannelComparer(bins, 8), new ChannelComparer(bins, 0)];
        while (boxes.Count < maxColors) {
            ColorBox? selected = null;
            foreach (ColorBox box in boxes) {
                if (box.Colors.Count > 1 && (selected is null || box.Range > selected.Range)) selected = box;
            }
            if (selected is null) break;
            selected.Colors.Sort(comparers[(16 - selected.Channel) / 8]);
            long half = selected.Weight / 2;
            long accumulated = 0;
            int split = 0;
            do { accumulated += bins[selected.Colors[split++]].Count; }
            while (accumulated < half && split < selected.Colors.Count - 1);
            var upper = selected.Colors.GetRange(split, selected.Colors.Count - split);
            selected.Colors.RemoveRange(split, selected.Colors.Count - split);
            // Only these two boxes changed. All other extrema remain cached.
            selected.Measure(bins);
            boxes.Add(new ColorBox(upper, bins));
        }
        var palette = new int[boxes.Count];
        for (int i = 0; i < boxes.Count; i++) {
            ColorBox box = boxes[i];
            palette[i] = unchecked((int)0xff000000) | (int)(box.Red / box.Weight) << 16 |
                (int)(box.Green / box.Weight) << 8 | (int)(box.Blue / box.Weight);
        }
        new PaletteDither(palette).Apply(pixels, width);
    }

    private struct ColorBin {
        internal int Count;
        internal int Color;
        internal long Red;
        internal long Green;
        internal long Blue;
    }

    private sealed class ColorBox {
        internal List<int> Colors { get; }
        internal int Channel { get; private set; }
        internal int Range { get; private set; }
        internal long Weight { get; private set; }
        internal long Red { get; private set; }
        internal long Green { get; private set; }
        internal long Blue { get; private set; }

        internal ColorBox(List<int> colors, ColorBin[] bins) {
            Colors = colors;
            Measure(bins);
        }

        internal void Measure(ColorBin[] bins) {
            int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
            Weight = Red = Green = Blue = 0;
            foreach (int index in Colors) {
                ref ColorBin bin = ref bins[index];
                int r = bin.Color >> 16, g = (bin.Color >> 8) & 255, b = bin.Color & 255;
                minR = Math.Min(minR, r); maxR = Math.Max(maxR, r);
                minG = Math.Min(minG, g); maxG = Math.Max(maxG, g);
                minB = Math.Min(minB, b); maxB = Math.Max(maxB, b);
                Weight += bin.Count;
                Red += bin.Red; Green += bin.Green; Blue += bin.Blue;
            }
            Channel = 16;
            Range = maxR - minR;
            if (maxG - minG > Range) { Channel = 8; Range = maxG - minG; }
            if (maxB - minB > Range) { Channel = 0; Range = maxB - minB; }
        }
    }

    private sealed class ChannelComparer(ColorBin[] bins, int shift) : IComparer<int> {
        public int Compare(int x, int y) => ((bins[x].Color >> shift) & 255).CompareTo((bins[y].Color >> shift) & 255);
    }
}
#endif
