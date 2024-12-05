using System.Text.Json.Serialization;

namespace DynamicDraw
{
    /// <summary>
    /// Preferences set by the user for various facets about how the program should behave.
    /// </summary>
    public class UserSettings
    {
        #region Fields
        /// <summary>
        /// How transparency will be displayed on canvas, e.g. white or checkered.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("BackgroundDisplayMode")]
        public BackgroundDisplayMode BackgroundDisplayMode { get; set; }

        /// <summary>
        /// How the brush indicator will appear, e.g. as a square or as a preview of the current brush.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("BrushCursorPreview")]
        public BrushCursorPreview BrushCursorPreview { get; set; }

        /// <summary>
        /// If true, the color picker will also copy the transparency of the selected pixel.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ColorPickerIncludesAlpha")]
        public bool ColorPickerIncludesAlpha { get; set; }

        /// <summary>
        /// If true, the color picker will switch to the previous tool after clicking.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ColorPickerSwitchesToLastTool")]
        public bool ColorPickerSwitchesToLastTool { get; set; }

        /// <summary>
        /// If true, the confirmation dialog asking if you want to close the plugin will not be displayed when
        /// attempting to close or cancel it.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("DisableConfirmationOnCloseOrSave")]
        public bool DisableConfirmationOnCloseOrSave { get; set; }

        /// <summary>
        /// How to sort detected pixels when using palette from image.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("PaletteFromImageSortMode")]
        public PaletteFromImageSortMode PaletteFromImageSortMode { get; set; }

        /// <summary>
        /// If true, custom brush paths (both directories and files) that are not found will result in those paths
        /// being removed from the list of custom image paths to load.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RemoveBrushImagePathsWhenNotFound")]
        public bool RemoveBrushImagePathsWhenNotFound { get; set; }

        /// <summary>
        /// If true, the circle that indicates how far the minimum distance goes will be shown as long as minimum
        /// distance is nonzero.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ShowCircleRadiusWhenUsingMinDistance")]
        public bool ShowCircleRadiusWhenUsingMinDistance { get; set; }

        /// <summary>
        /// if true, the symmetry origin will be shown at all times as long as a symmetry mode is active.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ShowSymmetryLinesWhenUsingSymmetry")]
        public bool ShowSymmetryLinesWhenUsingSymmetry { get; set; }

        /// <summary>
        /// The theme to use when setting up the form.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("PreferredTheme")]
        public ThemePreference PreferredTheme { get; set; }
        #endregion

        /// <summary>
        /// Creates an empty settings object with the default program preferences.
        /// </summary>
        public UserSettings()
        {
            BackgroundDisplayMode = BackgroundDisplayMode.ClipboardOnlyIfFits;
            BrushCursorPreview = BrushCursorPreview.Preview;
            ColorPickerIncludesAlpha = false;
            ColorPickerSwitchesToLastTool = true;
            DisableConfirmationOnCloseOrSave = false;
            PaletteFromImageSortMode = PaletteFromImageSortMode.HVSA;
            RemoveBrushImagePathsWhenNotFound = false;
            ShowCircleRadiusWhenUsingMinDistance = true;
            ShowSymmetryLinesWhenUsingSymmetry = true;
            PreferredTheme = ThemePreference.Inherited;
        }

        public UserSettings(UserSettings other)
        {
            BackgroundDisplayMode = other.BackgroundDisplayMode;
            BrushCursorPreview = other.BrushCursorPreview;
            ColorPickerIncludesAlpha = other.ColorPickerIncludesAlpha;
            ColorPickerSwitchesToLastTool = other.ColorPickerSwitchesToLastTool;
            DisableConfirmationOnCloseOrSave = other.DisableConfirmationOnCloseOrSave;
            PaletteFromImageSortMode = other.PaletteFromImageSortMode;
            RemoveBrushImagePathsWhenNotFound = other.RemoveBrushImagePathsWhenNotFound;
            ShowCircleRadiusWhenUsingMinDistance = other.ShowCircleRadiusWhenUsingMinDistance;
            ShowSymmetryLinesWhenUsingSymmetry = other.ShowSymmetryLinesWhenUsingSymmetry;
            PreferredTheme = other.PreferredTheme;
        }
    }

    /// <summary>
    /// Describes how to fill the transparent region underneath the user's image.
    /// </summary>
    public enum BackgroundDisplayMode
    {
        /// <summary>
        /// The area underneath will be filled with a checkerboard pattern similar to what paint.net uses.
        /// </summary>
        Transparent = 0,

        /// <summary>
        /// The area underneath will be filled by the image on the clipboard, centered and squashed to the bounds as
        /// needed. It won't be stretched to fill. Generally the user will copy a picture of all the layers merged
        /// under their current image so they can use this feature to see layers below for e.g. drawing on a blank
        /// layer. Below the image, if both have transparency showing up in the same place(s), is a checkerboard
        /// transparent pattern (and if no image is available when this mode is active, it only uses the checkerboard).
        /// </summary>
        ClipboardFit = 1,

        /// <summary>
        /// Similar to <see cref="ClipboardFit"/>, but no sizing occurs because the clipboard image is only used when
        /// the dimensions match the canvas.
        /// </summary>
        ClipboardOnlyIfFits = 2,

        /// <summary>
        /// The area underneath will not be filled. The gray of the canvas underneath will occupy the area. This is
        /// fastest because no additional drawing is performed.
        /// </summary>
        Gray = 3,

        /// <summary>
        /// The area underneath will be filled in white.
        /// </summary>
        White = 4,

        /// <summary>
        /// The area underneath will be filled in black.
        /// </summary>
        Black = 5
    }

    /// <summary>
    /// Preferences regarding how to display the brush cursor when the user hovers over the canvas.
    /// </summary>
    public enum BrushCursorPreview
    {
        /// <summary>
        /// Displays a square that moves with the cursor and has rotation/scaling based on the canvas state.
        /// </summary>
        Square = 0,

        /// <summary>
        /// Displays a half-transparent copy of the current brush that moves with the cursor.
        /// </summary>
        Preview = 1
    }

    public enum PaletteFromImageSortMode
    {
        /// <summary>
        /// Sorts in channel order: AHVS, grouping alpha and hue into chunks. Alpha is opaque-first.
        /// </summary>
        AHVS = 0,

        /// <summary>
        /// Sorts in channel order: HVSA, grouping hue into chunks. Alpha is opaque-first.
        /// </summary>
        HVSA = 1,

        /// <summary>
        /// Sorts pixels by how often they appear, then in channel order: HVSA, grouping hue into chunks. Alpha is
        /// opaque-first. This is mainly used to spot unwanted pixel colors when developing a paletted image.
        /// </summary>
        Usage = 2,

        /// <summary>
        /// Sorts pixels by how close they resemble the active primary color, then in channel order: HVSA, grouping hue
        /// into chunks. Secondary sorts will rarely be needed, but are included for deterministic order.
        /// </summary>
        PrimaryDistance = 3,

        /// <summary>
        /// Sorts in channel order: VHSA, grouping value and hue into chunks. Alpha is opaque-first.
        /// </summary>
        VHSA = 4
    }
}