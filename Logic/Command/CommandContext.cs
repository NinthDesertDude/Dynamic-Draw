namespace DynamicDraw
{
    /// <summary>
    /// Context refers to the scenarios in which a shortcut is available, like when the display canvas has focus, or
    /// the user is drawing. This is used to disable shortcuts based on current conditions.
    /// </summary>
    public enum CommandContext
    {
        /// <summary>
        /// When the user is hovered over or drawing/panning the canvas.
        /// </summary>
        OnCanvas = 0,

        /// <summary>
        /// When the user is hovered or focused on the sidebar.
        /// </summary>
        OnSidebar = 1,

        /// <summary>
        /// When the user has the brush tool active.
        /// </summary>
        ToolBrushActive = 2,

        /// <summary>
        /// When the user has the eraser tool active.
        /// </summary>
        ToolEraserActive = 3,

        /// <summary>
        /// When the user has the color picker tool active.
        /// </summary>
        ToolColorPickerActive = 4,

        /// <summary>
        /// When the user has the set origin tool active.
        /// </summary>
        ToolSetOriginActive = 5,

        /// <summary>
        /// When the user has the clone stamp tool active.
        /// </summary>
        ToolCloneStampActive = 6,

        /// <summary>
        /// When the user has the line tool active.
        /// </summary>
        ToolLineToolActive = 7,

        /// <summary>
        /// When the clone stamp is active, but the origin hasn't been set yet.
        /// </summary>
        CloneStampOriginUnsetStage = 8,

        /// <summary>
        /// When the clone stamp is active, and the origin has already been set.
        /// </summary>
        CloneStampOriginSetStage = 9,

        /// <summary>
        /// When the line tool is active, but the start point hasn't been set yet.
        /// </summary>
        LineToolUnstartedStage = 10,

        /// <summary>
        /// When the line tool is active, and both the start and end point have been set.
        /// </summary>
        LineToolConfirmStage = 11
    }
}