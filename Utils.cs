using PaintDotNet;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace BrushFactory
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
        public static unsafe void ColorImage(Bitmap img, Color color, float alpha)
        {
            BitmapData bmpData = img.LockBits(
                new Rectangle(0, 0,
                    img.Width,
                    img.Height),
                ImageLockMode.ReadOnly,
                img.PixelFormat);

            byte* row = (byte*)bmpData.Scan0;
            for (int y = 0; y < img.Height; y++)
            {
                for (int x = 0; x < img.Width; x++)
                {
                    int ptr = y * bmpData.Stride + x * 4;
                    Color pixel = Color.FromArgb(row[ptr + 3], row[ptr + 2], row[ptr + 1], row[ptr]);

                    row[ptr + 3] = (byte)(pixel.A * alpha);
                    row[ptr + 2] = color.R;
                    row[ptr + 1] = color.G;
                    row[ptr] = color.B;
                }
            }
            img.UnlockBits(bmpData);
        }

        /// <summary>
        /// Multiplies alpha by an amount.
        /// </summary>
        /// <param name="img">
        /// The affected image.
        /// </param>
        /// <param name="alpha">A value from 0 to 1 to multiply with.</param>
        public static unsafe void ColorImage(Bitmap img, float alpha)
        {
            BitmapData bmpData = img.LockBits(
                new Rectangle(0, 0,
                    img.Width,
                    img.Height),
                ImageLockMode.ReadOnly,
                img.PixelFormat);

            byte* row = (byte*)bmpData.Scan0;
            for (int y = 0; y < img.Height; y++)
            {
                for (int x = 0; x < img.Width; x++)
                {
                    int ptr = y * bmpData.Stride + x * 4;
                    Color pixel = Color.FromArgb(row[ptr + 3], row[ptr + 2], row[ptr + 1], row[ptr]);

                    row[ptr + 3] = (byte)(pixel.A * alpha);
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
            Bitmap img,
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
        /// Overwrites alpha from one identically-sized bitmap on another.
        /// Both images must have PixelFormat.Format32bppArgb.
        /// Returns success.
        /// </summary>
        /// <param name="srcImg">
        /// The image to copy alpha from.
        /// </param>
        /// <param name="dstImg">
        /// The image to have its alpha overwritten.
        /// </param>
        public static unsafe bool CopyAlpha(Bitmap srcImg, Bitmap dstImg)
        {
            //Formats and size must be the same.
            if (srcImg.PixelFormat != PixelFormat.Format32bppArgb ||
                dstImg.PixelFormat != PixelFormat.Format32bppArgb ||
                srcImg.Width != dstImg.Width ||
                srcImg.Height != dstImg.Height)
            {
                return false;
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

            //Iterates through each pixel (repeating bytes of argb), skipping
            //to the 'a' byte and copying it from the src over the dst bmp.
            byte* srcRow = (byte*)srcData.Scan0;
            byte* dstRow = (byte*)destData.Scan0;
            for (int y = 0; y < srcImg.Height; y++)
            {
                for (int x = 0; x < srcImg.Width; x++)
                {
                    int ptr = y * srcData.Stride + x * 4;
                    dstRow[ptr + 3] = srcRow[ptr + 3];
                }
            }

            srcImg.UnlockBits(srcData);
            dstImg.UnlockBits(destData);

            return true;
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
        public static unsafe bool CopyBitmapPure(Bitmap srcImg, Bitmap dstImg)
        {
            //TODO: Find the underlying issue and stop using workarounds.

            //Formats and size must be the same.
                if (srcImg.PixelFormat != PixelFormat.Format32bppArgb ||
                dstImg.PixelFormat != PixelFormat.Format32bppArgb ||
                srcImg.Width != dstImg.Width ||
                srcImg.Height != dstImg.Height)
            {
                return false;
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

            //Copies each pixel.
            byte* srcRow = (byte*)srcData.Scan0;
            byte* dstRow = (byte*)destData.Scan0;
            for (int y = 0; y < srcImg.Height; y++)
            {
                for (int x = 0; x < srcImg.Width; x++)
                {
                    int ptr = y * srcData.Stride + x * 4;

                    dstRow[ptr] = srcRow[ptr];
                    dstRow[ptr + 1] = srcRow[ptr + 1];
                    dstRow[ptr + 2] = srcRow[ptr + 2];
                    dstRow[ptr + 3] = srcRow[ptr + 3];
                }
            }

            srcImg.UnlockBits(srcData);
            dstImg.UnlockBits(destData);

            return true;
        }

        /// <summary>
        /// Returns the original bitmap data in another format by drawing it.
        /// </summary>
        public static Bitmap FormatImage(Bitmap img, PixelFormat format)
        {
            Bitmap clone = new Bitmap(img.Width, img.Height, format);
            using (Graphics gr = Graphics.FromImage(clone))
            {
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
            Bitmap image = FormatImage(img, PixelFormat.Format32bppArgb);

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
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    int pos = y * bmpData.Stride + x * 4;

                    //Sets the alpha channel based on its intensity.
                    row[pos + 3] = (byte)((row[pos + 2] + row[pos + 1] + row[pos]) / 3);
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
            Bitmap newImg = new Bitmap(size, size);

            using (Graphics graphics = Graphics.FromImage(newImg))
            {
                graphics.DrawImage(img,
                    (size - img.Width) / 2,
                    (size - img.Height) / 2,
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
        public static Bitmap RotateImage(Bitmap origBmp, float angle)
        {
            //Performs nothing if there is no need.
            if (angle == 0)
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
            Bitmap newBmp = new Bitmap(newWidth, newHeight);
            using (Graphics g = Graphics.FromImage(newBmp))
            {
                //Uses matrices to centrally-rotate the original image.
                g.TranslateTransform(
                    (float)(newWidth - origBmp.Width) / 2,
                    (float)(newHeight - origBmp.Height) / 2);

                g.TranslateTransform(
                    (float)origBmp.Width / 2,
                    (float)origBmp.Height / 2);

                g.RotateTransform(angle);

                //Undoes the transform.
                g.TranslateTransform(-(float)origBmp.Width / 2, -(float)origBmp.Height / 2);

                //Draws the image.
                g.DrawImage(origBmp, 0, 0, origBmp.Width, origBmp.Height);
                return newBmp;
            }
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
        /// Returns a copy of the image scaled to the given size.
        /// </summary>
        /// <param name="origBmp">
        /// The image to clone and scale.
        /// </param>
        /// <param name="newSize">
        /// The new width and height of the image.
        /// </param>
        public static Bitmap ScaleImage(Bitmap origBmp, Size newSize)
        {
            //Creates the new image and a graphic canvas to draw the rotation.
            Bitmap newBmp = new Bitmap(newSize.Width, newSize.Height);
            using (Graphics g = Graphics.FromImage(newBmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                g.DrawImage(origBmp, 0, 0, newSize.Width, newSize.Height);
                return newBmp;
            }
        }
		#endregion
	}
}