using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DynamicDraw.Parsing
{
    /// <summary>
    /// Tokenizes mathematical expressions to evaluate or symbolically
    /// manipulate them.
    /// </summary>
    public static class Parser
    {
        #region Variables and Properties
        #region Parsing Options
        /// <summary>
        /// If true, numbers next to each other will be multiplied together
        /// when no other operation is specified. True by default.
        /// </summary>
        public static bool OptUseImplicitMult;

        /// <summary>
        /// If true, parentheses groups must always be balanced. False by
        /// default.
        /// </summary>
        public static bool OptRequireRightPars;

        /// <summary>
        /// If true, tokens that aren't recognized will be added as unknown
        /// variables. True by default.
        /// </summary>
        public static bool OptIncludeUnknowns;

        /// <summary>
        /// If true, the result of all division operations is preserved as
        /// fractions.
        /// </summary>
        public static bool OptUseFractions;
        #endregion

        #region Function Tokens
        /// <summary>
        /// Returns the absolute value of the given number.
        /// </summary>
        public static readonly Function Fabs;

        /// <summary>
        /// Returns the angle of the given number in radians for cosine values.
        /// </summary>
        public static readonly Function Facos;

        /// <summary>
        /// Returns the angle of the given number in radians for sine values.
        /// </summary>
        public static readonly Function Fasin;

        /// <summary>
        /// Returns the angle of the given number in radians for tangent values.
        /// </summary>
        public static readonly Function Fatan;

        /// <summary>
        /// Returns the angle whose tangent is the quotient of the numbers.
        /// </summary>
        public static readonly Function Fatan2;

        /// <summary>
        /// Rounds up to the nearest integer.
        /// </summary>
        public static readonly Function Fceil;

        /// <summary>
        /// Rounds up to the nearest integer.
        /// </summary>
        public static readonly Function Fceiling;

        /// <summary>
        /// Returns the hyperbolic cosine of the angle in radians.
        /// </summary>
        public static readonly Function Fcosh;

        /// <summary>
        /// Rounds down to nearest integer.
        /// </summary>
        public static readonly Function Ffloor;

        /// <summary>
        /// Returns the base-e logarithm of the given number.
        /// </summary>
        public static readonly Function Flog;

        /// <summary>
        /// Returns the logarithm of the given number with the given base.
        /// </summary>
        public static readonly Function Flog2;

        /// <summary>
        /// Returns the base-e logarithm of the given number.
        /// </summary>
        public static readonly Function Fln;

        /// <summary>
        /// Returns the maximum value among given numbers.
        /// </summary>
        public static readonly Function Fmax;

        /// <summary>
        /// Returns the minimum value among given numbers.
        /// </summary>
        public static readonly Function Fmin;

        /// <summary>
        /// Returns the sign of the given number as 1 for positive and 0 for
        /// negative. The number zero is positive.
        /// </summary>
        public static readonly Function Fsign;

        /// <summary>
        /// Returns the hyperbolic sine of the angle in radians.
        /// </summary>
        public static readonly Function Fsinh;

        /// <summary>
        /// Returns the square root of the given number.
        /// </summary>
        public static readonly Function Fsqrt;

        /// <summary>
        /// Returns the hyperbolic tangent of the angle in radians.
        /// </summary>
        public static readonly Function Ftanh;

        /// <summary>
        /// The cosine function for radians.
        /// </summary>
        public static readonly Function Fcos;

        /// <summary>
        /// Rounds a single number to the nearest integer.
        /// </summary>
        public static readonly Function Frnd;

        /// <summary>
        /// Rounds a number to the nearest multiple of another.
        /// </summary>
        public static readonly Function Frnd2;

        /// <summary>
        /// The sine function for radians.
        /// </summary>
        public static readonly Function Fsin;

        /// <summary>
        /// The tangent function for radians.
        /// </summary>
        public static readonly Function Ftan;
        #endregion

        #region Identifier Tokens
        /// <summary>
        /// The mathematical constant, pi.
        /// </summary>
        public static readonly LiteralId VarPi;

        /// <summary>
        /// The mathematical constant, e.
        /// </summary>
        public static readonly LiteralId VarE;
        #endregion

        #region Operator Tokens
        /// <summary>
        /// The addition operator.
        /// </summary>
        public static readonly Operator OpAdd;

        /// <summary>
        /// The subtraction operator.
        /// </summary>
        public static readonly Operator OpSub;

        /// <summary>
        /// The multiplication operator.
        /// </summary>
        public static readonly Operator OpMlt;

        /// <summary>
        /// The division operator.
        /// </summary>
        public static readonly Operator OpDiv;

        /// <summary>
        /// The modulus operator.
        /// </summary>
        public static readonly Operator OpMod;

        /// <summary>
        /// The negation operator.
        /// </summary>
        public static readonly Operator OpNeg;

        /// <summary>
        /// The exponentiation operator.
        /// </summary>
        public static readonly Operator OpExp;

        /// <summary>
        /// The factorial operator.
        /// </summary>
        public static readonly Operator OpFac;

        /// <summary>
        /// The equality operator.
        /// </summary>
        public static readonly Operator OpEq;

        /// <summary>
        /// The inequality operator.
        /// </summary>
        public static readonly Operator OpNotEq;

        /// <summary>
        /// The greater-than operator.
        /// </summary>
        public static readonly Operator OpGt;

        /// <summary>
        /// The greater-than-or-equal operator.
        /// </summary>
        public static readonly Operator OpGte;

        /// <summary>
        /// The less-than operator.
        /// </summary>
        public static readonly Operator OpLt;

        /// <summary>
        /// The less-than-or-equal operator.
        /// </summary>
        public static readonly Operator OpLte;

        /// <summary>
        /// The logical not operator.
        /// </summary>
        public static readonly Operator OpLogNot;

        /// <summary>
        /// The logical and operator.
        /// </summary>
        public static readonly Operator OpLogAnd;

        /// <summary>
        /// The logical or operator.
        /// </summary>
        public static readonly Operator OpLogOr;
        #endregion

        #region Special Tokens
        /// <summary>
        /// Represents a left parenthesis.
        /// </summary>
        public static readonly Symbol Lpar;

        /// <summary>
        /// Represents a right parenthesis.
        /// </summary>
        public static readonly Symbol Rpar;

        /// <summary>
        /// Represents a function argument separator.
        /// </summary>
        public static readonly Symbol ArgSep;
        #endregion

        #region Token List
        /// <summary>
        /// A mutable list of all tokens to parse with.
        /// </summary>
        private static List<Token> tokens;
        #endregion
        #endregion

        #region Static Constructor
        /// <summary>
        /// Sets all default values for all statically-accessible items.
        /// </summary>
        static Parser()
        {
            //Sets parsing option defaults.
            OptUseImplicitMult = false;
            OptRequireRightPars = false;
            OptIncludeUnknowns = true;
            OptUseFractions = false;

            //Sets default operators.
            OpFac = new Operator(Placements.Left,
                Associativity.Left, 9, "!",
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum n0)
                    {
                        decimal givenVal = n0.Value;
                        decimal value = 1;

                        while (n0.Value > 1)
                        {
                            value *= givenVal--;
                        }

                        return new LiteralNum(value);
                    }

                    return null;
                }));

            OpNeg = new Operator(Placements.Right,
                Associativity.Right, 8, "-",
                new Func<Token[], Token>((num) =>
                {
                    if (num[1] is LiteralNum n1)
                    {
                        return new LiteralNum(-n1.Value);
                    }
                    else if (num[1] is Fraction frac)
                    {
                        return new Fraction(-frac.Numerator, frac.Denominator);
                    }

                    return null;
                }));

            OpExp = new Operator(Placements.Both,
                Associativity.Right, 8, "^",
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralNum n0) &&
                        (num[1] is LiteralNum n1))
                    {
                        if (n0.Value < 0 && n1.Value > 0 && n1.Value < 1)
                        {
                            throw new ParsingException("Number must be >= 0 " +
                                "to compute root.");
                        }
                        try
                        {
                            return checked(new LiteralNum((decimal)Math.Pow(
                                (double)n0.Value, (double)n1.Value)));
                        }
                        catch (OverflowException e)
                        {
                            throw new ParsingException(
                                n0.StrForm + " ^ " + n1.StrForm +
                                " is too large to compute.", e);
                        }
                    }

                    return null;
                }));

            OpMod = new Operator(Placements.Both,
                Associativity.Left, 7, "%",
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralNum n0) &&
                        (num[1] is LiteralNum n1))
                    {
                        if (n1.Value == 0)
                        {
                            throw new ParsingException("The expression " +
                                n0.StrForm + " % " + n1.StrForm +
                                " causes division by zero.");
                        }

                        return new LiteralNum(n0.Value % n1.Value);
                    }

                    return null;
                }));

            OpDiv = new Operator(Placements.Both,
                Associativity.Left, 7, "/",
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralNum n0) &&
                        (num[1] is LiteralNum n1))
                    {
                        if (OptUseFractions)
                        {
                            return new Fraction(n0.Value, n1.Value);
                        }
                        else if (n1.Value == 0)
                        {
                            throw new ParsingException("The expression " +
                                n0.StrForm + " / " + n1.StrForm +
                                " causes division by zero.");
                        }

                        return new LiteralNum(n0.Value / n1.Value);
                    }
                    else if (num[0] is Fraction && num[1] is Fraction)
                    {
                        return Fraction.Divide(
                            (Fraction)num[0],
                            (Fraction)num[1]);
                    }
                    else if (num[0] is LiteralNum && num[1] is Fraction)
                    {
                        return Fraction.Divide(
                            (LiteralNum)num[0],
                            (Fraction)num[1]);
                    }
                    else if (num[0] is Fraction && num[1] is LiteralNum)
                    {
                        return Fraction.Divide(
                            (Fraction)num[0],
                            (LiteralNum)num[1]);
                    }

                    return null;
                }));

            OpMlt = new Operator(Placements.Both,
                Associativity.Left, 7, "*",
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralNum n0) &&
                        (num[1] is LiteralNum n1))
                    {
                        return new LiteralNum(n0.Value * n1.Value);
                    }
                    else if (num[0] is Fraction && num[1] is Fraction)
                    {
                        return Fraction.Multiply(
                            (Fraction)num[0],
                            (Fraction)num[1]);
                    }
                    else if (num[0] is LiteralNum && num[1] is Fraction)
                    {
                        return Fraction.Multiply(
                            (LiteralNum)num[0],
                            (Fraction)num[1]);
                    }
                    else if (num[0] is Fraction && num[1] is LiteralNum)
                    {
                        return Fraction.Multiply(
                            (Fraction)num[0],
                            (LiteralNum)num[1]);
                    }

                    return null;
                }));

            OpSub = new Operator(Placements.Both,
                Associativity.Left, 6, "-",
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralNum n0) &&
                        (num[1] is LiteralNum n1))
                    {
                        return new LiteralNum(n0.Value - n1.Value);
                    }
                    else if (num[0] is Fraction && num[1] is Fraction)
                    {
                        return Fraction.Subtract(
                            (Fraction)num[0],
                            (Fraction)num[1]);
                    }
                    else if (num[0] is LiteralNum && num[1] is Fraction)
                    {
                        return Fraction.Subtract(
                            (LiteralNum)num[0],
                            (Fraction)num[1]);
                    }
                    else if (num[0] is Fraction && num[1] is LiteralNum)
                    {
                        return Fraction.Subtract(
                            (Fraction)num[0],
                            (LiteralNum)num[1]);
                    }

                    return null;
                }));

            OpAdd = new Operator(Placements.Both,
                Associativity.Left, 6, "+",
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralNum n0) &&
                        (num[1] is LiteralNum n1))
                    {
                        return new LiteralNum(n0.Value + n1.Value);
                    }
                    else if (num[0] is Fraction && num[1] is Fraction)
                    {
                        return Fraction.Add(
                            (Fraction)num[0],
                            (Fraction)num[1]);
                    }
                    else if (num[0] is LiteralNum && num[1] is Fraction)
                    {
                        return Fraction.Add(
                            (LiteralNum)num[0],
                            (Fraction)num[1]);
                    }
                    else if (num[0] is Fraction && num[1] is LiteralNum)
                    {
                        return Fraction.Add(
                            (Fraction)num[0],
                            (LiteralNum)num[1]);
                    }

                    return null;
                }));

            OpGt = new Operator(Placements.Both,
                Associativity.Left, 5, ">",
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralNum n0) &&
                        (num[1] is LiteralNum n1))
                    {
                        return new LiteralBool(n0.Value > n1.Value);
                    }
                    else if (num[0] is Fraction && num[1] is Fraction)
                    {
                        decimal leftVal = ((Fraction)num[0]).GetValue();
                        decimal rightVal = ((Fraction)num[1]).GetValue();
                        return new LiteralBool(leftVal > rightVal);
                    }
                    else if (num[0] is LiteralNum && num[1] is Fraction)
                    {
                        decimal leftVal = ((LiteralNum)num[0]).Value;
                        decimal rightVal = ((Fraction)num[1]).GetValue();
                        return new LiteralBool(leftVal > rightVal);
                    }
                    else if (num[0] is Fraction && num[1] is LiteralNum)
                    {
                        decimal leftVal = ((Fraction)num[0]).GetValue();
                        decimal rightVal = ((LiteralNum)num[1]).Value;
                        return new LiteralBool(leftVal > rightVal);
                    }

                    return null;
                }));

            OpGte = new Operator(Placements.Both,
                Associativity.Left, 5, ">=",
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralNum n0) &&
                        (num[1] is LiteralNum n1))
                    {
                        return new LiteralBool(n0.Value >= n1.Value);
                    }
                    else if (num[0] is Fraction && num[1] is Fraction)
                    {
                        decimal leftVal = ((Fraction)num[0]).GetValue();
                        decimal rightVal = ((Fraction)num[1]).GetValue();
                        return new LiteralBool(leftVal >= rightVal);
                    }
                    else if (num[0] is LiteralNum && num[1] is Fraction)
                    {
                        decimal leftVal = ((LiteralNum)num[0]).Value;
                        decimal rightVal = ((Fraction)num[1]).GetValue();
                        return new LiteralBool(leftVal >= rightVal);
                    }
                    else if (num[0] is Fraction && num[1] is LiteralNum)
                    {
                        decimal leftVal = ((Fraction)num[0]).GetValue();
                        decimal rightVal = ((LiteralNum)num[1]).Value;
                        return new LiteralBool(leftVal >= rightVal);
                    }

                    return null;
                }));

            OpLt = new Operator(Placements.Both,
                Associativity.Left, 5, "<",
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralNum n0) &&
                        (num[1] is LiteralNum n1))
                    {
                        return new LiteralBool(n0.Value < n1.Value);
                    }
                    else if (num[0] is Fraction && num[1] is Fraction)
                    {
                        decimal leftVal = ((Fraction)num[0]).GetValue();
                        decimal rightVal = ((Fraction)num[1]).GetValue();
                        return new LiteralBool(leftVal < rightVal);
                    }
                    else if (num[0] is LiteralNum && num[1] is Fraction)
                    {
                        decimal leftVal = ((LiteralNum)num[0]).Value;
                        decimal rightVal = ((Fraction)num[1]).GetValue();
                        return new LiteralBool(leftVal < rightVal);
                    }
                    else if (num[0] is Fraction && num[1] is LiteralNum)
                    {
                        decimal leftVal = ((Fraction)num[0]).GetValue();
                        decimal rightVal = ((LiteralNum)num[1]).Value;
                        return new LiteralBool(leftVal < rightVal);
                    }

                    return null;
                }));

            OpLte = new Operator(Placements.Both,
                Associativity.Left, 5, "<=",
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralNum n0) &&
                        (num[1] is LiteralNum n1))
                    {
                        return new LiteralBool(n0.Value <= n1.Value);
                    }
                    else if (num[0] is Fraction && num[1] is Fraction)
                    {
                        decimal leftVal = ((Fraction)num[0]).GetValue();
                        decimal rightVal = ((Fraction)num[1]).GetValue();
                        return new LiteralBool(leftVal <= rightVal);
                    }
                    else if (num[0] is LiteralNum && num[1] is Fraction)
                    {
                        decimal leftVal = ((LiteralNum)num[0]).Value;
                        decimal rightVal = ((Fraction)num[1]).GetValue();
                        return new LiteralBool(leftVal <= rightVal);
                    }
                    else if (num[0] is Fraction && num[1] is LiteralNum)
                    {
                        decimal leftVal = ((Fraction)num[0]).GetValue();
                        decimal rightVal = ((LiteralNum)num[1]).Value;
                        return new LiteralBool(leftVal <= rightVal);
                    }

                    return null;
                }));

            OpEq = new Operator(Placements.Both,
                Associativity.Left, 4, "=",
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralNum n0) &&
                        (num[1] is LiteralNum n1))
                    {
                        return new LiteralBool(n0.Value == n1.Value);
                    }
                    else if ((num[0] is LiteralBool n0bool) &&
                        (num[1] is LiteralBool n1bool))
                    {
                        return new LiteralBool(n0bool.Value == n1bool.Value);
                    }
                    else if (num[0] is Fraction && num[1] is Fraction)
                    {
                        decimal leftVal = ((Fraction)num[0]).GetValue();
                        decimal rightVal = ((Fraction)num[1]).GetValue();
                        return new LiteralBool(leftVal == rightVal);
                    }
                    else if (num[0] is LiteralNum && num[1] is Fraction)
                    {
                        decimal leftVal = ((LiteralNum)num[0]).Value;
                        decimal rightVal = ((Fraction)num[1]).GetValue();
                        return new LiteralBool(leftVal == rightVal);
                    }
                    else if (num[0] is Fraction && num[1] is LiteralNum)
                    {
                        decimal leftVal = ((Fraction)num[0]).GetValue();
                        decimal rightVal = ((LiteralNum)num[1]).Value;
                        return new LiteralBool(leftVal == rightVal);
                    }

                    return null;
                }));

            OpNotEq = new Operator(Placements.Both,
                Associativity.Left, 4, "!=",
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralNum n0) &&
                        (num[1] is LiteralNum n1))
                    {
                        return new LiteralBool(n0.Value != n1.Value);
                    }
                    else if ((num[0] is LiteralBool n0bool) &&
                        (num[1] is LiteralBool n1bool))
                    {
                        return new LiteralBool(n0bool.Value != n1bool.Value);
                    }
                    else if (num[0] is Fraction && num[1] is Fraction)
                    {
                        decimal leftVal = ((Fraction)num[0]).GetValue();
                        decimal rightVal = ((Fraction)num[1]).GetValue();
                        return new LiteralBool(leftVal != rightVal);
                    }
                    else if (num[0] is LiteralNum && num[1] is Fraction)
                    {
                        decimal leftVal = ((LiteralNum)num[0]).Value;
                        decimal rightVal = ((Fraction)num[1]).GetValue();
                        return new LiteralBool(leftVal != rightVal);
                    }
                    else if (num[0] is Fraction && num[1] is LiteralNum)
                    {
                        decimal leftVal = ((Fraction)num[0]).GetValue();
                        decimal rightVal = ((LiteralNum)num[1]).Value;
                        return new LiteralBool(leftVal != rightVal);
                    }

                    return null;
                }));

            OpLogNot = new Operator(Placements.Right,
                Associativity.Left, 3, "!",
                new Func<Token[], Token>((num) =>
                {
                    if (num[1] is LiteralBool n1)
                    {
                        return new LiteralBool(!n1.Value);
                    }

                    return null;
                }));

            OpLogOr = new Operator(Placements.Both,
                Associativity.Left, 2, "|",
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralBool n0) &&
                        (num[1] is LiteralBool n1))
                    {
                        return new LiteralBool(n0.Value || n1.Value);
                    }

                    return null;
                }));

            OpLogAnd = new Operator(Placements.Both,
                Associativity.Left, 1, "&",
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralBool n0) &&
                        (num[1] is LiteralBool n1))
                    {
                        return new LiteralBool(n0.Value && n1.Value);
                    }

                    return null;
                }));

            //Sets default functions.
            Fabs = new Function("abs", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        return new LiteralNum(Math.Abs(n0.Value));
                    }
                    else if (num[0] is Fraction)
                    {
                        Fraction n0 = (Fraction)num[0];
                        return new Fraction(
                            Math.Abs(n0.Numerator),
                            Math.Abs(n0.Denominator));
                    }

                    return null;
                }));

            Facos = new Function("acos", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        if (n0.Value < -1 || n0.Value > 1)
                        {
                            throw new ParsingException(n0.Value + " does not " +
                                "fit in the domain of arccosine.");
                        }
                        else
                        {
                            return new LiteralNum(
                                (decimal)Math.Acos((double)n0.Value));
                        }
                    }

                    return null;
                }));

            Fasin = new Function("asin", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        if (n0.Value < -1 || n0.Value > 1)
                        {
                            throw new ParsingException(n0.Value + " does not " +
                                "fit in the domain of arcsine.");
                        }
                        else
                        {
                            return new LiteralNum(
                                (decimal)Math.Asin((double)n0.Value));
                        }
                    }

                    return null;
                }));

            Fatan = new Function("atan", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        return new LiteralNum(
                            (decimal)Math.Atan((double)n0.Value));
                    }

                    return null;
                }));

            Fatan2 = new Function("atan", 2,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum &&
                        num[1] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        LiteralNum n1 = (LiteralNum)num[1];
                        return new LiteralNum(
                            (decimal)Math.Atan2(
                                (double)n0.Value,
                                (double)n1.Value));
                    }

                    return null;
                }));

            Fceil = new Function("ceil", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        return new LiteralNum(Math.Ceiling(n0.Value));
                    }

                    return null;
                }));

            Fceiling = new Function("ceiling", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        return new LiteralNum(Math.Ceiling(n0.Value));
                    }

                    return null;
                }));

            Fcos = new Function("cos", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        return new LiteralNum(
                            (decimal)Math.Cos((double)n0.Value));
                    }

                    return null;
                }));

            Fcosh = new Function("cosh", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        return new LiteralNum(
                            (decimal)Math.Cosh((double)n0.Value));
                    }

                    return null;
                }));

            Ffloor = new Function("floor", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        return new LiteralNum(Math.Floor(n0.Value));
                    }

                    return null;
                }));

            Flog = new Function("log", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        return new LiteralNum((decimal)Math.Log((double)n0.Value));
                    }

                    return null;
                }));

            Flog2 = new Function("log", 2,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum &&
                        num[1] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        LiteralNum n1 = (LiteralNum)num[1];
                        return new LiteralNum((decimal)
                            Math.Log((double)n0.Value, (double)n1.Value));
                    }

                    return null;
                }));

            Fln = new Function("ln", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        return new LiteralNum((decimal)Math.Log((double)n0.Value));
                    }

                    return null;
                }));

            Fmax = new Function("max", 2,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum &&
                        num[1] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        LiteralNum n1 = (LiteralNum)num[1];
                        return new LiteralNum(Math.Max(n0.Value, n1.Value));
                    }

                    return null;
                }));

            Fmin = new Function("min", 2,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum &&
                        num[1] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        LiteralNum n1 = (LiteralNum)num[1];
                        return new LiteralNum(Math.Min(n0.Value, n1.Value));
                    }

                    return null;
                }));

            Fsign = new Function("sign", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        if (n0.Value >= 0)
                        {
                            return new LiteralNum(1);
                        }

                        return new LiteralNum(0);
                    }

                    return null;
                }));

            Fsin = new Function("sin", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        return new LiteralNum(
                            (decimal)Math.Sin((double)n0.Value));
                    }

                    return null;
                }));

            Fsinh = new Function("sinh", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        return new LiteralNum(
                            (decimal)Math.Sinh((double)n0.Value));
                    }

                    return null;
                }));

            Fsqrt = new Function("sqrt", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        if (n0.Value < 0)
                        {
                            throw new ParsingException("Values must be >= 0.");
                        }

                        return new LiteralNum(
                            (decimal)Math.Sqrt((double)n0.Value));
                    }

                    return null;
                }));

            Frnd = new Function("round", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        return new LiteralNum(Math.Round(n0.Value));
                    }
                    else if (num[0] is Fraction)
                    {
                        Fraction n0 = (Fraction)num[0];
                        return new LiteralNum(Math.Round(n0.GetValue()));
                    }

                    return null;
                }));

            Frnd2 = new Function("round", 2,
                new Func<Token[], Token>((num) =>
                {
                    if ((num[0] is LiteralNum) &&
                        (num[1] is LiteralNum))
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        LiteralNum n1 = (LiteralNum)num[1];
                        return new LiteralNum(
                            Math.Round(n0.Value / n1.Value) * n1.Value);
                    }

                    return null;
                }));

            Ftan = new Function("tan", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        return new LiteralNum(
                            (decimal)Math.Tan((double)n0.Value));
                    }

                    return null;
                }));

            Ftanh = new Function("tanh", 1,
                new Func<Token[], Token>((num) =>
                {
                    if (num[0] is LiteralNum)
                    {
                        LiteralNum n0 = (LiteralNum)num[0];
                        return new LiteralNum(
                            (decimal)Math.Tanh((double)n0.Value));
                    }

                    return null;
                }));

            //Sets default identifiers.
            VarPi = new LiteralId("pi", (decimal)Math.PI);
            VarE = new LiteralId("e", (decimal)Math.E);

            //Sets default symbols
            Lpar = new Symbol("(");
            Rpar = new Symbol(")");
            ArgSep = new Symbol(",");

            //Sets the token list.
            //Omitted: OpFac
            tokens = new List<Token>()
            {
                OpExp, OpNeg, OpMod, OpDiv, OpMlt, OpSub, OpAdd, OpLogNot,
                OpLogOr, OpLogAnd, OpEq, OpGt, OpGte, OpLt, OpLte, OpNotEq,

                Fabs, Facos, Fasin, Fatan, Fatan2, Fceil, Fceiling, Fcos,
                Fcosh, Ffloor, Flog, Flog2, Fln, Fmax, Fmin, Frnd, Frnd2,
                Fsign, Fsin, Fsinh, Fsqrt, Ftan, Ftanh,

                VarPi, VarE,

                Lpar, Rpar, ArgSep
            };

            //Sorts tokens in reverse lexicographic order to support deferring.
            tokens = tokens.OrderByDescending(o => o.StrForm).ToList();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Adds a string-lowercased copy of the function.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a null token is given.
        /// </exception>
        public static void AddFunction(Function token)
        {
            if (token == null)
            {
                throw new ArgumentNullException("token");
            }

            tokens.Add(new Function(
                token.StrForm.ToLower(),
                token.NumArgs,
                token.Operation));

            //Sorts tokens in reverse lexicographic order to support deferring.
            tokens = tokens.OrderByDescending(o => o.StrForm).ToList();
        }

        /// <summary>
        /// Adds a string-lowercased copy of the identifier.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a null token is given.
        /// </exception>
        public static void AddIdentifier(LiteralId token)
        {
            if (token == null)
            {
                throw new ArgumentNullException("token");
            }

            tokens.Add(new LiteralId(
                token.StrForm.ToLower(),
                token.Value));

            //Sorts tokens in reverse lexicographic order to support deferring.
            tokens = tokens.OrderByDescending(o => o.StrForm).ToList();
        }

        /// <summary>
        /// Adds a string-lowercased copy of the operator.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a null token is given.
        /// </exception>
        public static void AddOperator(Operator token)
        {
            if (token == null)
            {
                throw new ArgumentNullException("token");
            }

            tokens.Add(new Operator(
                token.Placement,
                token.Assoc,
                token.Prec,
                token.StrForm.ToLower(),
                token.Operation));

            //Sorts tokens in reverse lexicographic order to support deferring.
            tokens = tokens.OrderByDescending(o => o.StrForm).ToList();
        }

        ///<summary>
        /// Parses an expression with operators, functions, and identifiers.
        /// </summary>
        /// <param name="expression">
        /// An expression composed of numbers and included operators,
        /// and functions. No implicit multiplication.
        /// </param>
        public static string Eval(string expression)
        {
            List<Token> tokensList = Tokenize(expression);
            var functions = tokens.OfType<Function>().ToList();

            //Solves each parenthesis group from deepest depth outward.
            while (true)
            {
                //Finds the end of the nearest complete sub-expression.
                int rbrPos = tokensList.IndexOf(Rpar) + 1;
                int subExpressionEnd = (rbrPos >= 1) ? rbrPos : (tokensList.Count);

                //Finds the start of the nearest complete sub-expression.
                int lbrPos = tokensList.GetRange(0, subExpressionEnd).LastIndexOf(Lpar);
                int subExpressionBegin = (lbrPos >= 0) ? lbrPos : 0;

                //Isolates the sub-expression.
                List<Token> expressionLHS = tokensList.GetRange(0, subExpressionBegin);
                List<Token> expressionRHS = tokensList.GetRange(subExpressionEnd, tokensList.Count - subExpressionEnd);
                List<Token> subExpression = tokensList.GetRange(subExpressionBegin, subExpressionEnd - subExpressionBegin);

                //Includes functions and picks a proper overload.
                Function subExpressionFunc = null;
                if (expressionLHS.LastOrDefault() is Function tokFunc)
                {
                    expressionLHS.RemoveAt(expressionLHS.Count - 1);
                    int numArgs = subExpression.Count((tok) => tok == ArgSep) + 1;
                    subExpressionFunc = functions.FirstOrDefault((f) =>
                        f.NumArgs == numArgs &&
                        f.StrForm == tokFunc.StrForm);
                }

                //Evaluates sub-expressions.
                tokensList = new List<Token>(expressionLHS);
                tokensList.AddRange(EvalNoPar(subExpression, subExpressionFunc));
                tokensList.AddRange(expressionRHS);

                //Returns when everything has been parsed.
                if (expressionLHS.Count == 0 && expressionRHS.Count == 0)
                {
                    string result = "";
                    for (int i = 0; i < tokensList.Count; i++)
                    {
                        result += tokensList[i].StrForm;
                    }
                    return result;
                }
            }
        }

        /// <summary>
        /// Returns an immutable list of all tokens in use.
        /// </summary>
        public static ReadOnlyCollection<Token> GetTokens()
        {
            return new ReadOnlyCollection<Token>(tokens);
        }

        /// <summary>
        /// Removes the first match for the given token from the list of
        /// tokens, if it exists. Returns true if found; false otherwise.
        /// </summary>
        public static bool RemoveToken(Token token)
        {
            if (token == null)
            {
                return false;
            }

            //Removes the first match, if any.
            for (int i = tokens.Count; i > 0; i--)
            {
                if (token.Equals(tokens[i]))
                {
                    tokens.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Converts the given string to tokens.
        /// </summary>
        /// <param name="expression">
        /// An expression with operators, functions, and identifiers.
        /// </param>
        public static List<Token> Tokenize(string expression)
        {
            List<Token> tokensList = new List<Token>();
            string token = "";

            //Catches null or whitespace strings.
            if (String.IsNullOrWhiteSpace(expression))
            {
                throw new ParsingException("No expression provided.");
            }

            //Lowercases and removes whitespaces.
            expression = Regex.Replace(expression, @"\s", "").ToLower();

            //Builds a token list.
            Token longestMatch = null;
            Token shortestMatch = null;
            Token candidateBeforeDefer = null;
            for (int i = 0; i < expression.Length; i++)
            {
                token += expression[i];

                //Matches longer tokens and tokens of the same length.
                longestMatch = tokens.FirstOrDefault(
                    (tok) => tok.StrForm.StartsWith(token));

                //Defers when the token is longer.
                if (i != expression.Length - 1 &&
                    longestMatch?.StrForm.Length > token.Length)
                {
                    shortestMatch = tokens.FirstOrDefault(
                        (tok) => tok.StrForm == token);

                    //Stores valid matches as token matching is deferred.
                    if (shortestMatch?.StrForm == token)
                    {
                        candidateBeforeDefer = shortestMatch;
                    }
                }

                //Matches when there are no longer candidates.
                else if (longestMatch != null)
                {
                    //Adds the token, parsing identifiers.
                    if (longestMatch is LiteralId tokenAsId &&
                        tokenAsId.Value != null)
                    {
                        tokensList.Add(new LiteralNum(tokenAsId.Value ?? 0));
                    }
                    else
                    {
                        tokensList.Add(longestMatch);
                    }

                    token = "";
                    candidateBeforeDefer = null;
                }

                else
                {
                    //Backtracks to the last valid token.
                    if (candidateBeforeDefer != null)
                    {
                        i -= (token.Length - candidateBeforeDefer.StrForm.Length);

                        //Adds the token, parsing identifiers.
                        if (candidateBeforeDefer is LiteralId tokenAsId &&
                        tokenAsId.Value != null)
                        {
                            tokensList.Add(new LiteralNum(tokenAsId.Value ?? 0));
                        }
                        else
                        {
                            tokensList.Add(candidateBeforeDefer);
                        }

                        token = "";
                        candidateBeforeDefer = null;
                    }

                    //Matches literals.
                    else if (Decimal.TryParse(token,
                        NumberStyles.AllowDecimalPoint, null, out decimal val))
                    {
                        //Adds the numeric token at end of string or boundary.
                        if (i == expression.Length - 1 ||
                            !Decimal.TryParse(token + expression[i + 1],
                            NumberStyles.AllowDecimalPoint, null, out decimal val2))
                        {
                            tokensList.Add(new LiteralNum(val));
                            token = "";
                        }
                    }

                    //Matches unknowns by-character if allowed.
                    else if (OptIncludeUnknowns)
                    {
                        tokensList.Add(new LiteralId(token[0].ToString(), null));
                        i -= token.Length - 1;
                        token = "";
                    }
                    else
                    {
                        throw new ParsingException("Error: token '" +
                            token + "' is not a recognized symbol.");
                    }
                }
            }

            return tokensList;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Parses a non-relational expression without parentheses with an
        /// optional argument to treat the expression as function arguments.
        /// </summary>
        /// <param name="subExpression">
        /// A mathematical expression without parentheses.
        /// </param>
        /// <param name="functionExpression">
        /// A function to apply using the sub-expression provided.
        /// </param>
        /// <exception cref="ParsingException">
        /// A parsing exception is thrown when an empty expression is
        /// provided or the expression is malformed.
        /// </exception>
        private static List<Token> EvalNoPar(
            List<Token> subExpression,
            Function function)
        {
            var identifiers = tokens.OfType<LiteralId>().ToList();
            var operators = tokens.OfType<Operator>().ToList();
            List<Token> result = new List<Token>();

            //Creates a string representation of the token list for errors.
            string subExpressionStr = "";
            for (int i = 0; i < subExpression.Count; i++)
            {
                subExpressionStr += subExpression[i].StrForm;
            }

            //Strips () and catches empty expressions.
            if (OptRequireRightPars &&
                subExpression.FirstOrDefault() == Lpar &&
                subExpression.LastOrDefault() != Rpar)
            {
                throw new ParsingException("The expression '" +
                    subExpressionStr + "' is missing a right parenthesis " +
                    "at the end.");
            }

            subExpression.RemoveAll((tok) => tok == Lpar || tok == Rpar);

            if (subExpression.Count == 0)
            {
                throw new ParsingException("An empty parenthesis group was " +
                    "provided; there is nothing to process within it.");
            }

            //Parses each argument separately, then applies the function.
            if (function != null)
            {
                List<List<Token>> args = subExpression.Split(ArgSep);
                Token[] argVals = new Token[args.Count];

                //Catches overloads with the wrong number of arguments.
                if (function.NumArgs != args.Count)
                {
                    throw new ParsingException("In expression '" +
                        subExpressionStr + "', the number of arguments for " +
                        function.StrForm + " should be " + function.NumArgs +
                        ", but " + args.Count + " arguments were given.");
                }

                //Simplifies each argument.
                for (int i = 0; i < args.Count; i++)
                {
                    List<Token> subResult = EvalNoPar(args[i], null);
                    if (subResult.FirstOrDefault() is LiteralNum)
                    {
                        argVals[i] = subResult[0];
                    }
                    else
                    {
                        throw new ParsingException("In expression '" +
                            subExpressionStr + "', arguments are not all " +
                            "decimal type.");
                    }
                }

                //Applies functions.
                result.Add(function.Operation(argVals));
                if (result[0] == null)
                {
                    throw new ParsingException("In expression '" +
                        subExpressionStr + "', arguments do not match " +
                        "parameter types used.");
                }
                else
                {
                    return result;
                }
            }

            //Minuses are binary by default; determines which ones are unary.
            //If the first token is a minus, it's a negation.
            if (subExpression[0] == OpSub)
            {
                subExpression[0] = OpNeg;
            }

            //Performs left-to-right modifications on the token list.
            for (int i = 1; i < subExpression.Count; i++)
            {
                //A minus after a binary operator or negation is a negation.
                if (subExpression[i] == OpSub &&
                    (subExpression[i - 1] is Operator &&
                    ((subExpression[i - 1] as Operator).NumArgs > 1 ||
                    subExpression[i - 1] == OpNeg)) ||
                    (subExpression[i - 1] is Function))
                {
                    subExpression[i] = OpNeg;
                }

                //Performs implicit multiplication.
                if (subExpression[i - 1] is LiteralNum &&
                    subExpression[i] is LiteralNum &&
                    OptUseImplicitMult)
                {
                    subExpression.Insert(i, OpMlt);
                }
            }

            //Gets max precedence within sub-expression.
            var opTokens = subExpression.OfType<Operator>();
            int maxPrecedence = (opTokens.Count() > 0)
                ? opTokens.Max(o => o.Prec) : 0;

            //Computes all operators with equal precedence.
            while (maxPrecedence > 0)
            {
                bool isRightAssociative =
                    (operators.Any(o => maxPrecedence == o.Prec &&
                    o.Assoc == Associativity.Right));

                //Iterates through each token forwards or backwards.
                int j = (isRightAssociative) ? subExpression.Count - 1 : 0;
                while ((isRightAssociative && j >= 0) ||
                    (!isRightAssociative && j < subExpression.Count))
                {
                    if (subExpression[j] is Operator &&
                        (subExpression[j] as Operator).Prec == maxPrecedence)
                    {
                        Operator opToken = (Operator)subExpression[j];
                        List<Token> argVals = new List<Token>();
                        argVals.Add(subExpression.ElementAtOrDefault(j - 1));
                        argVals.Add(subExpression.ElementAtOrDefault(j + 1));

                        Token opResult = null;

                        //Handles missing arguments.
                        if (argVals[0] == null &&
                            (opToken.Placement == Placements.Both ||
                            opToken.Placement == Placements.Left))
                        {
                            throw new ParsingException("In '" + subExpressionStr +
                                "', the '" + subExpression[j].StrForm + "' operator " +
                                "is missing a lefthand operand.");
                        }
                        else if (argVals[1] == null &&
                            (opToken.Placement == Placements.Both ||
                            opToken.Placement == Placements.Right))
                        {
                            throw new ParsingException("In '" + subExpressionStr +
                                "', the '" + subExpression[j].StrForm + "' operator " +
                                "is missing a righthand operand.");
                        }

                        //Applies each operator.
                        opResult = opToken.Operation(argVals.ToArray());

                        //Removes affected tokens and inserts new value.
                        if (opResult == null)
                        {
                            throw new ParsingException("In expression '" +
                                subExpressionStr + "', operand type(s) do " +
                                "not match operator.");
                        }
                        else
                        {
                            subExpression[j] = opResult;
                        }

                        if (opToken.Placement ==
                            Placements.Left)
                        {
                            subExpression.RemoveAt(j - 1);
                            j += (isRightAssociative) ? 0 : -1;
                        }
                        else if (opToken.Placement ==
                            Placements.Right)
                        {
                            subExpression.RemoveAt(j + 1);
                            j += (isRightAssociative) ? 1 : 0;
                        }
                        else if (opToken.Placement ==
                            Placements.Both)
                        {
                            subExpression.RemoveAt(j + 1);
                            subExpression.RemoveAt(j - 1);
                            j += (isRightAssociative) ? 0 : -1;
                        }
                    }

                    //Moves to next token to evaluate.
                    if (isRightAssociative)
                    {
                        j--;
                    }
                    else
                    {
                        j++;
                    }
                }

                //Gets new precedence within sub-expression.
                opTokens = subExpression.OfType<Operator>();
                maxPrecedence = (opTokens.Count() > 0)
                    ? opTokens.Max(o => o.Prec) : 0;
            }

            //Returns the final value.
            result.AddRange(subExpression);
            return result;
        }

        /// <summary>
        /// Returns all consecutive items between each matched delimiter item.
        /// For example, a list containing [0, 2, 1, 3, 1] delimited by 1 will
        /// return the lists [0, 2][3].
        /// </summary>
        /// <param name="delimiter">
        /// The item marking the boundaries between sub-lists. Matched
        /// delimiter items will not be included in the resulting lists.
        /// </param>
        /// <returns></returns>
        private static List<List<T>> Split<T>(this List<T> list, T delimiter)
        {
            List<List<T>> lists = new List<List<T>>();
            List<T> currentList = new List<T>();

            //Stores the running list and creates another for each delimiter.
            for (int i = 0; i < list.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(list[i], delimiter))
                {
                    lists.Add(new List<T>(currentList));
                    currentList = new List<T>();
                }
                else
                {
                    currentList.Add(list[i]);
                }
            }
            if (currentList.Count > 0)
            {
                lists.Add(currentList);
            }

            return lists;
        }
        #endregion
    }
}