using DynamicDraw.Gui;
using DynamicDraw.Localization;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;

namespace DynamicDraw
{
    /// <summary>
    /// Represents a set of settings that a brush may have.
    /// </summary>
    [DataContract(Name = "BrushSettings", Namespace = "")]
    public class BrushSettings : PaintDotNet.Effects.EffectConfigToken
    {
        #region Fields
        /// <summary>
        /// When true, the brush density is automatically updated according to the final brush
        /// size, ensuring the brush stroke stays smooth as the size changes.
        /// </summary>
        [DataMember(Name = "AutomaticBrushDensity")]
        public bool AutomaticBrushDensity
        {
            get;
            set;
        }

        /// <summary>
        /// The brush's blend mode.
        /// </summary>
        [DataMember(Name = "BlendMode")]
        public BlendMode BlendMode
        {
            get;
            set;
        }

        /// <summary>
        /// The color of the brush, which replaces the brush color.
        /// </summary>
        [DataMember(Name = "BrushColor")]
        public Color BrushColor
        {
            get;
            set;
        }

        /// <summary>
        /// The closeness of applied brush images while drawing.
        /// </summary>
        [DataMember(Name = "BrushDensity")]
        public int BrushDensity
        {
            get;
            set;
        }

        /// <summary>
        /// The transparency of the brush (multiplied, as opposed to opacity).
        /// </summary>
        [DataMember(Name = "BrushFlow")]
        public int BrushFlow
        {
            get;
            set;
        }

        /// <summary>
        /// The file path of the active brush. Built-in brushes use their name here instead.
        /// </summary>
        [DataMember(Name = "BrushImagePath")]
        public string BrushImagePath
        {
            get;
            set;
        }

        /// <summary>
        /// The max opacity allowed for any pixel in a brush stroke. Higher values are set to max.
        /// </summary>
        [DataMember(Name = "BrushOpacity")]
        public int BrushOpacity
        {
            get;
            set;
        }

        /// <summary>
        /// The brush's orientation in degrees.
        /// </summary>
        [DataMember(Name = "BrushRotation")]
        public int BrushRotation
        {
            get;
            set;
        }

        /// <summary>
        /// The brush's radius.
        /// </summary>
        [DataMember(Name = "BrushSize")]
        public int BrushSize
        {
            get;
            set;
        }

        /// <summary>
        /// The percent of the chosen color to blend with the brush color.
        /// </summary>
        [DataMember(Name = "ColorInfluence")]
        public int ColorInfluence
        {
            get;
            set;
        }

        /// <summary>
        /// When true, colorize brush is off, and color influence is nonzero, the mixed color affects hue.
        /// </summary>
        [DataMember(Name = "ColorInfluenceHue")]
        public bool ColorInfluenceHue
        {
            get;
            set;
        }

        /// <summary>
        /// When true, colorize brush is off, and color influence is nonzero, the mixed color affects saturation.
        /// </summary>
        [DataMember(Name = "ColorInfluenceSat")]
        public bool ColorInfluenceSat
        {
            get;
            set;
        }

        /// <summary>
        /// When true, colorize brush is off, and color influence is nonzero, the mixed color affects value.
        /// </summary>
        [DataMember(Name = "ColorInfluenceVal")]
        public bool ColorInfluenceVal
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to overwrite brush colors when drawing or not.
        /// </summary>
        [DataMember(Name = "DoColorizeBrush")]
        public bool DoColorizeBrush
        {
            get;
            set;
        }

        /// <summary>
        /// Whether the brush rotates with the mouse direction or not.
        /// </summary>
        [DataMember(Name = "DoRotateWithMouse")]
        public bool DoRotateWithMouse
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to draw in a checkerboard pattern (skipping every other pixel) or not.
        /// </summary>
        [DataMember(Name = "DoDitherDraw")]
        public bool DoDitherDraw
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing alpha or not.
        /// </summary>
        [DataMember(Name = "DoLockAlpha")]
        public bool DoLockAlpha
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing red channel.
        /// </summary>
        [DataMember(Name = "DoLockR")]
        public bool DoLockR
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing green channel.
        /// </summary>
        [DataMember(Name = "DoLockG")]
        public bool DoLockG
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing blue channel.
        /// </summary>
        [DataMember(Name = "DoLockB")]
        public bool DoLockB
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing hue or not.
        /// </summary>
        [DataMember(Name = "DoLockHue")]
        public bool DoLockHue
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing saturation or not.
        /// </summary>
        [DataMember(Name = "DoLockSat")]
        public bool DoLockSat
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing value or not.
        /// </summary>
        [DataMember(Name = "DoLockVal")]
        public bool DoLockVal
        {
            get;
            set;
        }

