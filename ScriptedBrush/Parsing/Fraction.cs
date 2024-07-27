using System;

namespace DynamicDraw.Parsing
{
    /// <summary>
    /// An immutable fraction of known values.
    /// </summary>
    public class Fraction : Token
    {
        #region Properties
        /// <summary>
        /// The numerator (number atop the fraction).
        /// </summary>
        public decimal Numerator { get; protected set; }

        /// The denominator (number below the fraction).
        /// </summary>
        public decimal Denominator { get; protected set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a fraction with known values.
        /// </summary>
        /// <param name="num">
        /// The numerator (number above fraction line).
        /// </param>
        /// <param name="denom">
        /// The denominator (number below fraction line).
        /// </param>
        public Fraction(decimal num, decimal denom)
        {
            //Cannot simplify with an invalid fraction.
            if (denom != 0)
            {
                //Moves negatives to numerator or cancels out.
                if (denom < 0)
                {
                    num *= -1;
                    denom *= -1;
                }

                //Multiplies numerator and denominator by 10^x, where x is the
                //longest number of digits after the decimal in either of them,
                //forcing both to be integers.
                decimal multiplier = (decimal)Math.Pow(10, Math.Max(
                    (num - (int)num).ToString().Trim('0').Length,
                    (denom - (int)denom).ToString().Trim('0').Length));
                num *= multiplier;
                denom *= multiplier;

                //Finds the greatest common divisor.
                decimal gcd = 1;

                decimal num1 = Math.Abs(num);
                decimal num2 = Math.Abs(denom);

                while (num2 != 0)
                {
                    num1 %= num2;

                    if (num1 == 0)
                    {
                        gcd = num2;
                        break;
                    }

                    num2 %= num1;
                }
                if (num2 == 0)
                {
                    gcd = num1;
                }

                //Applies the GCD.
                num /= gcd;
                denom /= gcd;
            }

            Numerator = num;
            Denominator = denom;
            if (Denominator != 1)
            {
                StrForm = "(" + Numerator + " / " + Denominator + ")";
            }
            else
            {
                StrForm = Numerator.ToString();
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Adds a fraction to a number.
        /// </summary>
        public static Fraction Add(LiteralNum num, Fraction frac)
        {
            return Add(new Fraction(num.Value, 1), frac);
        }

        /// <summary>
        /// Adds a number to a fraction.
        /// </summary>
        public static Fraction Add(Fraction frac, LiteralNum num)
        {
            return new Fraction(frac.Numerator +
                frac.Denominator * num.Value,
                frac.Denominator).Simplify();
        }

        /// <summary>
        /// Adds a number to a fraction.
        /// </summary>
        public static Fraction Add(Fraction frac, Fraction frac2)
        {
            return new Fraction(
                frac.Numerator * frac2.Denominator +
                frac2.Numerator * frac.Denominator,
                frac.Denominator * frac2.Denominator).Simplify();
        }

        /// <summary>
        /// Divides a number by a fraction.
        /// </summary>
        public static Fraction Divide(LiteralNum num, Fraction frac)
        {
            return Divide(new Fraction(num.Value, 1), frac);
        }

        /// <summary>
        /// Divides a fraction by a number.
        /// </summary>
        public static Fraction Divide(Fraction frac, LiteralNum num)
        {
            //Negatives are only placed in the numerator.
            if (num.Value < 0)
            {
                return new Fraction(
                    -frac.Numerator,
                    frac.Denominator * -num.Value).Simplify();
            }

            return new Fraction(frac.Numerator, frac.Denominator * num.Value)
                .Simplify();
        }

        /// <summary>
        /// Divides one fraction from another by multiplying by the reciprocal.
        /// </summary>
        public static Fraction Divide(Fraction frac, Fraction frac2)
        {
            return Multiply(frac, new Fraction(frac2.Denominator, frac2.Numerator));
        }

        /// <summary>
        /// Multiplies a number by a fraction.
        /// </summary>
        public static Fraction Multiply(LiteralNum num, Fraction frac)
        {
            return Multiply(new Fraction(num.Value, 1), frac);
        }

        /// <summary>
        /// Multiplies a fraction by a number.
        /// </summary>
        public static Fraction Multiply(Fraction frac, LiteralNum num)
        {
            return new Fraction(frac.Numerator * num.Value, frac.Denominator)
                .Simplify();
        }

        /// <summary>
        /// Multiplies one fraction by another.
        /// </summary>
        public static Fraction Multiply(Fraction frac, Fraction frac2)
        {
            return new Fraction(
                frac.Numerator * frac2.Numerator,
                frac.Denominator * frac2.Denominator).Simplify();
        }

        /// <summary>
        /// Reduces the fraction to an irreducible value.
        /// </summary>
        private Fraction Simplify()
        {
            decimal Num = Numerator;
            decimal Denom = Denominator;

            //Cannot simplify with an invalid fraction.
            if (Denom == 0)
            {
                return new Fraction(Num, Denom);
            }

            //Moves negatives to numerator or cancels out.
            else if (Denom < 0)
            {
                Num *= -1;
                Denom *= -1;
            }

            //Multiplies numerator and denominator by 10^x, where x is the
            //longest number of digits after the decimal in either of them,
            //forcing both to be integers.
            decimal multiplier = (decimal)Math.Pow(10, Math.Max(
                (Num - (int)Num).ToString().Length,
                (Denom - (int)Denom).ToString().Length));
            Num *= multiplier;
            Denom *= multiplier;

            //Finds the greatest common divisor.
            decimal gcd = -1;

            int num1 = (int)Math.Abs(Num);
            int num2 = (int)Math.Abs(Denom);

            while (num2 != 0)
            {
                num1 %= num2;

                if (num1 == 0)
                {
                    gcd = num2;
                    break;
                }

                num2 %= num1;
            }

            //Applies the GCD.
            Num /= gcd;
            Denom /= gcd;
            return new Fraction(Num, Denom);
        }

        /// <summary>
        /// Subtracts a fraction from a fraction.
        /// </summary>
        public static Fraction Subtract(LiteralNum num, Fraction frac)
        {
            return Subtract(new Fraction(num.Value, 1), frac);
        }

        /// <summary>
        /// Subtracts a number from a fraction.
        /// </summary>
        public static Fraction Subtract(Fraction frac, LiteralNum num)
        {
            return Add(frac, new LiteralNum(-num.Value));
        }

        /// <summary>
        /// Subtracts one fraction from another.
        /// </summary>
        public static Fraction Subtract(Fraction frac, Fraction frac2)
        {
            return Add(frac, new Fraction(-frac2.Numerator, frac2.Denominator));
        }

        /// <summary>
        /// Returns true if all properties of each token are the same.
        /// </summary>
        /// <param name="obj">
        /// The token to compare against for equality.
        /// </param>
        public bool Equals(Fraction obj)
        {
            if (obj == null)
            {
                return false;
            }

            return (StrForm == obj.StrForm &&
                Numerator == obj.Numerator &&
                Denominator == obj.Denominator);
        }

        /// <summary>
        /// Returns the decimal value of the fraction.
        /// </summary>
        public decimal GetValue()
        {
            if (Denominator == 0)
            {
                throw new ParsingException("Attempted to divide by zero.");
            }

            return Numerator / Denominator;
        }
        #endregion
    }
}