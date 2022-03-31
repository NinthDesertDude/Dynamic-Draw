using DynamicDraw.Gui;
using PaintDotNet;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DynamicDraw
{
    /// <summary>
    /// Provides common functionality shared across multiple classes.
    /// </summary>
    static class Utils
    {
        #region Methods
        /// <summary>
        /// If the given value is out of range, it's clamped to the nearest
        /// bound (low or high). Example: 104 in range 0 - 100 becomes 100.
        /// </summary>
        public static int Clamp(int value, int low, int high)
        {
            if (value < low)
            {
                value = low;
            }
            else if (value > high)
            {
                value = high;
            }

            return value;
        }

        /// <summary>
        /// If the given value is out of range, it's clamped to the nearest
        /// bound (low or high). Example: -0.1 in range 0 - 1 becomes 0.
        /// </summary>
        public static float ClampF(float value, float low, float high)
        {
            if (value < low)
            {
                value = low;
            }
            else if (value > high)
            {
                value = high;
            }

            return value;
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
                new Rectangle(0, 0,
                    img.Width,
                    img.Height),
                ImageLockMode.ReadOnly,
                img.PixelFormat);

            byte* row = (byte*)bmpData.Scan0;
            Color color = col ?? default;
            ColorBgra pixel;

            for (int y = 0; y < img.Height; y++)
            {
                for (int x = 0; x < img.Width; x++)
                {
                    int ptr = y * bmpData.Stride + x * 4;

                    if (col != null)
                    {
                        pixel = ColorBgra.FromBgra(color.B, color.G, color.R, (byte)(row[ptr + 3] * alpha)).ConvertToPremultipliedAlpha();
                    }
                    else
                    {
                        ColorBgra unmultipliedPixel = ColorBgra.FromBgra(row[ptr], row[ptr + 1], row[ptr + 2], row[ptr + 3]).ConvertFromPremultipliedAlpha();
                        unmultipliedPixel.A = (byte)(unmultipliedPixel.A * alpha);
                        pixel = unmultipliedPixel.ConvertToPremultipliedAlpha();
                    }

                    row[ptr + 3] = pixel.A;
                    row[ptr + 2] = pixel.R;
                    row[ptr + 1] = pixel.G;
                    row[ptr] = pixel.B;
                }
            }

            img.UnlockBits(bmpData);
        }

        /// <summary>
        /// Edits alpha to be 0 or a set maximum for all pixels in the given image.
        /// </summary>
        /// <param name="img">
        /// The affected image.
        /// </param>
        /// <param name="maxAlpha">
        /// The maximum alpha (usually 255, but can be lower for when alpha is pre-applied to an image).
        /// </param>
        public static unsafe void AliasImage(Bitmap img, byte maxAlpha)
        {
            BitmapData bmpData = img.LockBits(
                new Rectangle(0, 0,
                    img.Width,
                    img.Height),
                ImageLockMode.ReadOnly,
                img.PixelFormat);

            byte* row = (byte*)bmpData.Scan0;
            byte halfAlpha = (byte)(maxAlpha / 2);
            ColorBgra pixel;

            for (int y = 0; y < img.Height; y++)
            {
                for (int x = 0; x < img.Width; x++)
                {
                    int ptr = y * bmpData.Stride + x * 4;

                    ColorBgra unmultipliedPixel = ColorBgra.FromBgra(row[ptr], row[ptr + 1], row[ptr + 2], row[ptr + 3]).ConvertFromPremultipliedAlpha();
                    unmultipliedPixel.A = (byte)(unmultipliedPixel.A >= halfAlpha ? maxAlpha : 0);
                    pixel = unmultipliedPixel.ConvertToPremultipliedAlpha();

                    row[ptr + 3] = pixel.A;
                    row[ptr + 2] = pixel.R;
                    row[ptr + 1] = pixel.G;
                    row[ptr] = pixel.B;
                }
            }

            img.UnlockBits(bmpData);
        }

        /// <summary>
        /// Returns an ImageAttributes object containing info to set the
        /// RGB channels and multiply alpha. All values should be decimals
        /// between 0 and 1, inclusive.
        /// </summary>
        public static ImageAttributes ColorImageAttr(
            float r,
            float g,
            float b,
            float a)
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
        /// Strictly copies all data from one bitmap over the other. They
        /// must have the same size and pixel format. Returns success.
        /// </summary>
        /// <param name="srcImg">
        /// The image to copy from.
        /// </param>
        /// <param name="dstImg">
        /// The image to be overwritten.
        /// </param>
        public static unsafe void OverwriteBits(Bitmap srcImg, Bitmap dstImg, bool alphaOnly = false)
        {
            //Formats and size must be the same.
            if (srcImg.PixelFormat != PixelFormat.Format32bppPArgb && srcImg.PixelFormat != PixelFormat.Format32bppArgb ||
                dstImg.PixelFormat != PixelFormat.Format32bppPArgb ||
                srcImg.Width != dstImg.Width ||
                srcImg.Height != dstImg.Height)
            {
                return;
            }

            BitmapData srcData = srcImg.LockBits(
                new Rectangle(0, 0,
                    srcImg.Width,
                    srcImg.Height),
                ImageLockMode.ReadOnly,
                srcImg.PixelFormat);

            BitmapData destData = dstImg.LockBits(
                new Rectangle(0, 0,
                    dstImg.Width,
                    dstImg.Height),
                ImageLockMode.WriteOnly,
                dstImg.PixelFormat);

            bool premultiplySrc = srcImg.PixelFormat == PixelFormat.Format32bppArgb;

            //Copies each pixel.
            byte* srcRow = (byte*)srcData.Scan0;
            byte* dstRow = (byte*)destData.Scan0;
            float alphaFactor;
            for (int y = 0; y < srcImg.Height; y++)
            {
                ColorBgra* src = (ColorBgra*)(srcRow + (y * srcData.Stride));
                ColorBgra* dst = (ColorBgra*)(dstRow + (y * destData.Stride));

                for (int x = 0; x < srcImg.Width; x++)
                {
                    alphaFactor = src->A / 255f;

                    if (alphaOnly)
                    {
                        dst->Bgra = dst->ConvertFromPremultipliedAlpha().Bgra;
                        dst->B = (byte)Math.Ceiling(dst->B * alphaFactor);
                        dst->G = (byte)Math.Ceiling(dst->G * alphaFactor);
                        dst->R = (byte)Math.Ceiling(dst->R * alphaFactor);
                        dst->A = src->A;
                    }
                    else
                    {
                        if (premultiplySrc)
                        {
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

            srcImg.UnlockBits(srcData);
            dstImg.UnlockBits(destData);
        }

        /// <summary>
        /// Create a GDI+ bitmap from the specified Paint.NET surface.
        /// </summary>
        /// <param name="surface">The surface.</param>
        /// <returns>The created bitmap.</returns>
        public static unsafe Bitmap CreateBitmapFromSurface(Surface surface)
        {
            Bitmap image = new Bitmap(surface.Width, surface.Height, PixelFormat.Format32bppPArgb);

            BitmapData bitmapData = image.LockBits(
                new Rectangle(0, 0, image.Width, image.Height),
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
        /// Replaces a portion of the destination bitmap with the surface bitmap using a brush as an alpha mask.
        /// </summary>
        /// <param name="surface">The surface contains the source bitmap. It will be drawn to dest.</param>
        /// <param name="dest">The bitmap to edit.</param>
        /// <param name="alphaMask">The brush used to draw.</param>
        /// <param name="location">The location to draw the brush at.</param>
        public static unsafe void OverwriteMasked(Surface surface, Bitmap dest, Bitmap alphaMask, Point location)
        {
            // Calculates the brush regions outside the bounding area of the surface.
            int negativeX = location.X < 0 ? -location.X : 0;
            int negativeY = location.Y < 0 ? -location.Y : 0;
            int extraX = Math.Max(location.X + alphaMask.Width - surface.Width, 0);
            int extraY = Math.Max(location.Y + alphaMask.Height - surface.Height, 0);

            int adjWidth = alphaMask.Width - negativeX - extraX;
            int adjHeight = alphaMask.Height - negativeY - extraY;

            if (adjWidth < 1 || adjHeight < 1 ||
                surface.Width != dest.Width || surface.Height != dest.Height)
            {
                return;
            }

            Rectangle adjBounds = new Rectangle(
                location.X < 0 ? 0 : location.X,
                location.Y < 0 ? 0 : location.Y,
                adjWidth,
                adjHeight);

            BitmapData destData = dest.LockBits(
                adjBounds,
                ImageLockMode.ReadWrite,
                dest.PixelFormat);

            BitmapData alphaMaskData = alphaMask.LockBits(
                new Rectangle(0, 0,
                alphaMask.Width,
                alphaMask.Height),
                ImageLockMode.ReadOnly,
                alphaMask.PixelFormat);

            byte* destRow = (byte*)destData.Scan0;
            byte* alphaMaskRow = (byte*)alphaMaskData.Scan0;
            byte* srcRow = (byte*)surface.Scan0.Pointer;
            float alphaFactor;

            for (int y = 0; y < adjHeight; y++)
            {
                ColorBgra* alphaMaskPtr = (ColorBgra*)(alphaMaskRow + negativeX * 4 + ((negativeY + y) * alphaMaskData.Stride));
                ColorBgra* srcPtr = (ColorBgra*)(srcRow + adjBounds.X * 4 + ((adjBounds.Y + y) * surface.Stride));
                ColorBgra* destPtr = (ColorBgra*)(destRow + (y * destData.Stride));

                for (int x = 0; x < adjWidth; x++)
                {
                    ColorBgra newColor = ColorBgra.Blend(
                        destPtr->ConvertFromPremultipliedAlpha(),
                        *srcPtr,
                        alphaMaskPtr->A);

                    alphaFactor = newColor.A / 255f;
                    destPtr->B = (byte)Math.Ceiling(newColor.B * alphaFactor);
                    destPtr->G = (byte)Math.Ceiling(newColor.G * alphaFactor);
                    destPtr->R = (byte)Math.Ceiling(newColor.R * alphaFactor);
                    destPtr->A = newColor.A;

                    alphaMaskPtr++;
                    destPtr++;
                    srcPtr++;
                }
            }

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
        /// The active RGBA color, and the amount of extra reduction in transparency from alpha jitter. This is kept
        /// separate since the userColor alpha is already computed and the jitter alpha contribution needs to be
        /// applied separately when the original brush colors are used with alpha jitter.
        /// </param>
        /// <param name="colorInfluence">The amount of mixing with the active color to perform, and which HSV channels to affect.</param>
        /// <param name="blendMode">Determines the algorithm uesd to draw the brush on dest.</param>
        /// <param name="lockAlpha">Whether to allow alpha to change or not.</param>
        /// <param name="wrapAround">Whether to draw clipped brush parts to the opposite side of the canvas.</param>
        public static unsafe void DrawMasked(
            Bitmap dest,
            Bitmap brush,
            Point location,
            (ColorBgra Color, int MinAlpha) userColor,
            (int Amount, bool H, bool S, bool V)? colorInfluence,
            BlendMode blendMode,
            bool lockAlpha,
            bool wrapAround)
        {
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
                new Rectangle(0, 0, dest.Width, dest.Height),
                ImageLockMode.ReadWrite,
                dest.PixelFormat);

            BitmapData brushData = brush.LockBits(
                new Rectangle(0, 0, brush.Width, brush.Height),
                ImageLockMode.ReadOnly,
                brush.PixelFormat);

            byte* destRow = (byte*)destData.Scan0;
            byte* brushRow = (byte*)brushData.Scan0;
            float alphaFactor;

            ColorBgra userColorAdj = userColor.Color.ConvertFromPremultipliedAlpha();
            HsvColor userColorAdjHSV = HsvColor.FromColor(userColorAdj);
            float minAlphaFactor = (255 - userColor.MinAlpha) / 255f;
            float userColorBlendFactor = colorInfluence != null ? colorInfluence.Value.Amount / 100f : 0;

            ColorBgra newColor, destCol;
            ColorBgra intermediateBGRA = ColorBgra.Black;
            HsvColor intermediateHSV;

            void draw(int brushXOffset, int brushYOffset, int destXOffset, int destYOffset, int destWidth, int destHeight)
            {
                for (int y = 0; y < destHeight; y++)
                {
                    ColorBgra* brushPtr = (ColorBgra*)(brushRow + brushXOffset * 4 + ((brushYOffset + y) * brushData.Stride));
                    ColorBgra* destPtr = (ColorBgra*)(destRow + destXOffset * 4 + ((y + destYOffset) * destData.Stride));

                    for (int x = 0; x < destWidth; x++)
                    {
                        // HSV shift the pixel according to the color influence when colorize brush is off.
                        if (colorInfluence != null)
                        {
                            intermediateBGRA = brushPtr->ConvertFromPremultipliedAlpha();

                            if (colorInfluence.Value.Amount != 0)
                            {
                                intermediateHSV = HsvColor.FromColor(intermediateBGRA);

                                if (colorInfluence.Value.H)
                                {
                                    intermediateHSV.Hue = (int)Math.Round(intermediateHSV.Hue
                                        + userColorBlendFactor * (userColorAdjHSV.Hue - intermediateHSV.Hue));
                                }
                                if (colorInfluence.Value.S)
                                {

                                    intermediateHSV.Saturation = (int)Math.Round(intermediateHSV.Saturation
                                        + userColorBlendFactor * (userColorAdjHSV.Saturation - intermediateHSV.Saturation));
                                }
                                if (colorInfluence.Value.V)
                                {
                                    intermediateHSV.Value = (int)Math.Round(intermediateHSV.Value
                                        + userColorBlendFactor * (userColorAdjHSV.Value - intermediateHSV.Value));
                                }

                                byte alpha = intermediateBGRA.A;
                                intermediateBGRA = ColorBgra.FromColor(intermediateHSV.ToColor());
                                intermediateBGRA.A = alpha;
                            }
                        }

                        // Perform a blend mode op on the pixel.
                        if (blendMode == BlendMode.Normal)
                        {
                            newColor = ColorBgra.Blend(
                                destPtr->ConvertFromPremultipliedAlpha(),
                                colorInfluence == null
                                    ? userColorAdj.NewAlpha((byte)Math.Clamp(userColorAdj.A + destPtr->A, 0, 255))
                                    : intermediateBGRA.NewAlpha((byte)Math.Clamp(brushPtr->A + destPtr->A, 0, 255)),
                                colorInfluence == null ? brushPtr->A : (byte)Math.Round(brushPtr->A * minAlphaFactor)
                            );

                            alphaFactor = (!lockAlpha)
                                ? newColor.A / 255f
                                : destPtr->A / 255f;

                            destPtr->B = (byte)Math.Ceiling(newColor.B * alphaFactor);
                            destPtr->G = (byte)Math.Ceiling(newColor.G * alphaFactor);
                            destPtr->R = (byte)Math.Ceiling(newColor.R * alphaFactor);
                            if (!lockAlpha) { destPtr->A = newColor.A; }
                        }
                        else if (blendMode == BlendMode.Overwrite)
                        {
                            destCol = destPtr->ConvertFromPremultipliedAlpha();
                            newColor = ColorBgra.Blend(
                                destCol,
                                colorInfluence == null ? userColorAdj : intermediateBGRA,
                                brushPtr->A
                            );

                            alphaFactor = (!lockAlpha)
                                ? (destCol.A + brushPtr->A / 255f * (userColorAdj.A - destCol.A)) / 255f
                                : destCol.A / 255f;

                            destPtr->B = (byte)Math.Ceiling(newColor.B * alphaFactor);
                            destPtr->G = (byte)Math.Ceiling(newColor.G * alphaFactor);
                            destPtr->R = (byte)Math.Ceiling(newColor.R * alphaFactor);
                            if (!lockAlpha) { destPtr->A = (byte)Math.Ceiling(alphaFactor * 255); }
                        }

                        brushPtr++;
                        destPtr++;
                    }
                }
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
        /// Returns the original bitmap data in another format by drawing it.
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
            GraphicsPath path = new GraphicsPath();
            PdnRegion newRegion = region.Clone();

            //The size to scale the region by.
            Matrix scalematrix = new Matrix(
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
                new Rectangle(0, 0,
                    image.Width,
                    image.Height),
                ImageLockMode.ReadWrite,
                image.PixelFormat);

            //The top left pixel's address, from which the rest can be found.
            byte* row = (byte*)bmpData.Scan0;

            //Iterates through each pixel and tests if the image is
            //transparent. A transparent image won't be converted.
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    int pos = y * bmpData.Stride + x * 4;
                    if (row[pos + 3] != 255)
                    {
                        //Exits if the image is transparent.
                        image.UnlockBits(bmpData);
                        return img;
                    }
                }
            }

            //Iterates through each pixel to apply the change.
            ColorBgra temp;
            for (int y = 0; y < image.Height; y++)
            {
                ColorBgra* dst = (ColorBgra*)(row + (y * bmpData.Stride));

                for (int x = 0; x < image.Width; x++)
                {
                    int pos = y * bmpData.Stride + x * 4;

                    temp = dst->ConvertFromPremultipliedAlpha();
                    byte newAlpha = (byte)Math.Ceiling((dst->R + dst->G + dst->B) / 3d);
                    float alphaFactor = newAlpha / 255f;

                    //Sets the alpha channel based on its intensity.
                    dst->Bgra = temp.Bgra;
                    dst->B = (byte)Math.Ceiling(dst->B * alphaFactor);
                    dst->G = (byte)Math.Ceiling(dst->G * alphaFactor);
                    dst->R = (byte)Math.Ceiling(dst->R * alphaFactor);
                    dst->A = newAlpha;

                    dst++;
                }
            }

            image.UnlockBits(bmpData);
            return image;
        }

        /// <summary>
        /// Pads the given bitmap to be square.
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
            //Performs nothing if there is no need.
            if (angle == 0 && maxAliasedAlpha == null)
            {
                return origBmp;
            }

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

                g.RotateTransform(angle);

                //Undoes the transform.
                g.TranslateTransform(-origBmp.Width / 2f, -origBmp.Height / 2f);

                //Draws the image.
                g.DrawImage(origBmp, 0, 0, origBmp.Width, origBmp.Height);
            }

            // Manual aliasing after transform, since there's no way to turn off rotation anti-aliasing in GDI+
            if (maxAliasedAlpha != null)
            {
                AliasImage(newBmp, maxAliasedAlpha.Value);
            }

            return newBmp;
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
        /// Returns a copy of the image scaled to the given size, optionally flipped and with color information applied.
        /// </summary>
        /// <param name="origBmp">The image to clone and scale.</param>
        /// <param name="newSize">The new width and height of the image.</param>
        /// <param name="flipX">Whether to flip the image horizontally.</param>
        /// <param name="flipY">Whether to flip the image vertically.</param>
        /// <param name="attr">If supplied, the recolor matrix will also be applied.</param>
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

        /// <summary>
        /// Takes a given setting value and adjusts it linearly via a target value using the given value-handling
        /// method (which decides what the target value is). Returns the value unaffected if no mapping is set. This
        /// doesn't clamp or prevent resulting invalid values.
        /// </summary>
        /// <param name="settingValue">The value of a setting, e.g. the brush transparency slider's value.</param>
        /// <param name="targetValue">A number used to influence the setting value according to the handling.</param>
        /// <param name="maxRange">The </param>
        /// <param name="inputRatio"></param>
        /// <param name="method"></param>
        public static int GetStrengthMappedValue(
            int settingValue,
            int targetValue,
            int maxRange,
            float inputRatio,
            CmbxTabletValueType.ValueHandlingMethod method)
        {
            switch (method)
            {
                case CmbxTabletValueType.ValueHandlingMethod.Add:
                    return (int)(settingValue + inputRatio * targetValue);
                case CmbxTabletValueType.ValueHandlingMethod.AddPercent:
                    return (int)(settingValue + inputRatio * targetValue / 100 * maxRange);
                case CmbxTabletValueType.ValueHandlingMethod.AddPercentCurrent:
                    return (int)(settingValue + inputRatio * targetValue / 100 * settingValue);
                case CmbxTabletValueType.ValueHandlingMethod.MatchValue:
                    return (int)((1 - inputRatio) * settingValue + inputRatio * targetValue);
                case CmbxTabletValueType.ValueHandlingMethod.MatchPercent:
                    return (int)((1 - inputRatio) * settingValue + inputRatio * targetValue / 100 * maxRange);
            }

            return settingValue;
        }
    }
    #endregion
}