using System;
using System.Windows.Forms;

namespace BrushFactory.Gui
{
    public partial class TextboxDialog : Form
    {
        private Func<string, string> validationFunc;

        public TextboxDialog(string titleText, string descrText, string btnOkText, Func<string, string> validateFunc)
        {
            InitializeComponent();
            Text = titleText;
            this.txtDescription.Text = descrText;
            this.bttnOk.Text = btnOkText;
            validationFunc = validateFunc;
            this.txtbxInput.TextChanged += TxtbxInput_TextChanged;
            this.AcceptButton.DialogResult = DialogResult.OK;
            this.Load += TxtbxInput_TextChanged; // Run validation when form is displayed.
        }

        /// <summary>
        /// Gets the text that the user submitted through the textbox field. Intended only to be called after the
        /// dialog has completed with a value of DialogResult.OK.
        /// </summary>
        public string GetSubmittedText()
        {
            return this.txtbxInput.Text;
        }

        /// <summary>
        /// Evaluates whether input is valid or not on each character change.
        /// </summary>
        private void TxtbxInput_TextChanged(object sender, EventArgs e)
        {
            // Runs the provided validation function. If it gives a non-null, non-empty string back, that is treated as
            // an error message and displayed. Otherwise, no error is considered to exist.
            string error = this.validationFunc(this.txtbxInput.Text);

            if (string.IsNullOrEmpty(error))
            {
                this.txtError.Visible = false;
                this.bttnOk.Enabled = true;
            }
            else
            {
                this.txtError.Visible = true;
                this.txtError.Text = error;
                this.bttnOk.Enabled = false;
            }
        }
    }
}
