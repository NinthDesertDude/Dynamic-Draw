namespace DynamicDraw.Parsing
{
    /// <summary>
    /// An immutable symbol.
    /// </summary>
    public class Symbol : Token
    {
        #region Constructors
        /// <summary>
        /// Creates a symbol token.
        /// </summary>
        public Symbol(string name)
        {
            StrForm = name;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Returns true if all properties of each token are the same.
        /// </summary>
        /// <param name="obj">
        /// The token to compare against for equality.
        /// </param>
        public bool Equals(Symbol obj)
        {
            return (StrForm == obj.StrForm);
        }
        #endregion
    }
}