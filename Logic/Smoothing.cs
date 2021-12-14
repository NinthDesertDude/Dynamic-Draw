using System.Collections.Generic;
using System.Drawing.Drawing2D;

namespace DynamicDraw
{
    public static class CmbxSmoothing
    {
        /// <summary>
        /// Represents the brush smoothing mode when applying a brush stroke.
        /// </summary>
        public enum Smoothing
        {
            /// <summary>
            /// Bilinear antialiasing (regular speed).
            /// </summary>
            Normal = 0,

            /// <summary>
            /// Bicubic antialiasing (slow).
            /// </summary>
            High = 1,

            /// <summary>
            /// Nearest neighbor interpolation. No antialiasing, and fast.
            /// </summary>
            Jagged = 2
        }

        public readonly static Dictionary<Smoothing, InterpolationMode> SmoothingToInterpolationMode = new()
        {
            { Smoothing.Normal, InterpolationMode.Bilinear },
            { Smoothing.High, InterpolationMode.HighQualityBicubic },
            { Smoothing.Jagged, InterpolationMode.NearestNeighbor }
        };
    }
}
