using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DynamicDraw.Properties;
using DynamicDraw.Localization;
using PaintDotNet;

namespace DynamicDraw
{
    /// <summary>
    /// A color dialog that includes a hue-sat color wheel, with RGB and HSV sliders and optionally alpha.
    /// </summary>
    public class ColorPickerDialog : Form
    {
        private Color associatedColor;
        private bool isAlphaEnabled;

        /// <summary>
        /// The color bound to the dialog.
        /// </summary>
        public Color AssociatedColor
        {
            get
            {
                return associatedColor;
            }
            set
            {
                chosenColor.Swatches[0] = value;
                UpdateOnColorChange(null);
            }
        }

        /// <summary>
        /// Fires when the chosen color is updated, any time the form is active.
        /// </summary>
        public event Action ColorUpdated;

        /// <summary>
        /// When true, the alpha slider is shown and the hex editor accepts alpha values. When false, the alpha cannot
        /// be changed by any means except directly setting the associated color of the dialog.
        /// </summary>
        public bool IsAlphaEnabled
        {
            get
            {
                return isAlphaEnabled;
            }
            set
            {
                isAlphaEnabled = value;
                UpdateOnAlphaEnabledChange();
            }
        }

        #region Gui Members
        private FlowLayoutPanel panelControlsContainer;
        private FlowLayoutPanel panelLeftContainer;
        private FlowLayoutPanel panelWheelAndValue;
        private ColorWheel colorWheel;
        private Slider colorChannelValue;
        private Slider colorChannelAlpha;
        private FlowLayoutPanel panelColorAndHex;
        private SwatchBox chosenColor;
        private ColorTextbox txtbxHexEntry;
        private FlowLayoutPanel panelRGBHSSliders;
        private Slider colorChannelR;
        private Slider colorChannelG;
        private Slider colorChannelB;
        private Slider colorChannelH;
        private Slider colorChannelS;
        private FlowLayoutPanel panelOkCancel;
        private ThemedButton bttnOk;
        private ThemedButton bttnCancel;
        private FlowLayoutPanel panelMainContainer;
        #endregion

        public ColorPickerDialog(Color associatedColor, bool isAlphaEnabled = true)
        {
            this.associatedColor = associatedColor;
            this.isAlphaEnabled = isAlphaEnabled;

            SetupGui();
            CenterToScreen();
        }

