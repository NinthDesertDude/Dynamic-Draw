namespace BrushFactory
{
    /// <summary>
    /// The possible types of values a shortcut's target action could be.
    /// </summary>
    public enum ShortcutTargetDataType
    {
        /// <summary>
        /// Indicates the setting value is only true or false.
        /// </summary>
        Bool = 0,

        /// <summary>
        /// Indicates the setting value is a numeric type with only whole numbers (positive or negative). Settings
        /// that track an option index in e.g. a dropdown with predictable values should use this type.
        /// </summary>
        Integer = 1,

        /// <summary>
        /// Indicates the setting value is a string.
        /// </summary>
        String = 2,

        /// <summary>
        /// Indicates the setting value is an ARGB color, serialized as a uint.
        /// </summary>
        Color = 3,

        /// <summary>
        /// No data associated with the setting.
        /// </summary>
        Action = 4
    }
}