        /// <summary>
        /// Increments/decrements the flow by an amount after each stroke.
        /// </summary>
        [DataMember(Name = "FlowChange")]
        public int FlowChange
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized flow loss.
        /// </summary>
        [DataMember(Name = "RandFlowLoss")]
        public int RandFlowLoss
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized maximum brush size.
        /// </summary>
        [DataMember(Name = "RandMaxSize")]
        public int RandMaxSize
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized minimum brush size.
        /// </summary>
        [DataMember(Name = "RandMinSize")]
        public int RandMinSize
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized counter-clockwise rotation.
        /// </summary>
        [DataMember(Name = "RandRotLeft")]
        public int RandRotLeft
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized clockwise rotation.
        /// </summary>
        [DataMember(Name = "RandRotRight")]
        public int RandRotRight
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized horizontal shifting with respect to canvas size.
        /// </summary>
        [DataMember(Name = "RandHorzShift")]
        public int RandHorzShift
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized vertical shifting with respect to canvas size.
        /// </summary>
        [DataMember(Name = "RandVertShift")]
        public int RandVertShift
        {
            get;
            set;
        }

        /// <summary>
        /// Doesn't apply brush strokes until the mouse is a certain distance
        /// from its last location.
        /// </summary>
        [DataMember(Name = "MinDrawDistance")]
        public int MinDrawDistance
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of red to each stroke.
        /// </summary>
        [DataMember(Name = "RandMaxR")]
        public int RandMaxR
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of green to each stroke.
        /// </summary>
        [DataMember(Name = "RandMaxG")]
        public int RandMaxG
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of blue to each stroke.
        /// </summary>
        [DataMember(Name = "RandMaxB")]
        public int RandMaxB
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of red from each stroke.
        /// </summary>
        [DataMember(Name = "RandMinR")]
        public int RandMinR
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of green from each stroke.
        /// </summary>
        [DataMember(Name = "RandMinG")]
        public int RandMinG
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of blue from each stroke.
        /// </summary>
        [DataMember(Name = "RandMinB")]
        public int RandMinB
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of hue to each stroke.
        /// </summary>
        [DataMember(Name = "RandMaxH")]
        public int RandMaxH
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of saturation to each stroke.
        /// </summary>
        [DataMember(Name = "RandMaxS")]
        public int RandMaxS
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of value to each stroke.
        /// </summary>
        [DataMember(Name = "RandMaxV")]
        public int RandMaxV
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of hue from each stroke.
        /// </summary>
        [DataMember(Name = "RandMinH")]
        public int RandMinH
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of saturation from each stroke.
        /// </summary>
        [DataMember(Name = "RandMinS")]
        public int RandMinS
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of value from each stroke.
        /// </summary>
        [DataMember(Name = "RandMinV")]
        public int RandMinV
        {
            get;
            set;
        }

        /// <summary>
        /// Increments/decrements the size by an amount after each stroke.
        /// </summary>
        [DataMember(Name = "SizeChange")]
        public int SizeChange
        {
            get;
            set;
        }

        /// <summary>
        /// Increments/decrements the rotation by an amount after each stroke.
        /// </summary>
        [DataMember(Name = "RotChange")]
        public int RotChange
        {
            get;
            set;
        }

        /// <summary>
        /// Whether the areas of the brush that clip at the canvas edge should be wrapped around and drawn on the
        /// opposite sides.
        /// </summary>
        [DataMember(Name = "SeamlessDrawing")]
        public bool SeamlessDrawing
        {
            get;
            set;
        }

        /// <summary>
        /// Determines the smoothing applied to drawing.
        /// </summary>
        [DataMember(Name = "Smoothing")]
        public CmbxSmoothing.Smoothing Smoothing { get; set; }

