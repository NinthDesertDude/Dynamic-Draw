using System;
using System.Windows.Forms;

namespace BrushFactory.Gui
{
    /// <summary>
    /// Determines how a value is applied in conjunction with an input, e.g. pressure sensitivity.
    /// </summary>
    public class CmbxTabletValueType : ComboBox
    {
        /// <summary>
        /// The known ways that a value can be applied.
        /// </summary>
        public enum ValueHandlingMethod
        {
            /// <summary>
            /// Ignore the associated value. The setting isn't affected by the input in this case.
            /// </summary>
            DoNothing = 0,

            /// <summary>
            /// The (maybe negative) value is added to the setting based on a linear curve with the input strength.
            /// </summary>
            Add = 1,

            /// <summary>
            /// The (maybe negative) value, as a fraction of the setting's max range, is added to the setting based on
            /// a linear curve with the input strength.
            /// </summary>
            AddPercent = 2,

            /// <summary>
            /// The (maybe negative) value, as a fraction of the current setting value, is added to the setting based
            /// on a linear curve with the input strength.
            /// </summary>
            AddPercentCurrent = 3,

            /// <summary>
            /// The value is interpolated from 0% (current) to 100% (the associated value) on a linear curve with the
            /// input strength.
            /// </summary>
            MatchValue = 4,

            /// <summary>
            /// The value is interpolated from 0% (current) to 100% (the associated value * the max range) on a linear
            /// curve with the input strength.
            /// </summary>
            MatchPercent = 5
        }

        public class CmbxEntry
        {
            public ValueHandlingMethod ValueMember { get; set; }
            public string DisplayMember { get; set; }
        }

        public CmbxTabletValueType()
        {
            // Don't permanently generate more items over and over in winforms designer.
            if (!DesignMode)
            {
                this.DropDownStyle = ComboBoxStyle.DropDownList;
                this.Items.AddRange(GenerateItemOptions());
                this.SelectedItem = this.Items[0];
            }
        }

        /// <summary>
        /// Returns the usual combobox items.
        /// </summary>
        private CmbxEntry[] GenerateItemOptions()
        {
            return new CmbxEntry[6]
            {
                new CmbxEntry() { ValueMember = ValueHandlingMethod.DoNothing, DisplayMember = Localization.Strings.ValueTypeNothing },
                new CmbxEntry() { ValueMember = ValueHandlingMethod.Add, DisplayMember = Localization.Strings.ValueTypeAdd },
                new CmbxEntry() { ValueMember = ValueHandlingMethod.AddPercent, DisplayMember = Localization.Strings.ValueTypeAddPercent },
                new CmbxEntry() { ValueMember = ValueHandlingMethod.AddPercentCurrent, DisplayMember = Localization.Strings.ValueTypeAddPercentCurrent },
                new CmbxEntry() { ValueMember = ValueHandlingMethod.MatchValue, DisplayMember = Localization.Strings.ValueTypeMatchValue },
                new CmbxEntry() { ValueMember = ValueHandlingMethod.MatchPercent, DisplayMember = Localization.Strings.ValueTypeMatchPercent }
            };
        }
    }
}
