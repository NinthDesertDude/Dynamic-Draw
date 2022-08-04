using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DynamicDraw
{
    /// <summary>
    /// The modern UI equivalent of a track bar, that also allows directly typing a value.
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

        private (SliderSpecialType type, Color color)? specialMode;

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
        /// The maximum allowed value, which can't be less than the min allowed value. Adjusts the current value if it
        /// goes out of bounds after editing this.
        /// </summary>
        public float Maximum
        {
            get { return numericStops[^1]; }
            set
            {
                if (specialMode != null) { return; }
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
        /// The minimum allowed value, which can't be more than the max allowed value. Adjusts the current value if it
        /// goes out of bounds after editing this.
        /// </summary>
        public float Minimum
        {
            get { return numericStops[0]; }
            set
            {
                if (specialMode != null) { return; }
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
                if (specialMode != null) { return; }
                numericStops = value;
            }
        }

        /// <summary>
        /// Gets the slider's current color for special sliders, if set, null otherwise.
        /// </summary>
        public Color? SliderColor
        {
            get
            {
                return specialMode != null ? GetColor() : null;
            }
        }

        /// <summary>
        /// Gets the slider's special type if set, null otherwise.
        /// </summary>
        public SliderSpecialType? SliderType
        {
            get
            {
                return specialMode?.type;
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

                // Rounds to int or 4 decimal places
                this.value = integerOnly ? (int)value : (int)(value * 1000) / 1000f;

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
        /// Fires when the text is going to be set. Provides the value that would be displayed as an argument, taking
        /// the text to display.
        /// </summary>
        public Func<float, string> ComputeText;

        /// <summary>
        /// Fires when the current value is changed. Provides the new value as an argument.
        /// </summary>
        public event EventHandler<float> ValueChanged;

        /// <summary>
        /// Creates a new slider using the min/max range associated with the given shortcut target.
        /// </summary>
        /// <param name="value">The current value of the slider.</param>
        public Slider(ShortcutTarget target, float value)
            : this(Setting.AllSettings[target].MinMaxRangeF?.Item1 ?? Setting.AllSettings[target].MinMaxRange.Item1,
                  Setting.AllSettings[target].MinMaxRangeF?.Item2 ?? Setting.AllSettings[target].MinMaxRange.Item2,
                  value)
        { }

        /// <summary>
        /// Creates a new slider with a min and max range.
        /// </summary>
        /// <param name="value">The current value of the slider.</param>
        /// <param name="minimum">The minimum range allowed, which the current value can't go lower than.</param>
        /// <param name="maximum">The maximum range allowed, which the current value can't exceed.</param>
        public Slider(float minimum, float maximum, float value)
        {
            specialMode = null;
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
            specialMode = null;
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

        /// <summary>
        /// Creates a slider that represents a color channel, supporting RGBA and HSV. The min-max range is locked to
        /// the channel range: 0-255 for RGBA, 360 for H, 100 for SV. The slider back color is drawn as a gradient from
        /// min to max value for the given channel (using the associated color as the base).
        /// 
        /// Note: the color internally doesn't change when the value changes; this prevents the color from losing data
        /// for cases like e.g. setting value to 0 on a saturation slider (you would normally lose the hue). For tasks
        /// like linking up sliders, it's expected to call <see cref="GetColor"/> on value changed events and
        /// use <see cref="SetColor"/> on the sliders that aren't being edited to keep them up-to-date.
        /// </summary>
        public Slider(SliderSpecialType type, Color color)
        {
            specialMode = new(type, color);
            numericStops = new List<float>();

            switch (type)
            {
                case SliderSpecialType.RedGraph:
                    integerOnly = true;
                    numericStops.AddRange(new float[] { 0, 255 });
                    value = color.R;
                    break;
                case SliderSpecialType.GreenGraph:
                    integerOnly = true;
                    numericStops.AddRange(new float[] { 0, 255 });
                    value = color.G;
                    break;
                case SliderSpecialType.BlueGraph:
                    integerOnly = true;
                    numericStops.AddRange(new float[] { 0, 255 });
                    value = color.B;
                    break;
                case SliderSpecialType.AlphaGraph:
                    integerOnly = true;
                    numericStops.AddRange(new float[] { 0, 255 });
                    value = color.A;
                    break;
                case SliderSpecialType.HueGraph:
                    integerOnly = false;
                    numericStops.AddRange(new float[] { 0, 360 });
                    value = (float)ColorUtils.HSVFFromBgra(color).Hue;
                    break;
                case SliderSpecialType.SatGraph:
                    integerOnly = false;
                    numericStops.AddRange(new float[] { 0, 100 });
                    value = (float)ColorUtils.HSVFFromBgra(color).Saturation;
                    break;
                case SliderSpecialType.ValGraph:
                    integerOnly = false;
                    numericStops.AddRange(new float[] { 0, 100 });
                    value = (float)ColorUtils.HSVFFromBgra(color).Value;
                    break;
            }

            discreteStops = false;
            valuePercent = CalculatePercent(value);

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

            // Draws a specialized back color gradient for special sliders.
            if (specialMode != null)
            {
                // Draws a single gradient between two colors, for all but the hue slider.
                if (specialMode.Value.type != SliderSpecialType.HueGraph)
                {
                    Color computedColor1, computedColor2;
                    PaintDotNet.HsvColorF colHsv, hsvCol1, hsvCol2;
                    Color col = specialMode.Value.color;

                    switch (specialMode.Value.type)
                    {
                        case SliderSpecialType.RedGraph:
                            computedColor1 = Color.FromArgb(0, col.G, col.B);
                            computedColor2 = Color.FromArgb(255, col.G, col.B);
                            break;
                        case SliderSpecialType.GreenGraph:
                            computedColor1 = Color.FromArgb(col.R, 0, col.B);
                            computedColor2 = Color.FromArgb(col.R, 255, col.B);
                            break;
                        case SliderSpecialType.BlueGraph:
                            computedColor1 = Color.FromArgb(col.R, col.G, 0);
                            computedColor2 = Color.FromArgb(col.R, col.G, 255);
                            break;
                        case SliderSpecialType.AlphaGraph:
                            computedColor1 = Color.FromArgb(0, col.R, col.G, col.B);
                            computedColor2 = Color.FromArgb(255, col.R, col.G, col.B);
                            break;
                        case SliderSpecialType.SatGraph:
                            colHsv = ColorUtils.HSVFFromBgra(col);
                            hsvCol1 = new PaintDotNet.HsvColorF(colHsv.Hue, colHsv.Saturation, colHsv.Value);
                            hsvCol2 = new PaintDotNet.HsvColorF(colHsv.Hue, colHsv.Saturation, colHsv.Value);
                            hsvCol1.Saturation = 0;
                            hsvCol2.Saturation = 100;
                            computedColor1 = ColorUtils.HSVFToBgra(hsvCol1);
                            computedColor2 = ColorUtils.HSVFToBgra(hsvCol2);
                            break;
                        case SliderSpecialType.ValGraph:
                            colHsv = ColorUtils.HSVFFromBgra(col);
                            hsvCol1 = new PaintDotNet.HsvColorF(colHsv.Hue, colHsv.Saturation, colHsv.Value);
                            hsvCol2 = new PaintDotNet.HsvColorF(colHsv.Hue, colHsv.Saturation, colHsv.Value);
                            hsvCol1.Value = 0;
                            hsvCol2.Value = 100;
                            computedColor1 = ColorUtils.HSVFToBgra(hsvCol1);
                            computedColor2 = ColorUtils.HSVFToBgra(hsvCol2);
                            break;
                        default:
                            throw new Exception("Should never execute");
                    }

                    Rectangle bounds = new Rectangle(0, 0, Width, Height);

                    // Draws a checkered underlying background only for the alpha graph.
                    if (specialMode.Value.type == SliderSpecialType.AlphaGraph)
                    {
                        using HatchBrush hatchBrush = new HatchBrush(
                            HatchStyle.LargeCheckerBoard, Color.White, Color.FromArgb(191, 191, 191));
                        e.Graphics.FillRectangle(hatchBrush, bounds);
                    }

                    // Draws the gradient between the two colors for all graphs but hue.
                    using (var brush = new LinearGradientBrush(bounds, computedColor1, computedColor2, 0f))
                    {
                        e.Graphics.FillRectangle(brush, bounds);
                    }
                }

                // Draws the hue slider.
                else
                {
                    PaintDotNet.HsvColorF hsvCol = ColorUtils.HSVFFromBgra(specialMode.Value.color);
                    PaintDotNet.HsvColorF hsvColStaging;
                    for (int i = 0; i < Width; i++)
                    {
                        hsvColStaging = new PaintDotNet.HsvColorF(hsvCol.Hue, hsvCol.Saturation, hsvCol.Value);
                        hsvColStaging.Hue = (double)i / Width * MaximumInt;

                        using (Pen pen = new Pen(ColorUtils.HSVFToBgra(hsvColStaging)))
                        {
                            e.Graphics.DrawLine(pen, i, 0, i, Height);
                        }
                    }
                }

                // Draws a line marking the active place.
                float valWidthRatio = value / Maximum * Width;
                e.Graphics.DrawLine(SemanticTheme.Instance.GetPen(ThemeSlot.MenuControlActive),
                    valWidthRatio, 0, valWidthRatio, Height);
            }

            // Draws the filled bar according to the value, in disabled or non-disabled mode.
            else
            {
                if (!Enabled)
                {
                    e.Graphics.FillRectangle(SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBgDisabled),
                        fill, 0, ClientSize.Width - fill, ClientSize.Height);
                    e.Graphics.FillRectangle(SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBgHighlightDisabled),
                        0, 0, fill, ClientSize.Height);
                }
                else
                {
                    e.Graphics.FillRectangle(SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBg),
                        fill, 0, ClientSize.Width - fill, ClientSize.Height);
                    e.Graphics.FillRectangle(SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlActive),
                        0, 0, fill, ClientSize.Height);
                }
            }

            // Formats the text based on some minor, or custom user logic. Displays centered.
            string formatted = isTypingNewValue
                ? newValueString + "..." // ... tells the user they're interactively typing.
                : ComputeText?.Invoke(Value) ?? string.Format("{0:0.##}", Value);

            SizeF measures = e.Graphics.MeasureString(formatted, Font);

            // For gradient sliders, draws a black background behind the text itself for visibility.
            if (specialMode != null)
            {
                e.Graphics.FillRectangle(
                    SemanticTheme.Instance.GetBrush(ThemeSlot.HalfAlphaMenuControlBg),
                    (ClientSize.Width - measures.Width) / 2,
                    (ClientSize.Height - measures.Height) / 2,
                    measures.Width,
                    measures.Height);
            }

            e.Graphics.DrawString(formatted, Font, SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlText),
                (ClientSize.Width - measures.Width) / 2,
                (ClientSize.Height - measures.Height) / 2);

            // Draws a rectangle indicating focus (uses text color since the filled slider part is active color).
            if (Enabled && Focused && ShowFocusCues)
            {
                e.Graphics.DrawRectangle(
                    SemanticTheme.Instance.GetPen(ThemeSlot.MenuControlText), 0, 0, Width - 1, Height - 1);
            }
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
                        int leftStopIndex = (int)Math.Round(valuePercent / interval);

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
                        newValueString += newChar;
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
            Focus(); // Focusing allows the user to hover and type a value without clicking first, very useful.
            mouseOver = true;
        }

        private void Slider_MouseLeave(object sender, EventArgs e)
        {
            mouseOver = false;
        }

        /// <summary>
        /// Discards any value being typed.
        /// </summary>
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

        #region Not event handlers
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
            if (specialMode != null) { return; }
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
        /// Changes the slider to a special slider type, or null to clear the special slider status. The number range
        /// cannot change for special sliders and is 0-255 for RGBA, 0-360 for H, 0-100 for SV. If the slider isn't a
        /// special type (and thus doesn't already have a color), the associated color is set to the given color.
        /// </summary>
        public void SetSliderType(SliderSpecialType? type, Color color)
        {
            if (type == null)
            {
                specialMode = null;
                return;
            }

            specialMode = new((SliderSpecialType)type, specialMode?.color ?? color);
            SetColor(specialMode.Value.color);
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

        /// <summary>
        /// For sliders that define a <see cref="SliderSpecialType"/>, this gets the passed-in color, modified by
        /// the slider value for its channel.
        /// </summary>
        public Color GetColor()
        {
            if (specialMode == null) { throw new ArgumentException("Only special sliders can get color from value."); }
            Color col = specialMode.Value.color;
            PaintDotNet.HsvColorF hsvCol;

            switch (specialMode.Value.type)
            {
                case SliderSpecialType.RedGraph:
                    return Color.FromArgb(col.A, (int)value, col.G, col.B);
                case SliderSpecialType.GreenGraph:
                    return Color.FromArgb(col.A, col.R, (int)value, col.B);
                case SliderSpecialType.BlueGraph:
                    return Color.FromArgb(col.A, col.R, col.G, (int)value);
                case SliderSpecialType.AlphaGraph:
                    return Color.FromArgb((int)value, col.R, col.G, col.B);
                case SliderSpecialType.HueGraph:
                    hsvCol = ColorUtils.HSVFFromBgra(col);
                    hsvCol.Hue = Math.Round(value);
                    return ColorUtils.HSVFToBgra(hsvCol, col.A);
                case SliderSpecialType.SatGraph:
                    hsvCol = ColorUtils.HSVFFromBgra(col);
                    hsvCol.Saturation = Math.Round(value);
                    return ColorUtils.HSVFToBgra(hsvCol, col.A);
                case SliderSpecialType.ValGraph:
                    hsvCol = ColorUtils.HSVFFromBgra(col);
                    hsvCol.Value = Math.Round(value);
                    return ColorUtils.HSVFToBgra(hsvCol, col.A);
            }

            throw new Exception("Shouldn't execute.");
        }

        /// <summary>
        /// For sliders that define a <see cref="SliderSpecialType"/>, this updates the color and current value based
        /// on it for this slider's channel of interest. This does not invoke <see cref="ValueChanged"/>.
        /// </summary>
        public void SetColor(Color col)
        {
            if (specialMode == null) { throw new ArgumentException("Only special sliders can set value from color."); }
            specialMode = new(specialMode.Value.type, col);
            PaintDotNet.HsvColorF hsvCol;

            switch (specialMode.Value.type)
            {
                case SliderSpecialType.RedGraph:
                    value = col.R;
                    break;
                case SliderSpecialType.GreenGraph:
                    value = col.G;
                    break;
                case SliderSpecialType.BlueGraph:
                    value = col.B;
                    break;
                case SliderSpecialType.AlphaGraph:
                    value = col.A;
                    break;
                case SliderSpecialType.HueGraph:
                    hsvCol = ColorUtils.HSVFFromBgra(col);
                    value = (float)hsvCol.Hue;
                    break;
                case SliderSpecialType.SatGraph:
                    hsvCol = ColorUtils.HSVFFromBgra(col);
                    value = (float)hsvCol.Saturation;
                    break;
                case SliderSpecialType.ValGraph:
                    hsvCol = ColorUtils.HSVFFromBgra(col);
                    value = (float)hsvCol.Value;
                    break;
            }

            // intentionally not calling ValueChanged, it creates call stack cycles in interdepent slider use-cases.
            value = integerOnly ? (int)value : (int)(value * 1000) / 1000f;
            valuePercent = CalculatePercent(this.value);
            Refresh();
        }
        #endregion
    }
}
