using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Settings = ProjectBrowserPlus.Core.Settings;
using Autodesk.Revit.UI;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;
using ProjectBrowserPlus.Sections;
using ProjectBrowserPlus.Services;
using ProjectBrowserPlus.UI.Dialogs;

namespace ProjectBrowserPlus.UI
{
    public class TabVm : INotifyPropertyChanged
    {
        private bool _isSelected;
        public Section Section { get; set; }
        public string Key => Section.Key;
        public string Icon => Section.Icon;
        public string Title => Section.Title;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class SortVm
    {
        public SortMode Mode { get; set; }
        public string Title { get; set; }
        public string Icon { get; set; }
        public override string ToString() => Title;
    }

    /// <summary>All browser logic. Revit API work is delegated to RevitTask/RevitActions.</summary>
    public class BrowserViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        private readonly DispatcherTimer _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        private readonly DispatcherTimer _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        private List<BrowserItem> _roots = new List<BrowserItem>();
        private ObservableCollection<BrowserItem> _rows = new ObservableCollection<BrowserItem>();
        private string _search = "", _status = "", _projectName = "", _previewInfo;
        private TabVm _selectedTab;
        private GroupOption _selectedGroup;
        private SortVm _selectedSort;
        private bool _busy, _dirty, _isEmpty = true, _showPreview, _previewBusy;
        private ImageSource _previewImage;
        private BrowserItem _previewItem;
        private Dictionary<BrowserItem, bool> _savedExpansion;
        private string _docKey;
        private bool _loaded;

        public ObservableCollection<TabVm> Tabs { get; } = new ObservableCollection<TabVm>();
        public ObservableCollection<GroupOption> GroupOptions { get; } = new ObservableCollection<GroupOption>();
        public ObservableCollection<QuickFilter> Filters { get; } = new ObservableCollection<QuickFilter>();
        public ObservableCollection<PendingChange> Pending { get; } = new ObservableCollection<PendingChange>();
        public List<SortVm> SortModes { get; } = new List<SortVm>
        {
            new SortVm { Mode = SortMode.NameAsc, Title = L.T("sort.nameAsc"), Icon = "arrow-down-a-z" },
            new SortVm { Mode = SortMode.NameDesc, Title = L.T("sort.nameDesc"), Icon = "arrow-up-z-a" },
            new SortVm { Mode = SortMode.NumberAsc, Title = L.T("sort.numberAsc"), Icon = "arrow-down-0-1" },
            new SortVm { Mode = SortMode.NumberDesc, Title = L.T("sort.numberDesc"), Icon = "arrow-down-0-1" },
            new SortVm { Mode = SortMode.IdAsc, Title = L.T("sort.id"), Icon = "hash" },
        };

        public ObservableCollection<BrowserItem> Rows { get => _rows; private set { _rows = value; Raise(); } }

        /// <summary>Asks the view to make the ListBox selection match BrowserItem.IsSelected.</summary>
        public event Action SelectionSyncRequested;

        /// <summary>Select exactly these items (everything else, visible or not, is deselected).</summary>
        public void SelectOnly(IEnumerable<BrowserItem> items)
        {
            var set = new HashSet<BrowserItem>(items);
            foreach (var r in _roots) foreach (var n in Enumerable.Repeat(r, 1).Concat(r.Descendants())) n.IsSelected = set.Contains(n);
            SelectionSyncRequested?.Invoke();
            RebuildStatusOnly();
        }
        public string Search { get => _search; set { if (_search != value) { _search = value ?? ""; Raise(); Raise(nameof(HasSearch)); _searchTimer.Stop(); _searchTimer.Start(); } } }
        public bool HasSearch => !string.IsNullOrEmpty(_search);
        public string Status { get => _status; set { _status = value; Raise(); } }
        public string ProjectName { get => _projectName; set { _projectName = value; Raise(); } }
        public bool Busy { get => _busy; set { _busy = value; Raise(); } }
        public bool IsEmpty { get => _isEmpty; set { _isEmpty = value; Raise(); } }
        public bool HasPending => Pending.Count > 0;
        public string PendingText => string.Format(L.T("draft.count"), Pending.Count);
        public bool ShowPreview { get => _showPreview; set { _showPreview = value; Raise(); if (value) UpdatePreview(); } }
        public ImageSource PreviewImage { get => _previewImage; set { _previewImage = value; Raise(); Raise(nameof(HasPreviewImage)); } }
        public bool HasPreviewImage => _previewImage != null;
        public string PreviewInfo { get => _previewInfo; set { _previewInfo = value; Raise(); } }
        public bool PreviewBusy { get => _previewBusy; set { _previewBusy = value; Raise(); } }
        public bool CanGeneratePreview => _previewItem != null && _previewItem.IsViewLike && !PreviewBusy;
        public bool ShowBadges => Settings.Current.ShowBadges;
        public bool Compact => Settings.Current.CompactRows;
        public double RowHeight => Settings.Current.CompactRows ? 22 : 26;

