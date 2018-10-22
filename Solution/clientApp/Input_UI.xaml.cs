using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Autodesk.Inventor.IO
{
    /// <summary>
    /// Interaction logic for Input_UI.xaml
    /// </summary>
    public partial class Input_UI : Window
    {
        public string Input_height { get; set; } = "16";
        public string Input_width { get; set; } = "12";

        public bool IsCancelled { get; set; } = false;
        public Input_UI()
        {
            InitializeComponent();
        }
        private void Cancel_btn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            IsCancelled = true;
        }

        private void width_textbox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            bool approvedDecimalPoint = false;

            if (e.Text == ".")
            {
                if (!((TextBox)sender).Text.Contains("."))
                    approvedDecimalPoint = true;
            }

            if (!(char.IsDigit(e.Text, e.Text.Length - 1) || approvedDecimalPoint))
                e.Handled = true;
        }

        private void height_textbox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            bool approvedDecimalPoint = false;

            if (e.Text == ".")
            {
                if (!((TextBox)sender).Text.Contains("."))
                    approvedDecimalPoint = true;
            }

            if (!(char.IsDigit(e.Text, e.Text.Length - 1) || approvedDecimalPoint))
                e.Handled = true;
        }

        private void Apply_btn_Click(object sender, RoutedEventArgs e)
        {
            // MessageBox.Show("Hieght:" + height_textbox.Text + "Width:" + width_textbox.Text);
            if (height_textbox.Text.Trim() != string.Empty && width_textbox.Text.Trim() != string.Empty)
            {
                Input_height = height_textbox.Text.Trim();
                Input_width = width_textbox.Text.Trim();
            }
            this.Close();
        }
    }
}
