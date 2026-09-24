using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.UI.Dialogs
{
    public partial class RenameDialog : Window
    {
        public class Row { public BrowserItem Item; public string Old { get; set; } public string New { get; set; } public bool Changed => Old != New; }
        private readonly List<Row> _rows;
        public List<PendingChange> Result { get; } = new List<PendingChange>();
        public bool ApplyNow { get; private set; }
        private bool _ready;

        public RenameDialog(IList<BrowserItem> items)
        {
            InitializeComponent();
            _rows = items.Select(i => new Row { Item = i, Old = i.Kind == ItemKind.Sheet ? i.EffectiveName : i.EffectiveName, New = i.EffectiveName }).ToList();
            if (items.All(i => i.Kind == ItemKind.Sheet)) SheetNumbers.Visibility = Visibility.Visible;
            CaseMode.SelectedIndex = 0;
            Grid.DataContext = _rows;
            _ready = true;
            Recalc();
        }

        private void Changed(object sender, RoutedEventArgs e) { if (_ready) Recalc(); }

        private void Recalc()
        {
            var useNumbers = SheetNumbers.IsChecked == true;
            int.TryParse(Start.Text, out var start); if (!int.TryParse(Step.Text, out var step)) step = 1; if (!int.TryParse(Digits.Text, out var digits)) digits = 1;
            int n = start;
            int changed = 0;
            foreach (var r in _rows)
            {
                var src = useNumbers ? r.Item.EffectiveNumber : r.Item.EffectiveName;
                r.Old = src;
                var s = src ?? "";
                try
                {
                    if (!string.IsNullOrEmpty(Find.Text))
                    {
                        if (Regex.IsChecked == true)
                            s = System.Text.RegularExpressions.Regex.Replace(s, Find.Text, Replace.Text ?? "", CaseSens.IsChecked == true ? RegexOptions.None : RegexOptions.IgnoreCase);
                        else
                            s = ReplaceText(s, Find.Text, Replace.Text ?? "", CaseSens.IsChecked == true);
                    }
                }
                catch { }
                s = (Prefix.Text ?? "") + s + (Suffix.Text ?? "");
                if (Numbering.IsChecked == true)
                {
                    var num = n.ToString().PadLeft(Math.Max(1, digits), '0');
                    var pat = string.IsNullOrEmpty(Pattern.Text) ? "{name} {n}" : Pattern.Text;
                    s = pat.Replace("{name}", s).Replace("{n}", num);
                    n += step;
                }
                switch (CaseMode.SelectedIndex)
                {
                    case 1: s = s.ToUpperInvariant(); break;
                    case 2: s = s.ToLowerInvariant(); break;
                    case 3: s = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant()); break;
                }
                if (Trim.IsChecked == true) s = System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"\s{2,}", " ");
                r.New = s;
                if (r.Changed) changed++;
            }
            Grid.Items.Refresh();
            Summary.Text = string.Format(L.T("rn.summary"), changed, _rows.Count);
        }

        private static string ReplaceText(string s, string find, string repl, bool caseSensitive)
        {
            if (caseSensitive) return s.Replace(find, repl);
            var sb = new System.Text.StringBuilder(); int i = 0;
            while (true)
            {
                var j = s.IndexOf(find, i, StringComparison.OrdinalIgnoreCase);
                if (j < 0) { sb.Append(s.Substring(i)); break; }
                sb.Append(s, i, j - i).Append(repl); i = j + find.Length;
            }
            return sb.ToString();
        }

        private void Collect()
        {
            Result.Clear();
            var kind = SheetNumbers.IsChecked == true ? ChangeKind.Renumber : ChangeKind.Rename;
            foreach (var r in _rows.Where(r => r.Changed && !string.IsNullOrWhiteSpace(r.New)))
                Result.Add(new PendingChange { Id = r.Item.Id, Item = r.Item, Kind = kind, OldValue = r.Old, NewValue = r.New });
        }

        private void Draft_Click(object sender, RoutedEventArgs e) { Collect(); ApplyNow = false; DialogResult = true; }
        private void Apply_Click(object sender, RoutedEventArgs e) { Collect(); ApplyNow = true; DialogResult = true; }
    }
}
