using PaintDotNet;
using PaintDotNet.Collections;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;

namespace DynamicDraw
{
    /// <summary>
    /// Contains utility methods centered around color math and handling.
    /// </summary>
    static class ColorUtils
    {
        /// <summary>
        /// The maximum possible number of colors a palette can have.
        /// </summary>
        public static readonly int MaxPaletteSize = 256;

        /// <summary>
        /// Lossless conversion from BGRA to HSV. Regular, non-float HSV conversion is lossy across the colorspace.
        /// </summary>
        public static HsvColorF HSVFFromBgra(ColorBgra color)
        {
            return new RgbColorF(color.R / 255f, color.G / 255f, color.B / 255f).ToHsvColorF();
        }

        /// <summary>
        /// Lossless conversion from BGRA to HSV. Regular, non-float HSV conversion is lossy across the colorspace.
        /// </summary>
        public static HsvColorF HSVFFromBgra(Color color)
        {
            return new RgbColorF(color.R / 255f, color.G / 255f, color.B / 255f).ToHsvColorF();
        }

        /// <summary>
        /// Lossless conversion from HSV to BGRA. Regular, non-float RGB conversion is lossy across the colorspace.
        /// </summary>
        public static ColorBgra HSVFToBgra(HsvColorF color)
        {
            RgbColorF col = color.ToRgbColorF();
            return ColorBgra.FromBgr(
                (byte)Math.Round(col.Blue * 255),
                (byte)Math.Round(col.Green * 255),
                (byte)Math.Round(col.Red * 255));
        }

        /// <summary>
        /// Lossless conversion from HSV to BGRA. Regular, non-float RGB conversion is lossy across the colorspace.
        /// </summary>
        public static ColorBgra HSVFToBgra(HsvColorF color, byte alpha)
        {
            RgbColorF col = color.ToRgbColorF();
            return ColorBgra.FromBgra(
                (byte)Math.Round(col.Blue * 255),
                (byte)Math.Round(col.Green * 255),
                (byte)Math.Round(col.Red * 255),
                alpha);
        }

        /// <summary>
        /// Returns the parsed color from the text, or null if it's invalid. When allowAlpha is true, attempts to parse
        /// as an 8-character color first before defaulting to a 6-character color.
        /// </summary>
        public static Color? GetColorFromText(string text, bool allowAlpha, byte? defaultAlpha = null)
        {
            if (text.StartsWith("#"))
            {
                text = text.Substring(1);
            }

            if (allowAlpha)
            {
                if (Regex.Match(text, "^([0-9]|[a-f]){8}$", RegexOptions.IgnoreCase).Success)
                {
                    return Color.FromArgb(int.Parse(text, System.Globalization.NumberStyles.HexNumber));
                }
            }

            if (Regex.Match(text, "^([0-9]|[a-f]){6}$", RegexOptions.IgnoreCase).Success)
            {
                Color parsedColor = Color.FromArgb(int.Parse("ff" + text, System.Globalization.NumberStyles.HexNumber));

                if (defaultAlpha != null)
                {
                    return Color.FromArgb(defaultAlpha.Value, parsedColor);
                }

                return parsedColor;
            }

            return null;
        }

        /// <summary>
        /// Returns the color formatted as a hex string.
        /// </summary>
        public static string GetTextFromColor(Color col)
        {
            return ((ColorBgra)col).ToHexString();
        }

