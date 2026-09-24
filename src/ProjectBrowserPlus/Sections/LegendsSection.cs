using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.Sections
{
    public class LegendsSection : Section
    {
        public override string Key => "legends";
        public override string Icon => "book-open";

        public override IList<GroupOption> GroupOptions => new List<GroupOption>
        {
            G(Grouper.None, L.T("group.none")),
            G("prefix", L.T("group.prefix")),
            G("sheet", L.T("group.sheet")),
            G("scale", L.T("group.scale")),
        };

        public override IList<QuickFilter> Filters => new List<QuickFilter>
        {
            F("notOnSheet", "file-x", i => !i.IsOnSheet),
            F("onSheet", "file-check", i => i.IsOnSheet),
            F("fav", "star", i => i.IsFavorite),
        };

        public override List<BrowserItem> BuildLeaves(BuildContext ctx)
        {
            var list = new List<BrowserItem>();
            foreach (View v in new FilteredElementCollector(ctx.Doc).OfClass(typeof(View)))
            {
                if (v.IsTemplate || v.ViewType != ViewType.Legend) continue;
                list.Add(Make(ctx, v));
            }
            return list;
        }

        public static BrowserItem Make(BuildContext ctx, View v)
        {
            var it = new BrowserItem { Kind = ItemKind.Legend, Id = v.Id, Name = v.Name, IconKey = "book-open" };
            ViewHelpers.FillCommon(ctx, it, v);
            var scale = ViewHelpers.ScaleText(v);
            var nSheets = ctx.ViewSheets.TryGetValue(v.Id.IntegerValue, out var l) ? l.Count : 0;
            it.Badges = ViewHelpers.BuildBadges(scale, nSheets > 0 ? nSheets + " " + L.T("badge.sheetsCount") : null);
            it.Tooltip = string.Join("\n", new[] { v.Name, scale != null ? L.T("tip.scale") + ": " + scale : null, it.IsOnSheet ? L.T("tip.sheets") + ": " + it.SheetNumber : L.T("tip.notOnSheet"), "Id: " + v.Id.IntegerValue }.Where(x => x != null));
            it.SearchText = string.Join(" ", new[] { it.SheetNumber, it.Note }.Where(x => x != null));
            it.GroupPaths["prefix"] = new[] { ViewsSection.Prefix(v.Name) };
            it.GroupPaths["sheet"] = new[] { it.IsOnSheet ? L.T("group.onSheet") : L.T("group.notOnSheet") };
            it.GroupPaths["scale"] = new[] { scale ?? "—" };
            return it;
        }
    }
}
