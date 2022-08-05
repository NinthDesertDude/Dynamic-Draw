using PaintDotNet;
using System;
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
    }
}