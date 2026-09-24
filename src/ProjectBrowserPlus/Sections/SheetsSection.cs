using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.Sections
{
    public class SheetsSection : Section
    {
        public override string Key => "sheets";
        public override string Icon => "sheet";

        public override IList<GroupOption> GroupOptions => new List<GroupOption>
        {
            G("std", L.T("group.std")),
            G(Grouper.None, L.T("group.none")),
            G("prefix", L.T("group.sheetPrefix")),
            G("titleblock", L.T("group.titleblock")),
            G("revision", L.T("group.revision")),
            G("issue", L.T("group.issueDate")),
            G("drawnby", L.T("group.drawnBy")),
            G("workset", L.T("group.workset")),
        };

        public override IList<QuickFilter> Filters => new List<QuickFilter>
        {
            F("empty", "file-x", i => i.Children.Count == 0),
            F("placeholder", "square-dashed", i => i.IsUnused),
            F("open", "panels-top-left", i => i.IsOpen),
            F("fav", "star", i => i.IsFavorite),
        };

        public override List<BrowserItem> BuildLeaves(BuildContext ctx)
        {
            var doc = ctx.Doc;
            var list = new List<BrowserItem>();
            var showChildren = ctx.S.ShowSheetViewsAsChildren;
            foreach (ViewSheet s in new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)))
            {
                if (s.IsTemplate) continue;
                list.Add(Make(ctx, s, showChildren));
            }
            return list;
        }

        public static BrowserItem Make(BuildContext ctx, ViewSheet s, bool withChildren)
        {
            var it = new BrowserItem { Kind = ItemKind.Sheet, Id = s.Id, Name = s.Name, Number = s.SheetNumber, IconKey = s.IsPlaceholder ? "square-dashed" : "sheet" };
            ViewHelpers.FillCommon(ctx, it, s);
            it.IsUnused = s.IsPlaceholder;
            var rev = BuildContext.Param(s, BuiltInParameter.SHEET_CURRENT_REVISION);
            var issue = BuildContext.Param(s, BuiltInParameter.SHEET_ISSUE_DATE);
            var drawn = BuildContext.Param(s, BuiltInParameter.SHEET_DRAWN_BY);
            var tb = ctx.TitleBlockName(s.Id);
            var workset = ctx.WorksetName(s);
            int nViews = 0;
            if (withChildren)
            {
                foreach (var vid in s.GetAllPlacedViews())
                {
                    var v = ctx.Doc.GetElement(vid) as View;
                    if (v == null) continue;
                    BrowserItem child;
                    if (v is ViewSchedule sch) child = SchedulesSection.Make(ctx, sch);
                    else if (v.ViewType == ViewType.Legend) child = LegendsSection.Make(ctx, v);
                    else child = ViewsSection.Make(ctx, v);
                    child.GroupPaths.Clear();
                    it.Add(child);
                    nViews++;
                }
                foreach (ScheduleSheetInstance si in new FilteredElementCollector(ctx.Doc, s.Id).OfClass(typeof(ScheduleSheetInstance)))
                {
                    if (si.IsTitleblockRevisionSchedule) continue;
                    var sch = ctx.Doc.GetElement(si.ScheduleId) as ViewSchedule;
                    if (sch == null || it.Children.Any(c => c.Id == sch.Id)) continue;
                    var child = SchedulesSection.Make(ctx, sch);
                    child.GroupPaths.Clear();
                    it.Add(child);
                    nViews++;
                }
            }
            else nViews = s.GetAllPlacedViews().Count;

            it.Badges = ViewHelpers.BuildBadges(
                nViews > 0 ? nViews + " " + L.T("badge.views") : null,
                !string.IsNullOrEmpty(rev) ? L.T("badge.rev") + " " + rev : null,
                s.IsPlaceholder ? L.T("badge.placeholder") : null);
            it.Tooltip = string.Join("\n", new[]
            {
                s.SheetNumber + " - " + s.Name,
                tb != null ? L.T("tip.titleblock") + ": " + tb : null,
                rev != null ? L.T("tip.revision") + ": " + rev : null,
                issue != null ? L.T("tip.issueDate") + ": " + issue : null,
                drawn != null ? L.T("tip.drawnBy") + ": " + drawn : null,
                workset != null ? L.T("tip.workset") + ": " + workset : null,
                "Id: " + s.Id.IntegerValue,
                it.Note != null ? L.T("tip.note") + ": " + it.Note : null,
            }.Where(x => !string.IsNullOrEmpty(x)));
            it.SearchText = string.Join(" ", new[] { s.SheetNumber, tb, rev, issue, drawn, it.Note }.Where(x => x != null));

            it.GroupPaths["std"] = ctx.StandardFolders(ctx.SheetOrganization, s.Id);
            it.GroupPaths["prefix"] = new[] { SheetPrefix(s.SheetNumber) };
            it.GroupPaths["titleblock"] = new[] { tb ?? L.T("group.noTitleblock") };
            it.GroupPaths["revision"] = new[] { string.IsNullOrEmpty(rev) ? L.T("group.noRevision") : rev };
            it.GroupPaths["issue"] = new[] { string.IsNullOrEmpty(issue) ? "—" : issue };
            it.GroupPaths["drawnby"] = new[] { string.IsNullOrEmpty(drawn) ? "—" : drawn };
            it.GroupPaths["workset"] = new[] { workset ?? "—" };
            return it;
        }

        /// <summary>"A-101" → "A", "АР-05" → "АР", "101" → "1xx".</summary>
        public static string SheetPrefix(string number)
        {
            if (string.IsNullOrEmpty(number)) return "—";
            int i = 0;
            while (i < number.Length && !char.IsDigit(number[i])) i++;
            var p = number.Substring(0, i).TrimEnd('-', '_', ' ', '.');
            if (p.Length > 0) return p;
            return number.Length > 0 ? number[0] + new string('x', System.Math.Max(0, number.Length - 1)) : "—";
        }
    }
}
