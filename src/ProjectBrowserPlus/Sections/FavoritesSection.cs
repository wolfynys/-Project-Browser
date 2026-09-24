using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.Sections
{
    /// <summary>Favorites (starred anything) + recently opened views.</summary>
    public class FavoritesSection : Section
    {
        public override string Key => "favorites";
        public override string Icon => "star";
        public override bool SupportsGrouping => false;

        public override List<BrowserItem> BuildLeaves(BuildContext ctx) => new List<BrowserItem>();

        public override List<BrowserItem> BuildTree(BuildContext ctx, string groupKey, SortMode sort)
        {
            var doc = ctx.Doc;
            var fav = new BrowserItem { Kind = ItemKind.Folder, Name = L.T("fav.favorites"), IconKey = "star", IsExpanded = true, Level = 0 };
            var rec = new BrowserItem { Kind = ItemKind.Folder, Name = L.T("fav.recent"), IconKey = "history", IsExpanded = true, Level = 0 };
            var open = new BrowserItem { Kind = ItemKind.Folder, Name = L.T("fav.open"), IconKey = "panels-top-left", IsExpanded = true, Level = 0 };

            foreach (var uid in ctx.Fav.Data.Favorites.ToList())
            {
                var e = SafeGet(doc, uid);
                if (e == null) continue;
                var it = MakeAny(ctx, e);
                if (it != null) fav.Add(it);
            }
            foreach (var uid in ctx.Fav.Data.Recent.ToList())
            {
                var e = SafeGet(doc, uid) as View;
                if (e == null) continue;
                var it = MakeAny(ctx, e);
                if (it != null) rec.Add(it);
            }
            foreach (var id in ctx.OpenViewIds)
            {
                var v = doc.GetElement(new ElementId(id)) as View;
                if (v == null) continue;
                var it = MakeAny(ctx, v);
                if (it != null) open.Add(it);
            }
            Grouper.SortRecursive(fav, sort);
            Grouper.SortRecursive(open, sort);
            fav.Count = fav.Children.Count; rec.Count = rec.Children.Count; open.Count = open.Children.Count;
            return new List<BrowserItem> { fav, rec, open };
        }

        private static Element SafeGet(Document doc, string uid)
        {
            try { return doc.GetElement(uid); } catch { return null; }
        }

        public static BrowserItem MakeAny(BuildContext ctx, Element e)
        {
            BrowserItem it = null;
            if (e is ViewSheet s) it = SheetsSection.Make(ctx, s, false);
            else if (e is ViewSchedule sch) it = SchedulesSection.Make(ctx, sch);
            else if (e is View v) it = v.ViewType == ViewType.Legend ? LegendsSection.Make(ctx, v) : ViewsSection.Make(ctx, v);
            else if (e is Family f)
            {
                it = new BrowserItem { Kind = ItemKind.Family, Id = f.Id, UniqueId = f.UniqueId, Name = f.Name, IconKey = "package", Badges = f.FamilyCategory?.Name, IsFavorite = true };
                it.Tooltip = f.Name + "\n" + f.FamilyCategory?.Name;
            }
            else if (e is ElementType t)
            {
                it = new BrowserItem { Kind = ItemKind.FamilyType, Id = t.Id, UniqueId = t.UniqueId, Name = t.FamilyName + " : " + t.Name, IconKey = "square", Badges = t.Category?.Name, IsFavorite = true, NeedsPreview = true, IsPlaceable = true };
                it.Tooltip = t.FamilyName + " : " + t.Name + "\n" + t.Category?.Name;
            }
            else if (e is Level l)
            {
                it = new BrowserItem { Kind = ItemKind.Level, Id = l.Id, UniqueId = l.UniqueId, Name = l.Name, IconKey = "layers", Badges = ctx.FormatLength(l.Elevation), IsFavorite = true };
            }
            else if (e is GroupType g)
            {
                it = new BrowserItem { Kind = ItemKind.Group, Id = g.Id, UniqueId = g.UniqueId, Name = g.Name, IconKey = "boxes", IsFavorite = true };
            }
            else if (e is RevitLinkType rl)
            {
                it = new BrowserItem { Kind = ItemKind.Link, Id = rl.Id, UniqueId = rl.UniqueId, Name = rl.Name, IconKey = "link", IsFavorite = true, Number = "revit" };
            }
            if (it != null) it.GroupPaths.Clear();
            return it;
        }
    }
}
