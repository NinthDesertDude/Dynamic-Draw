using System;

namespace DynamicDraw.Parsing
{
    /// <summary>
    /// Defines the name and action of a function that manipulates tokens and
    /// returns one.
    /// </summary>
    public class Function : Token
    {
        #region Properties
        /// <summary>
        /// The number of arguments to be used.
        /// </summary>
        public int NumArgs { get; protected set; }

        /// <summary>
        /// When this function is used, the input numbers can be accessed as
        /// an array of objects. As many as provided by the number of
        /// arguments may be used.
        /// </summary>
        public Func<Token[], Token> Operation
        {
            get;
            set;
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a function.
        /// </summary>
        /// <param name="name">
        /// The unique name identifying the function.
        /// </param>
        /// <param name="numberOfArgs">
        /// The number of arguments the function takes.
        /// </param>
        /// <param name="operation">
        /// During evaluation, all involved numbers are passed to this
        /// function and returned.
        /// </param>
        public Function(string name,
            int numberOfArgs,
            Func<Token[], Token> operation)
        {
            NumArgs = numberOfArgs;
            StrForm = name;
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
        public bool Equals(Function obj)
        {
            return (StrForm == obj.StrForm &&
                NumArgs == obj.NumArgs &&
                Operation == obj.Operation);
        }
        #endregion
    }
}