        /// <summary>
        /// Sets whether to draw horizontal, vertical, or radial reflections
        /// of the current image.
        /// </summary>
        [DataMember(Name = "Symmetry")]
        public SymmetryMode Symmetry { get; set; }

        [DataMember(Name = "TabPressureBrushDensity")]
        public int TabPressureBrushDensity { get; set; }

        [DataMember(Name = "TabPressureBrushFlow")]
        public int TabPressureBrushFlow { get; set; }

        [DataMember(Name = "TabPressureBrushOpacity")]
        public int TabPressureBrushOpacity { get; set; }

        [DataMember(Name = "TabPressureBrushRotation")]
        public int TabPressureBrushRotation { get; set; }

        [DataMember(Name = "TabPressureBrushSize")]
        public int TabPressureBrushSize { get; set; }

        [DataMember(Name = "TabPressureMaxBlueJitter")]
        public int TabPressureMaxBlueJitter { get; set; }

        [DataMember(Name = "TabPressureMaxGreenJitter")]
        public int TabPressureMaxGreenJitter { get; set; }

        [DataMember(Name = "TabPressureMaxHueJitter")]
        public int TabPressureMaxHueJitter { get; set; }

        [DataMember(Name = "TabPressureMaxRedJitter")]
        public int TabPressureMaxRedJitter { get; set; }

        [DataMember(Name = "TabPressureMaxSatJitter")]
        public int TabPressureMaxSatJitter { get; set; }

        [DataMember(Name = "TabPressureMaxValueJitter")]
        public int TabPressureMaxValueJitter { get; set; }

        [DataMember(Name = "TabPressureMinBlueJitter")]
        public int TabPressureMinBlueJitter { get; set; }

        [DataMember(Name = "TabPressureMinDrawDistance")]
        public int TabPressureMinDrawDistance { get; set; }

        [DataMember(Name = "TabPressureMinGreenJitter")]
        public int TabPressureMinGreenJitter { get; set; }

        [DataMember(Name = "TabPressureMinHueJitter")]
        public int TabPressureMinHueJitter { get; set; }

        [DataMember(Name = "TabPressureMinRedJitter")]
        public int TabPressureMinRedJitter { get; set; }

        [DataMember(Name = "TabPressureMinSatJitter")]
        public int TabPressureMinSatJitter { get; set; }

        [DataMember(Name = "TabPressureMinValueJitter")]
        public int TabPressureMinValueJitter { get; set; }

        [DataMember(Name = "TabPressureRandHorShift")]
        public int TabPressureRandHorShift { get; set; }

        [DataMember(Name = "TabPressureRandMaxSize")]
        public int TabPressureRandMaxSize { get; set; }

        [DataMember(Name = "TabPressureRandFlowLoss")]
        public int TabPressureRandFlowLoss { get; set; }

        [DataMember(Name = "TabPressureRandMinSize")]
        public int TabPressureRandMinSize { get; set; }

        [DataMember(Name = "TabPressureRandRotLeft")]
        public int TabPressureRandRotLeft { get; set; }

        [DataMember(Name = "TabPressureRandRotRight")]
        public int TabPressureRandRotRight { get; set; }

        [DataMember(Name = "TabPressureRandVerShift")]
        public int TabPressureRandVerShift { get; set; }

        [DataMember(Name = "CmbxChosenEffect")]
        public int CmbxChosenEffect { get; set; }

        [DataMember(Name = "CmbxTabPressureBrushDensity")]
        public int CmbxTabPressureBrushDensity { get; set; }

        [DataMember(Name = "CmbxTabPressureBrushFlow")]
        public int CmbxTabPressureBrushFlow { get; set; }

        [DataMember(Name = "CmbxTabPressureBrushOpacity")]
        public int CmbxTabPressureBrushOpacity { get; set; }

        [DataMember(Name = "CmbxTabPressureBrushRotation")]
        public int CmbxTabPressureBrushRotation { get; set; }

        [DataMember(Name = "CmbxTabPressureBrushSize")]
        public int CmbxTabPressureBrushSize { get; set; }

        [DataMember(Name = "CmbxTabPressureBlueJitter")]
        public int CmbxTabPressureBlueJitter { get; set; }

