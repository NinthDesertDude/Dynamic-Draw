namespace DynamicDraw
{
    /// <summary>
    /// Describes the compatibility of a custom effect used in conjunction with this plugin.
    /// </summary>
    public enum CustomEffectCompatibilityStatus
    {
        /// <summary>
        /// The plugin does not work even when running natively in paint.net.
        /// </summary>
        BrokenEverywhere,

        /// <summary>
        /// The plugin works with no known issues.
        /// </summary>
        Compatible,

        /// <summary>
        /// The plugin works with mild issues or inconsistency to how it would work when run natively in paint.net.
        /// </summary>
        CompatibleWithDifferences,

        /// <summary>
        /// The plugin crashes paint.net intermittently or in certain conditions.
        /// </summary>
        ConditionalCrash,

        /// <summary>
        /// The plugin fails to render intermittently or in certain conditions.
        /// </summary>
        ConditionalFailToRender,

        /// <summary>
        /// The plugin reliably crashes paint.net when run within this plugin.
        /// </summary>
        ReliableCrash,

        /// <summary>
        /// The plugin fails to render.
        /// </summary>
        ReliableFailToRender,

        /// <summary>
        /// The plugin reliably fails to run.
        /// </summary>
        ReliableFailToStart,

        /// <summary>
        /// The compatibility isn't tested or is unknown.
        /// </summary>
        Unknown
    }
}
