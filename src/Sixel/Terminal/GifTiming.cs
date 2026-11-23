namespace Sixel.Terminal;

/// <summary>GIF frame timing as browsers apply it.</summary>
internal static class GifTiming {
    internal const int DefaultDelay = 100;

    /// <summary>
    /// GIF delays of 0 or 1 centisecond play at 100 ms in browsers, and many GIFs rely on it.
    /// Kitty treats a non-positive gap as a gapless frame that is never shown.
    /// </summary>
    internal static int Normalize(int milliseconds) => milliseconds <= 10 ? DefaultDelay : milliseconds;
}
