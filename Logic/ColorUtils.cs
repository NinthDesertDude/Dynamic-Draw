using PaintDotNet;
using System;
using System.Drawing;

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
    }
}