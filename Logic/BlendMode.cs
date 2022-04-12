using PaintDotNet;
using System;

namespace DynamicDraw
{
    /// <summary>
    /// Describes the drawing operation to perform per-pixel to combine one image with another.
    /// </summary>
    public enum BlendMode
    {
        /// <summary>
        /// Interpolates towards the brush stroke colors based on opacity. Opacity only increases.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Interpolates towards the brush stroke colors and chosen opacity based on the brush image's opacity.
        /// Opacity only increases.
        /// </summary>
        Overwrite = 1,

        /// <summary>
        /// Matches the corresponding Paint.Net blend mode.
        /// </summary>
        Additive = 2,

        /// <summary>
        /// Matches the corresponding Paint.Net blend mode.
        /// </summary>
        ColorBurn = 3,

        /// <summary>
        /// Matches the corresponding Paint.Net blend mode.
        /// </summary>
        ColorDodge = 4,

        /// <summary>
        /// Matches the corresponding Paint.Net blend mode.
        /// </summary>
        Darken = 5,

        /// <summary>
        /// Matches the corresponding Paint.Net blend mode.
        /// </summary>
        Difference = 6,

        /// <summary>
        /// Matches the corresponding Paint.Net blend mode.
        /// </summary>
        Glow = 7,

        /// <summary>
        /// Matches the corresponding Paint.Net blend mode.
        /// </summary>
        Lighten = 8,

        /// <summary>
        /// Matches the corresponding Paint.Net blend mode.
        /// </summary>
        Multiply = 9,

        /// <summary>
        /// Matches the corresponding Paint.Net blend mode.
        /// </summary>
        Negation = 10,

        /// <summary>
        /// Matches the corresponding Paint.Net blend mode.
        /// </summary>
        Overlay = 11,

        /// <summary>
        /// Matches the corresponding Paint.Net blend mode.
        /// </summary>
        Reflect = 12,

        /// <summary>
        /// Matches the corresponding Paint.Net blend mode.
        /// </summary>
        Screen = 13,

        /// <summary>
        /// Matches the corresponding Paint.Net blend mode.
        /// </summary>
        Xor = 14
    }

    /// <summary>
    /// Conversion to and from <see cref="BlendMode"/> & Paint.Net's <see cref="UserBlendOp"/>.
    /// </summary>
    public static class BlendModeUtils
    {
        /// <summary>
        /// Returns the corresponding paint.net user blend op type for the given BlendMode, or
        /// null if the provided BlendMode has no corresponding PDN type.
        /// </summary>
        public static Type BlendModeToUserBlendOp(BlendMode blendMode)
        {
            return blendMode switch
            {
                BlendMode.Additive => typeof(UserBlendOps.AdditiveBlendOp),
                BlendMode.ColorBurn => typeof(UserBlendOps.ColorBurnBlendOp),
                BlendMode.ColorDodge => typeof(UserBlendOps.ColorDodgeBlendOp),
                BlendMode.Darken => typeof(UserBlendOps.DarkenBlendOp),
                BlendMode.Difference => typeof(UserBlendOps.DifferenceBlendOp),
                BlendMode.Glow => typeof(UserBlendOps.GlowBlendOp),
                BlendMode.Lighten => typeof(UserBlendOps.LightenBlendOp),
                BlendMode.Multiply => typeof(UserBlendOps.MultiplyBlendOp),
                BlendMode.Negation => typeof(UserBlendOps.NegationBlendOp),
                BlendMode.Overlay => typeof(UserBlendOps.OverlayBlendOp),
                BlendMode.Reflect => typeof(UserBlendOps.ReflectBlendOp),
                BlendMode.Screen => typeof(UserBlendOps.ScreenBlendOp),
                BlendMode.Xor => typeof(UserBlendOps.XorBlendOp),
                _ => null,
            };
        }

        /// <summary>
        /// Returns the corresponding BlendMode for the given paint.net user blend type, or
        /// -1 casted to BlendMode if the provided Type doesn't correspond to a BlendMode.
        /// </summary>
        public static BlendMode UserBlendOpToBlendMode(Type blendOp)
        {
            if (blendOp == typeof(UserBlendOps.AdditiveBlendOp)) { return BlendMode.Additive; }
            if (blendOp == typeof(UserBlendOps.ColorBurnBlendOp)) { return BlendMode.ColorBurn; }
            if (blendOp == typeof(UserBlendOps.ColorDodgeBlendOp)) { return BlendMode.ColorDodge; }
            if (blendOp == typeof(UserBlendOps.DarkenBlendOp)) { return BlendMode.Darken; }
            if (blendOp == typeof(UserBlendOps.DifferenceBlendOp)) { return BlendMode.Difference; }
            if (blendOp == typeof(UserBlendOps.GlowBlendOp)) { return BlendMode.Glow; }
            if (blendOp == typeof(UserBlendOps.LightenBlendOp)) { return BlendMode.Lighten; }
            if (blendOp == typeof(UserBlendOps.MultiplyBlendOp)) { return BlendMode.Multiply; }
            if (blendOp == typeof(UserBlendOps.NegationBlendOp)) { return BlendMode.Negation; }
            if (blendOp == typeof(UserBlendOps.OverlayBlendOp)) { return BlendMode.Overlay; }
            if (blendOp == typeof(UserBlendOps.ReflectBlendOp)) { return BlendMode.Reflect; }
            if (blendOp == typeof(UserBlendOps.ScreenBlendOp)) { return BlendMode.Screen; }
            if (blendOp == typeof(UserBlendOps.XorBlendOp)) { return BlendMode.Xor; }
            return (BlendMode)(-1);
        }
    }
}
