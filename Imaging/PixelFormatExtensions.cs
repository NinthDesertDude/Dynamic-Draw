using System;
using PaintDotNet;
using PaintDotNet.Imaging;

using GdipPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace DynamicDraw.Imaging
{
    internal static class PixelFormatExtensions
    {
        public static GdipPixelFormat ToGdipPixelFormat(this in PixelFormat format)
        {
            if (format == PixelFormats.Bgr24)
            {
                return GdipPixelFormat.Format24bppRgb;
            }
            else if (format == PixelFormats.Bgra32)
            {
                return GdipPixelFormat.Format32bppArgb;
            }
            else if (format == PixelFormats.Pbgra32)
            {
                return GdipPixelFormat.Format32bppPArgb;
            }
            else
            {
                throw new ArgumentException($"Do not currently have a mapping from {format.GetName()} to System.Drawing.Imaging.PixelFormat");
            }
        }

        public static PixelFormat ToWicPixelFormat(this GdipPixelFormat format)
        {
            return format switch
            {
                GdipPixelFormat.Format24bppRgb => PixelFormats.Bgr24,
                GdipPixelFormat.Format32bppArgb => PixelFormats.Bgra32,
                GdipPixelFormat.Format32bppPArgb => PixelFormats.Pbgra32,
                _ => throw ExceptionUtil.InvalidEnumArgumentException(format)
            };
        }
    }
}
