using System.Collections.Generic;
using System.Linq;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.Sections
{
    /// <summary>
    /// The classic Project Browser layout in one tree (views organised exactly like Revit's
    /// current Browser Organization, legends, schedules, sheets, families, groups, links).
    /// </summary>
    public class AllSection : Section
    {
        public override string Key => "all";
        public override string Icon => "list-tree";
        public override bool SupportsGrouping => false;

        public override IList<QuickFilter> Filters => new List<QuickFilter>
        {
            F("notOnSheet", "file-x", i => i.IsViewLike && i.Kind != ItemKind.Sheet && !i.IsOnSheet),
            F("fav", "star", i => i.IsFavorite),
        };

        public override List<BrowserItem> BuildLeaves(BuildContext ctx) => new List<BrowserItem>();

        public override List<BrowserItem> BuildTree(BuildContext ctx, string groupKey, SortMode sort)
        {
            var roots = new List<BrowserItem>();
            BrowserItem Root(string title, string icon, List<BrowserItem> children)
            {
                var r = new BrowserItem { Kind = ItemKind.Folder, Name = title, IconKey = icon, Level = 0 };
                foreach (var c in children) r.Add(c);
                Grouper.CountRecursive(r);
                roots.Add(r);
                return r;
            }
            var viewsRoot = Root(L.T("tab.views"), "eye", new ViewsSection().BuildTree(ctx, "std", sort));
            viewsRoot.IsExpanded = true;
            Root(L.T("tab.legends"), "book-open", new LegendsSection().BuildTree(ctx, Grouper.None, sort));
            Root(L.T("tab.schedules"), "table", new SchedulesSection().BuildTree(ctx, Grouper.None, sort));
            Root(L.T("tab.sheets"), "sheet", new SheetsSection().BuildTree(ctx, "std", sort));
            Root(L.T("tab.families"), "package", new FamiliesSection().BuildTree(ctx, "category", sort));
            Root(L.T("tab.groups"), "boxes", new GroupsSection().BuildTree(ctx, "kind", sort));
            Root(L.T("tab.links"), "link", new LinksSection().BuildTree(ctx, "kind", sort));
            return roots;
        }
    }
}
