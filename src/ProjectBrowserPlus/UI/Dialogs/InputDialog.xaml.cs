using System.Windows;

namespace ProjectBrowserPlus.UI.Dialogs
{
    public partial class InputDialog : Window
    {
        public string Value { get; private set; }
        public InputDialog(string title, string info, string value)
        {
            InitializeComponent();
            Title = title; Info.Text = info; ValueBox.Text = value;
            Loaded += (s, e) => { ValueBox.Focus(); ValueBox.SelectAll(); };
        }
        private void Ok_Click(object sender, RoutedEventArgs e) { Value = ValueBox.Text; DialogResult = true; }
    }
}
