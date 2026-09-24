using System.Windows;
using Autodesk.Revit.DB;
using ProjectBrowserPlus.Core;

namespace ProjectBrowserPlus.UI.Dialogs
{
    public partial class DuplicateDialog : Window
    {
        public ViewDuplicateOption Option { get; private set; } = ViewDuplicateOption.Duplicate;
        public int Copies { get; private set; } = 1;
        public string Suffix { get; private set; }

        public DuplicateDialog(int count)
        {
            InitializeComponent();
            Info.Text = string.Format(L.T("dup.info"), count);
            SuffixBox.Text = " - " + L.T("dup.copyWord");
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Option = ModeDetail.IsChecked == true ? ViewDuplicateOption.WithDetailing : ModeDependent.IsChecked == true ? ViewDuplicateOption.AsDependent : ViewDuplicateOption.Duplicate;
            if (!int.TryParse(CopiesBox.Text, out var c) || c < 1) c = 1;
            Copies = System.Math.Min(c, 50);
            Suffix = SuffixBox.Text;
            DialogResult = true;
        }
    }
}
