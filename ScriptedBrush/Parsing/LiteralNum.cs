using System;

namespace DynamicDraw.Parsing
{
    /// <summary>
    /// An immutable decimal literal.
    /// </summary>
    public class LiteralNum : Token
    {
        #region Properties
        /// <summary>
        /// The associated numeric value.
        /// </summary>
        public decimal Value { get; protected set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a numeric parsing token.
        /// </summary>
        /// <param name="value">
        /// The number encapsulated in the token.
        /// </param>
        public LiteralNum(string value)
        {
            StrForm = value;

            if (Decimal.TryParse(value, out decimal result))
            {
                Value = result;
            }
            else
            {
                throw new ParsingException("The expression '" + value +
                    "' is not a valid number.");
            }
        }

        /// <summary>
        /// Creates a numeric parsing token.
        /// </summary>
        /// <param name="value">
        /// The number encapsulated in the token.
        /// </param>
        public LiteralNum(decimal value)
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
        public bool Equals(LiteralNum obj)
        {
            return (StrForm == obj.StrForm &&
                Value == obj.Value);
        }
        #endregion
    }
}