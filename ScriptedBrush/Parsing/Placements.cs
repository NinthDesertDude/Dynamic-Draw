namespace DynamicDraw.Parsing
{
    /// <summary>
    /// Determines how operands interact with an operator token.
    /// </summary>
    public enum Placements
    {
        /// <summary>
        /// For unary tokens that use the preceeding number, like negation.
        /// </summary>
        Left,

        /// <summary>
        /// For unary tokens that use the following number, like factorial.
        /// </summary>
        Right,

        /// <summary>
        /// For binary tokens.
        /// </summary>
        Both
    }
}
