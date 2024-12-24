using DynamicDraw.Localization;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DynamicDraw
{
    /// <summary>
    /// Represents a set of settings that a brush may have.
    /// </summary>
    public class BrushSettings : PaintDotNet.Effects.EffectConfigToken
    {
        #region Migration
        /// <summary>
        /// This was a single-string version of the modern BrushImagePaths setting.
        /// </summary>
        public static readonly string Legacy_BrushImagePath = "BrushImagePath";

        /// <summary>
        /// When JSON is updated, as long as the property name is different, deserialization will place old, loaded
        /// properties which don't correspond to modern values into this list. And at the top of this file, a clear
        /// set of expected names is provided to correspond to this. During setting conversion, these values will be
        /// read and transformed to the modern values without expensive custom converters.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> LegacySerializedInfo
        {
            get;
            set;
        }
        #endregion

        #region Fields
        /// <summary>
        /// When true, the brush density is automatically updated according to the final brush
        /// size, ensuring the brush stroke stays smooth as the size changes.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("AutomaticBrushDensity")]
        public bool AutomaticBrushDensity
        {
            get;
            set;
        }

        /// <summary>
        /// The brush's blend mode.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("BlendMode")]
        public BlendMode BlendMode
        {
            get;
            set;
        }

        /// <summary>
        /// The color of the brush, which replaces the brush color.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("BrushColor")]
        public int BrushColor
        {
            get;
            set;
        }

        /// <summary>
        /// The closeness of applied brush images while drawing.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("BrushDensity")]
        public int BrushDensity
        {
            get;
            set;
        }

        /// <summary>
        /// The transparency of the brush (multiplied, as opposed to opacity).
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("BrushFlow")]
        public int BrushFlow
        {
            get;
            set;
        }

        /// <summary>
        /// The file path of the active brush. Built-in brushes use their name here instead.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("BrushImagePaths")]
        public List<string> BrushImagePaths
        {
            get;
            set;
        }

        /// <summary>
        /// The max opacity allowed for any pixel in a brush stroke. Higher values are set to max.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("BrushOpacity")]
        public int BrushOpacity
        {
            get;
            set;
        }

        /// <summary>
        /// The brush's orientation in degrees.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("BrushRotation")]
        public int BrushRotation
        {
            get;
            set;
        }

        /// <summary>
        /// The brush's radius.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("BrushSize")]
        public int BrushSize
        {
            get;
            set;
        }

        [JsonInclude]
        [JsonPropertyName("CmbxChosenEffect")]
        public int CmbxChosenEffect { get; set; }

        /// <summary>
        /// The percent of the chosen color to blend with the brush color.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ColorInfluence")]
        public int ColorInfluence
        {
            get;
            set;
        }

        /// <summary>
        /// When true, colorize brush is off, and color influence is nonzero, the mixed color affects hue.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ColorInfluenceHue")]
        public bool ColorInfluenceHue
        {
            get;
            set;
        }

        /// <summary>
        /// When true, colorize brush is off, and color influence is nonzero, the mixed color affects saturation.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ColorInfluenceSat")]
        public bool ColorInfluenceSat
        {
            get;
            set;
        }

        /// <summary>
        /// When true, colorize brush is off, and color influence is nonzero, the mixed color affects value.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ColorInfluenceVal")]
        public bool ColorInfluenceVal
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to overwrite brush colors when drawing or not.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("DoColorizeBrush")]
        public bool DoColorizeBrush
        {
            get;
            set;
        }

        /// <summary>
        /// Whether the brush rotates with the mouse direction or not.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("DoRotateWithMouse")]
        public bool DoRotateWithMouse
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to draw in a checkerboard pattern (skipping every other pixel) or not.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("DoDitherDraw")]
        public bool DoDitherDraw
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing alpha or not.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("DoLockAlpha")]
        public bool DoLockAlpha
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing red channel.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("DoLockR")]
        public bool DoLockR
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing green channel.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("DoLockG")]
        public bool DoLockG
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing blue channel.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("DoLockB")]
        public bool DoLockB
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing hue or not.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("DoLockHue")]
        public bool DoLockHue
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing saturation or not.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("DoLockSat")]
        public bool DoLockSat
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing value or not.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("DoLockVal")]
        public bool DoLockVal
        {
            get;
            set;
        }

        /// <summary>
        /// Increments/decrements the flow by an amount after each stroke.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("FlowChange")]
        public int FlowChange
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized flow loss.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandFlowLoss")]
        public int RandFlowLoss
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized maximum brush size.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandMaxSize")]
        public int RandMaxSize
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized minimum brush size.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandMinSize")]
        public int RandMinSize
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized counter-clockwise rotation.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandRotLeft")]
        public int RandRotLeft
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized clockwise rotation.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandRotRight")]
        public int RandRotRight
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized horizontal shifting with respect to canvas size.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandHorzShift")]
        public int RandHorzShift
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized vertical shifting with respect to canvas size.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandVertShift")]
        public int RandVertShift
        {
            get;
            set;
        }

        /// <summary>
        /// Doesn't apply brush strokes until the mouse is a certain distance
        /// from its last location.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("MinDrawDistance")]
        public int MinDrawDistance
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of red to each stroke.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandMaxR")]
        public int RandMaxR
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of green to each stroke.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandMaxG")]
        public int RandMaxG
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of blue to each stroke.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandMaxB")]
        public int RandMaxB
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of red from each stroke.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandMinR")]
        public int RandMinR
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of green from each stroke.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandMinG")]
        public int RandMinG
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of blue from each stroke.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandMinB")]
        public int RandMinB
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of hue to each stroke.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandMaxH")]
        public int RandMaxH
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of saturation to each stroke.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandMaxS")]
        public int RandMaxS
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of value to each stroke.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandMaxV")]
        public int RandMaxV
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of hue from each stroke.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandMinH")]
        public int RandMinH
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of saturation from each stroke.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandMinS")]
        public int RandMinS
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of value from each stroke.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RandMinV")]
        public int RandMinV
        {
            get;
            set;
        }

        /// <summary>
        /// These are optional scripts that fire when an event occurs, and perform actions in order. They follow a
        /// versioned API of allowed script actions, and brushes that use brush scripting do not stamp the brush as
        /// it's used by default.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("BrushScripts")]
        public ToolScripts BrushScripts { get; set; }

        /// <summary>
        /// Whether the areas of the brush that clip at the canvas edge should be wrapped around and drawn on the
        /// opposite sides.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("SeamlessDrawing")]
        public bool SeamlessDrawing
        {
            get;
            set;
        }

        /// <summary>
        /// Determines the smoothing applied to drawing.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("Smoothing")]
        public CmbxSmoothing.Smoothing Smoothing { get; set; }

        /// <summary>
        /// Sets whether to draw horizontal, vertical, or radial reflections
        /// of the current image.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("Symmetry")]
        public SymmetryMode Symmetry { get; set; }

        /// <summary>
        /// All constraints based on tablet pressure. This takes a shortcut target and relates the existing value to
        /// the new value via the handling method. For example, the idea of "add 5 to brush opacity" can be expressed
        /// via <see cref="CommandTarget.BrushOpacity"/>, <see cref="ConstraintValueHandlingMethod.Add"/>, and the
        /// value 5. Whatever value is used, it's linearly interpolated based on the % of tablet pressure being read.
        /// That means at 0% tablet pressure, the constraint has no effect since it's multiplied by 0.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("TabPressureConstraints")]
        public Dictionary<CommandTarget, BrushSettingConstraint> TabPressureConstraints { get; set; }
        #endregion

        /// <summary>
        /// Creates a new brush settings object with defaults.
        /// </summary>
        [JsonConstructor]
        public BrushSettings()
        {
            AutomaticBrushDensity = true;
            BlendMode = BlendMode.Normal;
            BrushSize = 2;
            BrushImagePaths = new List<string> { Strings.DefaultBrushCircle };
            BrushRotation = 0;
            BrushColor = PdnUserSettings.userPrimaryColor.ToArgb();
            BrushDensity = 10;
            BrushFlow = 255;
            BrushOpacity = 255;
            LegacySerializedInfo = new();
            RandFlowLoss = 0;
            RandMaxSize = 0;
            RandMinSize = 0;
            RandRotLeft = 0;
            RandRotRight = 0;
            RandHorzShift = 0;
            RandVertShift = 0;
            DoRotateWithMouse = false;
            DoColorizeBrush = true;
            ColorInfluence = 0;
            ColorInfluenceHue = true;
            ColorInfluenceSat = true;
            ColorInfluenceVal = false;
            DoDitherDraw = false;
            DoLockAlpha = false;
            DoLockR = false;
            DoLockG = false;
            DoLockB = false;
            DoLockHue = false;
            DoLockSat = false;
            DoLockVal = false;
            FlowChange = 0;
            MinDrawDistance = 0;
            RandMaxR = 0;
            RandMaxG = 0;
            RandMaxB = 0;
            RandMinR = 0;
            RandMinG = 0;
            RandMinB = 0;
            RandMaxH = 0;
            RandMaxS = 0;
            RandMaxV = 0;
            RandMinH = 0;
            RandMinS = 0;
            RandMinV = 0;
            BrushScripts = new ToolScripts();
            SeamlessDrawing = false;
            Smoothing = CmbxSmoothing.Smoothing.Normal;
            Symmetry = SymmetryMode.None;
            CmbxChosenEffect = 0;
            TabPressureConstraints = new Dictionary<CommandTarget, BrushSettingConstraint>();
        }

        /// <summary>
        /// Copies all settings from another brush settings object.
        /// </summary>
        public BrushSettings(BrushSettings other)
            : base(other)
        {
            AutomaticBrushDensity = other.AutomaticBrushDensity;
            BlendMode = other.BlendMode;
            BrushSize = other.BrushSize;
            BrushImagePaths = new List<string>(other.BrushImagePaths);
            BrushRotation = other.BrushRotation;
            BrushFlow = other.BrushFlow;
            BrushColor = other.BrushColor;
            BrushDensity = other.BrushDensity;
            BrushOpacity = other.BrushOpacity;
            RandFlowLoss = other.RandFlowLoss;
            RandMaxSize = other.RandMaxSize;
            RandMinSize = other.RandMinSize;
            RandRotLeft = other.RandRotLeft;
            RandRotRight = other.RandRotRight;
            RandHorzShift = other.RandHorzShift;
            RandVertShift = other.RandVertShift;
            DoRotateWithMouse = other.DoRotateWithMouse;
            DoColorizeBrush = other.DoColorizeBrush;
            ColorInfluence = other.ColorInfluence;
            ColorInfluenceHue = other.ColorInfluenceHue;
            ColorInfluenceSat = other.ColorInfluenceSat;
            ColorInfluenceVal = other.ColorInfluenceVal;
            DoDitherDraw = other.DoDitherDraw;
            DoLockAlpha = other.DoLockAlpha;
            DoLockR = other.DoLockR;
            DoLockG = other.DoLockG;
            DoLockB = other.DoLockB;
            DoLockHue = other.DoLockHue;
            DoLockSat = other.DoLockSat;
            DoLockVal = other.DoLockVal;
            FlowChange = other.FlowChange;
            LegacySerializedInfo = new();
            MinDrawDistance = other.MinDrawDistance;
            RandMaxR = other.RandMaxR;
            RandMaxG = other.RandMaxG;
            RandMaxB = other.RandMaxB;
            RandMinR = other.RandMinR;
            RandMinG = other.RandMinG;
            RandMinB = other.RandMinB;
            RandMaxH = other.RandMaxH;
            RandMaxS = other.RandMaxS;
            RandMaxV = other.RandMaxV;
            RandMinH = other.RandMinH;
            RandMinS = other.RandMinS;
            RandMinV = other.RandMinV;
            CmbxChosenEffect = other.CmbxChosenEffect;
            TabPressureConstraints = new Dictionary<CommandTarget, BrushSettingConstraint>(other.TabPressureConstraints);
            BrushScripts = other.BrushScripts;
            SeamlessDrawing = other.SeamlessDrawing;
            Smoothing = other.Smoothing;
            Symmetry = other.Symmetry;
        }

        /// <summary>
        /// Copies all settings to another brush settings object.
        /// Called by Paint.NET to restore settings when the effect runs.
        /// </summary>
        public override object Clone()
        {
            return new BrushSettings(this);
        }
    }
}