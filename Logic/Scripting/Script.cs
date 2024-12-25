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

        #region Shortcuts -> Read-write shortcut targets
        public static readonly string BuiltInBrushDoAutoDensity = "brush density automatic";
        public static readonly string BuiltInBrushBlendMode = "brush blending";
        public static readonly string BuiltInBrushOpacity = "brush opacity";
        public static readonly string BuiltInBrushStrokeDensity = "brush density";
        public static readonly string BuiltInBrushEffect = "brush effect";
        public static readonly string BuiltInBrushColor = "brush color";
        public static readonly string BuiltInBrushColorInfluence = "brush color solid mix";
        public static readonly string BuiltInBrushColorInfluenceH = "brush color solid mix hue";
        public static readonly string BuiltInBrushColorInfluenceS = "brush color solid mix sat";
        public static readonly string BuiltInBrushColorInfluenceV = "brush color solid mix val";
        public static readonly string BuiltInBrushDoColorizeBrush = "brush color solid";
        public static readonly string BuiltInBrushDoDitherDraw = "brush checkered";
        public static readonly string BuiltInBrushDoLockAlpha = "brush channellock alpha";
        public static readonly string BuiltInBrushDoLockB = "brush channellock blue";
        public static readonly string BuiltInBrushDoLockG = "brush channellock green";
        public static readonly string BuiltInBrushDoLockH = "brush channellock hue";
        public static readonly string BuiltInBrushDoLockR = "brush channellock red";
        public static readonly string BuiltInBrushDoLockS = "brush channellock sat";
        public static readonly string BuiltInBrushDoLockV = "brush channellock val";
        public static readonly string BuiltInBrushFlow = "brush flow";
        public static readonly string BuiltInBrushJitterBlueMax = "brush jitter blue max";
        public static readonly string BuiltInBrushJitterBlueMin = "brush jitter blue min";
        public static readonly string BuiltInBrushJitterFlowLoss = "brush jitter flow loss";
        public static readonly string BuiltInBrushJitterGreenMax = "brush jitter green max";
        public static readonly string BuiltInBrushJitterGreenMin = "brush jitter green min";
        public static readonly string BuiltInBrushJitterHorSpray = "brush jitter spray horizontal";
        public static readonly string BuiltInBrushJitterHueMax = "brush jitter hue max";
        public static readonly string BuiltInBrushJitterHueMin = "brush jitter hue min";
        public static readonly string BuiltInBrushJitterSizeMax = "brush jitter size max";
        public static readonly string BuiltInBrushJitterSizeMin = "brush jitter size min";
        public static readonly string BuiltInBrushJitterRedMax = "brush jitter red max";
        public static readonly string BuiltInBrushJitterRedMin = "brush jitter red min";
        public static readonly string BuiltInBrushJitterLeftAngle = "brush jitter angle left";
        public static readonly string BuiltInBrushJitterRightAngle = "brush jitter angle right";
        public static readonly string BuiltInBrushJitterSatMax = "brush jitter sat max";
        public static readonly string BuiltInBrushJitterSatMin = "brush jitter sat min";
        public static readonly string BuiltInBrushJitterValMax = "brush jitter val max";
        public static readonly string BuiltInBrushJitterValMin = "brush jitter val min";
        public static readonly string BuiltInBrushJitterVerSpray = "brush jitter spray vertical";
        public static readonly string BuiltInBrushMinDrawDist = "brush distance";
        public static readonly string BuiltInBrushDoRotateWithMouse = "brush rotatewithmouse";
        public static readonly string BuiltInBrushAngle = "brush angle";
        public static readonly string BuiltInBrushDoSeamlessDrawing = "brush seamlessdraw";
        public static readonly string BuiltInBrushSettingsPath = "brush";
        public static readonly string BuiltInBrushImagePath = "brush image";
        public static readonly string BuiltInBrushSize = "brush size";
        public static readonly string BuiltInBrushSmoothing = "brush smoothing";
        public static readonly string BuiltInBrushSymmetry = "brush symmetry";
        public static readonly string BuiltInBrushStrokeDensityPressure = $"{BuiltInInputPressure} {BuiltInBrushStrokeDensity}";
        public static readonly string BuiltInBrushJitterBlueMaxPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterBlueMax}";
        public static readonly string BuiltInBrushJitterBlueMinPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterBlueMin}";
        public static readonly string BuiltInBrushJitterFlowLossPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterFlowLoss}";
        public static readonly string BuiltInBrushJitterGreenMaxPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterGreenMax}";
        public static readonly string BuiltInBrushJitterGreenMinPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterGreenMin}";
        public static readonly string BuiltInBrushJitterHorSprayPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterHorSpray}";
        public static readonly string BuiltInBrushJitterHueMaxPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterHueMax}";
        public static readonly string BuiltInBrushJitterHueMinPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterHueMin}";
        public static readonly string BuiltInBrushJitterSizeMaxPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterSizeMax}";
        public static readonly string BuiltInBrushJitterSizeMinPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterSizeMin}";
        public static readonly string BuiltInBrushJitterRedMaxPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterRedMax}";
        public static readonly string BuiltInBrushJitterRedMinPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterRedMin}";
        public static readonly string BuiltInBrushJitterLeftAnglePressure = $"{BuiltInInputPressure} {BuiltInBrushJitterLeftAngle}";
        public static readonly string BuiltInBrushJitterRightAnglePressure = $"{BuiltInInputPressure} {BuiltInBrushJitterRightAngle}";
        public static readonly string BuiltInBrushJitterSatMaxPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterSatMax}";
        public static readonly string BuiltInBrushJitterSatMinPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterSatMin}";
        public static readonly string BuiltInBrushJitterValMaxPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterValMax}";
        public static readonly string BuiltInBrushJitterValMinPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterValMin}";
        public static readonly string BuiltInBrushJitterVerSprayPressure = $"{BuiltInInputPressure} {BuiltInBrushJitterVerSpray}";
        public static readonly string BuiltInBrushMinDrawDistPressure = $"{BuiltInInputPressure} {BuiltInBrushMinDrawDist}";
        public static readonly string BuiltInBrushAnglePressure = $"{BuiltInInputPressure} {BuiltInBrushAngle}";
        public static readonly string BuiltInBrushSizePressure = $"{BuiltInInputPressure} {BuiltInBrushSize}";
        public static readonly string BuiltInPalettePick = "palette pick";
        public static readonly string BuiltInPaletteSwapColors = "palette swapcolors";
        public static readonly string BuiltInPaletteSwitch = "palette switch";
        public static readonly string BuiltInSelectedTool = "tool active";
        #endregion

        #region Shortcuts -> Read-only shortcut targets
        public static readonly string BuiltInCanvasAngle = "canvas angle";
        public static readonly string BuiltInCanvasX = "canvas x";
        public static readonly string BuiltInCanvasY = "canvas y";
        public static readonly string BuiltInCanvasZoom = "canvas zoom";
        public static readonly string BuiltInPosX = "position x";
        public static readonly string BuiltInPosY = "position y";
        public static readonly string BuiltInPosXPrev = "last position x";
        public static readonly string BuiltInPosYPrev = "last position y";
        public static readonly string BuiltInPosStampXPrev = "stroke start position x";
        public static readonly string BuiltInPosStampYPrev = "stroke start position y";
        public static readonly string BuiltInInputPressure = $"input pressure";
        public static readonly string BuiltInInputPressurePrev = "last input pressure";
        #endregion

        #region General API
        /// <summary>
        /// Syntax: (string shortcutScriptTarget): string.
        /// Takes any shortcut target and returns its value in whatever type it is (number, bool, etc.).
        /// Implementation: <see cref="WinDynamicDraw.ScriptAPIGet(string)"/>.
        /// </summary>
        public static readonly string APIGet = "get";

        /// <summary>
        /// Syntax: (string writeableShortcutScriptTarget, dynamic value): void.
        /// Sets any writeable shortcut target to the given value, if possible.
        /// Implementation: <see cref="WinDynamicDraw.ScriptAPISet(string, MoonSharp.Interpreter.DynValue)"/>.
        /// </summary>
        public static readonly string APISet = "set";

        /// <summary>
        /// Syntax: (float x, float y): void.
        /// Stamps the brush at the given position using all its settings.
        /// Implementation: <see cref="WinDynamicDraw.ScriptAPIStampBrush(float, float)"/>.
        /// </summary>
        public static readonly string APIStampBrush = "stampAt";

        /// <summary>
        /// Syntax: (float x, float y): void.
        /// Stamps the brush in a line from the mouse to the given position using all its settings.
        /// Implementation: <see cref="WinDynamicDraw.ScriptAPIStampLineTo(float, float)"/>.
        /// </summary>
        public static readonly string APIStampLineTo = "stampTo";

        /// <summary>
        /// Syntax: (float x1, float y1, float x2, float y2): void.
        /// Stamps the brush in a line between two given coordinates using all its settings.
        /// Implementation: <see cref="WinDynamicDraw.ScriptAPIStampLine(float, float, float, float)"/>.
        /// </summary>
        public static readonly string APIStampLine = "stampBetween";

        /// <summary>
        /// Syntax: Color? (float x, float y): void.
        /// Returns the color at the given position including its alpha, or null if the position is outside the canvas.
        /// Implementation: <see cref="WinDynamicDraw.ScriptAPIPickColor(float, float)"/>.
        /// </summary>
        public static readonly string APIPickColor = "getColor";
        #endregion
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
        /// This identifies the version of the plugin when the script was made. Newer versions of the plugin may update
        /// this version and change the way it works, in which case it's useful to track versions to support old
        /// scripts effectively. The version will only be updated when a breaking change is introduced.
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
            Trigger = ScriptTrigger.Disabled;
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
        public static CommandTarget ResolveBuiltInToCommandTarget(
            string targetName, bool includeReadOnly = false)
        {
            string lowercased = targetName.ToLower();

            if (lowercased == BuiltInBrushDoAutoDensity) { return CommandTarget.AutomaticBrushDensity; }
            if (lowercased == BuiltInBrushBlendMode) { return CommandTarget.BlendMode; }
            if (lowercased == BuiltInBrushOpacity) { return CommandTarget.BrushOpacity; }
            if (lowercased == BuiltInBrushStrokeDensity) { return CommandTarget.BrushStrokeDensity; }
            // CanvasZoomFit and CanvasZoomToMouse are omitted. These should not be allowed targets for scripts.
            if (lowercased == BuiltInBrushEffect) { return CommandTarget.ChosenEffect; }
            if (lowercased == BuiltInBrushColor) { return CommandTarget.Color; }
            if (lowercased == BuiltInBrushColorInfluence) { return CommandTarget.ColorInfluence; }
            if (lowercased == BuiltInBrushColorInfluenceH) { return CommandTarget.ColorInfluenceHue; }
            if (lowercased == BuiltInBrushColorInfluenceS) { return CommandTarget.ColorInfluenceSat; }
            if (lowercased == BuiltInBrushColorInfluenceV) { return CommandTarget.ColorInfluenceVal; }
            if (lowercased == BuiltInBrushDoColorizeBrush) { return CommandTarget.ColorizeBrush; }
            // ConfirmLine is omitted. These should not be allowed targets for scripts.
            if (lowercased == BuiltInBrushDoDitherDraw) { return CommandTarget.DitherDraw; }
            if (lowercased == BuiltInBrushDoLockAlpha) { return CommandTarget.DoLockAlpha; }
            if (lowercased == BuiltInBrushDoLockB) { return CommandTarget.DoLockB; }
            if (lowercased == BuiltInBrushDoLockG) { return CommandTarget.DoLockG; }
            if (lowercased == BuiltInBrushDoLockH) { return CommandTarget.DoLockHue; }
            if (lowercased == BuiltInBrushDoLockR) { return CommandTarget.DoLockR; }
            if (lowercased == BuiltInBrushDoLockS) { return CommandTarget.DoLockSat; }
            if (lowercased == BuiltInBrushDoLockV) { return CommandTarget.DoLockVal; }
            if (lowercased == BuiltInBrushFlow) { return CommandTarget.Flow; }
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
            // None, OpenColorPickerDialog, and OpenQuickCommandDialog are omitted. These should not be allowed targets for scripts.
            if (lowercased == BuiltInPalettePick) { return CommandTarget.PickFromPalette; }
            // RedoAction and ResetCanvasTransforms are omitted. These should not be allowed targets for scripts.
            if (lowercased == BuiltInBrushDoRotateWithMouse) { return CommandTarget.RotateWithMouse; }
            if (lowercased == BuiltInBrushAngle) { return CommandTarget.Rotation; }
            if (lowercased == BuiltInBrushDoSeamlessDrawing) { return CommandTarget.SeamlessDrawing; }
            if (lowercased == BuiltInBrushSettingsPath) { return CommandTarget.SelectedBrush; }
            if (lowercased == BuiltInBrushImagePath) { return CommandTarget.SelectedBrushImage; }
            if (lowercased == BuiltInSelectedTool) { return CommandTarget.SelectedTool; }
            if (lowercased == BuiltInBrushSize) { return CommandTarget.Size; }
            if (lowercased == BuiltInBrushSmoothing) { return CommandTarget.SmoothingMode; }
            if (lowercased == BuiltInBrushSymmetry) { return CommandTarget.SymmetryMode; }
            if (lowercased == BuiltInBrushStrokeDensityPressure) { return CommandTarget.TabPressureBrushDensity; }
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
            if (lowercased == BuiltInPaletteSwapColors) { return CommandTarget.SwapPrimarySecondaryColors; }
            if (lowercased == BuiltInPaletteSwitch) { return CommandTarget.SwitchPalette; }
            // UndoAction, OpenScriptEditorDialog are omitted. These should not be allowed targets for scripts.
            if (includeReadOnly)
            {
                if (lowercased == BuiltInCanvasAngle) { return CommandTarget.CanvasRotation; }
                if (lowercased == BuiltInCanvasX) { return CommandTarget.CanvasX; }
                if (lowercased == BuiltInCanvasY) { return CommandTarget.CanvasY; }
                if (lowercased == BuiltInCanvasZoom) { return CommandTarget.CanvasZoom; }
            }

            return CommandTarget.None;
        }

        /// <summary>
        /// A utility function to retrieve a script's line number and column.
        /// </summary>
        public static (string messageNoPos, string line, string colStart, string colEnd) GetScriptErrorPosition(string message)
        {
            //The two formats returned by MoonSharp, where n is a number:
            //:(n, n):
            //:(n, n - n):

            if (message.StartsWith(":("))
            {
                int endIndex = message.IndexOf("):");
                if (endIndex != -1)
                {
                    var allSections = message[2..endIndex]
                        .Replace(" ", "")
                        .Split(new[] { ',', '-' });

                    return (
                        message[(endIndex + 2)..].TrimStart(),
                        allSections.Length > 0 ? allSections[0] : "",
                        allSections.Length > 1 ? allSections[1] : "",
                        allSections.Length > 2 ? allSections[2] : "");
                }
            }

            return (message, "", "", "");
        }
        #endregion
    }
}
