namespace BrushFactory
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
        /// The distance between the mouse location and last applied location is filled with brush strokes while
        /// drawing. The distance is divided into fractions equal to 1/xth the size of the brush (assuming square),
        /// where x is this value. Zero turns off brush stroke density.
        /// </summary>
        BrushStrokeDensity = 10,

        /// <summary>
        /// What sort of symmetry to use when drawing, e.g. mirrors, radial, or multipoint.
        /// </summary>
        SymmetryMode = 11,

        /// <summary>
        /// The type of smoothing to apply on brush strokes.
        /// </summary>
        SmoothingMode = 12,

        /// <summary>
        /// Whether or not the brush should follow the mouse (assuming right points to the mouse in the unrotated
        /// image).
        /// </summary>
        RotateWithMouse = 13,

        /// <summary>
        /// If true, the alpha value is left unchanged at the end of a brush stroke.
        /// </summary>
        LockAlpha = 14,

        /// <summary>
        /// The amount the brush randomly shrinks from its normal value on each brush application.
        /// </summary>
        JitterMinSize = 15,

        /// <summary>
        /// The amount the brush randomly grows from its normal value on each brush application.
        /// </summary>
        JitterMaxSize = 16,

        /// <summary>
        /// The amount the brush is randomly rotated left from its normal value on each brush application.
        /// </summary>
        JitterRotLeft = 17,

        /// <summary>
        /// The amount the brush is randomly rotated right from its normal value on each brush application.
        /// </summary>
        JitterRotRight = 18,

        /// <summary>
        /// The amount of random transparency over the brush's normal value on each brush application.
        /// </summary>
        JitterMinAlpha = 19,

        /// <summary>
        /// The amount of random horizontal shift from the brush's normal x-position on each brush application.
        /// </summary>
        JitterHorSpray = 20,

        /// <summary>
        /// The amount of random vertical shift from the brush's normal y-position on each brush application.
        /// </summary>
        JitterVerSpray = 21,

        /// <summary>
        /// The amount of additional redness the brush has on each application.
        /// </summary>
        JitterRedMax = 22,

        /// <summary>
        /// The amount of additional greenness the brush has on each application.
        /// </summary>
        JitterGreenMax = 23,

        /// <summary>
        /// The amount of additional blueness the brush has on each application.
        /// </summary>
        JitterBlueMax = 24,

        /// <summary>
        /// How hue-shifted the brush is on each application.
        /// </summary>
        JitterHueMax = 25,

        /// <summary>
        /// The amount of saturation the brush has on each application.
        /// </summary>
        JitterSatMax = 26,

        /// <summary>
        /// How much extra brightness the brush has on each application.
        /// </summary>
        JitterValMax = 27,

        /// <summary>
        /// The amount of additional redness the brush has on each application.
        /// </summary>
        JitterRedMin = 28,

        /// <summary>
        /// The amount of additional greenness the brush has on each application.
        /// </summary>
        JitterGreenMin = 29,

        /// <summary>
        /// The amount of additional blueness the brush has on each application.
        /// </summary>
        JitterBlueMin = 30,

        /// <summary>
        /// How hue-shifted the brush is on each application.
        /// </summary>
        JitterHueMin = 31,

        /// <summary>
        /// The amount of saturation the brush has on each application.
        /// </summary>
        JitterSatMin = 32,

        /// <summary>
        /// How much extra brightness the brush has on each application.
        /// </summary>
        JitterValMin = 33,

        /// <summary>
        /// How much the size of the brush permanently increases on each application. Brush size reflects at the
        /// range bounds.
        /// </summary>
        SizeShift = 34,

        /// <summary>
        /// How much the tilt of the brush permanently increases on each application. Tilt wraps around at the range
        /// bounds.
        /// </summary>
        RotShift = 35,

        /// <summary>
        /// How much the transparency of the brush permanently increases on each application. Transparency reflects
        /// at the range bounds.
        /// </summary>
        AlphaShift = 36,

        TabPressureAlpha = 37,
        TabPressureSize = 38,
        TabPressureRotation = 39,
        TabPressureMinDrawDistance = 40,
        TabPressureBrushDensity = 41,
        TabPressureJitterMinSize = 42,
        TabPressureJitterMaxSize = 43,
        TabPressureJitterRotLeft = 44,
        TabPressureJitterRotRight = 45,
        TabPressureJitterMinAlpha = 46,
        TabPressureJitterHorShift = 47,
        TabPressureJitterVerShift = 48,
        TabPressureJitterRedMax = 49,
        TabPressureJitterRedMin = 50,
        TabPressureJitterGreenMax = 51,
        TabPressureJitterGreenMin = 52,
        TabPressureJitterBlueMax = 53,
        TabPressureJitterBlueMin = 54,
        TabPressureJitterHueMax = 55,
        TabPressureJitterHueMin = 56,
        TabPressureJitterSatMax = 57,
        TabPressureJitterSatMin = 58,
        TabPressureJitterValMax = 59,
        TabPressureJitterValMin = 60,

        /// <summary>
        /// The action to undo a change.
        /// </summary>
        UndoAction = 61,

        /// <summary>
        /// The action to redo a change.
        /// </summary>
        RedoAction = 62
    }
}