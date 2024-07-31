using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DynamicDraw
{
    /// <summary>
    /// Scripts store a series of actions that start with a trigger and perform the given actions.
    /// </summary>
    public class Script
    {
        #region Special variables in Script API
        /// <summary>
        /// The current script version (old scripts may have other versions and need special support in the future).
        /// </summary>
        private static readonly string ScriptVersion = "1";

        private static readonly string BuiltInBrushDoAutoDensity = "brush.density.automatic";
        private static readonly string BuiltInBrushBlendMode = "brush.blending";
        private static readonly string BuiltInBrushOpacity = "brush.opacity";
        private static readonly string BuiltInBrushStrokeDensity = "brush.density";
        private static readonly string BuiltInBrushEffect = "brush.effect";
        private static readonly string BuiltInBrushColor = "brush.color";
        private static readonly string BuiltInBrushColorInfluence = "brush.color.solid.mix";
        private static readonly string BuiltInBrushColorInfluenceH = "brush.color.solid.mix.hue";
        private static readonly string BuiltInBrushColorInfluenceS = "brush.color.solid.mix.sat";
        private static readonly string BuiltInBrushColorInfluenceV = "brush.color.solid.mix.val";
        private static readonly string BuiltInBrushDoColorizeBrush = "brush.color.solid";
        private static readonly string BuiltInBrushDoDitherDraw = "brush.checkered";
        private static readonly string BuiltInBrushDoLockAlpha = "brush.channellock.alpha";
        private static readonly string BuiltInBrushDoLockB = "brush.channellock.blue";
        private static readonly string BuiltInBrushDoLockG = "brush.channellock.green";
        private static readonly string BuiltInBrushDoLockH = "brush.channellock.hue";
        private static readonly string BuiltInBrushDoLockR = "brush.channellock.red";
        private static readonly string BuiltInBrushDoLockS = "brush.channellock.sat";
        private static readonly string BuiltInBrushDoLockV = "brush.channellock.val";
        private static readonly string BuiltInBrushFlow = "brush.flow";
        private static readonly string BuiltInBrushFlowShift = "brush.flowshift";
        private static readonly string BuiltInBrushJitterBlueMax = "brush.jitter.blue.max";
        private static readonly string BuiltInBrushJitterBlueMin = "brush.jitter.blue.min";
        private static readonly string BuiltInBrushJitterFlowLoss = "brush.jitter.flow.loss";
        private static readonly string BuiltInBrushJitterGreenMax = "brush.jitter.green.max";
        private static readonly string BuiltInBrushJitterGreenMin = "brush.jitter.green.min";
        private static readonly string BuiltInBrushJitterHorSpray = "brush.jitter.spray.horizontal";
        private static readonly string BuiltInBrushJitterHueMax = "brush.jitter.hue.max";
        private static readonly string BuiltInBrushJitterHueMin = "brush.jitter.hue.min";
        private static readonly string BuiltInBrushJitterSizeMax = "brush.jitter.size.max";
        private static readonly string BuiltInBrushJitterSizeMin = "brush.jitter.size.min";
        private static readonly string BuiltInBrushJitterRedMax = "brush.jitter.red.max";
        private static readonly string BuiltInBrushJitterRedMin = "brush.jitter.red.min";
        private static readonly string BuiltInBrushJitterLeftAngle = "brush.jitter.angle.left";
        private static readonly string BuiltInBrushJitterRightAngle = "brush.jitter.angle.right";
        private static readonly string BuiltInBrushJitterSatMax = "brush.jitter.sat.max";
        private static readonly string BuiltInBrushJitterSatMin = "brush.jitter.sat.min";
        private static readonly string BuiltInBrushJitterValMax = "brush.jitter.val.max";
        private static readonly string BuiltInBrushJitterValMin = "brush.jitter.val.min";
        private static readonly string BuiltInBrushJitterVerSpray = "brush.jitter.spray.vertical";
        private static readonly string BuiltInBrushMinDrawDist = "brush.distance";
        private static readonly string BuiltInPalettePick = "palette.pick";
        private static readonly string BuiltInBrushDoRotateWithMouse = "brush.rotatewithmouse";
        private static readonly string BuiltInBrushAngle = "brush.angle";
        private static readonly string BuiltInBrushAngleShift = "brush.angleshift";
        private static readonly string BuiltInBrushDoSeamlessDrawing = "brush.seamlessdraw";
        private static readonly string BuiltInBrushSettingsPath = "brush.filepath.brush";
        private static readonly string BuiltInBrushImagePath = "brush.filepath.image";
        private static readonly string BuiltInSelectedTool = "tool.active";
        private static readonly string BuiltInBrushSize = "brush.size";
        private static readonly string BuiltInBrushSizeShift = "brush.sizeshift";
        private static readonly string BuiltInBrushSmoothing = "brush.smoothing";
        private static readonly string BuiltInPaletteSwapColors = "palette.swapcolors";
        private static readonly string BuiltInPaletteSwitch = "palette.switch";
        private static readonly string BuiltInBrushSymmetry = "brush.symmetry";
        private static readonly string BuiltInBrushStrokeDensityPressure = $"{input_pressure}{BuiltInBrushStrokeDensity}";
        private static readonly string BuiltInBrushFlowShiftPressure = $"{input_pressure}{BuiltInBrushFlowShift}";
        private static readonly string BuiltInBrushJitterBlueMaxPressure = $"{input_pressure}{BuiltInBrushJitterBlueMax}";
        private static readonly string BuiltInBrushJitterBlueMinPressure = $"{input_pressure}{BuiltInBrushJitterBlueMin}";
        private static readonly string BuiltInBrushJitterFlowLossPressure = $"{input_pressure}{BuiltInBrushJitterFlowLoss}";
        private static readonly string BuiltInBrushJitterGreenMaxPressure = $"{input_pressure}{BuiltInBrushJitterGreenMax}";
        private static readonly string BuiltInBrushJitterGreenMinPressure = $"{input_pressure}{BuiltInBrushJitterGreenMin}";
        private static readonly string BuiltInBrushJitterHorSprayPressure = $"{input_pressure}{BuiltInBrushJitterHorSpray}";
        private static readonly string BuiltInBrushJitterHueMaxPressure = $"{input_pressure}{BuiltInBrushJitterHueMax}";
        private static readonly string BuiltInBrushJitterHueMinPressure = $"{input_pressure}{BuiltInBrushJitterHueMin}";
        private static readonly string BuiltInBrushJitterSizeMaxPressure = $"{input_pressure}{BuiltInBrushJitterSizeMax}";
        private static readonly string BuiltInBrushJitterSizeMinPressure = $"{input_pressure}{BuiltInBrushJitterSizeMin}";
        private static readonly string BuiltInBrushJitterRedMaxPressure = $"{input_pressure}{BuiltInBrushJitterRedMax}";
        private static readonly string BuiltInBrushJitterRedMinPressure = $"{input_pressure}{BuiltInBrushJitterRedMin}";
        private static readonly string BuiltInBrushJitterLeftAnglePressure = $"{input_pressure}{BuiltInBrushJitterLeftAngle}";
        private static readonly string BuiltInBrushJitterRightAnglePressure = $"{input_pressure}{BuiltInBrushJitterRightAngle}";
        private static readonly string BuiltInBrushJitterSatMaxPressure = $"{input_pressure}{BuiltInBrushJitterSatMax}";
        private static readonly string BuiltInBrushJitterSatMinPressure = $"{input_pressure}{BuiltInBrushJitterSatMin}";
        private static readonly string BuiltInBrushJitterValMaxPressure = $"{input_pressure}{BuiltInBrushJitterValMax}";
        private static readonly string BuiltInBrushJitterValMinPressure = $"{input_pressure}{BuiltInBrushJitterValMin}";
        private static readonly string BuiltInBrushJitterVerSprayPressure = $"{input_pressure}{BuiltInBrushJitterVerSpray}";
        private static readonly string BuiltInBrushMinDrawDistPressure = $"{input_pressure}{BuiltInBrushMinDrawDist}";
        private static readonly string BuiltInBrushAnglePressure = $"{input_pressure}{BuiltInBrushAngle}";
        private static readonly string BuiltInBrushSizePressure = $"{input_pressure}{BuiltInBrushSize}";
        private static readonly string BuiltInCanvasAngle = "canvas.angle";
        private static readonly string BuiltInCanvasX = "canvas.x";
        private static readonly string BuiltInCanvasY = "canvas.y";
        private static readonly string BuiltInCanvasZoom = "canvas.zoom";
        private static readonly string input_pressure = "input_pressure";
        #endregion

        #region Properties
        /// <summary>
        /// If the script is triggered, it performs this action in a LUA interpreter.
        /// </summary>
        [JsonPropertyName("Action")]
        public string Action { get; set; }

        /// <summary>
        /// An optional author field for who made the script. Appears in script details.
        /// </summary>
        [JsonPropertyName("Author")]
        public string Author { get; set; }

        /// <summary>
        /// An optional description for what the script does. Appears in script details.
        /// </summary>
        [JsonPropertyName("Description")]
        public string Description { get; set; }

        /// <summary>
        /// The name of the script is provided by its creator and used in GUIs for easy display.
        /// </summary>
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        /// <summary>
        /// The event that causes this script to begin.
        /// </summary>
        [JsonPropertyName("Trigger")]
        public ScriptTrigger Trigger { get; set; }


        /// <summary>
        /// This identifies the version of the script. Newer versions of the plugin may update this version and change
        /// the way it works, in which case it's useful to track versions to support old scripts effectively. The
        /// version will only be updated when a breaking change is introduced.
        /// </summary>
        [JsonPropertyName("Version")]
        public string Version { get; set; }
        #endregion

        [JsonConstructor]
        public Script()
        {
            Action = "";
            Author = "";
            Description = "";
            Name = "";
            Version = ScriptVersion;
            Trigger = ScriptTrigger.OnBrushStroke;
        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        public Script(Script other)
        {
            Action = other.Action;
            Author = other.Author;
            Description = other.Description;
            Name = other.Name;
            Version = other.Version;
            Trigger = other.Trigger;
        }

        #region Public Static Methods
        /// <summary>
        /// Returns a command with the given target and no action data. If <paramref name="commandName"/> doesn't
        /// resolve to a real command target, it defaults to <see cref="CommandTarget.None"/>.
        /// </summary>
        public static Command ResolveBuiltInToCommand(string commandName)
        {
            return new Command()
            {
                Target = ResolveBuiltInToCommandTarget(commandName)
            };
        }

        /// <summary>
        /// Returns a command with the given target and action data. If <paramref name="commandName"/> doesn't resolve
        /// to a real command target, it defaults to <see cref="CommandTarget.None"/>. Action data isn't validated.
        /// </summary>
        public static Command ResolveBuiltInToCommand(string commandName, string actionData)
        {
            return new Command()
            {
                Target = ResolveBuiltInToCommandTarget(commandName),
                ActionData = actionData
            };
        }

        /// <summary>
        /// Takes the given variable name, which comes from user scripts, and resolves it to a command target. These
        /// names are part of a versioned public API and should not be changed without incrementing the version and
        /// adding special casing for changed API.
        /// </summary>
        /// <param name="targetName">The name of the command to be resolved.</param>
        /// <param name="includeReadOnly">
        /// When true, this is able to return commands that are marked as readonly. Some command targets shouldn't be
        /// editable by automatic scripts, such as canvas zoom. Keeping these off limits helps prevent scripts from
        /// creating a jarring user experience.
        /// </param>
        public static CommandTarget ResolveBuiltInToCommandTarget(string targetName, bool includeReadOnly = false)
        {
            string lowercased = targetName.ToLower();

            if (lowercased == BuiltInBrushDoAutoDensity) { return CommandTarget.AutomaticBrushDensity; }
            if (lowercased == BuiltInBrushBlendMode) { return CommandTarget.BlendMode; }
            if (lowercased == BuiltInBrushOpacity) { return CommandTarget.BrushOpacity; }
            if (lowercased == BuiltInBrushStrokeDensity) { return CommandTarget.BrushStrokeDensity; }
            if (includeReadOnly)
            {
                if (lowercased == BuiltInCanvasAngle) { return CommandTarget.CanvasRotation; }
                if (lowercased == BuiltInCanvasX) { return CommandTarget.CanvasX; }
                if (lowercased == BuiltInCanvasY) { return CommandTarget.CanvasY; }
                if (lowercased == BuiltInCanvasZoom) { return CommandTarget.CanvasZoom; }
            }
            // CanvasZoomFit and CanvasZoomToMouse are omitted
            if (lowercased == BuiltInBrushEffect) { return CommandTarget.ChosenEffect; }
            if (lowercased == BuiltInBrushColor) { return CommandTarget.Color; }
            if (lowercased == BuiltInBrushColorInfluence) { return CommandTarget.ColorInfluence; }
            if (lowercased == BuiltInBrushColorInfluenceH) { return CommandTarget.ColorInfluenceHue; }
            if (lowercased == BuiltInBrushColorInfluenceS) { return CommandTarget.ColorInfluenceSat; }
            if (lowercased == BuiltInBrushColorInfluenceV) { return CommandTarget.ColorInfluenceVal; }
            if (lowercased == BuiltInBrushDoColorizeBrush) { return CommandTarget.ColorizeBrush; }
            // ConfirmLine is omitted
            if (lowercased == BuiltInBrushDoDitherDraw) { return CommandTarget.DitherDraw; }
            if (lowercased == BuiltInBrushDoLockAlpha) { return CommandTarget.DoLockAlpha; }
            if (lowercased == BuiltInBrushDoLockB) { return CommandTarget.DoLockB; }
            if (lowercased == BuiltInBrushDoLockG) { return CommandTarget.DoLockG; }
            if (lowercased == BuiltInBrushDoLockH) { return CommandTarget.DoLockHue; }
            if (lowercased == BuiltInBrushDoLockR) { return CommandTarget.DoLockR; }
            if (lowercased == BuiltInBrushDoLockS) { return CommandTarget.DoLockSat; }
            if (lowercased == BuiltInBrushDoLockV) { return CommandTarget.DoLockVal; }
            if (lowercased == BuiltInBrushFlow) { return CommandTarget.Flow; }
            if (lowercased == BuiltInBrushFlowShift) { return CommandTarget.FlowShift; }
            if (lowercased == BuiltInBrushJitterBlueMax) { return CommandTarget.JitterBlueMax; }
            if (lowercased == BuiltInBrushJitterBlueMin) { return CommandTarget.JitterBlueMin; }
            if (lowercased == BuiltInBrushJitterFlowLoss) { return CommandTarget.JitterFlowLoss; }
            if (lowercased == BuiltInBrushJitterGreenMax) { return CommandTarget.JitterGreenMax; }
            if (lowercased == BuiltInBrushJitterGreenMin) { return CommandTarget.JitterGreenMin; }
            if (lowercased == BuiltInBrushJitterHorSpray) { return CommandTarget.JitterHorSpray; }
            if (lowercased == BuiltInBrushJitterHueMax) { return CommandTarget.JitterHueMax; }
            if (lowercased == BuiltInBrushJitterHueMin) { return CommandTarget.JitterHueMin; }
            if (lowercased == BuiltInBrushJitterSizeMax) { return CommandTarget.JitterMaxSize; }
            if (lowercased == BuiltInBrushJitterSizeMin) { return CommandTarget.JitterMinSize; }
            if (lowercased == BuiltInBrushJitterRedMax) { return CommandTarget.JitterRedMax; }
            if (lowercased == BuiltInBrushJitterRedMin) { return CommandTarget.JitterRedMin; }
            if (lowercased == BuiltInBrushJitterLeftAngle) { return CommandTarget.JitterRotLeft; }
            if (lowercased == BuiltInBrushJitterRightAngle) { return CommandTarget.JitterRotRight; }
            if (lowercased == BuiltInBrushJitterSatMax) { return CommandTarget.JitterSatMax; }
            if (lowercased == BuiltInBrushJitterSatMin) { return CommandTarget.JitterSatMin; }
            if (lowercased == BuiltInBrushJitterValMax) { return CommandTarget.JitterValMax; }
            if (lowercased == BuiltInBrushJitterValMin) { return CommandTarget.JitterValMin; }
            if (lowercased == BuiltInBrushJitterVerSpray) { return CommandTarget.JitterVerSpray; }
            if (lowercased == BuiltInBrushMinDrawDist) { return CommandTarget.MinDrawDistance; }
            // None, OpenColorPickerDialog, and OpenQuickCommandDialog are omitted
            if (lowercased == BuiltInPalettePick) { return CommandTarget.PickFromPalette; }
            // RedoAction and ResetCanvasTransforms are omitted
            if (lowercased == BuiltInBrushDoRotateWithMouse) { return CommandTarget.RotateWithMouse; }
            if (lowercased == BuiltInBrushAngle) { return CommandTarget.Rotation; }
            if (lowercased == BuiltInBrushAngleShift) { return CommandTarget.RotShift; }
            if (lowercased == BuiltInBrushDoSeamlessDrawing) { return CommandTarget.SeamlessDrawing; }
            if (lowercased == BuiltInBrushSettingsPath) { return CommandTarget.SelectedBrush; }
            if (lowercased == BuiltInBrushImagePath) { return CommandTarget.SelectedBrushImage; }
            if (lowercased == BuiltInSelectedTool) { return CommandTarget.SelectedTool; }
            if (lowercased == BuiltInBrushSize) { return CommandTarget.Size; }
            if (lowercased == BuiltInBrushSizeShift) { return CommandTarget.SizeShift; }
            if (lowercased == BuiltInBrushSmoothing) { return CommandTarget.SmoothingMode; }
            if (lowercased == BuiltInPaletteSwapColors) { return CommandTarget.SwapPrimarySecondaryColors; }
            if (lowercased == BuiltInPaletteSwitch) { return CommandTarget.SwitchPalette; }
            if (lowercased == BuiltInBrushSymmetry) { return CommandTarget.SymmetryMode; }
            if (lowercased == BuiltInBrushStrokeDensityPressure) { return CommandTarget.TabPressureBrushDensity; }
            if (lowercased == BuiltInBrushFlowShiftPressure) { return CommandTarget.TabPressureFlow; }
            if (lowercased == BuiltInBrushJitterBlueMaxPressure) { return CommandTarget.TabPressureJitterBlueMax; }
            if (lowercased == BuiltInBrushJitterBlueMinPressure) { return CommandTarget.TabPressureJitterBlueMin; }
            if (lowercased == BuiltInBrushJitterFlowLossPressure) { return CommandTarget.TabPressureJitterFlowLoss; }
            if (lowercased == BuiltInBrushJitterGreenMaxPressure) { return CommandTarget.TabPressureJitterGreenMax; }
            if (lowercased == BuiltInBrushJitterGreenMinPressure) { return CommandTarget.TabPressureJitterGreenMin; }
            if (lowercased == BuiltInBrushJitterHorSprayPressure) { return CommandTarget.TabPressureJitterHorShift; }
            if (lowercased == BuiltInBrushJitterHueMaxPressure) { return CommandTarget.TabPressureJitterHueMax; }
            if (lowercased == BuiltInBrushJitterHueMinPressure) { return CommandTarget.TabPressureJitterHueMin; }
            if (lowercased == BuiltInBrushJitterSizeMaxPressure) { return CommandTarget.TabPressureJitterMaxSize; }
            if (lowercased == BuiltInBrushJitterSizeMinPressure) { return CommandTarget.TabPressureJitterMinSize; }
            if (lowercased == BuiltInBrushJitterRedMaxPressure) { return CommandTarget.TabPressureJitterRedMax; }
            if (lowercased == BuiltInBrushJitterRedMinPressure) { return CommandTarget.TabPressureJitterRedMin; }
            if (lowercased == BuiltInBrushJitterLeftAnglePressure) { return CommandTarget.TabPressureJitterRotLeft; }
            if (lowercased == BuiltInBrushJitterRightAnglePressure) { return CommandTarget.TabPressureJitterRotRight; }
            if (lowercased == BuiltInBrushJitterSatMaxPressure) { return CommandTarget.TabPressureJitterSatMax; }
            if (lowercased == BuiltInBrushJitterSatMinPressure) { return CommandTarget.TabPressureJitterSatMin; }
            if (lowercased == BuiltInBrushJitterValMaxPressure) { return CommandTarget.TabPressureJitterValMax; }
            if (lowercased == BuiltInBrushJitterValMinPressure) { return CommandTarget.TabPressureJitterValMin; }
            if (lowercased == BuiltInBrushJitterVerSprayPressure) { return CommandTarget.TabPressureJitterVerShift; }
            if (lowercased == BuiltInBrushMinDrawDistPressure) { return CommandTarget.TabPressureMinDrawDistance; }
            if (lowercased == BuiltInBrushAnglePressure) { return CommandTarget.TabPressureRotation; }
            if (lowercased == BuiltInBrushSizePressure) { return CommandTarget.TabPressureSize; }
            // UndoAction is omitted

            return CommandTarget.None;
        }
        #endregion
    }
}
