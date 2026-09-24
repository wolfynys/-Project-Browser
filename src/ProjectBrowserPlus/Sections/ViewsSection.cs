using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.Sections
{
    public class ViewsSection : Section
    {
        public override string Key => "views";
        public override string Icon => "eye";

        public override IList<GroupOption> GroupOptions => new List<GroupOption>
        {
            G("std", L.T("group.std")),
            G("type", L.T("group.viewtype")),
            G("family", L.T("group.viewfamily")),
            G("level", L.T("group.level")),
            G("template", L.T("group.template")),
            G("sheet", L.T("group.sheet")),
            G("discipline", L.T("group.discipline")),
            G("phase", L.T("group.phase")),
            G("scale", L.T("group.scale")),
            G("workset", L.T("group.workset")),
            G("prefix", L.T("group.prefix")),
            G(Grouper.None, L.T("group.none")),
        };

        public override IList<QuickFilter> Filters => new List<QuickFilter>
        {
            F("notOnSheet", "file-x", i => !i.IsOnSheet && !i.IsDependent),
            F("onSheet", "file-check", i => i.IsOnSheet),
            F("open", "panels-top-left", i => i.IsOpen),
            F("noTemplate", "eye-off", i => !i.IsTemplate),
            F("dependent", "file-symlink", i => i.IsDependent),
            F("fav", "star", i => i.IsFavorite),
        };

        public override List<BrowserItem> BuildLeaves(BuildContext ctx)
        {
            var doc = ctx.Doc;
            var list = new List<BrowserItem>();
            foreach (View v in new FilteredElementCollector(doc).OfClass(typeof(View)))
            {
                if (!ViewHelpers.IsGraphicalView(v)) continue;
                list.Add(Make(ctx, v));
            }
            return list;
        }

        public static BrowserItem Make(BuildContext ctx, View v)
        {
            var it = new BrowserItem { Kind = ItemKind.View, Id = v.Id, Name = v.Name, IconKey = ViewHelpers.IconFor(v) };
            ViewHelpers.FillCommon(ctx, it, v);
            var tmpl = ViewHelpers.TemplateName(ctx, v);
            it.IsTemplate = tmpl != null;
            var primary = v.GetPrimaryViewId();
            it.IsDependent = primary != null && primary != ElementId.InvalidElementId;
            var scale = ViewHelpers.ScaleText(v);
            var level = ViewHelpers.LevelName(v);
            var family = ViewHelpers.ViewFamilyName(ctx, v);
            var typeName = ViewHelpers.TypeName(ctx, v);
            var discipline = BuildContext.Param(v, BuiltInParameter.VIEW_DISCIPLINE);
            var phase = BuildContext.Param(v, BuiltInParameter.VIEW_PHASE);
            var workset = ctx.WorksetName(v);
            var detail = BuildContext.Param(v, BuiltInParameter.VIEW_DETAIL_LEVEL);

            it.Badges = ViewHelpers.BuildBadges(scale, it.IsOnSheet ? L.T("badge.sheet") + " " + it.SheetNumber : null, it.IsDependent ? L.T("badge.dependent") : null);
            it.Tooltip = string.Join("\n", new[]
            {
                v.Name,
                family + (typeName != null ? " · " + typeName : ""),
                scale != null ? L.T("tip.scale") + ": " + scale : null,
                detail != null ? L.T("tip.detail") + ": " + detail : null,
                level != null ? L.T("tip.level") + ": " + level : null,
                tmpl != null ? L.T("tip.template") + ": " + tmpl : null,
                it.IsOnSheet ? L.T("tip.sheets") + ": " + it.SheetNumber : L.T("tip.notOnSheet"),
                discipline != null ? L.T("tip.discipline") + ": " + discipline : null,
                phase != null ? L.T("tip.phase") + ": " + phase : null,
                workset != null ? L.T("tip.workset") + ": " + workset : null,
                "Id: " + v.Id.IntegerValue,
                it.Note != null ? L.T("tip.note") + ": " + it.Note : null,
            }.Where(s => !string.IsNullOrEmpty(s)));
            it.SearchText = string.Join(" ", new[] { it.SheetNumber, tmpl, level, family, typeName, discipline, phase, it.Note }.Where(s => s != null));

            it.GroupPaths["std"] = ctx.StandardFolders(ctx.ViewOrganization, v.Id);
            it.GroupPaths["type"] = new[] { family };
            it.GroupPaths["family"] = new[] { family, typeName ?? "—" };
            it.GroupPaths["level"] = new[] { level ?? L.T("group.noLevel") };
            it.GroupPaths["template"] = new[] { tmpl ?? L.T("group.noTemplate") };
            it.GroupPaths["sheet"] = new[] { it.IsOnSheet ? L.T("group.onSheet") + " " + it.SheetNumber : L.T("group.notOnSheet") };
            it.GroupPaths["discipline"] = new[] { discipline ?? "—" };
            it.GroupPaths["phase"] = new[] { phase ?? "—" };
            it.GroupPaths["scale"] = new[] { scale ?? "—" };
            it.GroupPaths["workset"] = new[] { workset ?? "—" };
            it.GroupPaths["prefix"] = new[] { Prefix(v.Name) };
            return it;
        }

        /// <summary>Text before the first separator ("_", "-", " ", ":") – a cheap, surprisingly useful grouping.</summary>
        public static string Prefix(string name)
        {
            if (string.IsNullOrEmpty(name)) return "—";
            var idx = name.IndexOfAny(new[] { '_', '-', ' ', ':', '.' });
            return idx > 0 ? name.Substring(0, idx) : name;
        }
    }
}
