using System.Collections.Generic;

namespace DynamicDraw
{
    /// <summary>
    /// Identifies an effect with information about its compatibility with this plugin.
    /// </summary>
    public struct CustomEffectCompatibility
    {
        /// <summary>
        /// The name + assembly uniquely identifies the effect.
        /// </summary>
        public string effectAssembly;

        /// <summary>
        /// The name + assembly uniquely identifies the effect.
        /// </summary>
        public string effectName;

        /// <summary>
        /// The compatibility of the effect. Known incompatible are marked as such.
        /// </summary>
        public CustomEffectCompatibilityStatus status;

        /// <summary>
        /// For known incompatible plugins, this field is used to provide more information about what's wrong.
        /// </summary>
        public string statusReason;

        /// <summary>
        /// Creates a record without a status reason, for e.g. fully compatible effects.
        /// </summary>
        public CustomEffectCompatibility(
            string name,
            string assembly,
            CustomEffectCompatibilityStatus status)
        {
            effectName = name;
            effectAssembly = assembly;
            this.status = status;
            statusReason = "";
        }

        /// <summary>
        /// Creates a record with a statuc reason to explain why the effect isn't compatible.
        /// </summary>
        public CustomEffectCompatibility(
            string name,
            string assembly,
            CustomEffectCompatibilityStatus status,
            string reason)
        {
            effectName = name;
            effectAssembly = assembly;
            this.status = status;
            statusReason = reason;
        }
    }

    /// <summary>
    /// All known compatibility issues with other, third-party plugins running within this one.
    /// </summary>
    public static class KnownEffectCompatibilities
    {
        // Note: these user-facing status reason strings are English only for the time being.
        public static readonly Dictionary<string, CustomEffectCompatibility> KnownCustomEffects = new Dictionary<string, CustomEffectCompatibility>()
        {
            { "Barcode", new CustomEffectCompatibility("Barcode", "Barcode.dll", CustomEffectCompatibilityStatus.ReliableFailToStart) },
            { "BlendModes Plus", new CustomEffectCompatibility("BlendModes Plus", "BlendModesPlus.dll", CustomEffectCompatibilityStatus.ConditionalCrash) },
            { "Blur Map", new CustomEffectCompatibility("Blur Map", "CurtisBlack.Effects.dll", CustomEffectCompatibilityStatus.BrokenEverywhere) },
            { "Channel Mask", new CustomEffectCompatibility("Channel Mask", "CurtisBlack.Effects.dll", CustomEffectCompatibilityStatus.BrokenEverywhere) },
            { "Displacement Map", new CustomEffectCompatibility("Displacement Map", "CurtisBlack.Effects.dll", CustomEffectCompatibilityStatus.BrokenEverywhere) },
            { "Equations", new CustomEffectCompatibility("Equations", "CurtisBlack.Effects.dll", CustomEffectCompatibilityStatus.BrokenEverywhere) },
            { "From Clipboard", new CustomEffectCompatibility("From Clipboard", "FillFromClipboard.dll", CustomEffectCompatibilityStatus.ConditionalCrash, "Intermittently hangs after a brush stroke, and when changing effect settings if no image is on clipboard.") },
            { "Paste Alpha", new CustomEffectCompatibility("Paste Alpha", "PasteAlpha.dll", CustomEffectCompatibilityStatus.ReliableCrash, "Hangs after a brush stroke, and when changing effect settings if no image is on clipboard.") },
            { "PS Only", new CustomEffectCompatibility("PS Only", "Asmageddon_PS Only.dll", CustomEffectCompatibilityStatus.ReliableFailToStart) },
            { "PS Mega", new CustomEffectCompatibility("PS Mega", "Asmageddon_PS Mega.dll", CustomEffectCompatibilityStatus.ConditionalFailToRender, "Crashes when re-rendering.") },
            { "Random Effect", new CustomEffectCompatibility("Random Effect", "Random Effect.dll", CustomEffectCompatibilityStatus.ConditionalCrash, "Compatible, but it may randomize to an effect that isn't and crash paint.net.") },
            { "Rounded Rectangle", new CustomEffectCompatibility("Rounded Rectangle", "LavEnt.Effects.RoundedRectangle.dll", CustomEffectCompatibilityStatus.ReliableFailToRender, "Doesn't update the preview when adjusting settings.") },
            { "Spiral", new CustomEffectCompatibility("Spiral", "Spiral.dll", CustomEffectCompatibilityStatus.ConditionalCrash, "Intermittently crashes paint.net when the effect preview opens.") },
            { "Stencil", new CustomEffectCompatibility("Stencil", "jchunn.dll", CustomEffectCompatibilityStatus.ReliableFailToRender, "Makes everything solid black when rendering.") },
            { "Strange Bulger", new CustomEffectCompatibility("Strange Bulger", "Asmageddon_Strange Bulger.dll", CustomEffectCompatibilityStatus.BrokenEverywhere) },
            { "Strange Bulger B", new CustomEffectCompatibility("Strange Bulger B", "Asmageddon_Strange Bulger B.dll", CustomEffectCompatibilityStatus.BrokenEverywhere) },
            { "That other app", new CustomEffectCompatibility("That other app", "ThatOtherApp.dll", CustomEffectCompatibilityStatus.BrokenEverywhere) },
            { "TR's Brush Strokes", new CustomEffectCompatibility("TR's Brush Strokes", "TRsBrushStrokes.dll", CustomEffectCompatibilityStatus.ReliableFailToStart) },
            { "TR's Copy with Alpha", new CustomEffectCompatibility("TR's Copy with Alpha", "CopyAlpha.dll", CustomEffectCompatibilityStatus.ReliableCrash, "Hangs after a brush stroke, and when changing effect settings if no image is on clipboard.")},
            { "TRs Displacement Map 3D", new CustomEffectCompatibility("TRs Displacement Map 3D", "DispMap.dll", CustomEffectCompatibilityStatus.ReliableCrash, "Hangs.")},
            { "TR's DistortThis!", new CustomEffectCompatibility("TR's DistortThis!", "DistortThis.dll", CustomEffectCompatibilityStatus.ReliableFailToStart) },
            { "TR's Export Selection", new CustomEffectCompatibility("TR's Export Selection", "TRsExportSelection.dll", CustomEffectCompatibilityStatus.ConditionalCrash) },
            { "TR's Filaments", new CustomEffectCompatibility("TR's Filaments", "TRsFilaments.dll", CustomEffectCompatibilityStatus.ReliableCrash, "Hangs.")},
            { "TR's FreeWarp", new CustomEffectCompatibility("TR's FreeWarp", "TRsFreeWarp.dll", CustomEffectCompatibilityStatus.ReliableFailToStart) },
            { "TR's Intensity Warp", new CustomEffectCompatibility("TR's Intensity Warp", "TRsIntensityWarp.dll", CustomEffectCompatibilityStatus.ConditionalCrash, "Hangs when using the clipboard options when no image is copied.") },
            { "TR's SelfEeZ", new CustomEffectCompatibility("TR's SelfEeZ", "TRsSelfeez.dll", CustomEffectCompatibilityStatus.ReliableFailToStart) },
            { "Vitrious", new CustomEffectCompatibility("Vitrious", "EdHarvey.Effects.dll", CustomEffectCompatibilityStatus.ReliableFailToRender) },
            { "White Balance", new CustomEffectCompatibility("White Balance", "EdHarvey.Effects.dll", CustomEffectCompatibilityStatus.ReliableFailToRender) }
        };
    }
}
