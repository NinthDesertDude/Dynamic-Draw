using PaintDotNet;
using PaintDotNet.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;

namespace DynamicDraw
{
    /// <summary>
    /// Contains utility methods related directly to palletization.
    /// </summary>
    public static class PalletizingUtils
    {
        /// <summary>
        /// Generates a palette from a provided image using simple heuristics. First, it creates a dictionary of
        /// sampled pixels, then discards any colors that don't appear >= a certain number of occurrences, then
        /// computes color distance for the provided channels. The colors are returned sorted via a given method.
        /// </summary>
        /// <param name="targets">
        /// The colors used to measure color distance, and multipliers which increase their weight for matching a color
        /// vs. the other colors.
        /// </param>
        /// <param name="channelMults">
        /// Channel multipliers for consideration when measuring color distance (0 to disable a channel comparison).
        /// This can be used to e.g. set alpha to 1 and all others to 0.1 so that differences in alpha are ten times as
        /// sensitive.
        /// </param>
        /// <param name="sampleRegionSize">
        /// Averages the image into squares of the provided size. Partial regions still compute an average. Sizes less
        /// than one are treated as being 1 pixel. This is useful to cut noise, finding the most common colors as a
        /// means of overall profiling.
        /// </param>
        /// <param name="minCount">
        /// If not negative, a color must appear at least this many times to be included in the output list. If sample
        /// regions of 2x2 or greater are used, this compares against those regions instead of per-pixel. Use <= 0 to
        /// forego using a minimum count. This is useful to cut out noise/one-off pixels especially in paletted or
        /// palette-restrictive images.
        /// </param>
        /// <param name="maxDistance">
        /// If not negative, a color must have a distance equal or smaller than this to be included in the output. Use a
        /// negative value to have no maximum distance.
        /// </param>
        /// <param name="colorLimit">Limits the number of colors returned to the top X matches.</param>
        public static List<KeyValuePair<ColorBgra, double>> GeneratePalette(
            Bitmap img,
            HashSet<(Color col, float multiplier)> targets,
            (float r, float g, float b, float a, float h, float s, float v) channelMults,
            int sampleRegionSize = 1,
            int minCount = 0,
            float maxDistance = 0,
            int colorLimit = 0)
        {
            ConcurrentDictionary<int, ConcurrentDictionary<int, SumRgba>> sumMap = null;
            ConcurrentDictionary<ColorBgra, int> countMap = null;

            // Exit for invalid values.
            if (targets == null ||
                targets.Count == 0 ||
                sampleRegionSize < 1 ||
                minCount < 0 ||
                maxDistance < 0 ||
                colorLimit < 0)
            {
                return new();
            }

            // Set up which channels are expected.
            bool hasR = channelMults.r != 0;
            bool hasG = channelMults.g != 0;
            bool hasB = channelMults.b != 0;
            bool hasH = channelMults.h != 0;
            bool hasS = channelMults.s != 0;
            bool hasV = channelMults.v != 0;
            bool hasA = channelMults.a != 0;
            bool isHSV = hasH || hasS || hasV;

            // Bake a division into the channel multipliers so final range is 0..1 for any sum of channels.
            int channelDivision = (hasR ? 1 : 0) + (hasG ? 1 : 0) + (hasB ? 1 : 0) +
                (hasH ? 1 : 0) + (hasS ? 1 : 0) + (hasV ? 1 : 0) + (hasA ? 1 : 0);

            if (channelDivision == 0) { return new(); } // Exit when there's no operation.

            targets = targets
                .Select((o) => { o.multiplier /= channelDivision; return o; })
                .ToHashSet();

            // Dividing by the max channel range is used to regulate results into range 0..1 per-channel.
            channelMults.r /= 255f;
            channelMults.g /= 255f;
            channelMults.b /= 255f;
            channelMults.h /= 360f;
            channelMults.s /= 100f;
            channelMults.v /= 100f;
            channelMults.a /= 255f;

            // Uses a sampled map as the basis for calculations, if given.
            if (sampleRegionSize > 1)
            {
                sumMap = SumRegions(img, sampleRegionSize);
                countMap = CountColors(sumMap, sampleRegionSize);
            }

            // Gets the count for each unique color in the image.
            countMap ??= CountColors(img);

            //Sorts first all colors based on their distance to any target color, favoring small distances.
            ConcurrentDictionary<ColorBgra, double> colorDistanceMap = new();
            IEnumerable<HsvColor> targetsHSV = isHSV ? targets.Select((o) => HsvColor.FromColor(o.col)) : null;

            // Multiplies color distance per-channel, finding the minimum to a target and adding it.
            void calcDistance(KeyValuePair<ColorBgra, int> kvPair)
            {
                if (kvPair.Value >= minCount)
                {
                    HsvColor colorAsHsv = isHSV ? HsvColor.FromColor(kvPair.Key) : default;
                    float finalDist = float.MaxValue;
                    float dist;

                    for (int i = 0; i < targets.Count; i++)
                    {
                        Color targetColor = targets.ElementAt(i).col;
                        HsvColor targetColorHsv = isHSV ? targetsHSV.ElementAt(i) : default;
                        dist = 
                            (hasR ? Math.Abs(kvPair.Key.R - targetColor.R) * channelMults.r : 0) +
                            (hasG ? Math.Abs(kvPair.Key.G - targetColor.G) * channelMults.g : 0) +
                            (hasB ? Math.Abs(kvPair.Key.B - targetColor.B) * channelMults.b : 0) +
                            (hasH ? Math.Abs(colorAsHsv.Hue - targetColorHsv.Hue) * channelMults.h : 0) +
                            (hasS ? Math.Abs(colorAsHsv.Saturation - targetColorHsv.Saturation) * channelMults.s : 0) +
                            (hasV ? Math.Abs(colorAsHsv.Value - targetColorHsv.Value) * channelMults.h : 0) +
                            (hasA ? Math.Abs(kvPair.Key.A - targetColor.A) * channelMults.a : 0)
                            * targets.ElementAt(i).multiplier;

                        if (dist < finalDist)
                        {
                            finalDist = dist;
                        }
                    }

                    // Associates this color to only the smallest distance among target colors, if it gets added.
                    if (maxDistance == 0 || finalDist <= maxDistance)
                    {
                        colorDistanceMap.TryAdd(kvPair.Key, finalDist);
                    }
                }
            }

            Parallel.ForEach(countMap, calcDistance);

            // Sorts colors with small distances from target colors to the top, then cuts the number.
            return colorLimit > 0 && colorLimit < colorDistanceMap.Count
                ? colorDistanceMap
                    .OrderBy((o) => o.Value)
                    .Take(colorLimit)
                    .ToList()
                : colorDistanceMap
                    .OrderBy((o) => o.Value)
                    .ToList();
        }

