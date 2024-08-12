namespace DynamicDraw
{
    /// <summary>
    /// The settings or actions to be taken. This identifies either actions such as Undo/Redo that have no associated
    /// data, or settings in the app such as Canvas Zoom which can have its value modified.
    /// </summary>
    public enum CommandTarget
    {
        /// <summary>
        /// Represents no shortcut.
        /// </summary>
        None = 0,

        /// <summary>
        /// Which type of tool is selected, e.g. brush or color picker.
        /// </summary>
        SelectedTool = 1,

        /// <summary>
        /// The current canvas's zoom level. Shortcuts targeting zoom this way will zoom towards canvas center.
        /// See <see cref="CanvasZoomToMouse"/> for the version that zooms to mouse location.
        /// </summary>
        CanvasZoom = 2,

        /// <summary>
        /// The brush the user has currently selected.
        /// </summary>
        SelectedBrush = 3,

        /// <summary>
        /// The brush image that the user has currently selected.
        /// </summary>
        SelectedBrushImage = 4,

        /// <summary>
        /// If true, the brush color is replaced by the active color. Otherwise, it's preserved.
        /// </summary>
        ColorizeBrush = 5,

        /// <summary>
        /// The current brush color.
        /// </summary>
        Color = 6,

        /// <summary>
        /// The current brush transparency level.
        /// </summary>
        Flow = 7,

        /// <summary>
        /// The angle that the brush is rotated at.
        /// </summary>
        Rotation = 8,

        /// <summary>
        /// How large the brush is (assuming square).
        /// </summary>
        Size = 9,

        /// <summary>
        /// How far the cursor must travel from the previous applied brush image for the next.
        /// </summary>
        MinDrawDistance = 10,

        /// <summary>
        /// If true, the value of brush stroke density will be automatically set based on the final brush size to
        /// always be smooth.
        /// </summary>
        AutomaticBrushDensity = 11,

        /// <summary>
        /// The distance between the mouse location and last applied location is filled with brush strokes while
        /// drawing. The distance is divided into fractions equal to 1/xth the size of the brush (assuming square),
        /// where x is this value. Zero turns off brush stroke density.
        /// </summary>
        BrushStrokeDensity = 12,

        /// <summary>
        /// What sort of symmetry to use when drawing, e.g. mirrors, radial, or multipoint.
        /// </summary>
        SymmetryMode = 13,

        /// <summary>
        /// The type of smoothing to apply on brush strokes.
        /// </summary>
        SmoothingMode = 14,

        /// <summary>
        /// Whether or not the brush should follow the mouse (assuming right points to the mouse in the unrotated
        /// image).
        /// </summary>
        RotateWithMouse = 15,

        /// <summary>
        /// If true, the alpha channel will not be affected while drawing.
        /// </summary>
        DoLockAlpha = 16,

        /// <summary>
        /// The amount the brush randomly shrinks from its normal value on each brush application.
        /// </summary>
        JitterMinSize = 17,

        /// <summary>
        /// The amount the brush randomly grows from its normal value on each brush application.
        /// </summary>
        JitterMaxSize = 18,

        /// <summary>
        /// The amount the brush is randomly rotated left from its normal value on each brush application.
        /// </summary>
        JitterRotLeft = 19,

        /// <summary>
        /// The amount the brush is randomly rotated right from its normal value on each brush application.
        /// </summary>
        JitterRotRight = 20,

        /// <summary>
        /// The amount of random transparency over the brush's normal value on each brush application.
        /// </summary>
        JitterFlowLoss = 21,

        /// <summary>
        /// The amount of random horizontal shift from the brush's normal x-position on each brush application.
        /// </summary>
        JitterHorSpray = 22,

        /// <summary>
        /// The amount of random vertical shift from the brush's normal y-position on each brush application.
        /// </summary>
        JitterVerSpray = 23,

        /// <summary>
        /// The amount of additional redness the brush has on each application.
        /// </summary>
        JitterRedMax = 24,

        /// <summary>
        /// The amount of additional greenness the brush has on each application.
        /// </summary>
        JitterGreenMax = 25,

        /// <summary>
        /// The amount of additional blueness the brush has on each application.
        /// </summary>
        JitterBlueMax = 26,

        /// <summary>
        /// How hue-shifted the brush is on each application.
        /// </summary>
        JitterHueMax = 27,

        /// <summary>
        /// The amount of saturation the brush has on each application.
        /// </summary>
        JitterSatMax = 28,

        /// <summary>
        /// How much extra brightness the brush has on each application.
        /// </summary>
        JitterValMax = 29,

        /// <summary>
        /// The amount of additional redness the brush has on each application.
        /// </summary>
        JitterRedMin = 30,

        /// <summary>
        /// The amount of additional greenness the brush has on each application.
        /// </summary>
        JitterGreenMin = 31,

        /// <summary>
        /// The amount of additional blueness the brush has on each application.
        /// </summary>
        JitterBlueMin = 32,

        /// <summary>
        /// How hue-shifted the brush is on each application.
        /// </summary>
        JitterHueMin = 33,

        /// <summary>
        /// The amount of saturation the brush has on each application.
        /// </summary>
        JitterSatMin = 34,

        /// <summary>
        /// How much extra brightness the brush has on each application.
        /// </summary>
        JitterValMin = 35,

        /// <summary>
        /// How much the size of the brush permanently increases on each application. Brush size reflects at the
        /// range bounds.
        /// </summary>
        SizeShift = 36,

        /// <summary>
        /// How much the tilt of the brush permanently increases on each application. Tilt wraps around at the range
        /// bounds.
        /// </summary>
        RotShift = 37,

        /// <summary>
        /// How much the transparency of the brush permanently increases on each application. Transparency reflects
        /// at the range bounds.
        /// </summary>
        FlowShift = 38,

        TabPressureFlow = 39,
        TabPressureSize = 40,
        TabPressureRotation = 41,
        TabPressureMinDrawDistance = 42,
        TabPressureBrushDensity = 43,
        TabPressureJitterMinSize = 44,
        TabPressureJitterMaxSize = 45,
        TabPressureJitterRotLeft = 46,
        TabPressureJitterRotRight = 47,
        TabPressureJitterFlowLoss = 48,
        TabPressureJitterHorShift = 49,
        TabPressureJitterVerShift = 50,
        TabPressureJitterRedMax = 51,
        TabPressureJitterRedMin = 52,
        TabPressureJitterGreenMax = 53,
        TabPressureJitterGreenMin = 54,
        TabPressureJitterBlueMax = 55,
        TabPressureJitterBlueMin = 56,
        TabPressureJitterHueMax = 57,
        TabPressureJitterHueMin = 58,
        TabPressureJitterSatMax = 59,
        TabPressureJitterSatMin = 60,
        TabPressureJitterValMax = 61,
        TabPressureJitterValMin = 62,

        /// <summary>
        /// The action to undo a change.
        /// </summary>
        UndoAction = 63,

        /// <summary>
        /// The action to redo a change.
        /// </summary>
        RedoAction = 64,

        /// <summary>
        /// Resets the canvas position, zoom, and rotation.
        /// </summary>
        ResetCanvasTransforms = 65,

        /// <summary>
        /// The canvas's horizontal position.
        /// </summary>
        CanvasX = 66,

        /// <summary>
        /// The canvas's vertical position.
        /// </summary>
        CanvasY = 67,

        /// <summary>
        /// The canvas's orientation in degrees.
        /// </summary>
        CanvasRotation = 68,

        /// <summary>
        /// The blending mode used when drawing with the brush.
        /// </summary>
        BlendMode = 69,

        /// <summary>
        /// Whether to wrap around to the other side of the canvas where brush stamps would clip.
        /// </summary>
        SeamlessDrawing = 70,

        /// <summary>
        /// The amount to mix the active color with the brush color when colorize brush is off.
        /// </summary>
        ColorInfluence = 71,

        /// <summary>
        /// Whether mixing the active color with the brush should affect hue when colorize brush is off.
        /// </summary>
        ColorInfluenceHue = 72,

        /// <summary>
        /// Whether mixing the active color with the brush should affect saturation when colorize brush is off.
        /// </summary>
        ColorInfluenceSat = 73,

        /// <summary>
        /// Whether mixing the active color with the brush should affect value when colorize brush is off.
        /// </summary>
        ColorInfluenceVal = 74,

        /// <summary>
        /// Whether to draw in a checkerboard pattern, skipping every other pixel or not.
        /// </summary>
        DitherDraw = 75,

        /// <summary>
        /// If true, the red channel will not be affected while drawing.
        /// </summary>
        DoLockR = 76,

        /// <summary>
        /// If true, the green channel will not be affected while drawing.
        /// </summary>
        DoLockG = 77,

        /// <summary>
        /// If true, the blue channel will not be affected while drawing.
        /// </summary>
        DoLockB = 78,

        /// <summary>
        /// If true, the hue will not be affected while drawing.
        /// </summary>
        DoLockHue = 79,

        /// <summary>
        /// If true, the saturation will not be affected while drawing.
        /// </summary>
        DoLockSat = 80,

        /// <summary>
        /// If true, the value will not be affected while drawing.
        /// </summary>
        DoLockVal = 81,

        /// <summary>
        /// The brush opacity (or rather, max alpha allowed on the layer). Anything greater truncates to max.
        /// </summary>
        BrushOpacity = 82,

        /// <summary>
        /// The chosen effect to draw with, if set.
        /// </summary>
        ChosenEffect = 83,

        /// <summary>
        /// The current canvas's zoom level. Shortcuts targeting zoom this way will zoom towards mouse location.
        /// See <see cref="CanvasZoom"/> for the version that zoom to canvas center.
        /// </summary>
        CanvasZoomToMouse = 84,

        /// <summary>
        /// Centers the canvas and zooms to fit the entire canvas in view.
        /// </summary>
        CanvasZoomFit = 85,

        /// <summary>
        /// Swaps the primary and secondary colors with each other.
        /// </summary>
        SwapPrimarySecondaryColors = 86,

        /// <summary>
        /// Opens the color picker dialog with RGBA, HSV controls.
        /// </summary>
        OpenColorPickerDialog = 87,

        /// <summary>
        /// Opens the quick command dialog that allows the user to execute a command from a list.
        /// </summary>
        OpenQuickCommandDialog = 88,

        /// <summary>
        /// Switches to the provided palette index.
        /// </summary>
        SwitchPalette = 89,

        /// <summary>
        /// Picks the color at the given index.
        /// </summary>
        PickFromPalette = 90,

        /// <summary>
        /// Confirms the line tool, creating a line and removing the drag handles.
        /// </summary>
        ConfirmLine = 91,

        /// <summary>
        /// Opens the script editor, defaulting to the scripts associated to the current brush, if applicable.
        /// </summary>
        OpenScriptEditorDialog = 92,

        /// <summary>
        /// Stamps the current brush at an x,y position relative to current position, at current input pressure
        /// </summary>
        StampBrush = 93,

        /// <summary>
        /// Stamps the current brush relative to the points between the prev to current mouse position,
        /// interpolating between prev/current input pressure
        /// </summary>
        StampLineTo = 94,

        /// <summary>
        /// Stamps the current brush between two arbitrary points, interpolating between prev/current input pressure
        /// </summary>
        StampLineBetween = 95,

        /// <summary>
        /// Picks a color at an x,y position
        /// </summary>
        PickColor = 96,

        /// <summary>
        /// The pressure as read from the tablet, overwritten every frame.
        /// </summary>
        InputPressure = 97,

        /// <summary>
        /// The pressure from last frame as read from the tablet, overwritten every frame.
        /// </summary>
        InputPressurePrev = 98,

        /// <summary>
        /// The current mouse position (X).
        /// </summary>
        ReadMousePosX = 99,

        /// <summary>
        /// The current mouse position (Y).
        /// </summary>
        ReadMousePosY = 100,

        /// <summary>
        /// The mouse position last frame (X).
        /// </summary>
        ReadMousePosXPrev = 101,

        /// <summary>
        /// The mouse position last frame (Y).
        /// </summary>
        ReadMousePosYPrev = 102,

        /// <summary>
        /// This is used by scripts to record the starting position of the last brush stroke (X).
        /// </summary>
        ReadStrokeStartPosX = 103,

        /// <summary>
        /// This is used by scripts to record the starting position of the last brush stroke (Y).
        /// </summary>
        ReadStrokeStartPosY = 104,
    }
}