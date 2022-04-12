namespace DynamicDraw
{
    /// <summary>
    /// The settings or actions to be taken. This identifies either actions such as Undo/Redo that have no associated
    /// data, or settings in the app such as Canvas Zoom which can have its value modified.
    /// </summary>
    public enum ShortcutTarget
    {
        /// <summary>
        /// Which type of tool is selected, e.g. brush or color picker.
        /// </summary>
        SelectedTool = 0,

        /// <summary>
        /// The current canvas's zoom level.
        /// </summary>
        CanvasZoom = 1,

        /// <summary>
        /// The brush the user has currently selected.
        /// </summary>
        SelectedBrush = 2,

        /// <summary>
        /// The brush image that the user has currently selected.
        /// </summary>
        SelectedBrushImage = 3,

        /// <summary>
        /// If true, the brush color is replaced by the active color. Otherwise, it's preserved.
        /// </summary>
        ColorizeBrush = 4,

        /// <summary>
        /// The current brush color.
        /// </summary>
        Color = 5,

        /// <summary>
        /// The current brush transparency level.
        /// </summary>
        Alpha = 6,

        /// <summary>
        /// The angle that the brush is rotated at.
        /// </summary>
        Rotation = 7,

        /// <summary>
        /// How large the brush is (assuming square).
        /// </summary>
        Size = 8,

        /// <summary>
        /// How far the cursor must travel from the previous applied brush image for the next.
        /// </summary>
        MinDrawDistance = 9,

        /// <summary>
        /// If true, the value of brush stroke density will be automatically set based on the final brush size to
        /// always be smooth.
        /// </summary>
        AutomaticBrushDensity = 10,

        /// <summary>
        /// The distance between the mouse location and last applied location is filled with brush strokes while
        /// drawing. The distance is divided into fractions equal to 1/xth the size of the brush (assuming square),
        /// where x is this value. Zero turns off brush stroke density.
        /// </summary>
        BrushStrokeDensity = 11,

        /// <summary>
        /// What sort of symmetry to use when drawing, e.g. mirrors, radial, or multipoint.
        /// </summary>
        SymmetryMode = 12,

        /// <summary>
        /// The type of smoothing to apply on brush strokes.
        /// </summary>
        SmoothingMode = 13,

        /// <summary>
        /// Whether or not the brush should follow the mouse (assuming right points to the mouse in the unrotated
        /// image).
        /// </summary>
        RotateWithMouse = 14,

        /// <summary>
        /// If true, the alpha channel will not be affected while drawing.
        /// </summary>
        DoLockAlpha = 15,

        /// <summary>
        /// The amount the brush randomly shrinks from its normal value on each brush application.
        /// </summary>
        JitterMinSize = 16,

        /// <summary>
        /// The amount the brush randomly grows from its normal value on each brush application.
        /// </summary>
        JitterMaxSize = 17,

        /// <summary>
        /// The amount the brush is randomly rotated left from its normal value on each brush application.
        /// </summary>
        JitterRotLeft = 18,

        /// <summary>
        /// The amount the brush is randomly rotated right from its normal value on each brush application.
        /// </summary>
        JitterRotRight = 19,

        /// <summary>
        /// The amount of random transparency over the brush's normal value on each brush application.
        /// </summary>
        JitterMinAlpha = 20,

        /// <summary>
        /// The amount of random horizontal shift from the brush's normal x-position on each brush application.
        /// </summary>
        JitterHorSpray = 21,

        /// <summary>
        /// The amount of random vertical shift from the brush's normal y-position on each brush application.
        /// </summary>
        JitterVerSpray = 22,

        /// <summary>
        /// The amount of additional redness the brush has on each application.
        /// </summary>
        JitterRedMax = 23,

        /// <summary>
        /// The amount of additional greenness the brush has on each application.
        /// </summary>
        JitterGreenMax = 24,

        /// <summary>
        /// The amount of additional blueness the brush has on each application.
        /// </summary>
        JitterBlueMax = 25,

        /// <summary>
        /// How hue-shifted the brush is on each application.
        /// </summary>
        JitterHueMax = 26,

        /// <summary>
        /// The amount of saturation the brush has on each application.
        /// </summary>
        JitterSatMax = 27,

        /// <summary>
        /// How much extra brightness the brush has on each application.
        /// </summary>
        JitterValMax = 28,

        /// <summary>
        /// The amount of additional redness the brush has on each application.
        /// </summary>
        JitterRedMin = 29,

        /// <summary>
        /// The amount of additional greenness the brush has on each application.
        /// </summary>
        JitterGreenMin = 30,

        /// <summary>
        /// The amount of additional blueness the brush has on each application.
        /// </summary>
        JitterBlueMin = 31,

        /// <summary>
        /// How hue-shifted the brush is on each application.
        /// </summary>
        JitterHueMin = 32,

        /// <summary>
        /// The amount of saturation the brush has on each application.
        /// </summary>
        JitterSatMin = 33,

        /// <summary>
        /// How much extra brightness the brush has on each application.
        /// </summary>
        JitterValMin = 34,

        /// <summary>
        /// How much the size of the brush permanently increases on each application. Brush size reflects at the
        /// range bounds.
        /// </summary>
        SizeShift = 35,

        /// <summary>
        /// How much the tilt of the brush permanently increases on each application. Tilt wraps around at the range
        /// bounds.
        /// </summary>
        RotShift = 36,

        /// <summary>
        /// How much the transparency of the brush permanently increases on each application. Transparency reflects
        /// at the range bounds.
        /// </summary>
        AlphaShift = 37,

        TabPressureAlpha = 38,
        TabPressureSize = 39,
        TabPressureRotation = 40,
        TabPressureMinDrawDistance = 41,
        TabPressureBrushDensity = 42,
        TabPressureJitterMinSize = 43,
        TabPressureJitterMaxSize = 44,
        TabPressureJitterRotLeft = 45,
        TabPressureJitterRotRight = 46,
        TabPressureJitterMinAlpha = 47,
        TabPressureJitterHorShift = 48,
        TabPressureJitterVerShift = 49,
        TabPressureJitterRedMax = 50,
        TabPressureJitterRedMin = 51,
        TabPressureJitterGreenMax = 52,
        TabPressureJitterGreenMin = 53,
        TabPressureJitterBlueMax = 54,
        TabPressureJitterBlueMin = 55,
        TabPressureJitterHueMax = 56,
        TabPressureJitterHueMin = 57,
        TabPressureJitterSatMax = 58,
        TabPressureJitterSatMin = 59,
        TabPressureJitterValMax = 60,
        TabPressureJitterValMin = 61,

        /// <summary>
        /// The action to undo a change.
        /// </summary>
        UndoAction = 62,

        /// <summary>
        /// The action to redo a change.
        /// </summary>
        RedoAction = 63,

        /// <summary>
        /// Resets the canvas position, zoom, and rotation.
        /// </summary>
        ResetCanvasTransforms = 64,

        /// <summary>
        /// The canvas's horizontal position.
        /// </summary>
        CanvasX = 65,

        /// <summary>
        /// The canvas's vertical position.
        /// </summary>
        CanvasY = 66,

        /// <summary>
        /// The canvas's orientation in degrees.
        /// </summary>
        CanvasRotation = 67,

        /// <summary>
        /// The blending mode used when drawing with the brush.
        /// </summary>
        BlendMode = 68,

        /// <summary>
        /// Whether to wrap around to the other side of the canvas where brush stamps would clip.
        /// </summary>
        SeamlessDrawing = 69,

        /// <summary>
        /// The amount to mix the active color with the brush color when colorize brush is off.
        /// </summary>
        ColorInfluence = 70,

        /// <summary>
        /// Whether mixing the active color with the brush should affect hue when colorize brush is off.
        /// </summary>
        ColorInfluenceHue = 71,

        /// <summary>
        /// Whether mixing the active color with the brush should affect saturation when colorize brush is off.
        /// </summary>
        ColorInfluenceSat = 72,

        /// <summary>
        /// Whether mixing the active color with the brush should affect value when colorize brush is off.
        /// </summary>
        ColorInfluenceVal = 73,

        /// <summary>
        /// Whether to draw in a checkerboard pattern, skipping every other pixel or not.
        /// </summary>
        DitherDraw = 74,

        /// <summary>
        /// If true, the red channel will not be affected while drawing.
        /// </summary>
        DoLockR = 75,

        /// <summary>
        /// If true, the green channel will not be affected while drawing.
        /// </summary>
        DoLockG = 76,

        /// <summary>
        /// If true, the blue channel will not be affected while drawing.
        /// </summary>
        DoLockB = 77,

        /// <summary>
        /// If true, the hue will not be affected while drawing.
        /// </summary>
        DoLockHue = 78,

        /// <summary>
        /// If true, the saturation will not be affected while drawing.
        /// </summary>
        DoLockSat = 79,

        /// <summary>
        /// If true, the value will not be affected while drawing.
        /// </summary>
        DoLockVal = 80,

        /// <summary>
        /// The brush opacity (or rather, max alpha allowed on the layer). Anything greater truncates to max.
        /// </summary>
        BrushOpacity = 81
    }
}