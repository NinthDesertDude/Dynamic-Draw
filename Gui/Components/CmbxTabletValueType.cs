using System.Windows.Forms;

namespace DynamicDraw
{
    /// <summary>
    /// Determines how a value is applied in conjunction with an input, e.g. pressure sensitivity.
    /// </summary>
    public class CmbxTabletValueType : ThemedComboBox
    {
        public class CmbxEntry
        {
            public ConstraintValueHandlingMethod ValueMember { get; set; }
            public string DisplayMember { get; set; }
        }

        /// <summary>
        /// The common entries for this combobox type.
        /// </summary>
        private readonly CmbxEntry[] entries = new CmbxEntry[6]
        {
            new CmbxEntry() { ValueMember = ConstraintValueHandlingMethod.DoNothing, DisplayMember = Localization.Strings.ValueTypeNothing },
            new CmbxEntry() { ValueMember = ConstraintValueHandlingMethod.Add, DisplayMember = Localization.Strings.ValueTypeAdd },
            new CmbxEntry() { ValueMember = ConstraintValueHandlingMethod.AddPercent, DisplayMember = Localization.Strings.ValueTypeAddPercent },
            new CmbxEntry() { ValueMember = ConstraintValueHandlingMethod.AddPercentCurrent, DisplayMember = Localization.Strings.ValueTypeAddPercentCurrent },
            new CmbxEntry() { ValueMember = ConstraintValueHandlingMethod.MatchValue, DisplayMember = Localization.Strings.ValueTypeMatchValue },
            new CmbxEntry() { ValueMember = ConstraintValueHandlingMethod.MatchPercent, DisplayMember = Localization.Strings.ValueTypeMatchPercent }
        };

        public CmbxTabletValueType()
        {
            this.DropDownStyle = ComboBoxStyle.DropDownList;
            this.Items.AddRange(entries);
            this.SelectedItem = this.Items[0];
            FlatStyle = FlatStyle.Flat;
            DisplayMember = "DisplayMember";
            ValueMember = "ValueMember";
            IntegralHeight = false;
            ItemHeight = 13;
            DropDownHeight = 140;
            DropDownWidth = 20;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FormattingEnabled = true;
        }

        /// <summary>
        /// Loops through each entry and selects the matching handling method.
        /// </summary>
        public void SelectMatchingItem(ConstraintValueHandlingMethod handlingMethod)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (((CmbxEntry)Items[i]).ValueMember == handlingMethod)
                {
                    SelectedIndex = i;
                    break;
                }
            }
        }
    }
}
