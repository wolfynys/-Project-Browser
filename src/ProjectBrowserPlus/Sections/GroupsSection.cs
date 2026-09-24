using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.Sections
{
    public class GroupsSection : Section
    {
        public override string Key => "groups";
        public override string Icon => "boxes";

        public override IList<GroupOption> GroupOptions => new List<GroupOption>
        {
            G("kind", L.T("group.groupKind")),
            G(Grouper.None, L.T("group.none")),
            G("prefix", L.T("group.prefix")),
        };

        public override IList<QuickFilter> Filters => new List<QuickFilter>
        {
            F("unused", "circle-x", i => i.IsUnused),
            F("fav", "star", i => i.IsFavorite),
        };

        public override List<BrowserItem> BuildLeaves(BuildContext ctx)
        {
            var list = new List<BrowserItem>();
            foreach (GroupType gt in new FilteredElementCollector(ctx.Doc).OfClass(typeof(GroupType)))
            {
                string kind;
                var catId = gt.Category?.Id.IntegerValue ?? 0;
                if (catId == (int)BuiltInCategory.OST_IOSDetailGroups) kind = L.T("kind.detailGroup");
                else if (catId == (int)BuiltInCategory.OST_IOSAttachedDetailGroups) kind = L.T("kind.attachedDetailGroup");
                else kind = L.T("kind.modelGroup");
                int n = 0;
                try { n = gt.Groups.Size; } catch { }
                var it = new BrowserItem
                {
                    Kind = ItemKind.Group, Id = gt.Id, UniqueId = gt.UniqueId, Name = gt.Name,
                    IconKey = catId == (int)BuiltInCategory.OST_IOSModelGroups ? "boxes" : "group",
                    Instances = n, IsUnused = n == 0,
                    Badges = n == 0 ? L.T("badge.unused") : n + " " + L.T("badge.inst"),
                };
                it.IsFavorite = ctx.Fav.IsFavorite(gt.UniqueId);
                it.Tooltip = gt.Name + "\n" + kind + "\n" + L.T("tip.instances") + ": " + n + "\nId: " + gt.Id.IntegerValue;
                it.SearchText = kind;
                it.GroupPaths["kind"] = new[] { kind };
                it.GroupPaths["prefix"] = new[] { kind, ViewsSection.Prefix(gt.Name) };
                list.Add(it);
            }
            return list;
        }
    }
}
