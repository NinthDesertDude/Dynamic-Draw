// Adapted from https://github.com/EsotericSoftware/hsl under MIT license. Original below:

/* Copyright (c) 2016 Alexei Boronine
 * Copyright (c) 2022 Nathan Sweet
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

namespace DynamicDraw
{
	/** Stores a color in the HSLuv color space. Provides conversion to and from RGB. Interpolation is done without introducing new
	 * intermediary hues and without losing brightness. Conversion and interpolation do not allocate.
	 * <p>
	 * Hue is in degrees, 0-360. Saturation and lightness are percentages, 0-1.
	 * <p>
	 * Based on: https://github.com/hsluv/hsluv-java/ and https://www.hsluv.org/
	 * @author Nathan Sweet */
	public class HSLuv
	{
		/// <summary>
		/// Color adjustment matrix of values from 0 to 1 for R, G, B that modifies based on human perception (sourced from
		/// CIElab studies).
		/// </summary>
		public static readonly float[][] RGBAdjustMatrix = new float[][] {
			new float[] {3.240969941904521f, -1.537383177570093f, -0.498610760293f},
			new float[] {-0.96924363628087f, 1.87596750150772f, 0.041555057407175f},
			new float[] {0.055630079696993f, -0.20397695888897f, 1.056971514242878f}};

		/// <summary>
		/// Inverse of the color adjustment matrix <see cref="RGBAdjustMatrix"/>.
		/// </summary>
		public static readonly float[][] rgbAdjustMatrixInverse = new float[][] {
			new float[] {0.41239079926595f, 0.35758433938387f, 0.18048078840183f},
			new float[] {0.21263900587151f, 0.71516867876775f, 0.072192315360733f},
			new float[] {0.019330818715591f, 0.11919477979462f, 0.95053215224966f}};

		private static readonly float refU = 0.19783000664283f;
		private static readonly float refV = 0.46831999493879f;
		public static readonly float kappa = 9.032962962f;
		public static readonly float epsilon = 0.0088564516f;
		private static readonly float radToDeg = (float)(180f / Math.PI);
		private static readonly float degToRad = (float)(Math.PI / 180);

		public float h, s, l;
		private Rgb rgb = new Rgb();

		public HSLuv()
		{
		}

		public HSLuv(HSLuv hsl)
		{
			Set(hsl);
		}

		public HSLuv(float h, float s, float l)
		{
			Set(h, s, l);
		}

		public HSLuv(Rgb rgb)
		{
			SetFromRgb(rgb);
		}

		public void Set(HSLuv hsl)
		{
			h = hsl.h;
			s = hsl.s;
			l = hsl.l;
		}

		public void Set(float h, float s, float l)
		{
			this.h = h < 0 ? 0 : (h > 360 ? 360 : h);
			this.s = s < 0 ? 0 : (s > 1 ? 1 : s);
			this.l = l < 0 ? 0 : (l > 1 ? 1 : l);
		}

		public void SetFromRgb(Rgb rgb)
		{
			SetFromRgb(rgb.r, rgb.g, rgb.b, false);
		}

		public void SetFromRgb(float r, float g, float b)
		{
			SetFromRgb(r, g, b, false);
		}

		public void FromRgb(int rgb)
		{
			float r = ((uint)(rgb & 0xff0000) >> 16) / 255f;
			float g = ((uint)(rgb & 0x00ff00) >> 8) / 255f;
			float b = (rgb & 0x0000ff) / 255f;
			SetFromRgb(r, g, b, false);
		}

		private void SetFromRgb(float r, float g, float b, bool keepL)
		{
			// RGB to XYZ
			r = RgbToXyz(r);
			g = RgbToXyz(g);
			b = RgbToXyz(b);
			float X = DotProduct(rgbAdjustMatrixInverse[0], r, g, b), Y = DotProduct(rgbAdjustMatrixInverse[1], r, g, b), Z = DotProduct(rgbAdjustMatrixInverse[2], r, g, b);

			// XYZ to Luv
			float L = keepL ? l : (Y <= epsilon ? Y * kappa : 1.16f * (float)Math.Pow(Y, 1 / 3f) - 0.16f), U, V;
			if (L < 0.00001f)
			{
				L = 0;
				U = 0;
				V = 0;
			}
			else
			{
				U = 13 * L * (4 * X / (X + 15 * Y + 3 * Z) - refU);
				V = 13 * L * (9 * Y / (X + 15 * Y + 3 * Z) - refV);
			}

			// Luv to Lch
			float C = (float)Math.Sqrt(U * U + V * V);
			if (C < 0.00001f)
            {
				h = 0;
			}
			else
			{
				h = (float)Math.Atan2(V, U) * radToDeg;

				if (h < 0)
				{
					h += 360;
				}
			}

			// Lch to HSLuv
			if (L > 0.99999f)
			{
				s = 0;
				l = 1;
			}
			else if (L < 0.00001f)
			{
				s = 0;
				l = 0;
			}
			else
			{
				s = Math.Min(C / GetMaxChromaForLH(L, h * degToRad), 1);
				l = L;
			}
		}

		/// <summary>
		/// Converts from HSLuv to RGB colorspace, returning the resulting RGB color.
		/// </summary>
		public Rgb ToRgb()
		{
			float hueInRadians = h * degToRad, L = l;
			float chroma;

			// HSLuv to Lch
			if (L > 0.99999f)
			{
				L = 1;
				chroma = 0;
			}
			else if (L < 0.00001f)
			{
				L = 0;
				chroma = 0;
			}
			else
            {
				chroma = GetMaxChromaForLH(L, hueInRadians) * s;
			}

			// Lch to Luv
			float U = (float)Math.Cos(hueInRadians) * chroma;
			float V = (float)Math.Sin(hueInRadians) * chroma;

			// Luv to XYZ
			float X, Y, Z;
			if (L < 0.00001f)
			{
				X = 0;
				Y = 0;
				Z = 0;
			}
			else
			{
				if (L <= 0.08f)
					Y = L / kappa;
				else
				{
					Y = (L + 0.16f) / 1.16f;
					Y *= Y * Y;
				}
				float varU = U / (13 * L) + refU;
				float varV = V / (13 * L) + refV;
				X = 9 * varU * Y / (4 * varV);
				Z = (3 * Y / varV) - X / 3 - 5 * Y;
			}

			// XYZ to RGB
			Rgb color = new Rgb();
			color.r = DotProduct(RGBAdjustMatrix[0], X, Y, Z);
			color.g = DotProduct(RGBAdjustMatrix[1], X, Y, Z);
			color.b = DotProduct(RGBAdjustMatrix[2], X, Y, Z);
			return color;
		}

		public void Lerp(HSLuv target, float a)
		{
			if (a == 0) { return; }
			if (a == 1) { Set(target); return; }

			l += (target.l - l) * a;
			rgb = ToRgb();
			target.rgb = target.ToRgb();

			rgb.r += (target.rgb.r - rgb.r) * a;
			rgb.g += (target.rgb.g - rgb.g) * a;
			rgb.b += (target.rgb.b - rgb.b) * a;

			SetFromRgb(XyzToRgb(rgb.r), XyzToRgb(rgb.g), XyzToRgb(rgb.b), true);
		}

		/// <summary>
		/// Creates a new color from the given two HSLuv colors, linearly interpolated by the fraction from 0 to 1.
		/// </summary>
		public static HSLuv Lerp(HSLuv first, HSLuv second, float fraction)
        {
			HSLuv newColor = new HSLuv(first);
			if (fraction == 0) { return newColor; }
			if (fraction == 1) { newColor.Set(second); return newColor; }

			newColor.l += (second.l - newColor.l) * fraction;
			newColor.rgb = newColor.ToRgb();
			second.rgb = second.ToRgb();

			newColor.rgb.r += (second.rgb.r - newColor.rgb.r) * fraction;
			newColor.rgb.g += (second.rgb.g - newColor.rgb.g) * fraction;
			newColor.rgb.b += (second.rgb.b - newColor.rgb.b) * fraction;

			newColor.SetFromRgb(XyzToRgb(newColor.rgb.r), XyzToRgb(newColor.rgb.g), XyzToRgb(newColor.rgb.b), true);
			return newColor;
		}

		private static float GetMaxChromaForLH(float L, float Hrad)
		{
			float sin = (float)Math.Sin(Hrad);
			float cos = (float)Math.Cos(Hrad);
			float sub1 = (L + 0.16f) / 1.16f;
			sub1 *= sub1 * sub1;
			float sub2 = sub1 > epsilon ? sub1 : L / kappa;
			float min = float.MaxValue;

			for (int i = 0; i < 3; i++)
			{
				float m1 = RGBAdjustMatrix[i][0] * sub2, m2 = RGBAdjustMatrix[i][1] * sub2, m3 = RGBAdjustMatrix[i][2] * sub2;
				for (int t = 0; t < 2; t++)
				{
					float top1 = 2845.17f * m1 - 948.39f * m3;
					float top2 = (8384.22f * m3 + 7698.60f * m2 + 7317.18f * m1 - 7698.60f * t) * L;
					float bottom = (6322.60f * m3 - 1264.52f * m2) + 1264.52f * t;
					float length = IntersectLength(sin, cos, top1 / bottom, top2 / bottom);
					if (length >= 0)
					{
						min = Math.Min(min, length);
					}
				}
			}

			return min;
		}

		private static float IntersectLength(float sin, float cos, float line1, float line2)
		{
			return line2 / (sin - line1 * cos);
		}

		public static float XyzToRgb(float value)
		{
			return value <= 0.0031308f ? value * 12.92f : (float)(Math.Pow(value, 1 / 2.4f) * 1.055f - 0.055f);
		}

		public static float RgbToXyz(float value)
		{
			return value <= 0.04045f ? value / 12.92f : (float)Math.Pow((value + 0.055f) / 1.055f, 2.4f);
		}

		private static float DotProduct(float[] a, float b0, float b1, float b2)
		{
			return a[0] * b0 + a[1] * b1 + a[2] * b2;
		}
	}
}