        #region Palette methods
        /// <summary>
        /// Generates a palette given a palette type.
        /// </summary>
        public static List<Color> GeneratePalette(
            Bitmap refBmp,
            PaletteSpecialType type,
            int amount,
            Color primary,
            Color secondary)
        {
            List<Color> paletteColors = new List<Color>();

            if (type == PaletteSpecialType.FromImagePrimaryDistance)
            {
                var rgbList = PalletizingUtils.GeneratePalette(
                    refBmp,
                    new HashSet<(Color, float mult)> { new(primary, 1) },
                    (1, 1, 1, 4, 0, 0, 0),
                    1, 0, 0.5f, MaxPaletteSize / 4); // Arbitrarily limiting to a quarter of palette size.

                // Lists larger than the palette size often have many colors with very slight differences. Rather than
                // taking the top chunk of that list, pick elements evenly distributed across it until the palette size
                // is reached. This creates a lookup that takes an index from 0 to palette size, and gives a new index
                // following a linear spread.
                HashSet<int> indicesToUse = new() { 0 };
                if (rgbList.Count > MaxPaletteSize)
                {
                    float skipRatio = rgbList.Count / MaxPaletteSize;
                    for (int i = 1; i < MaxPaletteSize; i++)
                    {
                        indicesToUse.Add((int)(skipRatio * i));
                    }
                }

                // Captures the HSV of each color.
                Dictionary<ColorBgra, HsvColor> hsvList = new();
                rgbList.ForEach((o) => hsvList.Add(o.Key, HsvColor.FromColor(o.Key)));

                var rgbSortedList = rgbList
                    .OrderBy((o) => o.Value)
                    .ThenByDescending((o) => o.Key.A / 64)
                    .ThenByDescending((o) => hsvList[o.Key].Saturation < 20 ? 0 : 1)
                    .ThenBy((o) => hsvList[o.Key].Saturation < 20 ? 0 : hsvList[o.Key].Hue / 10)
                    .ThenByDescending((o) => hsvList[o.Key].Value)
                    .ThenBy((o) => hsvList[o.Key].Saturation < 20 ? hsvList[o.Key].Hue / 10 : hsvList[o.Key].Saturation)
                    .Select((o) => o.Key.ToColor());

                paletteColors = (paletteColors.Count > MaxPaletteSize)
                    ? rgbSortedList.Where((_, index) => indicesToUse.Contains(index)).ToList()
                    : rgbSortedList.ToList();
            }

            // Takes the top colors from the given image sorted with most used first, up to max palette size
            else if (type == PaletteSpecialType.FromImageAHVS || type == PaletteSpecialType.FromImageHVSA
                || type == PaletteSpecialType.FromImageUsage || type == PaletteSpecialType.FromImageVHSA)
            {
                var rgbList = PalletizingUtils.CountColors(refBmp);

                // Lists larger than the palette size often have many colors with very slight differences. Rather than
                // taking the top chunk of that list, pick elements evenly distributed across it until the palette size
                // is reached. This creates a lookup that takes an index from 0 to palette size, and gives a new index
                // following a linear spread.
                HashSet<int> indicesToUse = new() { 0 };
                if (rgbList.Count > MaxPaletteSize)
                {
                    float skipRatio = rgbList.Count / MaxPaletteSize;
                    for (int i = 1; i < MaxPaletteSize; i++)
                    {
                        indicesToUse.Add((int)(skipRatio * i));
                    }
                }

                // Captures the HSV of each color.
                Dictionary<ColorBgra, HsvColor> hsvList = new();
                rgbList.ForEach((o) => hsvList.Add(o.Key, HsvColor.FromColor(o.Key)));
                IEnumerable<KeyValuePair<ColorBgra, int>> rgbSortedList = null;

                rgbSortedList = type switch
                {
                    // Same as AHVS sort but sorts first by most-used pixels with full precision
                    PaletteSpecialType.FromImageUsage => rgbList
                        .OrderByDescending((o) => o.Value)
                        .ThenByDescending((o) => o.Key.A / 64)
                        .ThenByDescending((o) => hsvList[o.Key].Saturation < 20 ? 0 : 1)
                        .ThenBy((o) => hsvList[o.Key].Saturation < 20 ? 0 : hsvList[o.Key].Hue / 10)
                        .ThenByDescending((o) => hsvList[o.Key].Value)
                        .ThenBy((o) => hsvList[o.Key].Saturation < 20 ? hsvList[o.Key].Hue / 10 : hsvList[o.Key].Saturation),
                    // Sort by low S last to handle precision loss in hue, H, V, H or S, A
                    PaletteSpecialType.FromImageHVSA => rgbList
                        .OrderByDescending((o) => hsvList[o.Key].Saturation < 20 ? 0 : 1)
                        .ThenBy((o) => hsvList[o.Key].Saturation < 20 ? 0 : hsvList[o.Key].Hue / 10)
                        .ThenByDescending((o) => hsvList[o.Key].Value)
                        .ThenBy((o) => hsvList[o.Key].Saturation < 20 ? hsvList[o.Key].Hue / 10 : hsvList[o.Key].Saturation)
                        .ThenByDescending((o) => o.Key.A / 64),
                    // Sort by low S last to handle precision loss in hue, V, H, H or S, A
                    PaletteSpecialType.FromImageVHSA => rgbList
                        .OrderByDescending((o) => hsvList[o.Key].Saturation < 20 ? 0 : 1)
                        .ThenByDescending((o) => hsvList[o.Key].Value / 10)
                        .ThenBy((o) => hsvList[o.Key].Hue / 10)
                        .ThenBy((o) => hsvList[o.Key].Saturation)
                        .ThenByDescending((o) => o.Key.A / 64),
                    // Sort by A, low S last to handle precision loss in hue, H, V, H or S
                    PaletteSpecialType.FromImageAHVS => rgbList
                        .OrderByDescending((o) => o.Key.A / 64)
                        .ThenByDescending((o) => hsvList[o.Key].Saturation < 20 ? 0 : 1)
                        .ThenBy((o) => hsvList[o.Key].Saturation < 20 ? 0 : hsvList[o.Key].Hue / 10)
                        .ThenByDescending((o) => hsvList[o.Key].Value)
                        .ThenBy((o) => hsvList[o.Key].Saturation < 20 ? hsvList[o.Key].Hue / 10 : hsvList[o.Key].Saturation),
                    _ => throw new NotImplementedException("Unhandled and unexpected enum case in switch."),
                };
                paletteColors = (rgbList.Count > MaxPaletteSize)
                    ? rgbSortedList
                        .Where((_, index) => indicesToUse.Contains(index))
                        .Select((o) => o.Key.ToColor())
                        .ToList()
                    : rgbSortedList.Select((o) => o.Key.ToColor()).ToList();
            }
            // Creates a gradient in perception-corrected HSLuv color space.
            else if (type == PaletteSpecialType.PrimaryToSecondary)
            {
                for (int i = 0; i < amount; i++)
                {
                    float fraction = i / (float)amount;

                    paletteColors.Add(Color.FromArgb(
                        (int)Math.Round(primary.A + fraction * (secondary.A - primary.A)),
                        Rgb.Lerp(new Rgb(primary), new Rgb(secondary), fraction).ToColor()));
                }
            }
            // Creates a lightness gradient.
            else if (type == PaletteSpecialType.LightToDark)
            {
                int halfAmount = amount / 2;
                int remaining = amount - halfAmount;
                Color black = Color.FromArgb(primary.A, Color.Black);
                Color white = Color.FromArgb(primary.A, Color.White);

                for (int i = 0; i < halfAmount; i++)
                {
                    paletteColors.Add(ColorBgra.Lerp(black, primary, i / (float)halfAmount));
                }
                for (int i = 0; i < remaining; i++)
                {
                    paletteColors.Add(ColorBgra.Lerp(primary, white, i / (float)remaining));
                }
            }
            // Picks X nearby hues to create lightness gradients from.
            else if (
                type == PaletteSpecialType.Similar3 ||
                type == PaletteSpecialType.Similar4)
            {
                HsvColor colorAsHsv = HsvColor.FromColor(primary);

                int chunks = type == PaletteSpecialType.Similar3 ? 3 : 4;
                int hueVariance = chunks == 3 ? 60 : 40; // the difference in hue between each color

                int chunk = amount / chunks;
                int lastChunk = chunk + (amount % chunk);
                int hueRange = (chunks / 2) * hueVariance; // intended integer truncation

                for (int i = 0; i < chunks; i++)
                {
                    int thisChunk = (i != chunks - 1) ? chunk : lastChunk;
                    int hue = -hueRange + (hueVariance * i);

                    // hue pattern is like {0}, {-20, 20}, {-20, 0, 20}, {-40, -20, 20, 40} relative to the given color.
                    // to match this pattern, skip 0 when the number of accent colors is even.
                    if (chunks % 2 == 0 && hue >= 0)
                    {
                        hue += hueVariance;
                    }

                    HsvColor accentInHsv = new HsvColor(colorAsHsv.Hue, colorAsHsv.Saturation, colorAsHsv.Value);
                    int newHue = (accentInHsv.Hue + hue) % 360;
                    if (newHue < 0) { newHue = 360 + newHue; }
                    accentInHsv.Hue = newHue;

                    Color accent = Color.FromArgb(primary.A, accentInHsv.ToColor());

                    Color dark = new HsvColor(
                    accentInHsv.Hue,
                    Math.Clamp(colorAsHsv.Saturation + 50, 0, 100),
                    Math.Clamp(colorAsHsv.Value - 50, 0, 100)).ToColor();
                    dark = Color.FromArgb(primary.A, dark);

                    Color bright = new HsvColor(
                        accentInHsv.Hue,
                        Math.Clamp(colorAsHsv.Saturation - 50, 0, 100),
                        Math.Clamp(colorAsHsv.Value + 50, 0, 100)).ToColor();
                    bright = Color.FromArgb(primary.A, bright);

                    int halfChunk = thisChunk / 2;
                    int remainderChunk = thisChunk - halfChunk;

                    for (int j = 0; j < halfChunk; j++)
                    {
                        paletteColors.Add(Color.FromArgb(
                            primary.A,
                            Rgb.Lerp(
                                new Rgb(dark),
                                new Rgb(accent),
                                j / (float)halfChunk)
                            .ToColor()));
                    }
                    for (int j = 0; j < remainderChunk; j++)
                    {
                        paletteColors.Add(Color.FromArgb(
                            primary.A,
                            Rgb.Lerp(
                                new Rgb(accent),
                                new Rgb(bright),
                                j / (float)remainderChunk)
                            .ToColor()));
                    }
                }
            }
            else if (
                type == PaletteSpecialType.Complement ||
                type == PaletteSpecialType.Triadic ||
                type == PaletteSpecialType.Square ||
                type == PaletteSpecialType.SplitComplement)
            {
                HsvColor colorAsHsv = HsvColor.FromColor(primary);

                int chunks =
                    type == PaletteSpecialType.Complement ? 2 :
                    type == PaletteSpecialType.Triadic ? 3 :
                    4;

                int hueVariance =
                    chunks == 2 ? 180 :
                    chunks == 3 ? 120 :
                    type == PaletteSpecialType.Square ? 90
                    : 40;

                int chunk = amount / chunks;
                int lastChunk = chunk + (amount % chunk);
                int hueRange = chunks * hueVariance;

                for (int i = 0; i < chunks; i++)
                {
                    int thisChunk = (i != chunks - 1) ? chunk : lastChunk;

                    int hue = type == PaletteSpecialType.SplitComplement
                        ? hueVariance * i + ((i >= 2) ? 100 : 0)
                        : hueRange + (hueVariance * i);

                    HsvColor accentInHsv = new HsvColor(colorAsHsv.Hue, colorAsHsv.Saturation, colorAsHsv.Value);
                    int newHue = (accentInHsv.Hue + hue) % 360;
                    if (newHue < 0) { newHue += 360; }
                    if (newHue > 360) { newHue -= 360; }
                    accentInHsv.Hue = newHue;

                    Color accent = Color.FromArgb(primary.A, accentInHsv.ToColor());

                    Color dark = new HsvColor(
                    accentInHsv.Hue,
                    Math.Clamp(colorAsHsv.Saturation + 50, 0, 100),
                    Math.Clamp(colorAsHsv.Value - 50, 0, 100)).ToColor();
                    dark = Color.FromArgb(primary.A, dark);

                    Color bright = new HsvColor(
                        accentInHsv.Hue,
                        Math.Clamp(colorAsHsv.Saturation - 50, 0, 100),
                        Math.Clamp(colorAsHsv.Value + 50, 0, 100)).ToColor();
                    bright = Color.FromArgb(primary.A, bright);

                    int halfChunk = thisChunk / 2;
                    int remainderChunk = thisChunk - halfChunk;

                    for (int j = 0; j < halfChunk; j++)
                    {
                        paletteColors.Add(Color.FromArgb(
                            primary.A,
                            Rgb.Lerp(
                                new Rgb(dark),
                                new Rgb(accent),
                                j / (float)halfChunk)
                            .ToColor()));
                    }
                    for (int j = 0; j < remainderChunk; j++)
                    {
                        paletteColors.Add(Color.FromArgb(
                            primary.A,
                            Rgb.Lerp(
                                new Rgb(accent),
                                new Rgb(bright),
                                j / (float)remainderChunk)
                            .ToColor()));
                    }
                }
            }

            return paletteColors;
        }
        #endregion
    }
}