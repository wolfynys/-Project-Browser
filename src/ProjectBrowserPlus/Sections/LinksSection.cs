using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.Sections
{
    public class LinksSection : Section
    {
        public override string Key => "links";
        public override string Icon => "link";

        public override IList<GroupOption> GroupOptions => new List<GroupOption>
        {
            G("kind", L.T("group.linkKind")),
            G("status", L.T("group.linkStatus")),
            G(Grouper.None, L.T("group.none")),
        };

        public override IList<QuickFilter> Filters => new List<QuickFilter>
        {
            F("problem", "triangle-alert", i => i.IsUnused),
        };

        public override List<BrowserItem> BuildLeaves(BuildContext ctx)
        {
            var doc = ctx.Doc;
            var list = new List<BrowserItem>();
            var instCount = new Dictionary<int, int>();
            foreach (RevitLinkInstance li in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)))
            {
                var t = li.GetTypeId().IntegerValue;
                instCount[t] = instCount.TryGetValue(t, out var c) ? c + 1 : 1;
            }
            foreach (RevitLinkType lt in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType)))
            {
                var status = StatusText(lt.GetLinkedFileStatus(), out var bad);
                var n = instCount.TryGetValue(lt.Id.IntegerValue, out var c) ? c : 0;
                var path = PathOf(lt);
                var it = new BrowserItem
                {
                    Kind = ItemKind.Link, Id = lt.Id, UniqueId = lt.UniqueId, Name = lt.Name, IconKey = "link",
                    Instances = n, IsUnused = bad, Number = "revit",
                    Badges = ViewHelpers.BuildBadges(status, n != 1 ? n + " " + L.T("badge.inst") : null, lt.IsNestedLink ? L.T("badge.nested") : null),
                };
                it.IsFavorite = ctx.Fav.IsFavorite(lt.UniqueId);
                it.Tooltip = string.Join("\n", new[] { lt.Name, L.T("kind.revitLink"), L.T("tip.status") + ": " + status, path != null ? L.T("tip.path") + ": " + path : null, L.T("tip.instances") + ": " + n, "Id: " + lt.Id.IntegerValue }.Where(x => x != null));
                it.SearchText = (path ?? "") + " " + status;
                it.GroupPaths["kind"] = new[] { L.T("kind.revitLink") };
                it.GroupPaths["status"] = new[] { status };
                list.Add(it);
            }
            foreach (CADLinkType ct in new FilteredElementCollector(doc).OfClass(typeof(CADLinkType)))
            {
                bool isLink = false; string status = null; bool bad = false; string path = null;
                try
                {
                    isLink = ct.IsExternalFileReference();
                    if (isLink)
                    {
                        var r = ct.GetExternalFileReference();
                        status = StatusText(r.GetLinkedFileStatus(), out bad);
                        path = ModelPathUtils.ConvertModelPathToUserVisiblePath(r.GetAbsolutePath());
                    }
                }
                catch { }
                var kind = isLink ? L.T("kind.cadLink") : L.T("kind.cadImport");
                var it = new BrowserItem
                {
                    Kind = ItemKind.Link, Id = ct.Id, UniqueId = ct.UniqueId, Name = ct.Name, IconKey = isLink ? "link-2" : "download",
                    IsUnused = bad, Number = isLink ? "cad" : "import",
                    Badges = ViewHelpers.BuildBadges(kind, status),
                };
                it.Tooltip = string.Join("\n", new[] { ct.Name, kind, status != null ? L.T("tip.status") + ": " + status : null, path != null ? L.T("tip.path") + ": " + path : null, "Id: " + ct.Id.IntegerValue }.Where(x => x != null));
                it.SearchText = (path ?? "") + " " + kind;
                it.GroupPaths["kind"] = new[] { kind };
                it.GroupPaths["status"] = new[] { status ?? kind };
                list.Add(it);
            }
            foreach (ImageType im in new FilteredElementCollector(doc).OfClass(typeof(ImageType)))
            {
                string path = null; try { path = im.Path; } catch { }
                var it = new BrowserItem { Kind = ItemKind.Link, Id = im.Id, UniqueId = im.UniqueId, Name = im.Name, IconKey = "image", Number = "image", Badges = L.T("kind.image") };
                it.Tooltip = im.Name + "\n" + L.T("kind.image") + (path != null ? "\n" + path : "") + "\nId: " + im.Id.IntegerValue;
                it.SearchText = path ?? "";
                it.GroupPaths["kind"] = new[] { L.T("kind.image") };
                it.GroupPaths["status"] = new[] { L.T("kind.image") };
                list.Add(it);
            }
            return list;
        }

        private static string PathOf(RevitLinkType lt)
        {
            try
            {
                var r = lt.GetExternalFileReference();
                if (r == null) return null;
                return ModelPathUtils.ConvertModelPathToUserVisiblePath(r.GetAbsolutePath());
            }
            catch { return null; }
        }

        public static string StatusText(LinkedFileStatus s, out bool bad)
        {
            bad = false;
            switch (s)
            {
                case LinkedFileStatus.Loaded: return L.T("status.loaded");
                case LinkedFileStatus.Unloaded: bad = true; return L.T("status.unloaded");
                case LinkedFileStatus.NotFound: bad = true; return L.T("status.notFound");
                case LinkedFileStatus.LocallyUnloaded: bad = true; return L.T("status.locallyUnloaded");
                case LinkedFileStatus.InClosedWorkset: return L.T("status.closedWorkset");
                default: return s.ToString();
            }
        }
    }
}
