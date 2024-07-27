namespace DynamicDraw.Parsing
{
    /// <summary>
    /// An immutable bool literal.
    /// </summary>
    public class LiteralBool : Token
    {
        #region Properties
        /// <summary>
        /// The associated boolean value.
        /// </summary>
        public bool Value { get; protected set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a bool parsing token.
        /// </summary>
        /// <param name="value">
        /// The boolean encapsulated in the token.
        /// </param>
        public LiteralBool(bool value)
        {
            StrForm = value.ToString();
            Value = value;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Returns true if all properties of each token are the same.
        /// </summary>
        /// <param name="obj">
        /// The token to compare against for equality.
        /// </param>
        public bool Equals(LiteralBool obj)
        {
            return (StrForm == obj.StrForm &&
                Value == obj.Value);
        }
        #endregion
    }
}