        #region Methods (not event handlers)
        private void SetupGui()
        {
            panelControlsContainer = new FlowLayoutPanel();
            panelLeftContainer = new FlowLayoutPanel();
            panelWheelAndValue = new FlowLayoutPanel();
            colorWheel = new ColorWheel();
            colorChannelValue = new Slider(SliderSpecialType.ValGraph, associatedColor);
            colorChannelAlpha = new Slider(SliderSpecialType.AlphaGraph, associatedColor);
            panelColorAndHex = new FlowLayoutPanel();
            chosenColor = new SwatchBox(new List<Color>() { associatedColor }, 1);
            txtbxHexEntry = new ColorTextbox(associatedColor, isAlphaEnabled);
            panelRGBHSSliders = new FlowLayoutPanel();
            colorChannelR = new Slider(SliderSpecialType.RedGraph, associatedColor);
            colorChannelG = new Slider(SliderSpecialType.GreenGraph, associatedColor);
            colorChannelB = new Slider(SliderSpecialType.BlueGraph, associatedColor);
            colorChannelH = new Slider(SliderSpecialType.HueGraph, associatedColor);
            colorChannelS = new Slider(SliderSpecialType.SatGraph, associatedColor);
            panelOkCancel = new FlowLayoutPanel();
            bttnOk = new ThemedButton();
            bttnCancel = new ThemedButton();
            panelMainContainer = new FlowLayoutPanel();
            panelMainContainer.SuspendLayout();
            panelControlsContainer.SuspendLayout();
            panelLeftContainer.SuspendLayout();
            panelWheelAndValue.SuspendLayout();
            panelColorAndHex.SuspendLayout();
            panelRGBHSSliders.SuspendLayout();
            panelOkCancel.SuspendLayout();
            SuspendLayout();

            #region panelControlsContainer
            panelControlsContainer.AutoSize = true;
            panelControlsContainer.Controls.Add(panelLeftContainer);
            panelControlsContainer.Controls.Add(panelRGBHSSliders);
            #endregion

            #region panelLeftContainer
            panelLeftContainer.AutoSize = true;
            panelLeftContainer.FlowDirection = FlowDirection.TopDown;
            panelLeftContainer.Controls.Add(panelWheelAndValue);
            panelLeftContainer.Controls.Add(panelColorAndHex);
            #endregion

            #region colorWheel
            colorWheel.Width = 120;
            colorWheel.Height = 120;
            colorWheel.HsvColor = HsvColor.FromColor(associatedColor);
            colorWheel.ColorChanged += (a, b) => { UpdateOnColorChange(null, true); };
            #endregion

            #region colorChannelValue
            colorChannelValue.AutoSize = false;
            colorChannelValue.Margin = new Padding(4, 0, 0, 0);
            colorChannelValue.Size = new Size(22, 120);
            colorChannelValue.ValueChanged += (_1, _2) => UpdateOnColorChange(SliderSpecialType.ValGraph);
            colorChannelValue.ComputeText = (val) => Math.Round(val).ToString();
            #endregion

            #region colorChannelAlpha
            colorChannelAlpha.AutoSize = false;
            colorChannelAlpha.Margin = new Padding(4, 0, 0, 0);
            colorChannelAlpha.Size = new Size(22, 120);
            colorChannelAlpha.ValueChanged += (_1, _2) => UpdateOnColorChange(SliderSpecialType.AlphaGraph);
            colorChannelAlpha.ComputeText = (val) => Math.Round(val).ToString();
            #endregion

            #region panelWheelAndValue
            panelWheelAndValue.AutoSize = true;
            panelWheelAndValue.Controls.Add(colorWheel);
            panelWheelAndValue.Controls.Add(colorChannelValue);
            panelWheelAndValue.Controls.Add(colorChannelAlpha);
            #endregion

            #region chosenColor
            chosenColor.Width = 32;
            chosenColor.Height = 32;
            #endregion

            #region txtbxHexEntry
            txtbxHexEntry.Height = 24;
            txtbxHexEntry.Width = 68;
            txtbxHexEntry.Margin = new Padding(4, 4, 0, 0);
            txtbxHexEntry.Text = ColorUtils.GetTextFromColor(associatedColor);
            txtbxHexEntry.ColorUpdatedByText += TxtbxHexEntry_ColorUpdatedByText;
            #endregion

            #region panelColorAndHex
            panelColorAndHex.AutoSize = true;
            panelColorAndHex.Controls.Add(chosenColor);
            panelColorAndHex.Controls.Add(txtbxHexEntry);
            #endregion

            #region colorChannelR
            colorChannelR.AutoSize = false;
            colorChannelR.Margin = new Padding(0, 4, 0, 0);
            colorChannelR.Size = new Size(120, 30);
            colorChannelR.TextAlign = ContentAlignment.MiddleLeft;
            colorChannelR.ValueChanged += (_1, _2) => UpdateOnColorChange(SliderSpecialType.RedGraph);
            colorChannelR.ComputeText = (val) => $"{Strings.ColorRedAbbr}: {val}";
            #endregion

            #region colorChannelG
            colorChannelG.AutoSize = false;
            colorChannelG.Margin = new Padding(0, 4, 0, 0);
            colorChannelG.Size = new Size(120, 30);
            colorChannelG.TextAlign = ContentAlignment.MiddleLeft;
            colorChannelG.ValueChanged += (_1, _2) => UpdateOnColorChange(SliderSpecialType.GreenGraph);
            colorChannelG.ComputeText = (val) => $"{Strings.ColorGreenAbbr}: {val}";
            #endregion

            #region colorChannelB
            colorChannelB.AutoSize = false;
            colorChannelB.Margin = new Padding(0, 4, 0, 0);
            colorChannelB.Size = new Size(120, 30);
            colorChannelB.TextAlign = ContentAlignment.MiddleLeft;
            colorChannelB.ValueChanged += (_1, _2) => UpdateOnColorChange(SliderSpecialType.BlueGraph);
            colorChannelB.ComputeText = (val) => $"{Strings.ColorBlueAbbr}: {val}";
            #endregion

            #region colorChannelH
            colorChannelH.AutoSize = false;
            colorChannelH.Margin = new Padding(0, 4, 0, 0);
            colorChannelH.Size = new Size(120, 30);
            colorChannelH.TextAlign = ContentAlignment.MiddleLeft;
            colorChannelH.ValueChanged += (_1, _2) => UpdateOnColorChange(SliderSpecialType.HueGraph);
            colorChannelH.ComputeText = (val) => $"{Strings.ColorHueAbbr}: {(int)Math.Round(val)}";
            #endregion

            #region colorChannelS
            colorChannelS.AutoSize = false;
            colorChannelS.Margin = new Padding(0, 4, 0, 0);
            colorChannelS.Size = new Size(120, 30);
            colorChannelS.TextAlign = ContentAlignment.MiddleLeft;
            colorChannelS.ValueChanged += (_1, _2) => UpdateOnColorChange(SliderSpecialType.SatGraph);
            colorChannelS.ComputeText = (val) => $"{Strings.ColorSatAbbr}: {(int)Math.Round(val)}";
            #endregion

            #region panelRGBHSSliders
            panelRGBHSSliders.AutoSize = true;
            panelRGBHSSliders.FlowDirection = FlowDirection.TopDown;
            panelRGBHSSliders.Margin = new Padding(4, 0, 0, 0);
            panelRGBHSSliders.Controls.Add(colorChannelR);
            panelRGBHSSliders.Controls.Add(colorChannelG);
            panelRGBHSSliders.Controls.Add(colorChannelB);
            panelRGBHSSliders.Controls.Add(colorChannelH);
            panelRGBHSSliders.Controls.Add(colorChannelS);
            #endregion

            #region bttnOk
            bttnOk.Margin = Padding.Empty;
            bttnOk.Size = new Size(100, 32);
            bttnOk.Text = Strings.Ok;
            bttnOk.Click += BttnOk_Click;
            #endregion

            #region bttnCancel
            bttnCancel.Margin = new Padding(10, 0, 0, 0);
            bttnCancel.Size = new Size(100, 32);
            bttnCancel.Text = Strings.Cancel;
            bttnCancel.Click += BttnCancel_Click;
            #endregion

            #region panelSaveCancel
            panelOkCancel.AutoSize = true;
            panelOkCancel.Margin = new Padding(60, 0, 40, 0);
            panelOkCancel.Controls.Add(bttnOk);
            panelOkCancel.Controls.Add(bttnCancel);
            panelOkCancel.FlowDirection = FlowDirection.LeftToRight;
            #endregion

            #region panelMainContainer
            panelMainContainer.AutoSize = true;
            panelMainContainer.FlowDirection = FlowDirection.TopDown;
            panelMainContainer.Controls.Add(panelControlsContainer);
            panelMainContainer.Controls.Add(panelOkCancel);
            #endregion

            #region ColorPickerDialog
            AutoSize = true;
            Height = 230;
            Icon = Resources.Icon;
            KeyPreview = true;
            Margin = new Padding(4, 3, 4, 3);
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            MaximizeBox = false;
            MinimizeBox = false;
            Text = Strings.PickAColor;
            Controls.Add(panelMainContainer);
            #endregion

            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();

            UpdateOnAlphaEnabledChange();
            panelControlsContainer.ResumeLayout(false);
            panelControlsContainer.PerformLayout();
            panelLeftContainer.ResumeLayout(false);
            panelLeftContainer.PerformLayout();
            panelWheelAndValue.ResumeLayout(false);
            panelWheelAndValue.PerformLayout();
            panelColorAndHex.ResumeLayout(false);
            panelColorAndHex.PerformLayout();
            panelRGBHSSliders.ResumeLayout(false);
            panelRGBHSSliders.PerformLayout();
            panelOkCancel.ResumeLayout(false);
            panelOkCancel.PerformLayout();
            panelMainContainer.ResumeLayout(false);
            panelMainContainer.PerformLayout();
            ResumeLayout(false);
        }
        /// <summary>
        /// Handles value manipulation using the keyboard.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (colorChannelR.IsTyping || colorChannelG.IsTyping || colorChannelB.IsTyping &&
                colorChannelAlpha.IsTyping || colorChannelH.IsTyping || colorChannelS.IsTyping &&
                colorChannelValue.IsTyping)
            {
                return false;
            }

