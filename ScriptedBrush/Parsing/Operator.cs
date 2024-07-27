using System;

namespace DynamicDraw.Parsing
{
    /// <summary>
    /// An immutable operator that performs an action on the token to its
    /// left and/or right and returns a single token.
    /// </summary>
    public class Operator : Token
    {
        #region Properties
        /// <summary>
        /// Indicates which side of the operator the operand is on, or if
        /// the operator is binary.
        /// </summary>
        public Placements Placement { get; protected set; }

        /// <summary>
        /// Sets whether an expression such as a ~ b ~ c is evaluated
        /// left-to-right or right-to-left.
        /// </summary>
        public Associativity Assoc { get; protected set; }

        /// <summary>
        /// The order in which operator tokens are evaluated.
        /// </summary>
        public int Prec { get; protected set; }

        /// <summary>
        /// The number of arguments to be used.
        /// </summary>
        public int NumArgs { get; protected set; }

        /// <summary>
        /// When this operator is used, the input numbers can be accessed as
        /// an array of objects. The lefthand operand is element 0 for
        /// binary operators.
        /// </summary>
        public Func<Token[], Token> Operation
        {
            get;
            set;
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates an operator parsing token.
        /// </summary>
        /// <param name="associativity">
        /// Determines whether a ~ b ~ c is evaluated from left or right side.
        /// </param>
        /// <param name="opPlacement">
        /// Determines which side of a number operator tokens are expected
        /// to be on.
        /// </param>
        /// <param name="precedence">
        /// Operator precedence determines the order in which operators are
        /// evaluated.
        /// </param>
        /// <param name="format">
        /// The unique symbols identifying the operator.
        /// </param>
        /// <param name="operation">
        /// During evaluation, all involved numbers are passed to this
        /// function and returned.
        /// </param>
        public Operator(
            Placements opPlacement,
            Associativity associativity,
            int precedence,
            string format,
            Func<Token[], Token> operation)
        {
            Placement = opPlacement;
            Assoc = associativity;
            Prec = precedence;

            if (opPlacement == Placements.Both)
            {
                NumArgs = 2;
            }
            else
            {
                NumArgs = 1;
            }

            StrForm = format;
            Operation = operation;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Returns true if all properties of each token are the same.
        /// </summary>
        /// <param name="obj">
        /// The token to compare against for equality.
        /// </param>
        public bool Equals(Operator obj)
        {
            return (StrForm == obj.StrForm &&
                Placement == obj.Placement &&
                Assoc == obj.Assoc &&
                Prec == obj.Prec &&
                NumArgs == obj.NumArgs &&
                Operation == obj.Operation);
        }
        #endregion
    }
}