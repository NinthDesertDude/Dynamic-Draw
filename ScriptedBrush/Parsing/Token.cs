namespace DynamicDraw.Parsing
{
    /// <summary>
    /// Represents a single token for evaluation. Abstract.
    /// </summary>
    public abstract class Token
    {
        #region Properties
        /// <summary>
        /// The string format of the token.
        /// </summary>
        public string StrForm { get; protected set; }
        #endregion
    }
}