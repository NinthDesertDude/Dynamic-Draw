using PaintDotNet;
using System;
using System.Drawing;
using System.Text.RegularExpressions;

namespace DynamicDraw
{
    /// <summary>
    /// Contains utility methods for GUI layout logic.
    /// </summary>
    static class LayoutUtils
    {
        /// <summary>
        /// Returns the position of a rectangle within the bounds of this control, according to the given alignment.
        /// </summary>
        public static Point PositionElement(
            ContentAlignment alignment,
            int elementWidth, int elementHeight,
            int containerWidth, int containerHeight)
        {
            switch (alignment)
            {
                case ContentAlignment.TopLeft:
                    return Point.Empty;
                case ContentAlignment.TopCenter:
                    return new Point((int)((containerWidth - elementWidth) / 2f), 0);
                case ContentAlignment.TopRight:
                    return new Point(containerWidth - elementWidth, 0);
                case ContentAlignment.MiddleLeft:
                    return new Point(0, (int)((containerHeight - elementHeight) / 2f));
                case ContentAlignment.MiddleCenter:
                    return new Point((int)((containerWidth - elementWidth) / 2f), (int)((containerHeight - elementHeight) / 2f));
                case ContentAlignment.MiddleRight:
                    return new Point(containerWidth - elementWidth, (int)((containerHeight - elementHeight) / 2f));
                case ContentAlignment.BottomLeft:
                    return new Point(0, containerHeight - elementHeight);
                case ContentAlignment.BottomCenter:
                    return new Point((int)((containerWidth - elementWidth) / 2f), containerHeight - elementHeight);
                case ContentAlignment.BottomRight:
                    return new Point(containerWidth - elementWidth, containerHeight - elementHeight);
                default:
                    return Point.Empty;
            }
        }
    }
}