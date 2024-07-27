using System;

namespace DynamicDraw.Parsing
{
    /// <summary>
    /// Represents an exception involving parsing mathematical expressions.
    /// </summary>
    public class ParsingException : Exception
    {
        /// <summary>
        /// Creates an empty exception with no message.
        /// </summary>
        public ParsingException()
        {
        }

        /// <summary>
        /// Creates a parsing exception with the given message.
        /// </summary>
        /// <param name="message">
        /// The associated error message.
        /// </param>
        public ParsingException(string message) : base(message)
        {
        }

        /// <summary>
        /// Creates a parsing exception with the given message and inner exception.
        /// </summary>
        /// <param name="message">
        /// The associated error message.
        /// </param>
        /// <param name="innerException">
        /// An exception inside this exception.
        /// </param>
        public ParsingException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
