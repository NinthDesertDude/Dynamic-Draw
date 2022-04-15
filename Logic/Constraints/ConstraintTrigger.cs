using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicDraw.Gui.Constraints
{
    /// <summary>
    /// Determines how a constraint runs, e.g. on brush stroke, jitter during stroke, etc.
    /// </summary>
    public enum ConstraintTrigger
    {
        /// <summary>
        /// Equivalent to null.
        /// </summary>
        None,

        /// <summary>
        /// A random value between the given min and max is applied with arithmetic on the start of each brush stroke.
        /// </summary>
        OnBrushStroke,

        /// <summary>
        /// A random value between the given min and max is applied every time the brush is stamped (excluding symmetry
        /// stamps), using the chosen handling method and strength curve.
        /// 
        /// If an interval is set, jitter recomputes occurs only at the interval ticks.
        /// </summary>
        JitterDuringStroke,

        /// <summary>
        /// The value starts at min range and increments to max range (these values are added to the current value
        /// instead of replacing it) according to the input source strength modified by a strength curve. 100% strength
        /// will go the full range between min and max, with smaller percents being proportionally smaller changes. The
        /// end behavior determines what happens when trying to increment past the end. It's possible for max range to
        /// be less than min range.
        /// 
        /// If an interval is set, shifting occurs only at the interval ticks.
        /// </summary>
        ShiftDuringStroke
    }
}
