using System;
using PaintDotNet;
using PaintDotNet.ComponentModel;
using PaintDotNet.Drawing;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;

using GdipBitmap = System.Drawing.Bitmap;

namespace DynamicDraw.Imaging
{
    /// <summary>
    /// Safely wraps an <see cref="IBitmap"/> so that it can also be used as a <see cref="System.Drawing.Bitmap"/> 
    /// without copying or other safety pitfalls.
    /// </summary>
    internal abstract unsafe class InteropBitmap
        : RefTrackedObject,
          IBitmap,
          IBitmapLock
    {
        private static readonly IImagingFactory imagingFactory = ImagingFactory.CreateRef();

        public static InteropBitmap<TPixel> CopyFrom<TPixel>(IBitmapSource<TPixel> source)
            where TPixel : unmanaged, INaturalPixelInfo
        {
            InteropBitmap<TPixel> bitmap = new InteropBitmap<TPixel>(source.Size);
            source.CopyPixels(bitmap.AsRegionPtr());
            return bitmap;
        }

        public static InteropBitmap<ColorBgra32> CopyFrom(Surface source)
        {
            // Surface implements IBitmap<ColorBgra32> if you ask for it
            using IBitmap<ColorBgra32> asBitmap = source.CreateRef<IBitmap<ColorBgra32>>();
            return CopyFrom(asBitmap);
        }

        private IBitmap bitmap;
        private IBitmapLock bitmapLock;
        private GdipBitmap gdipBitmap;

        protected InteropBitmap(SizeInt32 size, PixelFormat format)
        {
            bitmap = imagingFactory.CreateBitmap(size, format);
            bitmapLock = bitmap.Lock(new RectInt32(0, 0, bitmap.Size), BitmapLockOptions.ReadWrite);

            // Create a wrapper so we can easily interoperate with System.Drawing
            gdipBitmap = new GdipBitmap(size.Width, size.Height, bitmapLock.BufferStride, bitmap.PixelFormat.ToGdipPixelFormat(), (nint)bitmapLock.Buffer);

            // "Staple" ourself to the S.D.Bitmap to prevent issues where the IBitmap
            // is garbage collected but the S.D.Bitmap is still referenced. This would
            // result in a crash because the S.D.Bitmap is now pointing to memory that
            // has been freed, or it would result in random image corruption if the
            // bitmap is recycled.
            ObjectStapler.Add(gdipBitmap, this);
        }

        protected override void Dispose(bool disposing)
        {
            DisposableUtil.Free(ref gdipBitmap, disposing);
            DisposableUtil.Free(ref bitmapLock, disposing);
            DisposableUtil.Free(ref bitmap, disposing);
            base.Dispose(disposing);
        }

        protected IBitmap Bitmap
        {
            get => bitmap;
        }

        protected IBitmapLock BitmapLock
        {
            get => bitmapLock;
        }

        public Vector2Double Resolution
        {
            get => bitmap.Resolution;

            set => bitmap.Resolution = value;
        }

        public SizeInt32 Size => bitmap.Size;

        public int Width => Size.Width;

        public int Height => Size.Height;

        public RectInt32 Bounds => new RectInt32(Point2Int32.Zero, Size);

        public PixelFormat PixelFormat => bitmap.PixelFormat;

        Vector2Double IBitmapSource.Resolution => bitmap.Resolution;

        public void* Buffer => bitmapLock.Buffer;

        public int BufferStride => bitmapLock.BufferStride;

        public uint BufferSize => bitmapLock.BufferSize;

        public GdipBitmap AsGdipBitmap()
        {
            return this.gdipBitmap;
        }

        public void CopyPixels(void* pBuffer, int bufferStride, uint bufferSize, in RectInt32? srcRect)
        {
            bitmap.CopyPixels(pBuffer, bufferStride, bufferSize, srcRect);
        }

        public IPalette GetPalette()
        {
            return bitmap.GetPalette();
        }

        public IBitmapLock Lock()
        {
            return bitmapLock.CreateRef();
        }

        public IBitmapLock Lock(RectInt32 rect, BitmapLockOptions lockOptions)
        {
            return bitmap.Lock(rect, lockOptions);
        }

        public void SetPalette(IPalette palette)
        {
            bitmap.SetPalette(palette);
        }
    }
}
