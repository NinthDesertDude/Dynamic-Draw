namespace DynamicDraw
{
    /// <summary>
    /// The ways that a value can be applied.
    /// </summary>
    public enum ConstraintValueHandlingMethod
    {
        /// <summary>
        /// Ignore the associated value. The setting isn't affected by the input in this case.
        /// </summary>
        DoNothing = 0,

        /// <summary>
        /// The (maybe negative) value is added to the setting based on a linear curve with the input strength.
        /// </summary>
        Add = 1,

        /// <summary>
        /// The (maybe negative) value, as a fraction of the setting's max range, is added to the setting based on
        /// a linear curve with the input strength.
        /// </summary>
        AddPercent = 2,

        /// <summary>
        /// The (maybe negative) value, as a fraction of the current setting value, is added to the setting based
        /// on a linear curve with the input strength.
        /// </summary>
        AddPercentCurrent = 3,

        /// <summary>
        /// The value is interpolated from 0% (current) to 100% (the associated value) on a linear curve with the
        /// input strength.
        /// </summary>
        MatchValue = 4,

        /// <summary>
        /// The value is interpolated from 0% (current) to 100% (the associated value * the max range) on a linear
        /// curve with the input strength.
        /// </summary>
        MatchPercent = 5
    }
}
