// Adapted from https://github.com/EsotericSoftware/hsl under MIT license. Original below:

/* Copyright (c) 2022 Nathan Sweet
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Drawing;

namespace DynamicDraw
{
	/// <summary>
	/// Stores an RGB color. Interpolation is done without losing brightness. (Contributed by Nathan Sweet)
	/// </summary>
	public class Rgb
	{
		public float r, g, b;

		public Rgb()
		{
			r = 0;
			g = 0;
			b = 0;
		}

		public Rgb(Rgb rgb)
		{
			Set(rgb);
		}

		/// <summary>
		/// Expects values between 0 to 1 for each channel.
		/// </summary>
		public Rgb(float r, float g, float b)
		{
			Set(r, g, b);
		}

		public Rgb(Color color)
        {
			Set(color.R / 255f, color.G / 255f, color.B / 255f);
        }

		public void Set(Rgb rgb)
		{
			r = rgb.r < 0 ? 0 : (rgb.r > 1 ? 1 : rgb.r);
			g = rgb.g < 0 ? 0 : (rgb.g > 1 ? 1 : rgb.g);
			b = rgb.b < 0 ? 0 : (rgb.b > 1 ? 1 : rgb.b);
		}

		public void Set(float r, float g, float b)
		{
			this.r = r;
			this.g = g;
			this.b = b;
		}

		public void Set(int rgb)
		{
			r = ((uint)(rgb & 0xff0000) >> 16) / 255f;
			g = ((uint)(rgb & 0x00ff00) >> 8) / 255f;
			b = (rgb & 0x0000ff) / 255f;
		}

		/// <summary>
		/// Returns a new color between the two colors based on fraction (a value from 0 to 1).
		/// </summary>
		public static Rgb Lerp(Rgb from, Rgb to, float fraction)
		{
			if (fraction == 0) { return from; }
			if (fraction == 1) { return to; }

			float r = HSLuv.RgbToXyz(from.r);
			float g = HSLuv.RgbToXyz(from.g);
			float b = HSLuv.RgbToXyz(from.b);
			float r2 = HSLuv.RgbToXyz(to.r);
			float g2 = HSLuv.RgbToXyz(to.g);
			float b2 = HSLuv.RgbToXyz(to.b);
			float L = RGBToL(r, g, b);

			// Lerps RGB and then corrects with lerped brightness.
			L += (RGBToL(r2, g2, b2) - L) * fraction;
			r += (r2 - r) * fraction;
			g += (g2 - g) * fraction;
			b += (b2 - b) * fraction;

			float L2 = RGBToL(r, g, b);
			float scale = L2 < 0.00001f ? 1 : L / L2;

			return new Rgb(HSLuv.XyzToRgb(r * scale), HSLuv.XyzToRgb(g * scale), HSLuv.XyzToRgb(b * scale));
		}

		public int ToInt()
		{
			return ((int)(255 * r) << 16) | ((int)(255 * g) << 8) | ((int)(255 * b));
		}

		public override bool Equals(object o)
		{
			if (o == null || o.GetType() != typeof(Rgb))
			{
				return false;
			}

			Rgb other = (Rgb)o;
			return (int)(255 * r) == (int)(255 * other.r)
				&& (int)(255 * g) == (int)(255 * other.g)
				&& (int)(255 * b) == (int)(255 * other.b);
		}

		public override int GetHashCode()
		{
			int result = (int)(255 * r);
			result = 31 * result + (int)(255 * g);
			return 31 * result + (int)(255 * b);
		}

		/// <summary>
		/// Converts to a GDI+ Color.
		/// </summary>
		public Color ToColor()
        {
			return Color.FromArgb(
				(int)Math.Round(r * 255),
				(int)Math.Round(g * 255),
				(int)Math.Round(b * 255));
        }

		private static float RGBToL(float r, float g, float b)
		{
			float Y = HSLuv.rgbAdjustMatrixInverse[1][0] * r + HSLuv.rgbAdjustMatrixInverse[1][1] * g + HSLuv.rgbAdjustMatrixInverse[1][2] * b;
			return Y <= HSLuv.epsilon ? Y * HSLuv.kappa : 1.16f * (float)Math.Pow(Y, 1 / 3f) - 0.16f;
		}
	}
}