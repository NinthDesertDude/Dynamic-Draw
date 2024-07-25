using PaintDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
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

        /// <summary>
        /// Generates a palette given a palette type.
        /// </summary>
        public static List<Color> GeneratePalette(PaletteSpecialType type, int amount, Color primary, Color secondary)
        {
            List<Color> paletteColors = new List<Color>();

            // Creates a gradient in perception-corrected HSLuv color space.
            if (type == PaletteSpecialType.PrimaryToSecondary)
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
    }
}