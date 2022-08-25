using PaintDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace DynamicDraw
{
    /// <summary>
    /// Contains drawing utility methods and lengthy standalone drawing routines.
    /// </summary>
    static class DrawingUtils
    {
        /// <summary>
        /// Caches ROIs for fast lookup since it's constantly used.
        /// </summary>
        private static readonly Dictionary<(int, int), Rectangle[]> cachedRois = new Dictionary<(int, int), Rectangle[]>();

        #region Utility bitmap operations
        /// <summary>
        /// Caps alpha to a max value, rounding everything below half maxAlpha to 0.
        /// </summary>
        /// <param name="bmp">
        /// The affected image.
        /// </param>
        /// <param name="maxAlpha">
        /// The maximum alpha (usually 255, but can be lower for when alpha is pre-applied to an image).
        /// </param>
        public static unsafe void AliasImage(Bitmap bmp, byte maxAlpha)
        {
            BitmapData bmpData = bmp.LockBits(
                bmp.GetBounds(),
                ImageLockMode.ReadOnly,
                bmp.PixelFormat);

            byte* row = (byte*)bmpData.Scan0;
            byte halfAlpha = (byte)(maxAlpha / 2);

            Rectangle[] rois = GetRois(bmp.Width, bmp.Height);
            Parallel.For(0, rois.Length, (i, loopState) =>
            {
                Rectangle roi = rois[i];
                ColorBgra unmultipliedPixel;

                for (int y = roi.Y; y < roi.Y + roi.Height; y++)
                {
                    ColorBgra* dstPtr = (ColorBgra*)(row + (y * bmpData.Stride) + (roi.X * 4));

                    for (int x = roi.X; x < roi.X + roi.Width; x++)
                    {
                        unmultipliedPixel = dstPtr->ConvertFromPremultipliedAlpha();
                        unmultipliedPixel.A = (byte)(unmultipliedPixel.A >= halfAlpha ? maxAlpha : 0);

                        float alphaFactor = unmultipliedPixel.A / 255f;

                        *dstPtr = ColorBgra.FromBgra(
                            (byte)Math.Ceiling(unmultipliedPixel.B * alphaFactor),
                            (byte)Math.Ceiling(unmultipliedPixel.G * alphaFactor),
                            (byte)Math.Ceiling(unmultipliedPixel.R * alphaFactor),
                            unmultipliedPixel.A);

                        dstPtr++;
                    }
                }
            });

            bmp.UnlockBits(bmpData);
        }

        /// <summary>
        /// Creates a modified copy of the given bitmap with the alpha capped to a max value, rounding everything below
        /// half maxAlpha to 0.
        /// </summary>
        /// <param name="bmp">
        /// The affected image.
        /// </param>
        /// <param name="maxAlpha">
        /// The maximum alpha (usually 255, but can be lower for when alpha is pre-applied to an image).
        /// </param>
        public static unsafe Bitmap AliasImageCopy(Bitmap bmp, byte maxAlpha)
        {
            Bitmap newBmp = new Bitmap(bmp.Width, bmp.Height, bmp.PixelFormat);

            BitmapData bmpData = bmp.LockBits(
                bmp.GetBounds(),
                ImageLockMode.ReadOnly,
                bmp.PixelFormat);

            BitmapData newBmpData = newBmp.LockBits(
                newBmp.GetBounds(),
                ImageLockMode.WriteOnly,
                newBmp.PixelFormat);

            byte* srcRow = (byte*)bmpData.Scan0;
            byte* dstRow = (byte*)newBmpData.Scan0;
            byte halfAlpha = (byte)(maxAlpha / 2);

            Rectangle[] rois = GetRois(bmp.Width, bmp.Height);
            Parallel.For(0, rois.Length, (i, loopState) =>
            {
                Rectangle roi = rois[i];
                ColorBgra unmultipliedPixel;

                for (int y = roi.Y; y < roi.Y + roi.Height; y++)
                {
                    ColorBgra* srcPtr = (ColorBgra*)(srcRow + (y * bmpData.Stride) + (roi.X * 4));
                    ColorBgra* dstPtr = (ColorBgra*)(dstRow + (y * bmpData.Stride) + (roi.X * 4));

                    for (int x = roi.X; x < roi.X + roi.Width; x++)
                    {
                        unmultipliedPixel = srcPtr->ConvertFromPremultipliedAlpha();
                        unmultipliedPixel.A = (byte)(unmultipliedPixel.A >= halfAlpha ? maxAlpha : 0);

                        float alphaFactor = unmultipliedPixel.A / 255f;

                        *dstPtr = ColorBgra.FromBgra(
                            (byte)Math.Ceiling(unmultipliedPixel.B * alphaFactor),
                            (byte)Math.Ceiling(unmultipliedPixel.G * alphaFactor),
                            (byte)Math.Ceiling(unmultipliedPixel.R * alphaFactor),
                            unmultipliedPixel.A);

                        srcPtr++;
                        dstPtr++;
                    }
                }
            });

            bmp.UnlockBits(bmpData);
            newBmp.UnlockBits(newBmpData);

            return newBmp;
        }

        /// <summary>
        /// Overwrites RGB channels and multiplies alpha.
        /// </summary>
        /// <param name="img">
        /// The affected image.
        /// </param>
        /// <param name="color">The color to overwrite the image with.</param>
        /// <param name="alpha">A value from 0 to 1 to multiply with.</param>
        public static unsafe void ColorImage(Bitmap img, Color? col, float alpha)
        {
            BitmapData bmpData = img.LockBits(
                img.GetBounds(),
                ImageLockMode.ReadOnly,
                img.PixelFormat);

            byte* row = (byte*)bmpData.Scan0;
            Color color = col ?? default;

            Rectangle[] rois = GetRois(img.Width, img.Height);
            Parallel.For(0, rois.Length, (i, loopState) =>
            {
                Rectangle roi = rois[i];
                ColorBgra pixel;
                float alphaFactor;

                for (int y = roi.Y; y < roi.Y + roi.Height; y++)
                {
                    ColorBgra* dstPtr = (ColorBgra*)(row + (y * bmpData.Stride) + (roi.X * 4));

                    for (int x = roi.X; x < roi.X + roi.Width; x++)
                    {
                        if (col != null)
                        {
                            *dstPtr = ColorBgra.FromBgra(color.B, color.G, color.R, (byte)(dstPtr->A * alpha)).ConvertToPremultipliedAlpha();
                        }
                        else
                        {
                            alphaFactor = (dstPtr->A / 255f) * alpha;
                            pixel = dstPtr->ConvertFromPremultipliedAlpha();

                            // Overwrite values (except for locked channels). Premultiply by hand to use Ceiling(),
                            // which avoids a rounding error seen in ConvertToPremultipliedAlpha
                            dstPtr->B = (byte)Math.Ceiling(pixel.B * alphaFactor);
                            dstPtr->G = (byte)Math.Ceiling(pixel.G * alphaFactor);
                            dstPtr->R = (byte)Math.Ceiling(pixel.R * alphaFactor);
                            dstPtr->A = pixel.A;
                        }

                        dstPtr++;
                    }
                }
            });

            img.UnlockBits(bmpData);
        }

        /// <summary>
        /// Returns a new GDI+ bitmap from the specified Paint.Net surface.
        /// </summary>
        /// <param name="surface">The surface.</param>
        /// <returns>The created bitmap.</returns>
        public static unsafe Bitmap CreateBitmapFromSurface(Surface surface)
        {
            Bitmap image = new Bitmap(surface.Width, surface.Height, PixelFormat.Format32bppPArgb);

            BitmapData bitmapData = image.LockBits(
                image.GetBounds(),
                ImageLockMode.WriteOnly,
                image.PixelFormat);

            byte* dstScan0 = (byte*)bitmapData.Scan0;
            int dstStride = bitmapData.Stride;

            for (int y = 0; y < surface.Height; y++)
            {
                ColorBgra* src = surface.GetRowPointerUnchecked(y);
                // The ColorBgra structure matches the memory layout
                // of the 32bppArgb and 32bppPArgb pixel formats.
                ColorBgra* dst = (ColorBgra*)(dstScan0 + (y * dstStride));

                for (int x = 0; x < surface.Width; x++)
                {
                    dst->Bgra = src->ConvertToPremultipliedAlpha().Bgra;

                    src++;
                    dst++;
                }
            }

            image.UnlockBits(bitmapData);

            return image;
        }

        /// <summary>
        /// Returns a new bitmap that resembles the original, but in the given format.
        /// </summary>
        public static Bitmap FormatImage(Bitmap img, PixelFormat format)
        {
            Bitmap clone = new Bitmap(img.Width, img.Height, format);
            using (Graphics gr = Graphics.FromImage(clone))
            {
                gr.PixelOffsetMode = PixelOffsetMode.Half;
                gr.SmoothingMode = SmoothingMode.None;
                gr.DrawImage(img, 0, 0, img.Width, img.Height);
            }

            return clone;
        }

        /// <summary>
        /// Returns a new bitmap that's padded to be square.
        /// </summary>
        /// <param name="img">
        /// The image to pad. The original is untouched.
        /// </param>
        public static Bitmap MakeBitmapSquare(Bitmap img)
        {
            //Exits if it's already square.
            if (img.Width == img.Height)
            {
                return new Bitmap(img);
            }

            //Creates a new bitmap with the minimum square size.
            int size = Math.Max(img.Height, img.Width);
            Bitmap newImg = new Bitmap(size, size, PixelFormat.Format32bppPArgb);

            using (Graphics graphics = Graphics.FromImage(newImg))
            {
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.PixelOffsetMode = PixelOffsetMode.None;

                graphics.DrawImage(img,
                    (size - img.Width) / 2f,
                    (size - img.Height) / 2f,
                    img.Width, img.Height);
            }

            return newImg;
        }

        /// <summary>
        /// Overwrites the alpha channel of the image using each pixel's
        /// brightness after testing to see the alpha is always opaque.
        /// </summary>
        /// <param name="img">
        /// The affected image.
        /// </param>
        /// <param name="alpha">A value from 0 to 1 to multiply with.</param>
        public static unsafe Bitmap MakeTransparent(Bitmap img)
        {
            Bitmap image = FormatImage(img, PixelFormat.Format32bppPArgb);

            BitmapData bmpData = image.LockBits(
                image.GetBounds(),
                ImageLockMode.ReadWrite,
                image.PixelFormat);

            //The top left pixel's address, from which the rest can be found.
            byte* row = (byte*)bmpData.Scan0;

            //Iterates through each pixel and tests if the image is
            //transparent. A transparent image won't be converted.
            bool isTransparent = false;

            Rectangle[] rois = GetRois(image.Width, image.Height);
            Parallel.For(0, rois.Length, (i, loopState) =>
            {
                Rectangle roi = rois[i];

                for (int y = roi.Y; y < roi.Y + roi.Height; y++)
                {
                    ColorBgra* dstPtr = (ColorBgra*)(row + (y * bmpData.Stride) + (roi.X * 4));

                    for (int x = roi.X; x < roi.X + roi.Width; x++)
                    {
                        if (dstPtr->A != 255)
                        {
                            isTransparent = true;
                            loopState.Stop();
                            return;
                        }

                        dstPtr++;
                    }
                }
            });

            if (isTransparent)
            {
                image.UnlockBits(bmpData);
                return img;
            }

            //Iterates through each pixel to apply the change.
            Parallel.For(0, rois.Length, (i, loopState) =>
            {
                Rectangle roi = rois[i];

                for (int y = roi.Y; y < roi.Y + roi.Height; y++)
                {
                    ColorBgra* dst = (ColorBgra*)(row + (y * bmpData.Stride) + (roi.X * 4));

                    for (int x = roi.X; x < roi.X + roi.Width; x++)
                    {
                        byte newAlpha = (byte)Math.Ceiling((dst->R + dst->G + dst->B) / 3d);
                        float alphaFactor = newAlpha / 255f;

                        //Sets the alpha channel based on its intensity.
                        dst->Bgra = dst->ConvertFromPremultipliedAlpha().Bgra;

                        // Overwrite values (except for locked channels). Premultiply by hand to use Ceiling(),
                        // which avoids a rounding error seen in ConvertToPremultipliedAlpha
                        dst->B = (byte)Math.Ceiling(dst->B * alphaFactor);
                        dst->G = (byte)Math.Ceiling(dst->G * alphaFactor);
                        dst->R = (byte)Math.Ceiling(dst->R * alphaFactor);
                        dst->A = newAlpha;

                        dst++;
                    }
                }
            });

            image.UnlockBits(bmpData);
            return image;
        }

        /// <summary>
        /// Returns a copy of the image, rotated about its center.
        /// </summary>
        /// <param name="origBmp">
        /// The image to clone and change.
        /// </param>
        /// <param name="angle">
        /// The angle in degrees; positive or negative.
        /// </param>
        /// <param name="maxAliasedAlpha">
        /// If provided, aliasing will be manually performed by snapping all alpha values to 0 or the specified value,
        /// based on whether they're more/less transparent than half the given max.
        /// </param>
        public static Bitmap RotateImage(Bitmap origBmp, float angle, byte? maxAliasedAlpha = null)
        {
            //Places the angle in the range 0 <= x < 360.
            while (angle < 0)
            {
                angle += 360;
            }
            while (angle >= 360)
            {
                angle -= 360;
            }

            //Calculates the new bounds of the image with trigonometry.
            double radAngle = angle * Math.PI / 180;
            double cos = Math.Abs(Math.Cos(radAngle));
            double sin = Math.Abs(Math.Sin(radAngle));
            int newWidth = (int)Math.Ceiling(origBmp.Width * cos + origBmp.Height * sin);
            int newHeight = (int)Math.Ceiling(origBmp.Width * sin + origBmp.Height * cos);

            //Creates the new image and a graphic canvas to draw the rotation.
            Bitmap newBmp = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(newBmp))
            {
                g.PixelOffsetMode = PixelOffsetMode.Half;

                //Uses matrices to centrally-rotate the original image.
                g.TranslateTransform(
                    (newWidth - origBmp.Width) / 2f,
                    (newHeight - origBmp.Height) / 2f);

                g.TranslateTransform(
                    origBmp.Width / 2f,
                    origBmp.Height / 2f);

                if (angle != 0)
                {
                    g.RotateTransform(angle);
                }

                //Undoes the transform.
                g.TranslateTransform(-origBmp.Width / 2f, -origBmp.Height / 2f);

                //Draws the image.
                g.DrawImage(origBmp, 0, 0, origBmp.Width, origBmp.Height);
            }

            // Manual aliasing after transform, since there's no way to turn off rotation anti-aliasing in GDI+
            if (maxAliasedAlpha != null)
            {
                AliasImage(newBmp, maxAliasedAlpha ?? byte.MaxValue);
            }

            return newBmp;
        }

        /// <summary>
        /// Creates a new bitmap copy of the image, scaled to the given size, optionally flipped and with color info.
        /// </summary>
        /// <param name="origBmp">The image to clone and scale.</param>
        /// <param name="newSize">The new width and height of the image.</param>
        /// <param name="flipX">Whether to flip the image horizontally.</param>
        /// <param name="flipY">Whether to flip the image vertically.</param>
        /// <param name="attr">If supplied, the recolor matrix will also be applied.</param>
        /// <param name="smoothing">If supplied, the smoothing to be used.</param>
        public static Bitmap ScaleImage(
            Bitmap origBmp,
            Size newSize,
            bool flipX = false,
            bool flipY = false,
            ImageAttributes attr = null,
            CmbxSmoothing.Smoothing smoothing = CmbxSmoothing.Smoothing.Normal)
        {
            //Creates the new image and a graphic canvas to draw the rotation.
            Bitmap newBmp = new Bitmap(newSize.Width, newSize.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(newBmp))
            {
                g.InterpolationMode = CmbxSmoothing.SmoothingToInterpolationMode[smoothing];
                g.PixelOffsetMode = PixelOffsetMode.Half;

                if (attr != null)
                {
                    g.DrawImage(
                        origBmp,
                        new Rectangle(
                        flipX ? newSize.Width : 0,
                        flipY ? newSize.Height : 0,
                        flipX ? -newSize.Width : newSize.Width,
                        flipY ? -newSize.Height : newSize.Height),
                        0, 0, origBmp.Width, origBmp.Height, GraphicsUnit.Pixel, attr);
                }
                else
                {
                    g.DrawImage(
                        origBmp,
                        flipX ? newSize.Width : 0,
                        flipY ? newSize.Height : 0,
                        flipX ? -newSize.Width : newSize.Width,
                        flipY ? -newSize.Height : newSize.Height);
                }

                return newBmp;
            }
        }
        #endregion

        #region Drawing methods
        /// <summary>
        /// Strictly copies all data from one bitmap over the other. They
        /// must have the same size and pixel format.
        /// </summary>
        /// <param name="srcImg">
        /// The image to copy from.
        /// </param>
        /// <param name="dstImg">
        /// The image to be overwritten.
        /// </param>
        public static unsafe void OverwriteBits(Bitmap srcImg, Bitmap dstImg, bool alphaOnly = false)
        {
            if (srcImg == null || dstImg == null)
            {
                return;
            }

            //Formats and size must be the same.
            if (srcImg.PixelFormat != PixelFormat.Format32bppPArgb && srcImg.PixelFormat != PixelFormat.Format32bppArgb ||
                dstImg.PixelFormat != PixelFormat.Format32bppPArgb ||
                srcImg.Width != dstImg.Width ||
                srcImg.Height != dstImg.Height)
            {
                return;
            }

            BitmapData srcData = srcImg.LockBits(
                srcImg.GetBounds(),
                ImageLockMode.ReadOnly,
                srcImg.PixelFormat);

            BitmapData destData = dstImg.LockBits(
                dstImg.GetBounds(),
                ImageLockMode.WriteOnly,
                dstImg.PixelFormat);

            bool premultiplySrc = srcImg.PixelFormat == PixelFormat.Format32bppArgb;

            //Copies each pixel.
            byte* srcRow = (byte*)srcData.Scan0;
            byte* dstRow = (byte*)destData.Scan0;

            Rectangle[] rois = GetRois(srcImg.Width, srcImg.Height);
            Parallel.For(0, rois.Length, (i, loopState) =>
            {
                Rectangle roi = rois[i];
                float alphaFactor;

                for (int y = roi.Y; y < roi.Y + roi.Height; y++)
                {
                    ColorBgra* src = (ColorBgra*)(srcRow + (y * srcData.Stride) + (roi.X * 4));
                    ColorBgra* dst = (ColorBgra*)(dstRow + (y * destData.Stride) + (roi.X * 4));

                    for (int x = roi.X; x < roi.X + roi.Width; x++)
                    {
                        alphaFactor = src->A / 255f;

                        if (alphaOnly)
                        {
                            dst->Bgra = dst->ConvertFromPremultipliedAlpha().Bgra;

                            // Overwrite values (except for locked channels). Premultiply by hand to use Ceiling(),
                            // which avoids a rounding error seen in ConvertToPremultipliedAlpha
                            dst->B = (byte)Math.Ceiling(dst->B * alphaFactor);
                            dst->G = (byte)Math.Ceiling(dst->G * alphaFactor);
                            dst->R = (byte)Math.Ceiling(dst->R * alphaFactor);
                            dst->A = src->A;
                        }
                        else
                        {
                            if (premultiplySrc)
                            {
                                // Overwrite values (except for locked channels). Premultiply by hand to use Ceiling(),
                                // which avoids a rounding error seen in ConvertToPremultipliedAlpha
                                dst->B = (byte)Math.Ceiling(src->B * alphaFactor);
                                dst->G = (byte)Math.Ceiling(src->G * alphaFactor);
                                dst->R = (byte)Math.Ceiling(src->R * alphaFactor);
                                dst->A = src->A;
                            }
                            else
                            {
                                dst->Bgra = src->Bgra;
                            }
                        }

                        src++;
                        dst++;
                    }
                }
            });

            srcImg.UnlockBits(srcData);
            dstImg.UnlockBits(destData);
        }

        /// <summary>
        /// Replaces a portion of the destination bitmap with the surface bitmap using a brush as an alpha mask. This
        /// overload takes both a source and destination location, for tools like clone stamp.
        /// </summary>
        /// <param name="src">A bitmap-containing surface or a bitmap. It will be drawn to dest.</param>
        /// <param name="dest">The bitmap to edit.</param>
        /// <param name="alphaMask">The brush used to draw.</param>
        /// <param name="srcLoc">The location to copy from.</param>
        /// <param name="srcLoc">The location to draw at.</param>
        /// <param name="channelLocks">Whether to allow values to change or not (for each channel including HSV).</param>
        /// <param name="wrapAround">Whether to draw clipped brush parts to the opposite side of the canvas.</param>
        /// <param name="ditherFilter">Whether to draw only every other pixel to replicate a pixel art dither.</param>
        public static unsafe void OverwriteMasked(
            (Surface surface, Bitmap bmp) src, Bitmap dest, Bitmap alphaMask,
            Point srcLoc, Point destLoc,
            (bool R, bool G, bool B, bool H, bool S, bool V) channelLocks,
            bool wrapAround, bool ditherFilter)
        {
            if ((channelLocks.R && channelLocks.G && channelLocks.B) ||
                (channelLocks.H && channelLocks.S && channelLocks.V) ||
                (src.surface == null && src.bmp == null) ||
                (src.surface != null && src.bmp != null))
            {
                return;
            }

            // Calculates brush regions outside the bounding area for source.
            int srcNegX = srcLoc.X < 0 ? -srcLoc.X : 0;
            int srcNegY = srcLoc.Y < 0 ? -srcLoc.Y : 0;
            int srcExtraX = Math.Max(srcLoc.X + alphaMask.Width - (src.surface?.Width ?? src.bmp.Width), 0);
            int srcExtraY = Math.Max(srcLoc.Y + alphaMask.Height - (src.surface?.Height ?? src.bmp.Height), 0);

            int srcAdjW = alphaMask.Width - srcNegX - srcExtraX;
            int srcAdjH = alphaMask.Height - srcNegY - srcExtraY;

            if (srcAdjW < 1 || srcAdjH < 1 ||
                (src.surface?.Width != dest.Width && src.bmp.Width != dest.Width) ||
                (src.surface?.Height != dest.Height && src.bmp.Height != dest.Height) ||
                (src.surface != null && src.surface.Scan0 == null))
            {
                return;
            }

            Rectangle srcAdjBounds = new Rectangle(
                srcLoc.X < 0 ? 0 : srcLoc.X,
                srcLoc.Y < 0 ? 0 : srcLoc.Y,
                srcAdjW,
                srcAdjH);

            // Accounts for source rectangle regions outside bounding area.
            int destX = destLoc.X + srcNegX;
            int destY = destLoc.Y + srcNegY;
            int destWidth = alphaMask.Width - srcNegX - srcExtraX;
            int destHeight = alphaMask.Height - srcNegY - srcExtraY;

            // Calculates brush regions outside the bounding area for destination.
            int destNegX = destX < 0 ? -destX : 0;
            int destNegY = destY < 0 ? -destY : 0;
            int destExtraX = Math.Max(destX + destWidth - (src.surface?.Width ?? src.bmp.Width), 0);
            int destExtraY = Math.Max(destY + destHeight - (src.surface?.Height ?? src.bmp.Height), 0);

            int destAdjW = destWidth - destNegX - destExtraX;
            int destAdjH = destHeight - destNegY - destExtraY;

            Rectangle destAdjBounds = new Rectangle(
                destLoc.X < 0 ? 0 : destLoc.X,
                destLoc.Y < 0 ? 0 : destLoc.Y,
                destAdjW,
                destAdjH);

            BitmapData destData = dest.LockBits(
                dest.GetBounds(),
                ImageLockMode.ReadWrite,
                dest.PixelFormat);

            BitmapData alphaMaskData = alphaMask.LockBits(
                alphaMask.GetBounds(),
                ImageLockMode.ReadOnly,
                alphaMask.PixelFormat);

            BitmapData srcData = src.bmp?.LockBits(
                    src.bmp.GetBounds(),
                    ImageLockMode.ReadOnly,
                    src.bmp.PixelFormat);

            byte* destRow = (byte*)destData.Scan0;
            byte* alphaMaskRow = (byte*)alphaMaskData.Scan0;
            byte* srcRow = (byte*)(src.surface?.Scan0.Pointer ?? srcData.Scan0);

            bool hsvLocksInUse = channelLocks.H || channelLocks.S || channelLocks.V;

            void draw(int brushXOffset, int brushYOffset, int srcXOffset, int srcYOffset, int destXOffset, int destYOffset, int destWidth, int destHeight)
            {
                Rectangle[] rois = (src.surface != null)
                    ? GetRois(destWidth, destHeight)
                    : new Rectangle[] { new Rectangle(0, 0, destWidth, destHeight ) }; // TODO: use full ROIs instead.

                Parallel.For(0, rois.Length, (i, loopState) =>
                {
                    Rectangle roi = rois[i];
                    float alphaFactor;

                    ColorBgra srcCol;
                    ColorBgra destCol;
                    ColorBgra newColor = default;
                    HsvColorF srcColorHSV;
                    HsvColorF dstColorHSV;

                    for (int y = roi.Y; y < roi.Y + roi.Height; y++)
                    {
                        int x = roi.X;
                        ColorBgra* alphaMaskPtr = (ColorBgra*)(alphaMaskRow + (roi.X * 4) + (brushXOffset * 4) + ((brushYOffset + y) * alphaMaskData.Stride));
                        ColorBgra* srcPtr = (ColorBgra*)(srcRow + (roi.X * 4) + (srcXOffset * 4) + ((y + srcYOffset) * (src.surface?.Stride ?? srcData.Stride)));
                        ColorBgra* destPtr = (ColorBgra*)(destRow + (roi.X * 4) + (destXOffset * 4) + ((y + destYOffset) * destData.Stride));

                        // Dither align
                        if (ditherFilter && ((roi.X + destXOffset + y) % 2 != destYOffset % 2))
                        {
                            x++;
                            alphaMaskPtr++;
                            destPtr++;
                            srcPtr++;
                        }

                        for (; x < roi.X + roi.Width; x++)
                        {
                            destCol = destPtr->ConvertFromPremultipliedAlpha();
                            srcCol = (src.bmp?.PixelFormat == PixelFormat.Format32bppPArgb)
                                ? srcPtr->ConvertFromPremultipliedAlpha()
                                : *srcPtr;

                            // HSV conversion and channel locks
                            if (hsvLocksInUse)
                            {
                                dstColorHSV = ColorUtils.HSVFFromBgra(destCol);
                                srcColorHSV = ColorUtils.HSVFFromBgra(srcCol);
                                if (channelLocks.H) { srcColorHSV.Hue = dstColorHSV.Hue; }
                                if (channelLocks.S) { srcColorHSV.Saturation = dstColorHSV.Saturation; }
                                if (channelLocks.V) { srcColorHSV.Value = dstColorHSV.Value; }
                                newColor = ColorUtils.HSVFToBgra(srcColorHSV);
                                newColor.A = srcCol.A;
                            }

                            newColor = ColorBgra.Blend(
                                destCol,
                                hsvLocksInUse ? newColor : srcCol,
                                alphaMaskPtr->A);

                            alphaFactor = newColor.A / 255f;

                            // Overwrite values (except for locked channels). Premultiply by hand to use Ceiling(),
                            // which avoids a rounding error seen in ConvertToPremultipliedAlpha
                            if (!channelLocks.B) { destPtr->B = (byte)Math.Ceiling(newColor.B * alphaFactor); }
                            if (!channelLocks.G) { destPtr->G = (byte)Math.Ceiling(newColor.G * alphaFactor); }
                            if (!channelLocks.R) { destPtr->R = (byte)Math.Ceiling(newColor.R * alphaFactor); }
                            destPtr->A = newColor.A;

                            if (!ditherFilter)
                            {
                                alphaMaskPtr++;
                                destPtr++;
                                srcPtr++;
                            }
                            else // dither align
                            {
                                x++;
                                alphaMaskPtr += 2;
                                destPtr += 2;
                                srcPtr += 2;
                            }
                        }
                    }
                });
            }

            // Draw within normal bounds
            draw(srcNegX, srcNegY,
                srcAdjBounds.X, srcAdjBounds.Y + destNegY,
                destAdjBounds.X + srcNegX - destNegX, destAdjBounds.Y + srcNegY,
                destAdjBounds.Width, destAdjBounds.Height);

            // Draw brush cutoffs on the opposite side of the canvas (wrap-around / seamless texture)
            if (wrapAround)
            {
                // The brush is only guaranteed seamless when none of its dimensions are larger than any of the canvas dimensions.
                // The basic decision to clamp prevents having to copy excess chunks in loops -- it's just much simpler.
                srcNegX = Math.Clamp(srcNegX, 0, dest.Width);
                srcNegY = Math.Clamp(srcNegY, 0, dest.Height);
                srcExtraX = Math.Clamp(srcExtraX, 0, Math.Min(alphaMask.Width, dest.Width));
                srcExtraY = Math.Clamp(srcExtraY, 0, Math.Min(alphaMask.Height, dest.Height));

                // TODO: fix this.
                draw(0, srcNegY, -1, -1, dest.Width - srcNegX, srcAdjBounds.Y, srcNegX, srcAdjBounds.Height); // left
                draw(srcNegX, 0, -1, -1, srcAdjBounds.X, dest.Height - srcNegY, srcAdjBounds.Width, srcNegY); // top
                draw(alphaMask.Width - srcExtraX, srcNegY, -1, -1, 0, srcAdjBounds.Y, srcExtraX, srcAdjBounds.Height); // right
                draw(srcNegX, alphaMask.Height - srcExtraY, -1, -1, srcAdjBounds.X, 0, srcAdjBounds.Width, srcExtraY); // bottom
                draw(0, 0, -1, -1, dest.Width - srcNegX, dest.Height - srcNegY, srcNegX, srcNegY); // top left
                draw(alphaMask.Width - srcExtraX, 0, -1, -1, 0, dest.Height - srcNegY, srcExtraX, srcNegY); // top right
                draw(0, alphaMask.Height - srcExtraY, -1, -1, dest.Width - srcNegX, 0, srcNegX, srcExtraY); // bottom left
                draw(alphaMask.Width - srcExtraX, alphaMask.Height - srcExtraY, -1, -1, 0, 0, srcExtraX, srcExtraY); // bottom right
            }

            src.bmp?.UnlockBits(srcData);
            dest.UnlockBits(destData);
            alphaMask.UnlockBits(alphaMaskData);
        }

        /// <summary>
        /// Draws the brush image at a defined point on a destination bitmap using custom blending.
        /// </summary>
        /// <param name="dest">The bitmap to edit.</param>
        /// <param name="brush">The brush image that will be drawn to the destination using custom blending.</param>
        /// <param name="location">The location to draw the brush at.</param>
        /// <param name="userColor">
        /// The user's chosen color, the brush flow (min alpha), and opacity (max alpha). Min alpha isn't part of
        /// userColor as usercolor alpha is precomputed and jitter alpha contribution needs to be applied separately
        /// when the original brush colors are used with it.
        /// </param>
        /// <param name="colorInfluence">The amount of mixing with the active color to perform, and which HSV channels to affect.</param>
        /// <param name="blendMode">Determines the algorithm uesd to draw the brush on dest.</param>
        /// <param name="channelLocks">Whether to allow values to change or not (for each channel including HSV).</param>
        /// <param name="wrapAround">Whether to draw clipped brush parts to the opposite side of the canvas.</param>
        /// <param name="ditherFilter">Whether to draw only every other pixel to replicate a pixel art dither.</param>
        /// <param name="mergeRegionsToMark">Pass-by-ref list of rectangles to add all new draw regions to.</param>
        public static unsafe void DrawMasked(
            Bitmap dest, Bitmap brush,
            Point location,
            (ColorBgra Color, int MinAlpha, byte MaxAlpha) userColor,
            (int Amount, bool H, bool S, bool V)? colorInfluence,
            BlendMode blendMode,
            (bool A, bool R, bool G, bool B, bool H, bool S, bool V) channelLocks,
            bool wrapAround, bool ditherFilter,
            List<Rectangle> mergeRegionsToMark)
        {
            if (((channelLocks.H && channelLocks.S && channelLocks.V) ||
                (channelLocks.R && channelLocks.G && channelLocks.B))
                && channelLocks.A)
            {
                return;
            }

            // Calculates the brush regions outside the bounding area of the surface.
            int negativeX = location.X < 0 ? -location.X : 0;
            int negativeY = location.Y < 0 ? -location.Y : 0;
            int extraX = Math.Max(location.X + brush.Width - dest.Width, 0);
            int extraY = Math.Max(location.Y + brush.Height - dest.Height, 0);

            int adjWidth = brush.Width - negativeX - extraX;
            int adjHeight = brush.Height - negativeY - extraY;

            if (adjWidth < 1 || adjHeight < 1)
            {
                return;
            }

            Rectangle adjBounds = new Rectangle(
                location.X < 0 ? 0 : location.X,
                location.Y < 0 ? 0 : location.Y,
                adjWidth,
                adjHeight);

            BitmapData destData = dest.LockBits(
                dest.GetBounds(),
                ImageLockMode.ReadWrite,
                dest.PixelFormat);

            BitmapData brushData = brush.LockBits(
                brush.GetBounds(),
                ImageLockMode.ReadOnly,
                brush.PixelFormat);

            byte* destRow = (byte*)destData.Scan0;
            byte* brushRow = (byte*)brushData.Scan0;

            ColorBgra userColorAdj = userColor.Color.ConvertFromPremultipliedAlpha();
            HsvColorF userColorAdjHSV = ColorUtils.HSVFFromBgra(userColorAdj);

            float minAlphaFactor = (255 - userColor.MinAlpha) / 255f;
            float userColorBlendFactor = colorInfluence != null ? colorInfluence.Value.Amount / 100f : 0;
            bool hsvLocksInUse = channelLocks.H || channelLocks.S || channelLocks.V;

            void draw(int brushXOffset, int brushYOffset, int destXOffset, int destYOffset, int destWidth, int destHeight)
            {
                mergeRegionsToMark?.Add(new Rectangle(destXOffset, destYOffset, destWidth, destHeight));

                Rectangle[] rois = GetRois(destWidth, destHeight);
                Parallel.For(0, rois.Length, (i, loopState) =>
                {
                    Rectangle roi = rois[i];
                    int roiX2 = roi.X + roi.Width;
                    int roiY2 = roi.Y + roi.Height;

                    float alphaFactor;
                    ColorBgra newColor = default;
                    ColorBgra destCol;
                    ColorBgra intermediateBGRA = default;
                    HsvColorF intermediateHSV;
                    HsvColorF intermediateHSV2;

                    for (int y = roi.Y; y < roiY2; y++)
                    {
                        int x = roi.X;
                        ColorBgra* brushPtr = (ColorBgra*)(brushRow + (roi.X * 4) + (brushXOffset * 4) + ((brushYOffset + y) * brushData.Stride));
                        ColorBgra* destPtr = (ColorBgra*)(destRow + (roi.X * 4) + (destXOffset * 4) + ((y + destYOffset) * destData.Stride));

                        // Dither align
                        if (ditherFilter && ((roi.X + destXOffset + y) % 2 != destYOffset % 2))
                        {
                            x++;
                            brushPtr++;
                            destPtr++;
                        }

                        for (; x < roiX2; x++)
                        {
                            // HSV shift the pixel according to the color influence when colorize brush is off
                            // don't compute for any locked channels
                            if (colorInfluence != null)
                            {
                                intermediateBGRA = brushPtr->ConvertFromPremultipliedAlpha();

                                if (colorInfluence.Value.Amount != 0)
                                {
                                    intermediateHSV = ColorUtils.HSVFFromBgra(intermediateBGRA);

                                    if (colorInfluence.Value.H && !channelLocks.H)
                                    {
                                        intermediateHSV.Hue +=
                                            userColorBlendFactor * (userColorAdjHSV.Hue - intermediateHSV.Hue);
                                    }
                                    if (colorInfluence.Value.S && !channelLocks.S)
                                    {
                                        intermediateHSV.Saturation +=
                                            userColorBlendFactor * (userColorAdjHSV.Saturation - intermediateHSV.Saturation);
                                    }
                                    if (colorInfluence.Value.V && !channelLocks.V)
                                    {
                                        intermediateHSV.Value +=
                                            userColorBlendFactor * (userColorAdjHSV.Value - intermediateHSV.Value);
                                    }

                                    if (colorInfluence.Value.Amount != 0)
                                    {
                                        byte alpha = intermediateBGRA.A;
                                        intermediateBGRA = ColorUtils.HSVFToBgra(intermediateHSV);
                                        intermediateBGRA.A = alpha;
                                    }
                                }
                            }

                            // Perform a blend mode op on the pixel, specially handling overwrite mode. Blend modes beyond
                            // normal blending are all handled by the merge image function.
                            if (blendMode == BlendMode.Overwrite)
                            {
                                destCol = destPtr->ConvertFromPremultipliedAlpha();

                                // HSV conversion and channel locks
                                if (hsvLocksInUse)
                                {
                                    intermediateHSV = ColorUtils.HSVFFromBgra(destCol);
                                    intermediateHSV2 = ColorUtils.HSVFFromBgra(colorInfluence == null ? userColorAdj : intermediateBGRA);
                                    if (channelLocks.H) { intermediateHSV2.Hue = intermediateHSV.Hue; }
                                    if (channelLocks.S) { intermediateHSV2.Saturation = intermediateHSV.Saturation; }
                                    if (channelLocks.V) { intermediateHSV2.Value = intermediateHSV.Value; }
                                    newColor = ColorUtils.HSVFToBgra(userColorAdjHSV);
                                }

                                newColor = ColorBgra.Blend(
                                    destCol,
                                    hsvLocksInUse
                                        ? newColor
                                        : colorInfluence == null ? userColorAdj : intermediateBGRA,
                                    brushPtr->A);

                                alphaFactor = (!channelLocks.A)
                                    ? (destCol.A + brushPtr->A / 255f * (userColorAdj.A - destCol.A)) / 255f
                                    : destCol.A / 255f;

                                // Overwrite values (except for locked channels). Premultiply by hand to use Ceiling(),
                                // which avoids a rounding error seen in ConvertToPremultipliedAlpha
                                if (!channelLocks.B) { destPtr->B = (byte)Math.Ceiling(newColor.B * alphaFactor); }
                                if (!channelLocks.G) { destPtr->G = (byte)Math.Ceiling(newColor.G * alphaFactor); }
                                if (!channelLocks.R) { destPtr->R = (byte)Math.Ceiling(newColor.R * alphaFactor); }
                                if (!channelLocks.A) { destPtr->A = (byte)Math.Ceiling(alphaFactor * 255); }
                            }
                            else
                            {
                                destCol = destPtr->ConvertFromPremultipliedAlpha();

                                // HSV conversion and channel locks
                                if (hsvLocksInUse)
                                {
                                    intermediateHSV = ColorUtils.HSVFFromBgra(destCol);
                                    intermediateHSV2 = ColorUtils.HSVFFromBgra(colorInfluence == null ? userColorAdj : intermediateBGRA);
                                    if (channelLocks.H) { intermediateHSV2.Hue = intermediateHSV.Hue; }
                                    if (channelLocks.S) { intermediateHSV2.Saturation = intermediateHSV.Saturation; }
                                    if (channelLocks.V) { intermediateHSV2.Value = intermediateHSV.Value; }
                                    newColor = ColorUtils.HSVFToBgra(intermediateHSV2);
                                }

                                // Brush flow
                                byte strength = colorInfluence == null
                                    ? brushPtr->A
                                    : (byte)Math.Round(brushPtr->A * minAlphaFactor);

                                newColor = ColorBgra.Blend(
                                    destCol,
                                    hsvLocksInUse
                                        ? colorInfluence == null
                                            ? newColor.NewAlpha((byte)Math.Clamp(userColorAdj.A + destPtr->A, 0, 255))
                                            : newColor.NewAlpha((byte)Math.Clamp(intermediateBGRA.A + destPtr->A, 0, 255))
                                        : colorInfluence == null
                                            ? userColorAdj.NewAlpha((byte)Math.Clamp(userColorAdj.A + destPtr->A, 0, 255))
                                            : intermediateBGRA.NewAlpha((byte)Math.Clamp(brushPtr->A + destPtr->A, 0, 255)),
                                    strength);

                                // Brush opacity. Limits the alpha to max or dst, in case the max alpha
                                // is lowered & the user draws over pixels made in the same brush stroke.
                                // The need to read dst means this mode requires dst to be a staging
                                // layer i.e. transparent at start of brush stroke.
                                if (userColor.MaxAlpha != 255)
                                {
                                    newColor.A = Math.Min(
                                        newColor.A,
                                        Math.Max(userColor.MaxAlpha, destCol.A));
                                }

                                alphaFactor = (!channelLocks.A)
                                    ? newColor.A / 255f
                                    : destPtr->A / 255f;

                                // Overwrite values (except for locked channels). Premultiply by hand to use Ceiling(),
                                // which avoids a rounding error seen in ConvertToPremultipliedAlpha
                                if (!channelLocks.B) { destPtr->B = (byte)Math.Ceiling(newColor.B * alphaFactor); }
                                if (!channelLocks.G) { destPtr->G = (byte)Math.Ceiling(newColor.G * alphaFactor); }
                                if (!channelLocks.R) { destPtr->R = (byte)Math.Ceiling(newColor.R * alphaFactor); }
                                if (!channelLocks.A) { destPtr->A = newColor.A; }
                            }

                            if (!ditherFilter)
                            {
                                brushPtr++;
                                destPtr++;
                            }
                            else // dither align
                            {
                                x++;
                                brushPtr += 2;
                                destPtr += 2;
                            }
                        }
                    }
                });
            }

            // Draw within normal bounds
            draw(negativeX, negativeY, adjBounds.X, adjBounds.Y, adjBounds.Width, adjBounds.Height);

            // Draw brush cutoffs on the opposite side of the canvas (wrap-around / seamless texture)
            if (wrapAround)
            {
                // The brush is only guaranteed seamless when none of its dimensions are larger than any of the canvas dimensions.
                // The basic decision to clamp prevents having to copy excess chunks in loops -- it's just much simpler.
                negativeX = Math.Clamp(negativeX, 0, dest.Width);
                negativeY = Math.Clamp(negativeY, 0, dest.Height);
                extraX = Math.Clamp(extraX, 0, Math.Min(brush.Width, dest.Width));
                extraY = Math.Clamp(extraY, 0, Math.Min(brush.Height, dest.Height));

                draw(0, negativeY, dest.Width - negativeX, adjBounds.Y, negativeX, adjBounds.Height); // left
                draw(negativeX, 0, adjBounds.X, dest.Height - negativeY, adjBounds.Width, negativeY); // top
                draw(brush.Width - extraX, negativeY, 0, adjBounds.Y, extraX, adjBounds.Height); // right
                draw(negativeX, brush.Height - extraY, adjBounds.X, 0, adjBounds.Width, extraY); // bottom
                draw(0, 0, dest.Width - negativeX, dest.Height - negativeY, negativeX, negativeY); // top left
                draw(brush.Width - extraX, 0, 0, dest.Height - negativeY, extraX, negativeY); // top right
                draw(0, brush.Height - extraY, dest.Width - negativeX, 0, negativeX, extraY); // bottom left
                draw(brush.Width - extraX, brush.Height - extraY, 0, 0, extraX, extraY); // bottom right
            }

            dest.UnlockBits(destData);
            brush.UnlockBits(brushData);
        }

        /// <summary>
        /// Combines the staged bitmap using opacity and blendMode options to the committed bitmap, writing this data
        /// over the dest bitmap. All layers must be the same width and height. Arguments aren't checked for validity.
        /// </summary>
        /// <param name="staged">The layer to merge down, following opacity and blend mode choices.</param>
        /// <param name="committed">The current changes before merging staged into it.</param>
        /// <param name="dest">
        /// The bitmap to make the new changes on, which can be separate or match the staged/committed one.
        /// </param>
        /// <param name="regionToAffect">A smaller region to merge.</param>
        /// <param name="maxOpacityAllowed">
        /// Limits the final opacity on the staged layer before merging. This effect is similar to opacity in Krita.
        /// </param>
        /// <param name="blendModeType">
        /// The current blend mode. If it corresponds to a paint.net user blend op type, merge is done using that blend
        /// mode.
        /// </param>
        public static unsafe void MergeImage(
            Bitmap staged, Bitmap committed, Bitmap dest,
            Rectangle regionToAffect,
            BlendMode blendMode,
            (bool A, bool R, bool G, bool B, bool H, bool S, bool V) channelLocks)
        {
            if (((channelLocks.H && channelLocks.S && channelLocks.V) ||
                (channelLocks.R && channelLocks.G && channelLocks.B))
                && channelLocks.A)
            {
                return;
            }

            BitmapData destData = dest.LockBits(
                regionToAffect,
                ImageLockMode.ReadWrite,
                dest.PixelFormat);

            BitmapData stagedData = (dest == staged) ? destData : staged.LockBits(
                regionToAffect,
                ImageLockMode.ReadOnly,
                staged.PixelFormat);

            BitmapData committedData = (dest == committed) ? destData : committed.LockBits(
                regionToAffect,
                ImageLockMode.ReadOnly,
                committed.PixelFormat);

            byte* destRow = (byte*)destData.Scan0;
            byte* stagedRow = (byte*)stagedData.Scan0;
            byte* committedRow = (byte*)committedData.Scan0;

            bool hsvLocksInUse = channelLocks.H || channelLocks.S || channelLocks.V;

            Type blendType = blendMode != BlendMode.Normal ? BlendModeUtils.BlendModeToUserBlendOp(blendMode) : null;
            UserBlendOp userBlendOp = blendType != null ? UserBlendOps.CreateBlendOp(blendType) : null;

            Rectangle[] rois = GetRois(regionToAffect.Width, regionToAffect.Height);
            Parallel.For(0, rois.Length, (i, loopState) =>
            {
                Rectangle roi = rois[i];
                for (int y = roi.Y; y < roi.Y + roi.Height; y++)
                {
                    ColorBgra* stagedPtr = (ColorBgra*)(stagedRow + (roi.X * 4) + (y * stagedData.Stride));
                    ColorBgra* committedPtr = (ColorBgra*)(committedRow + (roi.X * 4) + (y * committedData.Stride));
                    ColorBgra* destPtr = (ColorBgra*)(destRow + (roi.X * 4) + (y * destData.Stride));

                    ColorBgra final;
                    float finalAlpha;
                    HsvColorF mergedHSV, dstHSV;
                    
                    for (int x = roi.X; x < roi.X + roi.Width; x++)
                    {
                        if (userBlendOp == null)
                        {
                            final = ColorBgra.Blend(
                                committedPtr->ConvertFromPremultipliedAlpha(),
                                stagedPtr->ConvertFromPremultipliedAlpha().NewAlpha((byte)Math.Clamp(stagedPtr->A + committedPtr->A, 0, 255)),
                                stagedPtr->A);
                        }
                        else
                        {
                            final = userBlendOp.Apply(
                                committedPtr->ConvertFromPremultipliedAlpha(),
                                stagedPtr->ConvertFromPremultipliedAlpha());
                        }

                        // HSV conversion and channel locks
                        if (hsvLocksInUse)
                        {
                            dstHSV = ColorUtils.HSVFFromBgra(destPtr->ConvertFromPremultipliedAlpha());
                            mergedHSV = ColorUtils.HSVFFromBgra(final);
                            if (channelLocks.H) { mergedHSV.Hue = dstHSV.Hue; }
                            if (channelLocks.S) { mergedHSV.Saturation = dstHSV.Saturation; }
                            if (channelLocks.V) { mergedHSV.Value = dstHSV.Value; }
                            final = ColorUtils.HSVFToBgra(mergedHSV);
                        }

                        finalAlpha = final.A / 255f;

                        // Overwrite values. Premultiply by hand to use Ceiling(), which avoids a
                        // rounding error seen in ConvertToPremultipliedAlpha
                        if (!channelLocks.B) { destPtr->B = (byte)Math.Ceiling(final.B * finalAlpha); }
                        if (!channelLocks.G) { destPtr->G = (byte)Math.Ceiling(final.G * finalAlpha); }
                        if (!channelLocks.R) { destPtr->R = (byte)Math.Ceiling(final.R * finalAlpha); }
                        if (!channelLocks.A) { destPtr->A = final.A; }

                        stagedPtr++;
                        committedPtr++;
                        destPtr++;
                    }
                }
            });

            dest.UnlockBits(destData);
            if (staged != dest) { staged.UnlockBits(stagedData); }
            if (committed != dest) { committed.UnlockBits(committedData); }
        }
        #endregion

        #region Miscellaneous utility methods
        /// <summary>
        /// Returns an ImageAttributes object containing info to set the
        /// RGB channels and multiply alpha. All values should be decimals
        /// between 0 and 1, inclusive.
        /// </summary>
        public static ImageAttributes ColorImageAttr(float r, float g, float b, float a)
        {
            //Creates an RGBAw matrix to multiply all the color channels by.
            //The last channel should be [0,0,0,0,1] to function properly.
            float[][] matrixAlpha =
            {
                new float[] {0, 0, 0, 0, 0},
                new float[] {0, 0, 0, 0, 0},
                new float[] {0, 0, 0, 0, 0},
                new float[] {0, 0, 0, a, 0},
                new float[] {r, g, b, 0, 1}
            };

            //Sets up an image attributes object.
            ImageAttributes recolorSettings = new ImageAttributes();
            recolorSettings.SetColorMatrix(
                new ColorMatrix(matrixAlpha),
                ColorMatrixFlag.Default,
                ColorAdjustType.Bitmap);

            return recolorSettings;
        }

        /// <summary>
        /// Computes the new brush size ratio given the dimensions and desired
        /// size of the longest dimension.
        /// </summary>
        public static Size ComputeBrushSize(int origWidth, int origHeight, int maxDimensionSize)
        {
            if (origWidth == 0 || origHeight == 0)
            {
                return new Size(1, 1);
            }

            double scaleRatio = Math.Min((double)maxDimensionSize / origWidth, (double)maxDimensionSize / origHeight);

            return new Size((int)Math.Round(origWidth * scaleRatio), (int)Math.Round(origHeight * scaleRatio));
        }

        /// <summary>
        /// Constructs an outline of the given region with the given bounds
        /// and scaling factor.
        /// </summary>
        /// <param name="region">
        /// The selection to approximate.
        /// </param>
        /// <param name="bounds">
        /// The boundaries of the image.
        /// </param>
        /// <param name="scalingMultiplier">
        /// The amount to scale the size of the outline by.
        /// </param>
        public static PdnRegion ConstructOutline(
            this PdnRegion region,
            RectangleF bounds,
            float scalingMultiplier)
        {
            using GraphicsPath path = new GraphicsPath();
            using PdnRegion newRegion = region.Clone();

            //The size to scale the region by.
            using Matrix scalematrix = new Matrix(
                bounds,
                new PointF[]{
                    new PointF(bounds.Left, bounds.Top),
                    new PointF(bounds.Right * scalingMultiplier, bounds.Top),
                    new PointF(bounds.Left, bounds.Bottom * scalingMultiplier)
                });

            newRegion.Transform(scalematrix);

            //Makes the new region slightly larger by inflating rectangles.
            foreach (RectangleF rect in newRegion.GetRegionScans())
            {
                path.AddRectangle(RectangleF.Inflate(rect, 1, 1));
            }

            //Subtracts the old region, leaving an outline from the expansion.
            PdnRegion result = new PdnRegion(path);
            result.Exclude(newRegion);

            return result;
        }

        /// <summary>
        /// Convenience extension method for bitmaps to return pixel-based bounds info as a rectangle.
        /// </summary>
        public static Rectangle GetBounds(this Bitmap bmp)
        {
            return new Rectangle(0, 0, bmp.Width, bmp.Height);
        }

        /// <summary>
        /// Separates the rectangle defined by width and height into squares of the given size.
        /// The square size is determined by the original bounds given. The remainder is added
        /// afterwards as 2 separate rectangles. This is used for parallel rendering.
        /// </summary>
        public static Rectangle[] GetRois(int width, int height)
        {
            if (cachedRois.ContainsKey((width, height)))
            {
                return cachedRois[(width, height)];
            }

            // Don't want unlimited amounts of entries, so remove the 10 oldest at a time when needed.
            if (cachedRois.Count == 110)
            {
                int i = 0;

                foreach ((int, int) key in cachedRois.Keys)
                {
                    i++;

                    cachedRois.Remove(key);

                    if (i >= 10)
                    {
                        break;
                    }
                }
            }

            List<Rectangle> rois = new List<Rectangle>();
            int squareSize;

            if (width >= 384 && height >= 384) { squareSize = 128; } // 9+ chunks
            else if (width >= 128 && height >= 128) { squareSize = 64; } // 9-36 chunks
            else if (width >= 48 && height >= 48) { squareSize = 32; } // 3-16 chunks
            else
            {
                // not worth parallelizing regions < 48x48, so return whole rect.
                cachedRois.Add((width, height), new Rectangle[] { new Rectangle(0, 0, width, height) });
                return cachedRois[(width, height)];
            }

            int chunksX = width / squareSize;
            int chunksY = height / squareSize;
            int chunkXRem = width % squareSize;
            int chunkYRem = height % squareSize;

            for (int y = 0; y < chunksY; y++)
            {
                for (int x = 0; x < chunksX; x++)
                {
                    rois.Add(new Rectangle(x * squareSize, y * squareSize, squareSize, squareSize));
                }
            }

            if (chunkYRem > 0) { rois.Add(new Rectangle(0, chunksY * squareSize, width, chunkYRem)); }
            if (chunkXRem > 0) { rois.Add(new Rectangle(chunksX * squareSize, 0, chunkXRem, height - chunkYRem)); }

            cachedRois.Add((width, height), rois.ToArray());
            return cachedRois[(width, height)];
        }
        #endregion
    }
}