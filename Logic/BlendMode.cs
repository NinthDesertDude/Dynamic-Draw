using PaintDotNet;
using System;

namespace DynamicDraw
{
    /// <summary>
    /// Describes the drawing operation to perform per-pixel to combine one image with another. All blend modes match
    /// Paint.Net blend modes. See PDN documentation for how they work.
    /// </summary>
    public enum BlendMode
    {
        Normal = 0,
        Multiply = 1,
        Additive = 2,
        ColorBurn = 3,
        ColorDodge = 4,
        Reflect = 5,
        Glow = 6,
        Overlay = 7,
        Difference = 8,
        Negation = 9,
        Lighten = 10,
        Darken = 11,
        Screen = 12,
        Xor = 13,
        Overwrite = 14
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
