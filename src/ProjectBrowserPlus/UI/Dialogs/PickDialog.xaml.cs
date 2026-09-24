using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;

namespace ProjectBrowserPlus.UI.Dialogs
{
    public partial class PickDialog : Window
    {
        private readonly List<KeyValuePair<ElementId, string>> _all;
        public List<ElementId> SelectedIds { get; } = new List<ElementId>();
        public string OptionText { set { Option.Content = value; Option.Visibility = System.Windows.Visibility.Visible; } }
        public bool OptionChecked { get => Option.IsChecked == true; set => Option.IsChecked = value; }

        public PickDialog(string title, List<KeyValuePair<ElementId, string>> items, bool multi)
        {
            InitializeComponent();
            Title = title;
            _all = items;
            List.SelectionMode = multi ? SelectionMode.Extended : SelectionMode.Single;
            AllBtn.Visibility = multi ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            Fill();
            Filter.Focus();
        }

        private void Fill()
        {
            var f = Filter.Text?.Trim();
            var sel = List.SelectedItems.Cast<KeyValuePair<ElementId, string>>().Select(k => k.Key.IntegerValue).ToHashSet();
            List.ItemsSource = string.IsNullOrEmpty(f) ? _all : _all.Where(k => k.Value.IndexOf(f, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            foreach (var it in (IEnumerable<KeyValuePair<ElementId, string>>)List.ItemsSource) if (sel.Contains(it.Key.IntegerValue)) List.SelectedItems.Add(it);
        }

        private void Filter_TextChanged(object sender, TextChangedEventArgs e) => Fill();
        private void All_Click(object sender, RoutedEventArgs e) => List.SelectAll();
        private void List_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (List.SelectedItem != null) Ok_Click(sender, e); }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            SelectedIds.Clear();
            foreach (var it in List.SelectedItems.Cast<KeyValuePair<ElementId, string>>()) SelectedIds.Add(it.Key);
            DialogResult = true;
        }
    }
}
