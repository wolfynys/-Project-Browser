using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB;
using ProjectBrowserPlus.Core;

namespace ProjectBrowserPlus.UI.Dialogs
{
    public partial class CreateSheetsDialog : Window
    {
        public ElementId TitleBlockId { get; private set; }
        public string StartNumber { get; private set; }
        public string SheetName { get; private set; }
        public int Count { get; private set; } = 1;
        public bool Placeholder { get; private set; }
        public bool OpenFirst { get; private set; }

        public CreateSheetsDialog(List<KeyValuePair<ElementId, string>> titleBlocks, string suggestedNumber)
        {
            InitializeComponent();
            var list = new List<KeyValuePair<ElementId, string>>(titleBlocks);
            list.Insert(0, new KeyValuePair<ElementId, string>(ElementId.InvalidElementId, L.T("dlg.noTitleblock")));
            TitleBlock.ItemsSource = list;
            TitleBlock.SelectedIndex = list.Count > 1 ? 1 : 0;
            Number.Text = suggestedNumber;
            NameBox.Text = L.T("cs.defaultName");
            Number.Focus(); Number.SelectAll();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            TitleBlockId = TitleBlock.SelectedItem is KeyValuePair<ElementId, string> kv ? kv.Key : ElementId.InvalidElementId;
            StartNumber = string.IsNullOrWhiteSpace(Number.Text) ? "1" : Number.Text.Trim();
            SheetName = NameBox.Text;
            if (!int.TryParse(CountBox.Text, out var c) || c < 1) c = 1;
            Count = System.Math.Min(c, 500);
            Placeholder = PlaceholderBox.IsChecked == true;
            OpenFirst = OpenBox.IsChecked == true && !Placeholder;
            DialogResult = true;
        }
    }
}
