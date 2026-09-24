using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.UI.Dialogs
{
    public partial class RenumberDialog : Window
    {
        public class Row : INotifyPropertyChanged
        {
            private bool _on = true; private string _new;
            public BrowserItem Item;
            public bool On { get => _on; set { _on = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(On))); Owner?.Recalc(); } }
            public string Old { get; set; }
            public string New { get => _new; set { _new = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(New))); } }
            public string Name { get; set; }
            public RenumberDialog Owner;
            public event PropertyChangedEventHandler PropertyChanged;
        }

        private readonly ObservableCollection<Row> _rows;
        public List<PendingChange> Result { get; } = new List<PendingChange>();
        public bool ApplyNow { get; private set; }
        private bool _ready;

        public RenumberDialog(IList<BrowserItem> sheets)
        {
            InitializeComponent();
            _rows = new ObservableCollection<Row>(sheets.Select(s => new Row { Item = s, Old = s.EffectiveNumber, Name = s.EffectiveName, Owner = this }));
            Grid.ItemsSource = _rows;
            // guess prefix / digits from the first number
            var first = sheets.FirstOrDefault()?.EffectiveNumber ?? "";
            int i = 0; while (i < first.Length && !char.IsDigit(first[i])) i++;
            Prefix.Text = first.Substring(0, i);
            int j = i; while (j < first.Length && char.IsDigit(first[j])) j++;
            if (j > i) { Digits.Text = (j - i).ToString(); Start.Text = int.Parse(first.Substring(i, j - i)).ToString(); Suffix.Text = first.Substring(j); }
            _ready = true;
            Recalc();
        }

        private void Changed(object sender, RoutedEventArgs e) { if (_ready) Recalc(); }

        public void Recalc()
        {
            if (!_ready) return;
            if (!int.TryParse(Start.Text, out var n)) n = 1;
            if (!int.TryParse(Step.Text, out var step)) step = 1;
            if (!int.TryParse(Digits.Text, out var digits)) digits = 1;
            int changed = 0;
            foreach (var r in _rows)
            {
                if (!r.On) { r.New = r.Old; continue; }
                r.New = (Prefix.Text ?? "") + n.ToString().PadLeft(Math.Max(1, digits), '0') + (Suffix.Text ?? "");
                n += step;
                if (r.New != r.Old) changed++;
            }
            Summary.Text = string.Format(L.T("rn.summary"), changed, _rows.Count);
        }

        private void All_Click(object sender, RoutedEventArgs e) { foreach (var r in _rows) r._onSilent(true); Recalc(); Grid.Items.Refresh(); }
        private void None_Click(object sender, RoutedEventArgs e) { foreach (var r in _rows) r._onSilent(false); Recalc(); Grid.Items.Refresh(); }

        private List<Row> _treeOrder;
        private void KeepOrder_Click(object sender, RoutedEventArgs e)
        {
            if (_treeOrder == null) _treeOrder = _rows.ToList();
            var order = KeepOrder.IsChecked == true ? _treeOrder : _treeOrder.OrderBy(r => r.Old, NaturalComparer.Instance).ToList();
            _rows.Clear();
            foreach (var r in order) _rows.Add(r);
            Recalc();
        }

        private void Up_Click(object sender, RoutedEventArgs e) => Move(-1);
        private void Down_Click(object sender, RoutedEventArgs e) => Move(1);

        private void Move(int dir)
        {
            var sel = Grid.SelectedItems.Cast<Row>().ToList();
            if (sel.Count == 0) return;
            var idx = sel.Select(s => _rows.IndexOf(s)).OrderBy(x => x).ToList();
            if (dir < 0 && idx[0] == 0) return;
            if (dir > 0 && idx.Last() == _rows.Count - 1) return;
            foreach (var i in dir < 0 ? idx : Enumerable.Reverse(idx).ToList())
                _rows.Move(i, i + dir);
            foreach (var s in sel) Grid.SelectedItems.Add(s);
            Recalc();
        }

        private void Collect()
        {
            Result.Clear();
            foreach (var r in _rows.Where(r => r.On && r.New != r.Old && !string.IsNullOrWhiteSpace(r.New)))
                Result.Add(new PendingChange { Id = r.Item.Id, Item = r.Item, Kind = ChangeKind.Renumber, OldValue = r.Old, NewValue = r.New });
        }

        private void Draft_Click(object sender, RoutedEventArgs e) { Collect(); ApplyNow = false; DialogResult = true; }
        private void Apply_Click(object sender, RoutedEventArgs e) { Collect(); ApplyNow = true; DialogResult = true; }
    }

    internal static class RowExt
    {
        public static void _onSilent(this RenumberDialog.Row r, bool v)
        {
            var owner = r.Owner; r.Owner = null; r.On = v; r.Owner = owner;
        }
    }
}
