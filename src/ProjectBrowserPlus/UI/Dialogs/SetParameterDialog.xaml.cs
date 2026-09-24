using System.Collections.Generic;
using System.Windows;
using ProjectBrowserPlus.Core;

namespace ProjectBrowserPlus.UI.Dialogs
{
    public partial class SetParameterDialog : Window
    {
        public string ParameterName { get; private set; }
        public string Value { get; private set; }

        public SetParameterDialog(List<string> names, int count)
        {
            InitializeComponent();
            Params.ItemsSource = names;
            if (names.Count > 0) Params.SelectedIndex = 0;
            Info.Text = string.Format(L.T("sp.info"), count);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ParameterName = (Params.SelectedItem as string) ?? Params.Text;
            if (string.IsNullOrWhiteSpace(ParameterName)) return;
            Value = ValueBox.Text;
            DialogResult = true;
        }
    }
}