        /// <summary>
        /// Computes sums of the RGB channels into a tuple dictionary structure indexed first by Y, then X. The size of
        /// this map is the dimensions of the image divided by regionSize, rounded up and contains the raw sums of all
        /// RGB values in the associated regions. For example, [2, 3] with regionSize 3 would be the sum of pixels in
        /// the rectangle defined from {6, 9} to {9, 12} inclusive.
        /// </summary>
        /// <param name="regionSize">
        /// Divides the image into NxN squares, which determines the size of the returned map. Values are concurrently
        /// appended to their respective locations. Note that the sum for incomplete squares will not contain NxN values
        /// in the sum, and if such a square is averaged, it should be divided by less than NxN accordingly.
        /// </param>
        public static unsafe ConcurrentDictionary<int, ConcurrentDictionary<int, SumRgba>> SumRegions(Bitmap img, int regionSize)
        {
            if (regionSize <= 0) { regionSize = 1; }

            var regions = new ConcurrentDictionary<int, ConcurrentDictionary<int, SumRgba>>();

            BitmapData bmpData = img.LockBits(
                img.GetBounds(),
                ImageLockMode.ReadOnly,
                img.PixelFormat);

            byte* row = (byte*)bmpData.Scan0;

            Rectangle[] rois = DrawingUtils.GetRois(img.Width, img.Height);
            Parallel.For(0, rois.Length, (i, loopState) =>
            {
                Rectangle roi = rois[i];
                for (int y = roi.Y; y < roi.Y + roi.Height; y++)
                {
                    int yBucket = y / regionSize; //implicitly floored on purpose

                    if (!regions.ContainsKey(yBucket))
                    {
                        regions.TryAdd(yBucket, new());
                    }

                    ColorBgra* dstPtr = (ColorBgra*)(row + (y * bmpData.Stride) + (roi.X * 4));
                    for (int x = roi.X; x < roi.X + roi.Width; x++)
                    {
                        int xBucket = x / regionSize; //implicitly floored on purpose

                        regions[yBucket].AddOrUpdate(xBucket,
                            (_) => new((*dstPtr).ConvertFromPremultipliedAlpha()),
                            (_, color) =>
                            {
                                ColorBgra pixel = (*dstPtr).ConvertFromPremultipliedAlpha();
                                return new(
                                color.r + pixel.R,
                                color.g + pixel.G,
                                color.b + pixel.B,
                                color.a + dstPtr->A);
                            });

                        dstPtr++;
                    }
                }
            });

            img.UnlockBits(bmpData);

            return regions;
        }

