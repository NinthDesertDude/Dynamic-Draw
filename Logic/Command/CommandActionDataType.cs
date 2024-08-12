namespace DynamicDraw
{
    /// <summary>
    /// The possible types of values a shortcut's target action could be. These numeric values are serialized as part
    /// of keyboard shortcuts, so treat them as part of a public API and do not change existing ones.
    /// </summary>
    public enum CommandActionDataType
    {
        /// <summary>
        /// Indicates the command value is only true or false.
        /// </summary>
        Bool = 0,

        /// <summary>
        /// Indicates the command value is a numeric type with only whole numbers (positive or negative). Settings
        /// that track an option index in e.g. a dropdown with predictable values should use this type.
        /// </summary>
        Integer = 1,

        /// <summary>
        /// Indicates the command value is a numeric type with floating point numbers (positive or negative). Settings
        /// that affect values used in sensitive math, e.g. canvas rotation angle should use this type.
        /// </summary>
        Float = 2,

        /// <summary>
        /// Indicates the command value is a string.
        /// </summary>
        String = 3,

        /// <summary>
        /// Indicates the command value is an ARGB color, serialized as a uint.
        /// </summary>
        Color = 4,

        /// <summary>
        /// No data associated with the command.
        /// </summary>
        Action = 5
    }
}