        [DataMember(Name = "CmbxTabPressureGreenJitter")]
        public int CmbxTabPressureGreenJitter { get; set; }

        [DataMember(Name = "CmbxTabPressureHueJitter")]
        public int CmbxTabPressureHueJitter { get; set; }

        [DataMember(Name = "CmbxTabPressureMinDrawDistance")]
        public int CmbxTabPressureMinDrawDistance { get; set; }

        [DataMember(Name = "CmbxTabPressureRedJitter")]
        public int CmbxTabPressureRedJitter { get; set; }

        [DataMember(Name = "CmbxTabPressureSatJitter")]
        public int CmbxTabPressureSatJitter { get; set; }

        [DataMember(Name = "CmbxTabPressureValueJitter")]
        public int CmbxTabPressureValueJitter { get; set; }

        [DataMember(Name = "CmbxTabPressureRandHorShift")]
        public int CmbxTabPressureRandHorShift { get; set; }

        [DataMember(Name = "CmbxTabPressureRandMaxSize")]
        public int CmbxTabPressureRandMaxSize { get; set; }

        [DataMember(Name = "CmbxTabPressureRandFlowLoss")]
        public int CmbxTabPressureRandFlowLoss { get; set; }

        [DataMember(Name = "CmbxTabPressureRandMinSize")]
        public int CmbxTabPressureRandMinSize { get; set; }

        [DataMember(Name = "CmbxTabPressureRandRotLeft")]
        public int CmbxTabPressureRandRotLeft { get; set; }

        [DataMember(Name = "CmbxTabPressureRandRotRight")]
        public int CmbxTabPressureRandRotRight { get; set; }

        [DataMember(Name = "CmbxTabPressureRandVerShift")]
        public int CmbxTabPressureRandVerShift { get; set; }
        #endregion

