using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using ProjectBrowserPlus.Core;

namespace ProjectBrowserPlus.UI.Dialogs
{
    public partial class CreateViewsDialog : Window
    {
        public List<ElementId> LevelIds { get; } = new List<ElementId>();
        public ElementId ViewTypeId { get; private set; }
        public ElementId TemplateId { get; private set; } = ElementId.InvalidElementId;
        public string NameTemplate { get; private set; }

        public CreateViewsDialog(List<KeyValuePair<ElementId, string>> levels, List<KeyValuePair<ElementId, string>> types, List<KeyValuePair<ElementId, string>> templates, List<ElementId> preselect)
        {
            InitializeComponent();
            Levels.ItemsSource = levels;
            foreach (var l in levels) if (preselect.Contains(l.Key)) Levels.SelectedItems.Add(l);
            if (Levels.SelectedItems.Count == 0) Levels.SelectAll();
            Types.ItemsSource = types; Types.SelectedIndex = types.Count > 0 ? 0 : -1;
            var t = new List<KeyValuePair<ElementId, string>>(templates);
            t.Insert(0, new KeyValuePair<ElementId, string>(ElementId.InvalidElementId, L.T("cv.noTemplate")));
            Templates.ItemsSource = t; Templates.SelectedIndex = 0;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            LevelIds.Clear();
            foreach (var l in Levels.SelectedItems.Cast<KeyValuePair<ElementId, string>>()) LevelIds.Add(l.Key);
            if (LevelIds.Count == 0 || Types.SelectedItem == null) return;
            ViewTypeId = ((KeyValuePair<ElementId, string>)Types.SelectedItem).Key;
            TemplateId = Templates.SelectedItem is KeyValuePair<ElementId, string> kv ? kv.Key : ElementId.InvalidElementId;
            NameTemplate = NameBox.Text;
            DialogResult = true;
        }
    }
}
