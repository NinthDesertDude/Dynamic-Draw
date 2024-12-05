using PaintDotNet;

namespace DynamicDraw
{
	/// <summary>
	/// Stores an RGBA color with 32-bit precision per channel, intended to store many additions of 32-bit colors. It does
	/// not include the number of colors that make up this sum.
	/// </summary>
	public readonly struct SumRgba
	{
		public readonly int r, g, b, a;

		public SumRgba()
		{
			r = 0;
			g = 0;
			b = 0;
			a = 0;
		}

		/// <summary>
		/// Expects values between 0 to 1 for each channel.
		/// </summary>
		public SumRgba(int rSum, int gSum, int bSum, int aSum)
		{
            r = rSum;
            g = gSum;
            b = bSum;
            a = aSum;
        }

		public SumRgba(ColorBgra color)
		{
			r = color.R;
			g = color.G;
			b = color.B;
			a = color.A;
		}

		/// <summary>
		/// Convert to a BGRA color instance, dividing each field by the given sumCount (if provided). The division is
		/// a convenience to get the average channel values, since this represents a summation of pixels.
		/// </summary>
		/// <param name="sumCount">
		/// How many pixels are in this color. A 2x2 region would have 4, for example. Used to get the average.
		/// </param>
		public readonly ColorBgra ToBGRA(int sumCount = 1)
		{
			return ColorBgra.FromBgra(
				(byte)(b / (float)sumCount),
                (byte)(g / (float)sumCount),
                (byte)(r / (float)sumCount),
                (byte)(a / (float)sumCount));
        }
	}
}