        /// <summary>
        /// Returns a new dictionary keyed by each unique color in the image, with a count of occurrences.
        /// </summary>
        public static unsafe ConcurrentDictionary<ColorBgra, int> CountColors(Bitmap img)
        {
            ConcurrentDictionary<ColorBgra, int> palette = new();

            BitmapData bmpData = img.LockBits(
                img.GetBounds(),
                ImageLockMode.ReadOnly,
                img.PixelFormat);

            byte* row = (byte*)bmpData.Scan0;

            Rectangle[] rois = DrawingUtils.GetRois(img.Width, img.Height);
            Parallel.For(0, rois.Length, (i, loopState) =>
            {
                Rectangle roi = rois[i];
                for (int y = roi.Y; y < roi.Y + roi.Height; y++)
                {
                    ColorBgra* dstPtr = (ColorBgra*)(row + (y * bmpData.Stride) + (roi.X * 4));
                    for (int x = roi.X; x < roi.X + roi.Width; x++)
                    {
                        palette.AddOrUpdate(
                            (*dstPtr).ConvertFromPremultipliedAlpha(),
                            (color) => 1,
                            (color, count) => count + 1);

                        dstPtr++;
                    }
                }
            });

            img.UnlockBits(bmpData);
            return palette;
        }

        /// <summary>
        /// Returns a new dictionary keyed by each unique color in the map, with a count of occurrences.
        /// </summary>
        public static unsafe ConcurrentDictionary<ColorBgra, int> CountColors(
            ConcurrentDictionary<int, ConcurrentDictionary<int, SumRgba>> map,
            int regionSize)
        {
            ConcurrentDictionary<ColorBgra, int> palette = new();
            if (map == null || map.IsEmpty) { return palette; }

            int pixelsPerRegion = regionSize * regionSize;
            Rectangle[] rois = DrawingUtils.GetRois(map[0].Count, map.Count);
            Parallel.For(0, rois.Length, (i, loopState) =>
            {
                Rectangle roi = rois[i];
                for (int y = roi.Y; y < roi.Y + roi.Height; y++)
                {
                    for (int x = roi.X; x < roi.X + roi.Width; x++)
                    {
                        palette.AddOrUpdate(
                            map[y][x].ToBGRA(pixelsPerRegion),
                            (color) => 0,
                            (color, count) => count + 1);
                    }
                }
            });

            return palette;
        }
    }
}