        /// <summary>
        /// Creates a new brush settings object with defaults.
        /// </summary>
        public BrushSettings()
        {
            AutomaticBrushDensity = true;
            BlendMode = BlendMode.Normal;
            BrushSize = 2;
            BrushImagePath = Strings.DefaultBrushCircle;
            BrushRotation = 0;
            BrushColor = UserSettings.userPrimaryColor;
            BrushDensity = 10;
            BrushFlow = 255;
            BrushOpacity = 255;
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
            SeamlessDrawing = false;
            SizeChange = 0;
            RotChange = 0;
            Smoothing = CmbxSmoothing.Smoothing.Normal;
            Symmetry = SymmetryMode.None;
            CmbxChosenEffect = 0;
            CmbxTabPressureBrushDensity = 0;
            CmbxTabPressureBrushFlow = 0;
            CmbxTabPressureBrushOpacity = 0;
            CmbxTabPressureBrushRotation = 0;
            CmbxTabPressureBrushSize = 0;
            CmbxTabPressureBlueJitter = 0;
            CmbxTabPressureGreenJitter = 0;
            CmbxTabPressureHueJitter = 0;
            CmbxTabPressureMinDrawDistance = 0;
            CmbxTabPressureRedJitter = 0;
            CmbxTabPressureSatJitter = 0;
            CmbxTabPressureValueJitter = 0;
            CmbxTabPressureRandFlowLoss = 0;
            CmbxTabPressureRandHorShift = 0;
            CmbxTabPressureRandMaxSize = 0;
            CmbxTabPressureRandMinSize = 0;
            CmbxTabPressureRandRotLeft = 0;
            CmbxTabPressureRandRotRight = 0;
            CmbxTabPressureRandVerShift = 0;
            TabPressureBrushDensity = 0;
            TabPressureBrushFlow = 0;
            TabPressureBrushOpacity = 0;
            TabPressureBrushRotation = 0;
            TabPressureBrushSize = 0;
            TabPressureMaxBlueJitter = 0;
            TabPressureMaxGreenJitter = 0;
            TabPressureMaxHueJitter = 0;
            TabPressureMaxRedJitter = 0;
            TabPressureMaxSatJitter = 0;
            TabPressureMaxValueJitter = 0;
            TabPressureMinBlueJitter = 0;
            TabPressureMinDrawDistance = 0;
            TabPressureMinGreenJitter = 0;
            TabPressureMinHueJitter = 0;
            TabPressureMinRedJitter = 0;
            TabPressureMinSatJitter = 0;
            TabPressureMinValueJitter = 0;
            TabPressureRandFlowLoss = 0;
            TabPressureRandHorShift = 0;
            TabPressureRandMaxSize = 0;
            TabPressureRandMinSize = 0;
            TabPressureRandRotLeft = 0;
            TabPressureRandRotRight = 0;
            TabPressureRandVerShift = 0;
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
            BrushImagePath = other.BrushImagePath;
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
            CmbxTabPressureBrushDensity = other.CmbxTabPressureBrushDensity;
            CmbxTabPressureBrushFlow = other.CmbxTabPressureBrushFlow;
            CmbxTabPressureBrushOpacity = other.CmbxTabPressureBrushOpacity;
            CmbxTabPressureBrushRotation = other.CmbxTabPressureBrushRotation;
            CmbxTabPressureBrushSize = other.CmbxTabPressureBrushSize;
            CmbxTabPressureBlueJitter = other.CmbxTabPressureBlueJitter;
            CmbxTabPressureGreenJitter = other.CmbxTabPressureGreenJitter;
            CmbxTabPressureHueJitter = other.CmbxTabPressureHueJitter;
            CmbxTabPressureMinDrawDistance = other.CmbxTabPressureMinDrawDistance;
            CmbxTabPressureRedJitter = other.CmbxTabPressureRedJitter;
            CmbxTabPressureSatJitter = other.CmbxTabPressureSatJitter;
            CmbxTabPressureValueJitter = other.CmbxTabPressureValueJitter;
            CmbxTabPressureRandFlowLoss = other.CmbxTabPressureRandFlowLoss;
            CmbxTabPressureRandHorShift = other.CmbxTabPressureRandHorShift;
            CmbxTabPressureRandMaxSize = other.CmbxTabPressureRandMaxSize;
            CmbxTabPressureRandMinSize = other.CmbxTabPressureRandMinSize;
            CmbxTabPressureRandRotLeft = other.CmbxTabPressureRandRotLeft;
            CmbxTabPressureRandRotRight = other.CmbxTabPressureRandRotRight;
            CmbxTabPressureRandVerShift = other.CmbxTabPressureRandVerShift;
            TabPressureBrushDensity = other.TabPressureBrushDensity;
            TabPressureBrushFlow = other.TabPressureBrushFlow;
            TabPressureBrushOpacity = other.TabPressureBrushOpacity;
            TabPressureBrushRotation = other.TabPressureBrushRotation;
            TabPressureBrushSize = other.TabPressureBrushSize;
            TabPressureMaxBlueJitter = other.TabPressureMaxBlueJitter;
            TabPressureMaxGreenJitter = other.TabPressureMaxGreenJitter;
            TabPressureMaxHueJitter = other.TabPressureMaxHueJitter;
            TabPressureMaxRedJitter = other.TabPressureMaxRedJitter;
            TabPressureMaxSatJitter = other.TabPressureMaxSatJitter;
            TabPressureMaxValueJitter = other.TabPressureMaxValueJitter;
            TabPressureMinBlueJitter = other.TabPressureMinBlueJitter;
            TabPressureMinDrawDistance = other.TabPressureMinDrawDistance;
            TabPressureMinGreenJitter = other.TabPressureMinGreenJitter;
            TabPressureMinHueJitter = other.TabPressureMinHueJitter;
            TabPressureMinRedJitter = other.TabPressureMinRedJitter;
            TabPressureMinSatJitter = other.TabPressureMinSatJitter;
            TabPressureMinValueJitter = other.TabPressureMinValueJitter;
            TabPressureRandFlowLoss = other.TabPressureRandFlowLoss;
            TabPressureRandHorShift = other.TabPressureRandHorShift;
            TabPressureRandMaxSize = other.TabPressureRandMaxSize;
            TabPressureRandMinSize = other.TabPressureRandMinSize;
            TabPressureRandRotLeft = other.TabPressureRandRotLeft;
            TabPressureRandRotRight = other.TabPressureRandRotRight;
            TabPressureRandVerShift = other.TabPressureRandVerShift;
            SeamlessDrawing = other.SeamlessDrawing;
            SizeChange = other.SizeChange;
            RotChange = other.RotChange;
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