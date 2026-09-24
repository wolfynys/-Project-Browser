using System.Windows;
using System.Windows.Controls;
using ProjectBrowserPlus.Core;

namespace ProjectBrowserPlus.UI.Dialogs
{
    public partial class SettingsDialog : Window
    {
        public SettingsDialog()
        {
            InitializeComponent();
            var s = Settings.Current;
            Lang.SelectedIndex = s.Language == "en" ? 1 : 0;
            Follow.IsChecked = s.FollowActiveView; SingleClick.IsChecked = s.OpenOnSingleClick; CloseOthers.IsChecked = s.CloseOthersOnOpen;
            Confirm.IsChecked = s.ConfirmDelete; RememberTab.IsChecked = s.RememberLastTab;
            Badges.IsChecked = s.ShowBadges; Natural.IsChecked = s.NaturalSort; CompactRows.IsChecked = s.CompactRows; SheetChildren.IsChecked = s.ShowSheetViewsAsChildren;
            AutoPreview.IsChecked = s.AutoFamilyPreviews; CountInst.IsChecked = s.CountInstances;
            PreviewSize.Text = s.PreviewSize.ToString(); MaxRecent.Text = s.MaxRecent.ToString();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var s = Settings.Current;
            s.Language = (Lang.SelectedItem as ComboBoxItem)?.Tag as string ?? "ru";
            s.FollowActiveView = Follow.IsChecked == true; s.OpenOnSingleClick = SingleClick.IsChecked == true; s.CloseOthersOnOpen = CloseOthers.IsChecked == true;
            s.ConfirmDelete = Confirm.IsChecked == true; s.RememberLastTab = RememberTab.IsChecked == true;
            s.ShowBadges = Badges.IsChecked == true; s.NaturalSort = Natural.IsChecked == true; s.CompactRows = CompactRows.IsChecked == true; s.ShowSheetViewsAsChildren = SheetChildren.IsChecked == true;
            s.AutoFamilyPreviews = AutoPreview.IsChecked == true; s.CountInstances = CountInst.IsChecked == true;
            if (int.TryParse(PreviewSize.Text, out var ps)) s.PreviewSize = System.Math.Max(32, System.Math.Min(256, ps));
            if (int.TryParse(MaxRecent.Text, out var mr)) s.MaxRecent = System.Math.Max(3, System.Math.Min(100, mr));
            s.Save();
            DialogResult = true;
        }

        private void Folder_Click(object sender, RoutedEventArgs e)
        {
            try { System.IO.Directory.CreateDirectory(Log.Dir); System.Diagnostics.Process.Start("explorer.exe", Log.Dir); } catch { }
        }
    }
}