        public TabVm SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab == value || value == null) return;
                foreach (var t in Tabs) t.IsSelected = t == value;
                _selectedTab = value;
                Raise(); Raise(nameof(SupportsGrouping)); Raise(nameof(SearchHint)); Raise(nameof(IsFamiliesTab)); Raise(nameof(IsSheetsTab)); Raise(nameof(IsViewsTab)); Raise(nameof(IsLevelsTab));
                Settings.Current.LastTab = value.Key; Settings.Current.Save();
                LoadGroupOptions();
                RequestRefresh(true);
            }
        }

        public bool SupportsGrouping => _selectedTab?.Section.SupportsGrouping ?? false;
        public string SearchHint => _selectedTab?.Section.SearchHint ?? "";
        public bool IsFamiliesTab => _selectedTab?.Key == "families";
        public bool IsSheetsTab => _selectedTab?.Key == "sheets";
        public bool IsViewsTab => _selectedTab?.Key == "views" || _selectedTab?.Key == "all";
        public bool IsLevelsTab => _selectedTab?.Key == "levels";

        public GroupOption SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (value == null || _selectedGroup == value) return;
                _selectedGroup = value; Raise();
                if (_selectedTab != null) { Settings.Current.GroupByPerTab[_selectedTab.Key] = value.Key; Settings.Current.Save(); }
                if (_loaded) RequestRefresh(true);
            }
        }

        public SortVm SelectedSort
        {
            get => _selectedSort;
            set { if (value == null || _selectedSort == value) return; _selectedSort = value; Raise(); if (_loaded) RequestRefresh(true); }
        }

        // ------------------------------------------------------------------ commands
        public ICommand RefreshCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand ToggleFilterCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand ToggleExpandCommand { get; }
        public ICommand ApplyDraftCommand { get; }
        public ICommand DiscardDraftCommand { get; }
        public ICommand GeneratePreviewCommand { get; }
        public ICommand TogglePreviewCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand GoToCurrentCommand { get; }

        public BrowserViewModel()
        {
            foreach (var s in new Section[] { new AllSection(), new ViewsSection(), new SheetsSection(), new SchedulesSection(), new LegendsSection(), new FamiliesSection(), new GroupsSection(), new LinksSection(), new LevelsSection(), new FavoritesSection() })
                Tabs.Add(new TabVm { Section = s });
            _selectedSort = SortModes[0];
            _refreshTimer.Tick += (s, e) => { _refreshTimer.Stop(); if (_dirty) RequestRefresh(false); };
            _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); ApplyFilter(); };
            Pending.CollectionChanged += (s, e) => { Raise(nameof(HasPending)); Raise(nameof(PendingText)); };

            RefreshCommand = new RelayCommand(() => RequestRefresh(true));
            ExpandAllCommand = new RelayCommand(() => SetExpanded(true));
            CollapseAllCommand = new RelayCommand(() => SetExpanded(false));
            ClearSearchCommand = new RelayCommand(() => Search = "");
            ToggleFilterCommand = new RelayCommand(p => { if (p is QuickFilter f) { f.IsActive = !f.IsActive; ApplyFilter(); Raise(nameof(ActiveFilterCount)); } });
            OpenCommand = new RelayCommand(p => Activate(p as BrowserItem));
            ToggleFavoriteCommand = new RelayCommand(p => ToggleFavorite(p as BrowserItem));
            ToggleExpandCommand = new RelayCommand(p => { if (p is BrowserItem i && i.HasChildren) { i.IsExpanded = !i.IsExpanded; RebuildRows(); } });
            ApplyDraftCommand = new RelayCommand(ApplyDraft, () => Pending.Count > 0);
            DiscardDraftCommand = new RelayCommand(DiscardDraft, () => Pending.Count > 0);
            GeneratePreviewCommand = new RelayCommand(() => GenerateViewPreview(true), () => CanGeneratePreview);
            TogglePreviewCommand = new RelayCommand(() => ShowPreview = !ShowPreview);
            SettingsCommand = new RelayCommand(OpenSettings);
            GoToCurrentCommand = new RelayCommand(() => RevealCurrent(true));

            RevitEvents.DocumentChanged += () => { _dirty = true; _refreshTimer.Stop(); _refreshTimer.Start(); };
            RevitEvents.ViewActivated += id => OnViewActivated(id);
            RevitEvents.DocumentSwitched += () => { _dirty = true; PreviewService.ClearMemory(); _refreshTimer.Stop(); _refreshTimer.Start(); };

            var last = Settings.Current.RememberLastTab ? Settings.Current.LastTab : "all";
            var tab = Tabs.FirstOrDefault(t => t.Key == last) ?? Tabs[0];
            _selectedTab = tab; tab.IsSelected = true;
            LoadGroupOptions();
            _loaded = true;
        }

        public int ActiveFilterCount => Filters.Count(f => f.IsActive);

        private void LoadGroupOptions()
        {
            GroupOptions.Clear();
            Filters.Clear();
            if (_selectedTab == null) return;
            foreach (var g in _selectedTab.Section.GroupOptions) GroupOptions.Add(g);
            foreach (var f in _selectedTab.Section.Filters) Filters.Add(f);
            Raise(nameof(ActiveFilterCount));
            Raise(nameof(HasFilters));
            var key = Settings.Current.GroupByPerTab.TryGetValue(_selectedTab.Key, out var k) ? k : _selectedTab.Section.DefaultGroup;
            _selectedGroup = GroupOptions.FirstOrDefault(g => g.Key == key) ?? GroupOptions.FirstOrDefault();
            Raise(nameof(SelectedGroup));
        }

        public bool HasFilters => Filters.Count > 0;

        // ------------------------------------------------------------------ refresh
        public void RequestRefresh(bool force)
        {
            _dirty = false;
            if (Busy && !force) { _dirty = true; _refreshTimer.Start(); return; }
            Busy = true;
            var tab = _selectedTab;
            var group = _selectedGroup?.Key;
            var sort = _selectedSort?.Mode ?? SortMode.NameAsc;
            RevitTask.Run(app =>
            {
                List<BrowserItem> roots = null; string project = ""; string docKey = null; string error = null;
                try
                {
                    var uidoc = app.ActiveUIDocument;
                    if (uidoc?.Document != null)
                    {
                        var doc = uidoc.Document;
                        project = doc.Title + (doc.IsFamilyDocument ? "  (" + L.T("family.doc") + ")" : "");
                        docKey = Favorites.KeyFor(doc);
                        var ctx = new BuildContext(uidoc);
                        roots = tab.Section.BuildTree(ctx, group, sort);
                    }
                }
                catch (Exception ex) { error = ex.Message; Log.Error("build " + tab.Key, ex); }
                OnBuilt(roots, project, docKey, error);
            });
        }

        private void OnBuilt(List<BrowserItem> roots, string project, string docKey, string error)
        {
            Busy = false;
            ProjectName = project;
            if (roots == null)
            {
                _roots = new List<BrowserItem>();
                Rows = new ObservableCollection<BrowserItem>();
                IsEmpty = true;
                Status = error ?? L.T("status.noDocument");
                return;
            }
            foreach (var r in roots) AssignLevels(r, 0);
            // keep expansion state of same-named folders and the selection between refreshes
            var oldExp = new HashSet<string>();
            foreach (var r in _roots) CollectExpanded(r, "", oldExp);
            var oldSel = new HashSet<int>(_rows.Where(i => i.IsSelected && i.IsElement).Select(i => i.IdValue));
            var sameDoc = docKey == _docKey;
            _docKey = docKey;
            if (!sameDoc) { Pending.Clear(); _savedExpansion = null; }
            _roots = roots;
            if (sameDoc && oldExp.Count > 0) foreach (var r in _roots) RestoreExpanded(r, "", oldExp);
            else
            {
                var total = _roots.Sum(r => r.IsFolder ? r.Count : 1);
                foreach (var r in _roots) if (r.IsFolder && (total <= 400 || _roots.Count <= 3)) r.IsExpanded = true;
            }
            if (sameDoc && oldSel.Count > 0)
                foreach (var r in _roots) foreach (var it in Enumerable.Repeat(r, 1).Concat(r.Descendants()))
                    if (it.IsElement && oldSel.Contains(it.IdValue) && it.Parent?.Kind != ItemKind.Sheet) it.IsSelected = true;
            // re-attach pending (draft) values
            if (Pending.Count > 0)
            {
                var byId = new Dictionary<int, PendingChange>();
                foreach (var p in Pending) byId[p.Id.IntegerValue * 2 + (p.Kind == ChangeKind.Renumber ? 1 : 0)] = p;
                foreach (var r in _roots) foreach (var it in Enumerable.Repeat(r, 1).Concat(r.Descendants()))
                {
                    if (byId.TryGetValue(it.IdValue * 2, out var pn)) { it.PendingName = pn.NewValue; pn.Item = it; }
                    if (byId.TryGetValue(it.IdValue * 2 + 1, out var pr)) { it.PendingNumber = pr.NewValue; pr.Item = it; }
                }
            }
            IsEmpty = _roots.Count == 0;
            ApplyFilter();
            if (Settings.Current.FollowActiveView) RevealCurrent(false);
        }

        private static void AssignLevels(BrowserItem n, int level)
        {
            n.Level = level;
            foreach (var c in n.Children) { c.Parent = n; AssignLevels(c, level + 1); }
        }

        private static void CollectExpanded(BrowserItem n, string path, HashSet<string> set)
        {
            var p = path + "/" + (n.IsFolder ? n.Name : n.IdValue.ToString());
            if (n.IsExpanded) set.Add(p);
            foreach (var c in n.Children) CollectExpanded(c, p, set);
        }

        private static void RestoreExpanded(BrowserItem n, string path, HashSet<string> set)
        {
            var p = path + "/" + (n.IsFolder ? n.Name : n.IdValue.ToString());
            n.IsExpanded = set.Contains(p);
            foreach (var c in n.Children) RestoreExpanded(c, p, set);
        }

        // ------------------------------------------------------------------ filtering
        private static string[] Tokens(string s) => (s ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        public void ApplyFilter()
        {
            var tokens = Tokens(_search).Select(t => t.ToLowerInvariant()).ToArray();
            var active = Filters.Where(f => f.IsActive).ToList();
            var searching = tokens.Length > 0;
            if (searching && _savedExpansion == null)
            {
                _savedExpansion = new Dictionary<BrowserItem, bool>();
                foreach (var r in _roots) foreach (var n in Enumerable.Repeat(r, 1).Concat(r.Descendants())) if (n.HasChildren) _savedExpansion[n] = n.IsExpanded;
            }
            if (!searching && _savedExpansion != null)
            {
                foreach (var kv in _savedExpansion) kv.Key.IsExpanded = kv.Value;
                _savedExpansion = null;
            }
            foreach (var r in _roots) Match(r, tokens, active, searching);
            RebuildRows();
        }

        private static bool Match(BrowserItem n, string[] tokens, List<QuickFilter> filters, bool searching)
        {
            bool selfText = tokens.Length == 0 || TextMatches(n, tokens);
            bool selfFilter = n.IsFolder || filters.Count == 0 || filters.All(f => f.Predicate(n));
            bool anyChild = false;
            foreach (var c in n.Children) if (Match(c, tokens, filters, searching)) anyChild = true;
            bool m;
            if (n.IsFolder) m = anyChild;
            else m = (selfText && selfFilter) || anyChild;
            if (n.IsFolder && selfText && tokens.Length > 0 && !anyChild)
            {
                // folder name matched: reveal its (filter-passing) children
                foreach (var c in n.Children) ForceMatch(c, filters);
                m = n.Children.Any(c => c.IsMatch);
            }
            n.IsMatch = m;
            if (searching && m && n.HasChildren) n.IsExpanded = true;
            return m;
        }

        private static void ForceMatch(BrowserItem n, List<QuickFilter> filters)
        {
            var pass = n.IsFolder || filters.Count == 0 || filters.All(f => f.Predicate(n));
            foreach (var c in n.Children) ForceMatch(c, filters);
            n.IsMatch = pass || n.Children.Any(c => c.IsMatch);
        }

        private static bool TextMatches(BrowserItem n, string[] tokens)
        {
            foreach (var t in tokens)
            {
                if (t.StartsWith("id:") && int.TryParse(t.Substring(3), out var id)) { if (n.IdValue != id) return false; continue; }
                if (t == "*") { if (!n.IsFavorite) return false; continue; }
                bool hit = Contains(n.Name, t) || Contains(n.Number, t) || Contains(n.SearchText, t) || Contains(n.PendingName, t) || Contains(n.PendingNumber, t);
                if (!hit) return false;
            }
            return true;
        }

        private static bool Contains(string s, string t) => !string.IsNullOrEmpty(s) && s.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0;

        public void RebuildRows()
        {
            var list = new List<BrowserItem>(Math.Max(64, _rows.Count));
            foreach (var r in _roots) AddVisible(r, list);
            // replacing ItemsSource makes the ListBox report every old row as deselected; keep what is still visible
            var keep = new HashSet<BrowserItem>(_rows.Where(i => i.IsSelected));
            ApplyRows(list);
            foreach (var i in list) if (keep.Contains(i)) i.IsSelected = true;
            SelectionSyncRequested?.Invoke();
            var leaves = list.Count(i => !i.IsFolder);
            var total = _roots.Sum(r => r.IsFolder ? r.Count : 1);
            var sel = list.Count(i => i.IsSelected);
            Status = (HasSearch || ActiveFilterCount > 0 ? string.Format(L.T("status.filtered"), leaves, total) : string.Format(L.T("status.count"), total)) + (sel > 0 ? "  ·  " + string.Format(L.T("status.selected"), sel) : "");
        }

        /// <summary>Minimal-diff update of the visible rows so the ListBox keeps its scroll position.</summary>
        private void ApplyRows(List<BrowserItem> list)
        {
            var old = _rows;
            int prefix = 0;
            while (prefix < old.Count && prefix < list.Count && ReferenceEquals(old[prefix], list[prefix])) prefix++;
            int suffix = 0;
            while (suffix < old.Count - prefix && suffix < list.Count - prefix && ReferenceEquals(old[old.Count - 1 - suffix], list[list.Count - 1 - suffix])) suffix++;
            int removeCount = old.Count - prefix - suffix;
            int insertCount = list.Count - prefix - suffix;
            if (removeCount + insertCount > 1500 || old.Count == 0)
            {
                Rows = new ObservableCollection<BrowserItem>(list);
                return;
            }
            for (int i = 0; i < removeCount; i++) old.RemoveAt(prefix);
            for (int i = 0; i < insertCount; i++) old.Insert(prefix + i, list[prefix + i]);
        }

        private static void AddVisible(BrowserItem n, List<BrowserItem> list)
        {
            if (!n.IsMatch) return;
            list.Add(n);
            if (n.IsExpanded) foreach (var c in n.Children) AddVisible(c, list);
        }

        public void SetExpanded(bool value)
        {
            foreach (var r in _roots) foreach (var n in Enumerable.Repeat(r, 1).Concat(r.Descendants())) if (n.HasChildren) n.IsExpanded = value;
            RebuildRows();
        }

        public void UpdateSelectionStatus() => RebuildStatusOnly();

        private void RebuildStatusOnly()
        {
            var sel = _rows.Count(i => i.IsSelected);
            var leaves = _rows.Count(i => !i.IsFolder);
            var total = _roots.Sum(r => r.IsFolder ? r.Count : 1);
            Status = (HasSearch || ActiveFilterCount > 0 ? string.Format(L.T("status.filtered"), leaves, total) : string.Format(L.T("status.count"), total)) + (sel > 0 ? "  ·  " + string.Format(L.T("status.selected"), sel) : "");
            UpdatePreview();
        }

        // ------------------------------------------------------------------ selection helpers
        public List<BrowserItem> SelectedItems => _rows.Where(i => i.IsSelected).ToList();

        /// <summary>Selected leaves, plus all leaves of selected folders (for batch operations).</summary>
        public List<BrowserItem> TargetItems(Func<BrowserItem, bool> filter = null)
        {
            var set = new List<BrowserItem>();
            var seen = new HashSet<int>();
            void add(BrowserItem i)
            {
                if (i.IsFolder) { foreach (var c in i.Children) add(c); return; }
                if (filter != null && !filter(i)) return;
                if (i.IsElement && seen.Add(i.IdValue)) set.Add(i);
            }
            foreach (var s in SelectedItems) add(s);
            return set;
        }

        public BrowserItem Find(int id) => _roots.SelectMany(r => Enumerable.Repeat(r, 1).Concat(r.Descendants())).FirstOrDefault(i => i.IdValue == id && !i.IsFolder);

        public event Action<BrowserItem> ScrollIntoViewRequested;

        public void Reveal(BrowserItem it, bool select)
        {
            if (it == null) return;
            for (var p = it.Parent; p != null; p = p.Parent) p.IsExpanded = true;
            it.IsMatch = true;
            RebuildRows();
            if (select) SelectOnly(new[] { it });
            ScrollIntoViewRequested?.Invoke(it);
        }

        private int _activeViewId = -1;
        private void OnViewActivated(int id)
        {
            _activeViewId = id;
            foreach (var r in _roots) foreach (var n in Enumerable.Repeat(r, 1).Concat(r.Descendants()))
            {
                if (n.Kind == ItemKind.View || n.Kind == ItemKind.Sheet || n.Kind == ItemKind.Schedule || n.Kind == ItemKind.Legend)
                    n.IsCurrent = n.IdValue == id;
            }
            if (Settings.Current.FollowActiveView && !HasSearch) RevealCurrent(false);
        }

        public void RevealCurrent(bool select)
        {
            var it = _roots.SelectMany(r => Enumerable.Repeat(r, 1).Concat(r.Descendants())).FirstOrDefault(i => i.IsCurrent && (i.Kind == ItemKind.View || i.Kind == ItemKind.Sheet || i.Kind == ItemKind.Schedule || i.Kind == ItemKind.Legend) && i.Parent?.Kind != ItemKind.Sheet && i.Parent?.Kind != ItemKind.Level);
            if (it == null) it = _roots.SelectMany(r => r.Descendants()).FirstOrDefault(i => i.IsCurrent);
            if (it != null) Reveal(it, select);
        }

        // ------------------------------------------------------------------ item actions
        public void Activate(BrowserItem it)
        {
            if (it == null) return;
            if (it.IsFolder || (it.HasChildren && it.Kind == ItemKind.Family) || it.Kind == ItemKind.Level && !it.IsElement)
            {
                it.IsExpanded = !it.IsExpanded; RebuildRows(); return;
            }
            switch (it.Kind)
            {
                case ItemKind.View:
                case ItemKind.Sheet:
                case ItemKind.Schedule:
                case ItemKind.Legend:
                    Run(app => RevitActions.OpenView(app, it.Id));
                    break;
                case ItemKind.FamilyType:
                    if (it.IsPlaceable) Run(app => Report(RevitActions.PlaceType(app, it.Id)));
                    break;
                case ItemKind.Family:
                    it.IsExpanded = !it.IsExpanded; RebuildRows();
                    break;
                case ItemKind.Level:
                    it.IsExpanded = !it.IsExpanded; RebuildRows();
                    break;
                case ItemKind.Group:
                case ItemKind.Link:
                    Run(app => { var ids = RevitActions.InstancesOf(app.ActiveUIDocument.Document, it, null); RevitActions.Select(app, ids); RevitActions.ShowElements(app, ids); });
                    break;
            }
        }

        public void ToggleFavorite(BrowserItem it)
        {
            if (it == null || it.UniqueId == null) return;
            var items = SelectedItems.Contains(it) ? SelectedItems.Where(i => i.UniqueId != null).ToList() : new List<BrowserItem> { it };
            var value = !it.IsFavorite;
            Run(app =>
            {
                var fav = Favorites.For(app.ActiveUIDocument.Document);
                foreach (var i in items) { fav.Set(i.UniqueId, value); i.IsFavorite = value; }
            });
        }

        public void SetNote(BrowserItem it, string note)
        {
            if (it?.UniqueId == null) return;
            Run(app => { Favorites.For(app.ActiveUIDocument.Document).SetNote(it.UniqueId, note); it.Note = note; it.Tooltip = (it.Tooltip ?? "") + (string.IsNullOrEmpty(note) ? "" : "\n" + L.T("tip.note") + ": " + note); });
        }

        public void SetColor(BrowserItem it, string color)
        {
            var items = SelectedItems.Contains(it) ? SelectedItems.Where(i => i.UniqueId != null).ToList() : new List<BrowserItem> { it };
            Run(app =>
            {
                var fav = Favorites.For(app.ActiveUIDocument.Document);
                foreach (var i in items) fav.SetColor(i.UniqueId, color);
                RequestRefresh(true);
            });
        }

        /// <summary>Inline rename committed from the row editor: goes to the draft.</summary>
        public void StageRename(BrowserItem it, string newName)
        {
            if (it == null) return;
            newName = (newName ?? "").Trim();
            if (it.Kind == ItemKind.Sheet)
            {
                // sheets: allow "NUM - Name" syntax to change both
                var idx = newName.IndexOf(" - ", StringComparison.Ordinal);
                if (idx > 0)
                {
                    var num = newName.Substring(0, idx).Trim(); var nm = newName.Substring(idx + 3).Trim();
                    StageChange(it, ChangeKind.Renumber, it.Number, num);
                    StageChange(it, ChangeKind.Rename, it.Name, nm);
                    return;
                }
            }
            StageChange(it, ChangeKind.Rename, it.Name, newName);
        }

        public void StageChange(BrowserItem it, ChangeKind kind, string oldValue, string newValue)
        {
            var existing = Pending.FirstOrDefault(p => p.Id == it.Id && p.Kind == kind);
            var original = existing?.OldValue ?? oldValue;
            if (existing != null) Pending.Remove(existing);
            if (string.IsNullOrEmpty(newValue) || newValue == original)
            {
                if (kind == ChangeKind.Rename) it.PendingName = null; else it.PendingNumber = null;
                return;
            }
            Pending.Add(new PendingChange { Id = it.Id, Kind = kind, OldValue = original, NewValue = newValue, Item = it });
            if (kind == ChangeKind.Rename) it.PendingName = newValue; else it.PendingNumber = newValue;
        }

        public void ApplyDraft()
        {
            var list = Pending.ToList();
            if (list.Count == 0) return;
            Run(app =>
            {
                var err = RevitActions.ApplyChanges(app, list);
                Pending.Clear();
                foreach (var c in list) { if (c.Item != null) { c.Item.PendingName = null; c.Item.PendingNumber = null; } }
                Report(err == null ? string.Format(L.T("msg.draftApplied"), list.Count) : err);
            });
        }

        public void DiscardDraft()
        {
            foreach (var c in Pending) if (c.Item != null) { c.Item.PendingName = null; c.Item.PendingNumber = null; }
            Pending.Clear();
        }

        public void ApplyNow(IList<PendingChange> changes)
        {
            if (changes.Count == 0) return;
            Run(app => Report(RevitActions.ApplyChanges(app, changes) ?? string.Format(L.T("msg.draftApplied"), changes.Count)));
        }

        public void Stage(IList<PendingChange> changes)
        {
            foreach (var c in changes)
            {
                var it = c.Item ?? Find(c.Id.IntegerValue);
                if (it != null) StageChange(it, c.Kind, c.OldValue, c.NewValue);
                else Pending.Add(c);
            }
        }

        // ------------------------------------------------------------------ preview
        public void UpdatePreview()
        {
            if (!ShowPreview) return;
            var it = SelectedItems.LastOrDefault();
            _previewItem = it;
            Raise(nameof(CanGeneratePreview));
            if (it == null) { PreviewImage = null; PreviewInfo = L.T("preview.none"); return; }
            PreviewInfo = it.Tooltip ?? it.DisplayText;
            if (it.Kind == ItemKind.FamilyType || it.Kind == ItemKind.Family)
            {
                PreviewImage = it.Preview;
                var item = it;
                Run(app =>
                {
                    try
                    {
                        var img = PreviewService.TypePreview(app.ActiveUIDocument.Document, item.Id, 220);
                        if (_previewItem == item) PreviewImage = img;
                    }
                    catch { }
                });
            }
            else if (it.IsViewLike)
            {
                PreviewImage = null;
                GenerateViewPreview(false);
            }
            else PreviewImage = null;
        }

        public void GenerateViewPreview(bool force)
        {
            var it = _previewItem;
            if (it == null || !it.IsViewLike) return;
            PreviewBusy = true; Raise(nameof(CanGeneratePreview));
            Run(app =>
            {
                try
                {
                    var doc = app.ActiveUIDocument.Document;
                    var view = doc.GetElement(it.Id) as View;
                    if (view == null) return;
                    if (!force && !CacheExists(doc, view)) return; // nothing cached yet: leave the "generate" button
                    var img = PreviewService.ViewPreview(doc, view, 640, force);
                    if (_previewItem == it) PreviewImage = img;
                }
                catch (Exception ex) { Report(ex.Message); }
                finally { PreviewBusy = false; Raise(nameof(CanGeneratePreview)); }
            });
        }

        private static bool CacheExists(Document doc, View view)
        {
            try
            {
                var dir = System.IO.Path.Combine(PreviewService.CacheDir, Favorites.KeyFor(doc));
                return System.IO.Directory.Exists(dir) && System.IO.Directory.GetFiles(dir, view.Id.IntegerValue + "_*.png").Length > 0;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------------ misc
        public void OpenSettings()
        {
            var dlg = new SettingsDialog();
            DialogHelper.Own(dlg, RevitTask.UIApp);
            if (dlg.ShowDialog() == true) OnSettingsChanged();
        }

        public void OnSettingsChanged()
        {
            Raise(nameof(ShowBadges)); Raise(nameof(Compact)); Raise(nameof(RowHeight));
            PreviewService.ClearMemory();
            RequestRefresh(true);
        }

        public void Run(Action<UIApplication> a) => RevitTask.Run(a);

        public void Report(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            Status = msg.Split('\n')[0];
            if (msg.Contains("\n"))
            {
                try { TaskDialog.Show("Project Browser+", msg); } catch { MessageBox.Show(msg, "Project Browser+"); }
            }
        }

        public string ExportText(bool csv)
        {
            var sb = new StringBuilder();
            var items = SelectedItems.Count > 1 ? SelectedItems : _rows.ToList();
            if (csv) sb.AppendLine("Kind;Number;Name;Badges;Id");
            foreach (var i in items)
            {
                if (csv) sb.AppendLine($"{i.Kind};{Csv(i.EffectiveNumber)};{Csv(i.EffectiveName)};{Csv(i.Badges)};{(i.IsElement ? i.IdValue.ToString() : "")}");
                else sb.AppendLine(new string(' ', Math.Max(0, i.Level) * 2) + i.DisplayText);
            }
            return sb.ToString();
        }

        private static string Csv(string s) => s == null ? "" : (s.Contains(";") || s.Contains("\"") ? "\"" + s.Replace("\"", "\"\"") + "\"" : s);
    }
}
