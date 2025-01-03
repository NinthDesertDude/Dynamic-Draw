using System;
using PaintDotNet.ComponentModel;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;

namespace DynamicDraw.Imaging
{
    internal sealed unsafe class InteropBitmap<TPixel>
        : InteropBitmap,
          IBitmap<TPixel>,
          IBitmapLock<TPixel>
          where TPixel : unmanaged, INaturalPixelInfo
    {
        private IBitmap<TPixel> bitmapT;
        private IBitmapLock<TPixel> bitmapLockT;

        public InteropBitmap(SizeInt32 size)
            : base(size, default(TPixel).PixelFormat)
        {
            bitmapT = Bitmap.CreateRef<IBitmap<TPixel>>();
            bitmapLockT = BitmapLock.CreateRef<IBitmapLock<TPixel>>();
        }

        public InteropBitmap(int width, int height)
            : this(new SizeInt32(width, height))
        {
        }            

        public new TPixel* Buffer => bitmapLockT.Buffer;

        public new IBitmapLock<TPixel> Lock(RectInt32 rect, BitmapLockOptions lockOptions)
        {
            return bitmapT.Lock(rect, lockOptions);
        }
    }
}
