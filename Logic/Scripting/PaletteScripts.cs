using PaintDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace DynamicDraw
{
    /// <summary>
    /// Customization related to special handling of colors for palette files, like the kind that users will provide to
    /// Paint.NET.
    /// </summary>
    public class PaletteScripts
    {
        #region Special variables in Palette File API
        public static readonly string PaletteFileGradientCommand = "gradient";
        public static readonly string PaletteFilePrimaryKeyword = "primary";
        public static readonly string PaletteFileSecondaryKeyword = "secondary";
        #endregion

        /// <summary>
        /// Attempts to create a gradient by interpreting a string. The text is comma-delimited and describes a start
        /// color, end color, count, and any of the characters RGBHSVA (channels to exclude if given).
        /// Example: ff00ff, 00ffdd, 10, RG which will interpolate only blue, resulting in ffffdd.
        /// 
        /// Returns a list of colors, or none if it was malformed or empty.
        /// </summary>
        public static List<Color> GetGradientFromText(string text, Color primary, Color secondary)
        {
            List<Color> colors = new();

            if (string.IsNullOrEmpty(text)) { return colors; }

            var chunks = text.Trim().ToLower().Split(",");
            if (chunks.Length < 3) { return colors; }

            Color? startColor = 
                chunks[0].StartsWith(PaletteFilePrimaryKeyword)
                    ? GetModifiedColorFromText(chunks[0][PaletteFilePrimaryKeyword.Length..], primary)
                : chunks[0].StartsWith(PaletteFileSecondaryKeyword)
                    ? GetModifiedColorFromText(chunks[0][PaletteFileSecondaryKeyword.Length..], secondary)
                : GetModifiedColorFromText(chunks[0]);
            if (startColor == null) { return colors; }

            chunks[1] = chunks[1].Trim();
            Color? endColor =
                chunks[1].StartsWith(PaletteFilePrimaryKeyword)
                    ? GetModifiedColorFromText(chunks[1][PaletteFilePrimaryKeyword.Length..], primary)
                : chunks[1].StartsWith(PaletteFileSecondaryKeyword)
                    ? GetModifiedColorFromText(chunks[1][PaletteFileSecondaryKeyword.Length..], secondary)
                : GetModifiedColorFromText(chunks[1]);
            if (endColor == null) { return colors; }

            int? count = int.TryParse(chunks[2].Trim(), out int result) ? result : null;
            if (count == null || count < 0 || count > ColorUtils.MaxPaletteSize) { return colors; }

            if (chunks.Length >= 4 && !string.IsNullOrEmpty(chunks[3].Trim()))
            {
                chunks[3] = chunks[3].Trim();
                endColor = Color.FromArgb(
                    chunks[3].Contains('a') ? startColor.Value.A : endColor.Value.A,
                    chunks[3].Contains('r') ? startColor.Value.R : endColor.Value.R,
                    chunks[3].Contains('g') ? startColor.Value.G : endColor.Value.G,
                    chunks[3].Contains('b') ? startColor.Value.B : endColor.Value.B);
            }

            HsvColor startHsv = HsvColor.FromColor(startColor.Value);
            for (int i = 0; i < count; i++)
            {
                float fraction = i / (float)count;

                Color color = Color.FromArgb(
                    (int)Math.Round(startColor.Value.A + fraction * (endColor.Value.A - startColor.Value.A)),
                    Rgb.Lerp(new Rgb(startColor.Value), new Rgb(endColor.Value), fraction).ToColor());

                HsvColor colorHsv = HsvColor.FromColor(color);

                if (chunks.Length >= 4 && !string.IsNullOrWhiteSpace(chunks[3]))
                {
                    if (chunks[3].Contains('h')) { colorHsv.Hue = startHsv.Hue; }
                    if (chunks[3].Contains('s')) { colorHsv.Saturation = startHsv.Saturation; }
                    if (chunks[3].Contains('v')) { colorHsv.Value = startHsv.Value; }
                    color = colorHsv.ToColor();
                }

                colors.Add(color);
            }

            return colors;
        }

        /// <summary>
        /// Returns the modified parsed color from the text, or null if it's invalid. A color is a case-insensitive
        /// hex string of six characters 0-9 and a-f, followed by a space-delimited list of modifiers each consisting of
        /// the first letter of a channel name (rgbhsva), an arithmetic operator (+->*) or greater/less than symbols, and
        /// a value. The less-than and greater-than operators in this case clamp the value. The modifiers are applied in
        /// order, so modifying an rgb channel, then hsv, then rgb again can be useful.
        /// </summary>
        public static Color? GetModifiedColorFromText(string text, Color? startColor = null)
        {
            if (startColor != null)
            {
                text = ColorUtils.GetTextFromColor(startColor.Value) + text;
            }
            else if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            
            var chunks = text.Trim().ToLower().Split(" ");
            if (chunks.Length < 1) { return null; }

            Color? col = startColor ?? ColorUtils.GetColorFromText(chunks[0], true);
            if (col == null) { return null; }
            if (chunks.Length == 1) { return col; }

            // Copies all rgb and hsv values into a dictionary to be performed on.
            Dictionary<char, int> channels = new()
            {
                {'r', col.Value.R },
                {'g', col.Value.G },
                {'b', col.Value.B },
                {'a', col.Value.A },
                // Operating on HSV generates out of RGB, edits, and writes over RGB
                // It's just convenient to define a default than to check if the key exists
                {'h', 0 }, {'s', 0 }, {'v', 0 }
            };

            // To avoid many hardcoded if statements, match channel by character into a dictionary, then match the
            // operation to perform with the expected value that follows.
            for (int i = 1; i < chunks.Length; i++)
            {
                var chunk = chunks[i].Trim();
                if (chunk.Length >= 2)
                {
                    char channelName = chunk[0];
                    char operation = chunk[1];

                    if (channels.ContainsKey(channelName) && int.TryParse(chunk[2..], out int val))
                    {
                        if (channelName == 'h' || channelName == 's' || channelName == 'v')
                        {
                            var hsv = HsvColor.FromColor(Color.FromArgb(255,
                                Math.Clamp(channels['r'], 0, 255),
                                Math.Clamp(channels['g'], 0, 255),
                                Math.Clamp(channels['b'], 0, 255)));
                            channels['h'] = hsv.Hue;
                            channels['s'] = hsv.Saturation;
                            channels['v'] = hsv.Value;
                        }

                        if (operation == '=')
                            { channels[channelName] = val; }
                        else if (operation == '+')
                            { channels[channelName] += val; }
                        else if (operation == '-')
                            { channels[channelName] -= val; }
                        else if (operation == '*')
                            { channels[channelName] *= val; }
                        else if (operation == '/')
                            { channels[channelName] /= val; }
                        else if (operation == '>')
                            { channels[channelName] = Math.Min(channels[channelName], val); }
                        else if (operation == '<')
                            { channels[channelName] = Math.Max(channels[channelName], val); }

                        if (channelName == 'h' || channelName == 's' || channelName == 'v')
                        {
                            var rgb = new HsvColor(
                                Math.Clamp(channels['h'], 0, 360),
                                Math.Clamp(channels['s'], 0, 100),
                                Math.Clamp(channels['v'], 0, 100)).ToColor();
                            channels['r'] = rgb.R;
                            channels['g'] = rgb.G;
                            channels['b'] = rgb.B;
                        }
                    }
                }
            }

            return Color.FromArgb(
                Math.Clamp(channels['a'], 0, 255),
                Math.Clamp(channels['r'], 0, 255),
                Math.Clamp(channels['g'], 0, 255),
                Math.Clamp(channels['b'], 0, 255));
        }
    }
}
