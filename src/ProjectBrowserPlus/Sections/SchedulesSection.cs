using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.Sections
{
    public class SchedulesSection : Section
    {
        public override string Key => "schedules";
        public override string Icon => "table";

        public override IList<GroupOption> GroupOptions => new List<GroupOption>
        {
            G("category", L.T("group.category")),
            G("kind", L.T("group.scheduleKind")),
            G("sheet", L.T("group.sheet")),
            G("prefix", L.T("group.prefix")),
            G(Grouper.None, L.T("group.none")),
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
            foreach (ViewSchedule s in new FilteredElementCollector(ctx.Doc).OfClass(typeof(ViewSchedule)))
            {
                if (s.IsTemplate || s.IsTitleblockRevisionSchedule || s.IsInternalKeynoteSchedule) continue;
                list.Add(Make(ctx, s));
            }
            return list;
        }

        public static BrowserItem Make(BuildContext ctx, ViewSchedule s)
        {
            var it = new BrowserItem { Kind = ItemKind.Schedule, Id = s.Id, Name = s.Name, IconKey = ViewHelpers.IconFor(s) };
            ViewHelpers.FillCommon(ctx, it, s);
            string cat = null, kind;
            try
            {
                var def = s.Definition;
                if (def != null && def.CategoryId != null && def.CategoryId != ElementId.InvalidElementId)
                {
                    var c = Category.GetCategory(ctx.Doc, def.CategoryId);
                    cat = c?.Name;
                    if (cat == null) { try { cat = LabelUtils.GetLabelFor((BuiltInCategory)def.CategoryId.IntegerValue); } catch { } }
                }
            }
            catch { }
            if (s.ViewType == ViewType.PanelSchedule) kind = L.T("kind.panelSchedule");
            else if (s.ViewType == ViewType.ColumnSchedule) kind = L.T("kind.columnSchedule");
            else
            {
                try
                {
                    if (s.Definition.IsKeySchedule) kind = L.T("kind.keySchedule");
                    else if (s.Definition.IsMaterialTakeoff) kind = L.T("kind.materialTakeoff");
                    else if (s.IsTitleblockRevisionSchedule) kind = L.T("kind.revisionSchedule");
                    else kind = L.T("kind.schedule");
                }
                catch { kind = L.T("kind.schedule"); }
            }
            if (cat == null) cat = kind;
            it.Badges = ViewHelpers.BuildBadges(cat, it.IsOnSheet ? L.T("badge.sheet") + " " + it.SheetNumber : null);
            it.Tooltip = string.Join("\n", new[] { s.Name, kind + (cat != null ? " · " + cat : ""), it.IsOnSheet ? L.T("tip.sheets") + ": " + it.SheetNumber : L.T("tip.notOnSheet"), "Id: " + s.Id.IntegerValue });
            it.SearchText = string.Join(" ", new[] { cat, kind, it.SheetNumber, it.Note }.Where(x => x != null));
            it.GroupPaths["category"] = new[] { cat ?? "—" };
            it.GroupPaths["kind"] = new[] { kind };
            it.GroupPaths["sheet"] = new[] { it.IsOnSheet ? L.T("group.onSheet") + " " + it.SheetNumber : L.T("group.notOnSheet") };
            it.GroupPaths["prefix"] = new[] { ViewsSection.Prefix(s.Name) };
            return it;
        }
    }
}
