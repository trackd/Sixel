﻿using Sixel.Terminal.Models;
using System.Collections;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Sixel.Terminal;

public static class SizeHelper {
    public static Size GetTerminalImageSize(Image<Rgba32> image, int? maxWidth = null)
    {
        return GetTerminalImageSize(image.Width, image.Height, maxWidth);
    }
    public static Size GetTerminalImageSize(Stream image, int? maxWidth = null)
    {
        using var _image = Image.Load<Rgba32>(image);
        return GetTerminalImageSize(_image.Width, _image.Height, maxWidth);
    }

    public static Size GetTerminalImageSize(int Width, int Height, int? maxWidth = null)
    {
        var cellSize = Compatibility.GetCellSize();

        // calculate natural dimensions in cells (rounded down to ensure fit)
        var naturalCellWidth = (int)Math.Floor((double)Width / cellSize.PixelWidth);
        var naturalCellHeight = (int)Math.Floor((double)Height / cellSize.PixelHeight);

        // If no maxWidth specified, constrain to console width
        if (!maxWidth.HasValue || maxWidth.Value <= 0)
        {
            var targetCellWidth = Math.Min(naturalCellWidth, Console.WindowWidth - 2);
            if (targetCellWidth == naturalCellWidth)
            {
                return new Size(naturalCellWidth, naturalCellHeight);
            }

            // Calculate new height maintaining aspect ratio
            var targetPixelWidth = targetCellWidth * cellSize.PixelWidth;
            var targetPixelHeight = (int)Math.Round((double)Height / Width * targetPixelWidth);

            // round down height to nearest Sixel boundary (6 pixels)
            targetPixelHeight -= targetPixelHeight % 6;

            var targetCellHeight = (int)Math.Floor((double)targetPixelHeight / cellSize.PixelHeight);
            return new Size(targetCellWidth, targetCellHeight);
        }

        // maxWidth specified - honor it regardless of natural size
        var requestedCellWidth = maxWidth.Value;
        var requestedPixelWidth = requestedCellWidth * cellSize.PixelWidth;
        var requestedPixelHeight = (int)Math.Round((double)Height / Width * requestedPixelWidth);

        // round to sixel boundary
        requestedPixelHeight -= requestedPixelHeight % 6;

        var requestedCellHeight = (int)Math.Floor((double)requestedPixelHeight / cellSize.PixelHeight);
        return new Size(requestedCellWidth, requestedCellHeight);
    }

    public static Size GetImageSizeMax(int Width, int Height, int? maxWidth = null)
    {
        // this doesnt allow resize to bigger, will only scale down
        var cellSize = Compatibility.GetCellSize();

        // calculate natural dimensions in cells (rounded down to ensure fit)
        var naturalCellWidth = (int)Math.Floor((double)Width / cellSize.PixelWidth);
        var naturalCellHeight = (int)Math.Floor((double)Height / cellSize.PixelHeight);

        // adjust width if needed by maxWidth
        var targetCellWidth = maxWidth.HasValue && maxWidth.Value > 0
            ? Math.Min(naturalCellWidth, maxWidth.Value)
            : Math.Min(naturalCellWidth, Console.WindowWidth - 2);

        if (targetCellWidth == naturalCellWidth)
        {
            // No resize needed
            return new Size(naturalCellWidth, naturalCellHeight);
        }

        // Calculate new height maintaining aspect ratio
        var targetPixelWidth = targetCellWidth * cellSize.PixelWidth;
        var targetPixelHeight = (int)Math.Round((double)Height / Width * targetPixelWidth);

        // round down height to nearest Sixel boundary (6 pixels)
        targetPixelHeight -= targetPixelHeight % 6;

        var targetCellHeight = (int)Math.Floor((double)targetPixelHeight / cellSize.PixelHeight);

        return new Size(targetCellWidth, targetCellHeight);
    }
}
