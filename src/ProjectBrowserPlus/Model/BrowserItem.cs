using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace ProjectBrowserPlus.Model
{
    /// <summary>
    /// One row in the browser. Rows are pure data (no Revit objects are kept alive here),
    /// so the UI thread can bind to them freely while Revit is busy.
    /// </summary>
    public class BrowserItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void Raise([CallerMemberName] string p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        private string _name;
        private bool _isExpanded, _isSelected, _isFavorite, _isCurrent, _isOpen, _isEditing, _isMatch = true;
        private ImageSource _preview;
        private string _pendingName, _pendingNumber, _badges;

        public ItemKind Kind { get; set; }
        public string IconKey { get; set; }
        public ElementId Id { get; set; } = ElementId.InvalidElementId;
        public string UniqueId { get; set; }
        public int IdValue => Id?.IntegerValue ?? -1;

        /// <summary>Display name (view name, sheet name, family name...).</summary>
        public string Name { get => _name; set { if (_name != value) { _name = value; Raise(); Raise(nameof(DisplayText)); } } }
        /// <summary>Secondary identifier: sheet number, type mark...</summary>
        public string Number { get; set; }
        /// <summary>Short badges shown greyed after the name (scale, template, count...).</summary>
        public string Badges { get => _badges; set { _badges = value; Raise(); } }
        public string Tooltip { get; set; }
        public string Note { get; set; }
        public string TagColor { get; set; }

        public int Level { get; set; }
        public int Count { get; set; }          // for folders: number of leaves
        public bool HasChildren => Children.Count > 0;
        public bool IsOnSheet { get; set; }
        public string SheetNumber { get; set; }
        public bool IsTemplate { get; set; }
        public bool IsDependent { get; set; }
        public bool IsSystemFamily { get; set; }
        public bool IsInPlace { get; set; }
        public bool IsPlaceable { get; set; }
        public bool IsUnused { get; set; }
        public int Instances { get; set; } = -1;
        public double SortNumber { get; set; }  // level elevation etc.
        public bool NeedsPreview { get; set; }
        public string ExtraSortKey { get; set; }

        /// <summary>Group path per grouping option key (computed once at build time).</summary>
        public Dictionary<string, string[]> GroupPaths { get; } = new Dictionary<string, string[]>();
        /// <summary>Searchable text in addition to the name (parameters, sheet number...).</summary>
        public string SearchText { get; set; }

        public BrowserItem Parent { get; set; }
        public ObservableCollection<BrowserItem> Children { get; } = new ObservableCollection<BrowserItem>();

        public bool IsExpanded { get => _isExpanded; set { if (_isExpanded != value) { _isExpanded = value; Raise(); } } }
        public bool IsSelected { get => _isSelected; set { if (_isSelected != value) { _isSelected = value; Raise(); } } }
        public bool IsFavorite { get => _isFavorite; set { if (_isFavorite != value) { _isFavorite = value; Raise(); } } }
        public bool IsCurrent { get => _isCurrent; set { if (_isCurrent != value) { _isCurrent = value; Raise(); } } }
        public bool IsOpen { get => _isOpen; set { if (_isOpen != value) { _isOpen = value; Raise(); } } }
        public bool IsEditing { get => _isEditing; set { if (_isEditing != value) { _isEditing = value; Raise(); } } }
        public bool IsMatch { get => _isMatch; set { if (_isMatch != value) { _isMatch = value; Raise(); } } }
        public ImageSource Preview { get => _preview; set { _preview = value; Raise(); Raise(nameof(HasPreview)); } }
        public bool HasPreview => _preview != null;

        /// <summary>Draft (not yet applied) rename.</summary>
        public string PendingName { get => _pendingName; set { _pendingName = value; Raise(); Raise(nameof(HasPending)); Raise(nameof(DisplayText)); } }
        public string PendingNumber { get => _pendingNumber; set { _pendingNumber = value; Raise(); Raise(nameof(HasPending)); Raise(nameof(DisplayText)); } }
        public bool HasPending => _pendingName != null || _pendingNumber != null;

        public string EffectiveName => _pendingName ?? Name;
        public string EffectiveNumber => _pendingNumber ?? Number;

        public string DisplayText
        {
            get
            {
                var n = EffectiveName;
                var num = EffectiveNumber;
                if (Kind == ItemKind.Sheet && !string.IsNullOrEmpty(num)) return num + " - " + n;
                return n;
            }
        }

        public bool IsFolder => Kind == ItemKind.Folder || Kind == ItemKind.Category;
        public bool IsElement => Id != null && Id != ElementId.InvalidElementId && !IsFolder;
        public bool IsViewLike => Kind == ItemKind.View || Kind == ItemKind.Sheet || Kind == ItemKind.Schedule || Kind == ItemKind.Legend;

        public IEnumerable<BrowserItem> Descendants()
        {
            foreach (var c in Children)
            {
                yield return c;
                foreach (var d in c.Descendants()) yield return d;
            }
        }

        public IEnumerable<BrowserItem> Leaves() => Descendants().Where(d => !d.IsFolder);

        public void Add(BrowserItem child)
        {
            child.Parent = this;
            child.Level = Level + 1;
            Children.Add(child);
        }

        public override string ToString() => DisplayText;
    }

    public class GroupOption
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public override string ToString() => Title;
    }

    public class QuickFilter
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Icon { get; set; }
        public Func<BrowserItem, bool> Predicate { get; set; }
        public bool IsActive { get; set; }
        public override string ToString() => Title;
    }
}
