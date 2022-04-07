using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace DynamicDraw.Gui
{
    /// <summary>
    /// Determines how a value is applied in conjunction with an input, e.g. pressure sensitivity.
    /// </summary>
    public class CmbxTabletValueType : ComboBox
    {
        public class CmbxEntry
        {
            public ConstraintValueHandlingMethod ValueMember { get; set; }
            public string DisplayMember { get; set; }
        }

        public CmbxTabletValueType()
        {
            // Don't permanently generate more items over and over in winforms designer.
            if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
            {
                this.DropDownStyle = ComboBoxStyle.DropDownList;
                this.Items.AddRange(GenerateItemOptions());
                this.SelectedItem = this.Items[0];
            }
        }

        /// <summary>
        /// Returns the usual combobox items.
        /// </summary>
        private static CmbxEntry[] GenerateItemOptions()
        {
            return new CmbxEntry[6]
            {
                new CmbxEntry() { ValueMember = ConstraintValueHandlingMethod.DoNothing, DisplayMember = Localization.Strings.ValueTypeNothing },
                new CmbxEntry() { ValueMember = ConstraintValueHandlingMethod.Add, DisplayMember = Localization.Strings.ValueTypeAdd },
                new CmbxEntry() { ValueMember = ConstraintValueHandlingMethod.AddPercent, DisplayMember = Localization.Strings.ValueTypeAddPercent },
                new CmbxEntry() { ValueMember = ConstraintValueHandlingMethod.AddPercentCurrent, DisplayMember = Localization.Strings.ValueTypeAddPercentCurrent },
                new CmbxEntry() { ValueMember = ConstraintValueHandlingMethod.MatchValue, DisplayMember = Localization.Strings.ValueTypeMatchValue },
                new CmbxEntry() { ValueMember = ConstraintValueHandlingMethod.MatchPercent, DisplayMember = Localization.Strings.ValueTypeMatchPercent }
            };
        }
    }
}
