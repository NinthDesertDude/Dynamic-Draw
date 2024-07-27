namespace DynamicDraw.Parsing
{
    /// <summary>
    /// An immutable identifier token, such as a constant or variable.
    /// </summary>
    public class LiteralId : Token
    {
        #region Properties
        /// <summary>
        /// The associated numeric value.
        /// </summary>
        public decimal? Value { get; protected set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates an identifier parsing token.
        /// </summary>
        /// <param name="format">
        /// The unique name for the identifier.
        /// </param>
        /// <param name="value">
        /// The number encapsulated in the token.
        /// </param>
        public LiteralId(string name, decimal? value)
        {
            StrForm = name;
            Value = value;
        }

        /// <summary>
        /// Returns true if all properties of each token are the same.
        /// </summary>
        /// <param name="obj">
        /// The token to compare against for equality.
        /// </param>
        public bool Equals(LiteralId obj)
        {
            return (StrForm == obj.StrForm &&
                Value == obj.Value);
        }
        #endregion
    }
}