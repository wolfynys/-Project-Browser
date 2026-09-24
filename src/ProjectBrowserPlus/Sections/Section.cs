using System;
using System.Collections.Generic;
using System.Linq;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.Sections
{
    /// <summary>A tab of the browser. Produces flat leaves; <see cref="Grouper"/> turns them into a tree.</summary>
    public abstract class Section
    {
        public abstract string Key { get; }
        public abstract string Icon { get; }
        public string Title => L.T("tab." + Key);
        public virtual bool SupportsGrouping => true;
        public virtual IList<GroupOption> GroupOptions => new List<GroupOption>();
        public virtual IList<QuickFilter> Filters => new List<QuickFilter>();
        public virtual string DefaultGroup => GroupOptions.FirstOrDefault()?.Key;
        public virtual string SearchHint => L.T("search.hint." + Key);

        /// <summary>Runs inside the Revit API context.</summary>
        public abstract List<BrowserItem> BuildLeaves(BuildContext ctx);

        /// <summary>Optional: build a tree that is not derived from leaves (families).</summary>
        public virtual List<BrowserItem> BuildTree(BuildContext ctx, string groupKey, SortMode sort)
        {
            var leaves = BuildLeaves(ctx);
            return Grouper.Group(leaves, groupKey, sort);
        }

        protected static GroupOption G(string key, string title) => new GroupOption { Key = key, Title = title };
        protected static QuickFilter F(string key, string icon, Func<BrowserItem, bool> p) => new QuickFilter { Key = key, Title = L.T("filter." + key), Icon = icon, Predicate = p };
    }

    public static class Grouper
    {
        public const string None = "none";

        public static List<BrowserItem> Group(IEnumerable<BrowserItem> leaves, string groupKey, SortMode sort)
        {
            var root = new BrowserItem { Kind = ItemKind.Folder, Level = -1 };
            var folders = new Dictionary<string, BrowserItem>(StringComparer.Ordinal);
            foreach (var leaf in leaves)
            {
                string[] path = null;
                if (groupKey != null && groupKey != None && leaf.GroupPaths.TryGetValue(groupKey, out var p)) path = p;
                var parent = root;
                if (path != null)
                {
                    var key = "";
                    foreach (var seg in path)
                    {
                        var name = string.IsNullOrEmpty(seg) ? "—" : seg;
                        key += "\u0001" + name;
                        if (!folders.TryGetValue(key, out var f))
                        {
                            f = new BrowserItem { Kind = ItemKind.Folder, Name = name, IconKey = "folder" };
                            folders[key] = f;
                            parent.Add(f);
                        }
                        parent = f;
                    }
                }
                parent.Add(leaf);
            }
            SortRecursive(root, sort);
            CountRecursive(root);
            return root.Children.ToList();
        }

        public static void SortRecursive(BrowserItem node, SortMode sort)
        {
            if (node.Children.Count == 0) return;
            var folders = node.Children.Where(c => c.Kind == ItemKind.Folder || c.Kind == ItemKind.Category).ToList();
            var leaves = node.Children.Where(c => !(c.Kind == ItemKind.Folder || c.Kind == ItemKind.Category)).ToList();
            var cmp = Settings.Current.NaturalSort ? (IComparer<string>)NaturalComparer.Instance : StringComparer.CurrentCultureIgnoreCase;
            folders.Sort((a, b) => cmp.Compare(a.Name, b.Name));
            leaves.Sort((a, b) => CompareLeaves(a, b, sort, cmp));
            node.Children.Clear();
            foreach (var f in folders) { node.Children.Add(f); SortRecursive(f, sort); }
            foreach (var l in leaves) { node.Children.Add(l); if (l.Children.Count > 0) SortRecursive(l, sort); }
        }

        private static int CompareLeaves(BrowserItem a, BrowserItem b, SortMode sort, IComparer<string> cmp)
        {
            switch (sort)
            {
                case SortMode.NameDesc: return -cmp.Compare(a.Name, b.Name);
                case SortMode.NumberAsc:
                    {
                        var c = a.SortNumber.CompareTo(b.SortNumber);
                        if (c != 0) return c;
                        c = cmp.Compare(a.Number ?? "", b.Number ?? "");
                        return c != 0 ? c : cmp.Compare(a.Name, b.Name);
                    }
                case SortMode.NumberDesc:
                    {
                        var c = -a.SortNumber.CompareTo(b.SortNumber);
                        if (c != 0) return c;
                        c = -cmp.Compare(a.Number ?? "", b.Number ?? "");
                        return c != 0 ? c : cmp.Compare(a.Name, b.Name);
                    }
                case SortMode.IdAsc: return a.IdValue.CompareTo(b.IdValue);
                default:
                    {
                        if (a.Kind == ItemKind.Sheet && b.Kind == ItemKind.Sheet)
                        {
                            var c = cmp.Compare(a.Number ?? "", b.Number ?? "");
                            if (c != 0) return c;
                        }
                        var n = cmp.Compare(a.Name, b.Name);
                        return n != 0 ? n : cmp.Compare(a.Number ?? "", b.Number ?? "");
                    }
            }
        }

        public static int CountRecursive(BrowserItem node)
        {
            if (node.Children.Count == 0) { node.Count = 0; return node.IsFolder ? 0 : 1; }
            int n = 0;
            foreach (var c in node.Children)
            {
                var sub = CountRecursive(c);
                n += c.IsFolder ? sub : 1;
            }
            // only folders show a count badge; leaves with children (sheets, families) use their own badges
            node.Count = node.IsFolder ? n : 0;
            return node.IsFolder ? n : 1;
        }
    }
}
