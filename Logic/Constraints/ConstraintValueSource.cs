namespace DynamicDraw
{
    /// <summary>
    /// The initial strength input for a constraint. It works like this: A constraint consists of naming the property
    /// to change, giving an amplitude for that change, and defining how the final value is applied. There is no final
    /// value without an initial value, which is determined by this. This value is then modified by the strength curve,
    /// and combined according to the input method associated with the constraint to get the final value.
    /// </summary>
    public enum ConstraintValueSource
    {
        /// <summary>
        /// The strength input is based on an interval range.
        /// </summary>
        Interval,

        /// <summary>
        /// There is no origin for the strength input. The strength input is treated as always 100%.
        /// </summary>
        None,

        /// <summary>
        /// The strength input is a random percent from the min to max range inclusive, any time it's computed.
        /// </summary>
        Random,

        /// <summary>
        /// The strength input is linked to the tablet pressure, so it ranges from 0 to 100% based on how much pressure
        /// the user exerts.
        /// </summary>
        TabletPressure
    }
}
