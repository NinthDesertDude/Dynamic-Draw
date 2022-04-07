using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace DynamicDraw
{
    public partial class DynamicConstraintEntry : UserControl
    {
        private int a;

        public DynamicConstraintEntry(int a)
        {

        }

        /// <summary>
        /// Internal only, do not call.
        /// </summary>
        public DynamicConstraintEntry()
        {
            InitializeComponent();
        }
    }
}