            if (keyData == Keys.Enter)
            {
                BttnOk_Click(null, null);
                return true;
            }
            else if (keyData == Keys.Escape)
            {
                BttnCancel_Click(null, null);
                return true;
            }

            return false;
        }

        private void TxtbxHexEntry_ColorUpdatedByText()
        {
            UpdateOnColorChange(null, false, true);
        }
        #endregion

        #region Methods (event handlers)
        /// <summary>
        /// Cancels and doesn't apply the preference changes.
        /// </summary>
        private void BttnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// Accepts and applies the preference changes.
        /// </summary>
        private void BttnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// Any color logic that gets set only once, dependent on the current theme, needs to subscribe to the theme
        /// changed event so it can be recalculated when theme preference loads from asynchronous user settings.
        /// </summary>
        private void HandleTheme()
        {
            BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            Refresh();
        }

        /// <summary>
        /// Updates all color-tracking controls based on the color being altered by any of them.
        /// </summary>
        private void UpdateOnColorChange(SliderSpecialType? sliderChanged, bool fromWheel = false, bool fromHex = false)
        {
            // Gets the modified color from the slider that was changed, or the current primary color
            Color newColor = chosenColor.Swatches[0];

            // Overwrites the color with the hex entry, if relevant.
            if (fromHex)
            {
                newColor = txtbxHexEntry.AssociatedColor;
            }

            // Overwrite the hue and saturation of the new color from the wheel, if picking color from it.
            if (fromWheel)
            {
                HsvColorF hsvCol = ColorUtils.HSVFFromBgra(newColor);
                hsvCol.Hue = colorWheel.HsvColor.Hue;
                hsvCol.Saturation = colorWheel.HsvColor.Saturation;

                // Users often have pure black as their color and expect the color to change to match the hue wheel,
                // but also expect it not to adjust value in other cases. So if it's pure black, change the value. This
                // also matches Paint.NET's native behavior.
                if (hsvCol.Value == 0)
                {
                    hsvCol.Value = colorWheel.HsvColor.Value;
                }

                newColor = ColorUtils.HSVFToBgra(hsvCol, newColor.A);
            }
            else
            {
                HsvColorF hsvCol = ColorUtils.HSVFFromBgra(newColor);
                colorWheel.HsvColor = new HsvColor(
                    (int)Math.Round(hsvCol.Hue),
                    (int)Math.Round(hsvCol.Saturation),
                    100);
            }

            if (sliderChanged == SliderSpecialType.RedGraph) { newColor = colorChannelR.GetColor(); }
            else if (sliderChanged == SliderSpecialType.GreenGraph) { newColor = colorChannelG.GetColor(); }
            else if (sliderChanged == SliderSpecialType.BlueGraph) { newColor = colorChannelB.GetColor(); }
            else if (sliderChanged == SliderSpecialType.AlphaGraph) { newColor = colorChannelAlpha.GetColor(); }
            else if (sliderChanged == SliderSpecialType.HueGraph) { newColor = colorChannelH.GetColor(); }
            else if (sliderChanged == SliderSpecialType.SatGraph) { newColor = colorChannelS.GetColor(); }
            else if (sliderChanged == SliderSpecialType.ValGraph) { newColor = colorChannelValue.GetColor(); }

            // Updates every slider that isn't invoking this change to the new color.
            if (sliderChanged != SliderSpecialType.RedGraph) { colorChannelR.SetColor(newColor); }
            if (sliderChanged != SliderSpecialType.GreenGraph) { colorChannelG.SetColor(newColor); }
            if (sliderChanged != SliderSpecialType.BlueGraph) { colorChannelB.SetColor(newColor); }
            if (isAlphaEnabled && sliderChanged != SliderSpecialType.AlphaGraph) { colorChannelAlpha.SetColor(newColor); }
            if (sliderChanged != SliderSpecialType.HueGraph) { colorChannelH.SetColor(newColor); }
            if (sliderChanged != SliderSpecialType.SatGraph) { colorChannelS.SetColor(newColor); }
            if (sliderChanged != SliderSpecialType.ValGraph) { colorChannelValue.SetColor(newColor); }

            // Updates the color box and associated color if it changed.
            if (sliderChanged != null || fromHex || fromWheel)
            {
                txtbxHexEntry.AssociatedColor = newColor;
                chosenColor.Swatches[0] = newColor;
                chosenColor.Refresh();
            }

            // Updates this dialog's color.
            associatedColor = newColor;
            ColorUpdated?.Invoke();
        }

        /// <summary>
        /// Updates controls based on whether the color dialog handles alpha.
        /// </summary>
        private void UpdateOnAlphaEnabledChange()
        {
            colorChannelAlpha.Visible = isAlphaEnabled;
            txtbxHexEntry.IncludeAlpha = isAlphaEnabled;
        }
        #endregion
    }
}