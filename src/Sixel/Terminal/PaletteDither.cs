#if NET472
namespace Sixel.Terminal;

/// <summary>Serpentine Floyd-Steinberg diffusion with exact, cached palette lookup.</summary>
internal sealed class PaletteDither {
    private int[] colors;
    private Node[] nodes;
    private int root;
    private readonly int[] cacheKeys = new int[16384];
    private readonly int[] cacheColors = new int[16384];

    internal PaletteDither(int[] colors) {
        this.colors = colors;
        nodes = new Node[colors.Length];
        root = BuildTree();
    }

    internal void ChangePalette(int[] palette) {
        colors = palette;
        if (nodes.Length < palette.Length) nodes = new Node[palette.Length];
        // Keys gate every cache read. Colors from the previous frame become unreachable.
        Array.Clear(cacheKeys, 0, cacheKeys.Length);
        root = BuildTree();
    }

    private int BuildTree() {
        var order = new int[colors.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        return Build(order, 0, order.Length);
    }

    private int Build(int[] order, int start, int count) {
        if (count == 0) return -1;
        int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
        for (int i = start; i < start + count; i++) {
            int color = colors[order[i]], r = (color >> 16) & 255, g = (color >> 8) & 255, b = color & 255;
            minR = Math.Min(minR, r); maxR = Math.Max(maxR, r);
            minG = Math.Min(minG, g); maxG = Math.Max(maxG, g);
            minB = Math.Min(minB, b); maxB = Math.Max(maxB, b);
        }
        int shift = maxG - minG > maxR - minR ? 8 : 16;
        if (maxB - minB > Math.Max(maxR - minR, maxG - minG)) shift = 0;
        Array.Sort(order, start, count, Comparer<int>.Create((a, b) =>
            ((colors[a] >> shift) & 255).CompareTo((colors[b] >> shift) & 255)));
        int middle = start + count / 2;
        int index = order[middle];
        nodes[index] = new Node {
            Shift = shift,
            Left = Build(order, start, middle - start),
            Right = Build(order, middle + 1, start + count - middle - 1)
        };
        return index;
    }

    internal int Nearest(int red, int green, int blue) {
        int rgb = red << 16 | green << 8 | blue;
        int slot = (int)(unchecked((uint)rgb * 2654435761u) >> 18);
        if (cacheKeys[slot] == rgb + 1) return cacheColors[slot];
        int best = 0, distance = int.MaxValue;
        Search(root, red, green, blue, ref best, ref distance);
        cacheKeys[slot] = rgb + 1;
        return cacheColors[slot] = colors[best];
    }

    private void Search(int index, int red, int green, int blue, ref int best, ref int distance) {
        if (index < 0) return;
        int color = colors[index];
        int dr = red - ((color >> 16) & 255), dg = green - ((color >> 8) & 255), db = blue - (color & 255);
        int squared = dr * dr + dg * dg + db * db;
        if (squared < distance || (squared == distance && index < best)) { best = index; distance = squared; }
        Node node = nodes[index];
        int delta = node.Shift == 16 ? dr : node.Shift == 8 ? dg : db;
        Search(delta < 0 ? node.Left : node.Right, red, green, blue, ref best, ref distance);
        if (delta * delta <= distance)
            Search(delta < 0 ? node.Right : node.Left, red, green, blue, ref best, ref distance);
    }

    internal void Apply(int[] pixels, int width) {
        var current = new int[checked((width + 2) * 3)];
        var next = new int[current.Length];
        for (int y = 0; y < pixels.Length / width; y++) {
            int rowStart = y * width;
            bool reverse = (y & 1) != 0;
            int step = reverse ? -1 : 1;
            for (int x = reverse ? width - 1 : 0; x >= 0 && x < width; x += step) {
                int pixel = pixels[rowStart + x];
                if ((pixel & unchecked((int)0xff000000)) == 0) { pixels[rowStart + x] = 0; continue; }
                int at = (x + 1) * 3;
                int red = Correct((pixel >> 16) & 255, current[at]);
                int green = Correct((pixel >> 8) & 255, current[at + 1]);
                int blue = Correct(pixel & 255, current[at + 2]);
                int color = Nearest(red, green, blue);
                pixels[rowStart + x] = color;
                Spread(red - ((color >> 16) & 255), at, step * 3, current, next);
                Spread(green - ((color >> 8) & 255), at + 1, step * 3, current, next);
                Spread(blue - (color & 255), at + 2, step * 3, current, next);
            }
            (current, next) = (next, current);
            Array.Clear(next, 0, next.Length);
        }
    }

    private static int Correct(int value, int error) => Math.Max(0, Math.Min(255,
        value + (error >= 0 ? error + 8 : error - 8) / 16));

    private static void Spread(int error, int at, int step, int[] current, int[] next) {
        current[at + step] += error * 7;
        next[at - step] += error * 3;
        next[at] += error * 5;
        next[at + step] += error;
    }

    private struct Node {
        internal int Shift;
        internal int Left;
        internal int Right;
    }
}
#endif
