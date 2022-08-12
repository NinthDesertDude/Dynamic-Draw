using DynamicDraw.Gui.Constraints;
using System;
using System.Drawing;
using System.Text.Json.Serialization;

namespace DynamicDraw
{
    /// <summary>
    /// A dynamic constraint. Includes the affected property, the 
    /// </summary>
    public class Constraint
    {
        #region Private variables
        [JsonIgnore]
        private int strengthCurveResolution = 1000;

        [JsonIgnore]
        private PointF[] strengthCurveControlPoints = null;
        #endregion

        #region Members
        /// <summary>
        /// The property that the constraint definition concerns.
        /// </summary>
        public ShortcutTarget Property { get; set; }

        /// <summary>
        /// Determines how a constraint runs, e.g. on brush stroke, jitter during stroke, etc.
        /// </summary>
        public ConstraintTrigger Trigger { get; set; }

        /// <summary>
        /// The points on the graph that are used to generate the curve table / decide the curve strength.
        /// </summary>
        public PointF[] StrengthCurveControlPoints
        {
            get
            {
                return strengthCurveControlPoints;
            }
            set
            {
                CurveTable = Array.Empty<float>();
                strengthCurveControlPoints = value;
            }
        }

        /// <summary>
        /// The amount of points in the curve table, hence "resolution" because a low number will
        /// produce very blocky graphs. Ideally it should match the domain of the property to avoid
        /// the slight overhead of lerping between points on the graph.
        /// </summary>
        public int StrengthCurveResolution
        {
            get
            {
                return strengthCurveResolution;
            }
            set
            {
                if (strengthCurveResolution != value)
                {
                    CurveTable = Array.Empty<float>();
                    strengthCurveResolution = value;
                }
            }
        }

        /// <summary>
        /// A direct access table of curve-modified values taking X as the key and giving Y, where
        /// X is a value from 0 inclusive to the curve resolution, exclusive. The table is only
        /// used for 2+ point graphs and not used for the perfect linear 2-pt graph.
        /// 
        /// Lazy constructed when needed or at request.
        /// </summary>
        [JsonIgnore]
        public float[] CurveTable { get; set; } = Array.Empty<float>();

        /// <summary>
        /// The value handling method.
        /// </summary>
        public ConstraintValueHandlingMethod ValueType { get; set; } = ConstraintValueHandlingMethod.DoNothing;

        public ConstraintValueSource ValueSource { get; set; } = ConstraintValueSource.None;

        /// <summary>
        /// For properties that require a float.
        /// </summary>
        public float ValFloat1 { get; set; } = 0f;

        /// <summary>
        /// For properties that requires two float values.
        /// </summary>
        public float ValFloat2 { get; set; } = 0f;
        #endregion

        #region Methods
        /// <summary>
        /// Recreates the curve table, which is lazy-loaded by default to conserve memory. Setting the control points
        /// array/changing resolution clears the table; it's useful to create the curve table ahead of time to avoid
        /// any slight pause that might occur for realtime tasks, e.g. user drawing. See <see cref="CurveTable"/>.
        /// </summary>
        public void CreateCurveTable()
        {
            CurveTable = CurveGraph.GetCurveTable(StrengthCurveControlPoints, StrengthCurveResolution);
        }

        /// <summary>
        /// Returns the curve-modified value.
        /// </summary>
        /// <param name="input">A value from 0 to 1.</param>
        public float GetCurvedValue(float input)
        {
            // Regenerate curve table as needed.
            if (CurveTable.Length == 0)
            {
                CurveTable = CurveGraph.GetCurveTable(StrengthCurveControlPoints, StrengthCurveResolution);
            }

            return CurveGraph.GetCurvedValue(StrengthCurveControlPoints, CurveTable, StrengthCurveResolution, input);
        }
        #endregion

        #region Static Methods
        /// <summary>
        /// Takes a given setting value and adjusts it linearly via a target value using the given value-handling
        /// method (which decides what the target value is). Returns the value unaffected if no mapping is set. This
        /// doesn't clamp or prevent resulting invalid values.
        /// </summary>
        /// <param name="settingValue">The value of a setting, e.g. the brush transparency slider's value.</param>
        /// <param name="targetValue">A number used to influence the setting value according to the handling.</param>
        public static int GetStrengthMappedValue(
            int settingValue,
            int targetValue,
            int maxRange,
            float inputRatio,
            ConstraintValueHandlingMethod method)
        {
            switch (method)
            {
                case ConstraintValueHandlingMethod.Add:
                    return (int)(settingValue + inputRatio * targetValue);
                case ConstraintValueHandlingMethod.AddPercent:
                    return (int)(settingValue + inputRatio * targetValue / 100 * maxRange);
                case ConstraintValueHandlingMethod.AddPercentCurrent:
                    return (int)(settingValue + inputRatio * targetValue / 100 * settingValue);
                case ConstraintValueHandlingMethod.MatchValue:
                    return (int)((1 - inputRatio) * settingValue + inputRatio * targetValue);
                case ConstraintValueHandlingMethod.MatchPercent:
                    return (int)((1 - inputRatio) * settingValue + inputRatio * targetValue / 100 * maxRange);
            }

            return settingValue;
        }
        #endregion
    }
}
