using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.Sections
{
    public class LevelsSection : Section
    {
        public override string Key => "levels";
        public override string Icon => "layers";
        public override bool SupportsGrouping => false;

        public override IList<QuickFilter> Filters => new List<QuickFilter>
        {
            F("noViews", "eye-off", i => i.Instances == 0),
        };

        public override List<BrowserItem> BuildLeaves(BuildContext ctx)
        {
            var doc = ctx.Doc;
            var viewsPerLevel = new Dictionary<int, List<View>>();
            foreach (View v in new FilteredElementCollector(doc).OfClass(typeof(View)))
            {
                if (!ViewHelpers.IsGraphicalView(v)) continue;
                Level l = null; try { l = v.GenLevel; } catch { }
                if (l == null) continue;
                if (!viewsPerLevel.TryGetValue(l.Id.IntegerValue, out var list)) viewsPerLevel[l.Id.IntegerValue] = list = new List<View>();
                list.Add(v);
            }
            var result = new List<BrowserItem>();
            foreach (Level l in new FilteredElementCollector(doc).OfClass(typeof(Level)))
            {
                var elev = ctx.FormatLength(l.Elevation);
                var n = viewsPerLevel.TryGetValue(l.Id.IntegerValue, out var vs) ? vs.Count : 0;
                var it = new BrowserItem
                {
                    Kind = ItemKind.Level, Id = l.Id, UniqueId = l.UniqueId, Name = l.Name, IconKey = "layers",
                    SortNumber = l.Elevation, Number = elev, Instances = n,
                    Badges = ViewHelpers.BuildBadges(elev, n + " " + L.T("badge.views")),
                };
                it.IsFavorite = ctx.Fav.IsFavorite(l.UniqueId);
                var story = BuildContext.Param(l, BuiltInParameter.LEVEL_IS_BUILDING_STORY);
                it.Tooltip = string.Join("\n", new[] { l.Name, L.T("tip.elevation") + ": " + elev, L.T("tip.views") + ": " + n, story != null ? L.T("tip.buildingStory") + ": " + story : null, "Id: " + l.Id.IntegerValue }.Where(x => x != null));
                it.SearchText = elev;
                if (vs != null)
                    foreach (var v in vs.OrderBy(v => v.Name, NaturalComparer.Instance))
                    {
                        var c = ViewsSection.Make(ctx, v); c.GroupPaths.Clear(); it.Add(c);
                    }
                result.Add(it);
            }
            return result;
        }

        public override List<BrowserItem> BuildTree(BuildContext ctx, string groupKey, SortMode sort)
        {
            var leaves = BuildLeaves(ctx);
            var mode = sort == SortMode.NameAsc || sort == SortMode.NumberAsc ? SortMode.NumberDesc : sort == SortMode.NameDesc || sort == SortMode.NumberDesc ? SortMode.NumberAsc : sort;
            // levels: highest first by default (like an elevation)
            return Grouper.Group(leaves, Grouper.None, mode);
        }
    }
}
