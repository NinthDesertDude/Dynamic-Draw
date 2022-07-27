using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DynamicDraw.Gui
{
    /// <summary>
    /// A button that acts like a track bar with a modern appearance.
    /// </summary>
    public class Slider : Button
    {
        private bool integerOnly;
        private float value;
        private float valuePercent;
        private List<float> numericStops;
        private bool discreteStops;

        private bool mouseOver;
        private bool mouseHeld;
        private bool didMouseMove;
        private bool isTypingNewValue;
        private string newValueString;

        /// <summary>
        /// The minimum allowed value, which can't be more than the max allowed value. Adjusts the current value if it
        /// goes out of bounds after editing this.
        /// </summary>
        public float Minimum
        {
            get { return numericStops[0]; }
            set
            {
                if (numericStops[0] == value) { return; }

                numericStops[0] = integerOnly ? (int)value : value;

                if (numericStops[0] > numericStops[^1])
                {
                    throw new ArgumentOutOfRangeException("Min value must be less than maximum value.");
                }

                if (this.value < numericStops[0])
                {
                    this.value = numericStops[0];
                    ValueChanged?.Invoke(this, this.value);
                    valuePercent = CalculatePercent(this.value);
                }

                Refresh();
            }
        }

        /// <summary>
        /// The minimum allowed value, constrained to an int. Get/set both deal with the value in ints.
        /// </summary>
        public int MinimumInt
        {
            get
            {
                return (int)numericStops[0];
            }
            set
            {
                Minimum = value;
            }
        }

        /// <summary>
        /// The maximum allowed value, which can't be less than the min allowed value. Adjusts the current value if it
        /// goes out of bounds after editing this.
        /// </summary>
        public float Maximum
        {
            get { return numericStops[^1]; }
            set
            {
                if (numericStops[^1] == value) { return; }

                numericStops[^1] = integerOnly ? (int)value : value;

                if (numericStops[^1] < numericStops[0])
                {
                    throw new ArgumentOutOfRangeException("Max value must be greater than minimum value.");
                }

                if (this.value > numericStops[^1])
                {
                    this.value = numericStops[^1];
                    ValueChanged?.Invoke(this, this.value);
                    valuePercent = CalculatePercent(this.value);
                }

                Refresh();
            }
        }

        /// <summary>
        /// The maximum allowed value, constrained to an int. Get/set both deal with the value in ints.
        /// </summary>
        public int MaximumInt
        {
            get
            {
                return (int)numericStops[^1];
            }
            set
            {
                Maximum = value;
            }
        }

        /// <summary>
        /// The current value.
        /// </summary>
        public float Value
        {
            get
            {
                return value;
            }
            set
            {
                if (this.value == value)
                {
                    return;
                }

                this.value = integerOnly ? (int)value : value;

                if (this.value < numericStops[0])
                {
                    throw new Exception("Attempted to set value below minimum range.");
                }

                if (this.value > numericStops[^1])
                {
                    throw new Exception("Attempted to set value beyond maximum range.");
                }

                ValueChanged?.Invoke(this, this.value);
                valuePercent = CalculatePercent(this.value);
                Refresh();
            }
        }

        /// <summary>
        /// The current value, constrained to an int. Get/set both deal with the value in ints.
        /// </summary>
        public int ValueInt
        {
            get
            {
                return (int)this.value;
            }
            set
            {
                Value = value;
            }
        }

        /// <summary>
        /// Gets a copy of the evenly-spaced numeric stops used by the slider.
        /// 
        /// Numeric stops are arbitrary values evenly-spaced along a slider. When the user clicks or drags along the
        /// slider, the value is a linear interpolation between the two nearest sliders based on proximity.
        /// </summary>
        public List<float> NumericStops
        {
            get
            {
                return new List<float>(numericStops);
            }
            private set
            {
                numericStops = value;
            }
        }

        /// <summary>
        /// Whether to allow values between the numeric stops or not. When false, the value will
        /// be exactly equal to the nearest stop instead of an interpolation of the two nearest.
        /// </summary>
        public bool DiscreteStops
        {
            get
            {
                return discreteStops;
            }
            set
            {
                discreteStops = value;
            }
        }

        /// <summary>
        /// Whether the data must be constrained to whole numbers or not. If true, all values set will automatically
        /// be adjusted to integer values (without rounding) for all slider functionality.
        /// </summary>
        public bool IntegerOnly
        {
            get { return integerOnly; }
            set
            {
                integerOnly = value;

                if (value)
                {
                    for (int i = 0; i < numericStops.Count; i++)
                    {
                        numericStops[i] = (int)numericStops[i];
                    }

                    if (this.value != (int)this.value)
                    {
                        this.value = (int)this.value;
                        ValueChanged?.Invoke(this, this.value);
                        valuePercent = CalculatePercent(this.value);
                    }
                }

                Refresh();
            }
        }

        /// <summary>
        /// Fires when the text is going to be set. Provides the value that would be displayed as an argument, taking
        /// the text to display.
        /// </summary>
        public Func<float, string> ComputeText;

        /// <summary>
        /// Fires when the current value is changed. Provides the new value as an argument.
        /// </summary>
        public event EventHandler<float> ValueChanged;

        /// <summary>
        /// Creates a new slider with a min and max range.
        /// </summary>
        /// <param name="value">The current value of the slider.</param>
        /// <param name="minimum">The minimum range allowed, which the current value can't go lower than.</param>
        /// <param name="maximum">The maximum range allowed, which the current value can't exceed.</param>
        public Slider(float minimum, float maximum, float value)
        {
            numericStops = new List<float>() { minimum, maximum };

            if (minimum >= maximum)
            {
                throw new Exception($"The minimum ({minimum}) must be less than ({maximum}) in slider constructor.");
            }
            if (value < minimum || value > maximum)
            {
                throw new Exception("Value must lie within the range of minimum and maximum.");
            }

            this.value = value;
            valuePercent = CalculatePercent(this.value);

            discreteStops = false;
            integerOnly = false;

            mouseOver = false;
            mouseHeld = false;
            didMouseMove = false;
            isTypingNewValue = false;
            newValueString = "";

            ComputeText = null;
            LostFocus += Slider_LostFocus;
            MouseEnter += Slider_MouseEnter;
            MouseLeave += Slider_MouseLeave;
            MouseDown += Slider_MouseDown;
            MouseMove += Slider_MouseMove;
            MouseUp += Slider_MouseUp;
        }

        /// <summary>
        /// Creates a new slider with any number of stops, auto-sorted, evenly distributed along it. The computed
        /// value at a point on the slider will be a linear interpolation of the two nearest stops based on proximity.
        /// </summary>
        /// <param name="numericStops">
        /// A collection of values to be evenly distributed along the slider. They will be automatically sorted. At
        /// least 2 are required to serve as the minimum and maximum.
        /// </param>
        /// <param name="value">The current value of the slider.</param>
        public Slider(IEnumerable<float> numericStops, float value)
        {
            this.value = value;
            SetNumericStops(numericStops, true);
            valuePercent = CalculatePercent(this.value);

            if (this.numericStops[0] >= this.numericStops[^1])
            {
                throw new Exception($"The minimum ({this.numericStops[0]}) must be less than ({this.numericStops[^1]}) in slider constructor.");
            }
            if (value < this.numericStops[0] || value > this.numericStops[^1])
            {
                throw new Exception("Value must lie within the range of minimum and maximum.");
            }

            discreteStops = false;
            integerOnly = false;

            mouseOver = false;
            mouseHeld = false;
            didMouseMove = false;
            isTypingNewValue = false;
            newValueString = "";

            ComputeText = null;
            MouseEnter += Slider_MouseEnter;
            MouseLeave += Slider_MouseLeave;
            MouseDown += Slider_MouseDown;
            MouseMove += Slider_MouseMove;
            MouseUp += Slider_MouseUp;
        }

        #region Overriden methods
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Uses the value for the filled bar if not typing a new value, else tries to use that value.
            float valueToComputeWith = value;
            if (newValueString != "" && (isTypingNewValue || mouseOver))
            {
                if (float.TryParse(newValueString, out float newValueNumeric))
                {
                    valueToComputeWith = Math.Clamp(
                        discreteStops ? (int)newValueNumeric : newValueNumeric,
                        numericStops[0],
                        numericStops[^1]);
                }
            }

            float fill = ClientSize.Width * valuePercent;

            // Draws the filled bar according to the value, in disabled or non-disabled mode.
            if (!Enabled)
            {
                e.Graphics.FillRectangle(SystemBrushes.ControlDark, fill, 0, ClientSize.Width - fill, ClientSize.Height);
                e.Graphics.FillRectangle(SystemBrushes.ControlLight, 0, 0, fill, ClientSize.Height);
            }
            else
            {
                e.Graphics.FillRectangle(SystemBrushes.ControlDarkDark, fill, 0, ClientSize.Width - fill, ClientSize.Height);
                e.Graphics.FillRectangle(SystemBrushes.Highlight, 0, 0, fill, ClientSize.Height);
            }

            // Formats the text based on some minor, or custom user logic. Displays centered.
            string formatted = isTypingNewValue
                ? newValueString + "..." // ... tells the user they're interactively typing.
                : ComputeText?.Invoke(Value) ?? string.Format("{0:0.##}", Value);

            SizeF measures = e.Graphics.MeasureString(formatted, Font);

            e.Graphics.DrawString(formatted, Font, Brushes.White,
                (ClientSize.Width - measures.Width) / 2,
                (ClientSize.Height - measures.Height) / 2);
        }

        /// <summary>
        /// Handles value manipulation using the keyboard.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Focused || mouseOver || isTypingNewValue)
            {
                // enters typing mode and deletes a character.
                if (keyData == Keys.Back)
                {
                    if (!isTypingNewValue)
                    {
                        newValueString = Value.ToString();
                        isTypingNewValue = true;
                    }

                    if (newValueString.Length > 0)
                    {
                        newValueString = newValueString[..^1];
                    }

                    Refresh();
                    return true;
                }

                // exits typing mode, discarding any typed changes.
                if (keyData == Keys.Escape)
                {
                    if (isTypingNewValue)
                    {
                        isTypingNewValue = false;
                        newValueString = "";
                        Refresh();
                        return true;
                    }
                }

                // if interactively typing, accepts a valid typed value bound to the min/max.
                else if (keyData == Keys.Enter || keyData == Keys.Space)
                {
                    if (isTypingNewValue)
                    {
                        if (newValueString != "" && float.TryParse(newValueString, out float result))
                        {
                            Value = Math.Clamp(
                                integerOnly ? (int)result : result,
                                numericStops[0],
                                numericStops[^1]);

                        }

                        isTypingNewValue = false;
                        newValueString = "";
                        Refresh();
                        return true;
                    }
                }

                // if not typing, nudges the value.
                else if (!isTypingNewValue && (keyData == Keys.Left || keyData == Keys.Right))
                {
                    float nudge = (keyData == Keys.Left) ? -0.01f : 0.01f;

                    if (discreteStops)
                    {
                        float interval = 1f / (numericStops.Count - 1);
                        int leftStopIndex = (int)(valuePercent / interval);

                        if (keyData == Keys.Left && leftStopIndex != 0)
                        {
                            Value = numericStops[leftStopIndex - 1];
                            valuePercent = interval * (leftStopIndex - 1);
                        }
                        else if (keyData == Keys.Right && leftStopIndex < numericStops.Count - 1)
                        {
                            Value = numericStops[leftStopIndex + 1];
                            valuePercent = interval * (leftStopIndex + 1);
                        }
                    }
                    else
                    {
                        float currentNudge = 0;
                        float newVal = 0;

                        // Increases the nudging force until it overcomes integer rounding, if necessary.
                        while (currentNudge == 0 || (newVal != this.value && integerOnly && (int)newVal == this.value))
                        {
                            currentNudge += nudge;
                            newVal = CalculateValue(Math.Clamp(valuePercent + currentNudge, 0, 1));
                        }

                        Value = newVal;
                        valuePercent = CalculatePercent(value);
                    }

                    Refresh();
                    return true;
                }

                // enters typing mode and appends a character.
                else
                {
                    string newChar = KeyboardShortcut.GetPrintableKey(keyData);

                    if (newChar != "")
                    {
                        newValueString += isTypingNewValue
                            ? newChar
                            : Value.ToString() + newChar;
                        isTypingNewValue = true;
                        Refresh();
                        return true;
                    }
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
        #endregion

        #region Event handlers
        private void Slider_MouseEnter(object sender, EventArgs e)
        {
            mouseOver = true;
        }

        private void Slider_MouseLeave(object sender, EventArgs e)
        {
            mouseOver = false;
        }

        private void Slider_LostFocus(object sender, EventArgs e)
        {
            mouseOver = false;
            mouseHeld = false;
            didMouseMove = false;
            isTypingNewValue = false;
            newValueString = "";
            Refresh();
        }

        /// <summary>
        /// Updates the tracked value based on click location.
        /// </summary>
        private void Slider_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseHeld)
            {
                didMouseMove = true;
                AdjustValue();
            }
        }

        /// <summary>
        /// Disables dragging the slider value by mouse, and handles clicks that never moved the mouse.
        /// </summary>
        private void Slider_MouseUp(object sender, MouseEventArgs e)
        {
            // Performs a click if the mouse was depressed and released without triggering it via mouse move.
            if (mouseHeld && !didMouseMove)
            {
                AdjustValue();
                Refresh();
            }

            mouseHeld = false;
            didMouseMove = false;
        }

        /// <summary>
        /// Enables dragging the slider value by mouse.
        /// </summary>
        private void Slider_MouseDown(object sender, MouseEventArgs e)
        {
            mouseHeld = true;
        }
        #endregion

        #region Non-event handlers
        /// <summary>
        /// Finds where the slider was clicked and sets the correct value based on the numeric stops.
        /// </summary>
        private void AdjustValue()
        {
            int pos = PointToClient(Cursor.Position).X;
            float percent = Math.Clamp(pos / (float)ClientSize.Width, 0f, 1f);
            Value = CalculateValue(percent);
        }

        /// <summary>
        /// Sets the new numeric stops (auto-sorted).
        /// </summary>
        private void SetNumericStops(IEnumerable<float> stops, bool skipValueChecking)
        {
            if (stops.Count() < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(stops), "At least 2 stops are required, received " + stops.Count());
            }

            List<float> stopsList = integerOnly
                ? stops.Select((number) => (float)Math.Floor(number)).ToList()
                : stops.ToList();
            stopsList.Sort();

            numericStops = stopsList;

            if (!skipValueChecking && this.value < numericStops[0])
            {
                throw new Exception("Attempted to set value below minimum range.");
            }

            if (!skipValueChecking && this.value > numericStops[^1])
            {
                throw new Exception("Attempted to set value beyond maximum range.");
            }
        }

        /// <summary>
        /// Sets the new numeric stops (auto-sorted).
        /// </summary>
        public void SetNumericStops(IEnumerable<float> stops)
        {
            SetNumericStops(stops, false);
        }

        /// <summary>
        /// The inverse of <see cref="CalculatePercent(float)"/>.
        /// 
        /// Given a position along the slider (as a value from 0 to 1), returns the interpolated value between the two
        /// nearest stops evenly distributed along the range. If <see cref="DiscreteStops"/> is true, returns the value
        /// of the nearest stop instead.
        /// </summary>
        /// <param name="percentAlongSlider">
        /// A value from 0 to 1 representing the distance along the slider.
        /// </param>
        public float CalculateValue(float percentAlongSlider)
        {
            if (percentAlongSlider < 0 || percentAlongSlider > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(percentAlongSlider), "Percent along slider must be a value from 0 to 1 inclusive.");
            }

            // Gets what the stop index would be if there was one at the given percent. This is fractional otherwise.
            float evenIntervalOfEachStop = 1f / (numericStops.Count - 1f);
            float fractionalIndex = percentAlongSlider / evenIntervalOfEachStop;

            // Rounds to the nearest actual index and returns that stop's value.
            if (discreteStops)
            {
                return numericStops[(int)Math.Round(fractionalIndex)];
            }

            // Gets the actual indices of the nearest left and right stop, and how far "along" the old value is.
            int leftStop = (int)fractionalIndex;
            int rightStop = (int)Math.Ceiling(fractionalIndex);
            float newPercent = fractionalIndex - leftStop;

            // Linear interpolation between the nearest left & right stops based on proximity.
            float finalValue = numericStops[leftStop] + newPercent * (numericStops[rightStop] - numericStops[leftStop]);
            return finalValue;
        }

        /// <summary>
        /// The inverse of <see cref="CalculateValue(float)"/>.
        /// 
        /// Given a value in the range of the slider, returns a value from 0 to 1 indicating how far along it is on the
        /// slider, where 0 = left side, 1 = right side, and values between lie on it accordingly.
        /// </summary>
        public float CalculatePercent(float value)
        {
            if (value < numericStops[0] || value > numericStops[^1])
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Value {value} passed in does not fall within "
                    + $"minimum ({numericStops[0]}) and maximum ({numericStops[^1]}) range.");
            }

            float evenIntervalOfEachStop = 1f / (numericStops.Count - 1f);
            int leftSideIndex = numericStops.Count - 1;

            // Finds the index of the nearest numeric stop to the left.
            for (int i = 0; i < numericStops.Count; i++)
            {
                if (value < numericStops[i])
                {
                    leftSideIndex = i - 1;
                    break;
                }
            }

            // Catches edge case at 100% where there's no right-side numeric stop
            // Also skips lerping for exact matches, esp. useful when discrete steps is true.
            if (value == numericStops[leftSideIndex])
            {
                return leftSideIndex * evenIntervalOfEachStop;
            }

            // This is the lerp function, but solved for t. Lerp function is a + t * (b - a)
            // t in this case is the percent along from the left to the right stop.
            float leftStop = numericStops[leftSideIndex];
            float rightStop = numericStops[leftSideIndex + 1];
            float percentBetween = (value - leftStop) / (rightStop - leftStop);

            // This is (left side * interval) + (percent * interval), but simplified by one operation. This is the % of
            // the slider covered up to the start of the left stop, plus the % from the left stop to right stop
            // multiplied by how much one interval is worth in terms of %.
            return (leftSideIndex + percentBetween) * evenIntervalOfEachStop;
        }
        #endregion
    }
}
