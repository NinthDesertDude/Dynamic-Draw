using DynamicDraw.Gui;
using DynamicDraw.Gui.Constraints;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Serialization;

namespace DynamicDraw
{
    /// <summary>
    /// A dynamic constraint. Includes the affected property, the 
    /// </summary>
    public class ConstraintDefinition
    {
        #region Private variables
        [IgnoreDataMember]
        private int strengthCurveResolution = 1000;

        [IgnoreDataMember]
        private PointF[] strengthCurveControlPoints = null;
        #endregion

        #region Members
        /// <summary>
        /// The property that the constraint definition concerns.
        /// </summary>
        [DataMember(Name = "Property")]
        public ShortcutTarget Property { get; set; }

        /// <summary>
        /// Determines how a constraint runs, e.g. on brush stroke, jitter during stroke, etc.
        /// </summary>
        [DataMember(Name = "Trigger")]
        public ConstraintTrigger Trigger { get; set; }

        /// <summary>
        /// The points on the graph that are used to generate the curve table / decide the curve strength.
        /// </summary>
        [DataMember(Name = "StrengthCurve")]
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
        [DataMember(Name = "StrengthResolution")]
        /// <summary>
        /// A positive value with a reasonable range e.g. 100 to 20000.
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
        [IgnoreDataMember]
        public float[] CurveTable { get; set; } = Array.Empty<float>();

        /// <summary>
        /// The value handling method.
        /// </summary>
        [DataMember(Name = "ValueType")]
        public ConstraintValueHandlingMethod ValueType { get; set; } = ConstraintValueHandlingMethod.DoNothing;

        [DataMember(Name = "ValueSource")]
        public ConstraintValueSource ValueSource { get; set; } = ConstraintValueSource.None;

        /// <summary>
        /// For properties that require a float.
        /// </summary>
        [DataMember(Name = "ValFloat1")]
        public float ValFloat1 { get; set; } = 0f;

        /// <summary>
        /// For properties that requires two float values.
        /// </summary>
        [DataMember(Name = "ValFloat2")]
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
    }
}
