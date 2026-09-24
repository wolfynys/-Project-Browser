using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.Sections
{
    /// <summary>
    /// Categories → families → types, like the standard browser, plus instance counts,
    /// "unused" detection and lazily generated preview images.
    /// </summary>
    public class FamiliesSection : Section
    {
        public override string Key => "families";
        public override string Icon => "package";

        public override IList<GroupOption> GroupOptions => new List<GroupOption>
        {
            G("category", L.T("group.category")),
            G("kind", L.T("group.familyKind")),
            G("discipline", L.T("group.categoryType")),
            G("flat", L.T("group.flatFamilies")),
            G("types", L.T("group.flatTypes")),
        };

        public override IList<QuickFilter> Filters => new List<QuickFilter>
        {
            F("unused", "circle-x", i => i.IsUnused),
            F("used", "badge-check", i => !i.IsUnused),
            F("loadable", "package", i => !i.IsSystemFamily && !i.IsInPlace),
            F("system", "blocks", i => i.IsSystemFamily),
            F("inplace", "component", i => i.IsInPlace),
            F("fav", "star", i => i.IsFavorite),
        };

        private class FamRec
        {
            public string Category, FamilyName, Kind, CategoryType;
            public ElementId FamilyId;
            public string FamilyUid;
            public bool IsSystem, IsInPlace;
            public List<ElementType> Types = new List<ElementType>();
        }

        public override List<BrowserItem> BuildLeaves(BuildContext ctx) => BuildFamilies(ctx);

        public override List<BrowserItem> BuildTree(BuildContext ctx, string groupKey, SortMode sort)
        {
            var fams = BuildFamilies(ctx);
            if (groupKey == "types")
            {
                var types = new List<BrowserItem>();
                foreach (var f in fams)
                {
                    foreach (var t in f.Children.ToList())
                    {
                        t.Name = f.Name + " : " + t.Name;
                        t.Parent = null;
                        t.GroupPaths["types"] = new[] { f.GroupPaths["category"][0] };
                        types.Add(t);
                    }
                }
                return Grouper.Group(types, "types", sort);
            }
            var tree = Grouper.Group(fams, groupKey, sort);
            foreach (var n in tree) MarkCategories(n);
            return tree;
        }

        private static void MarkCategories(BrowserItem n)
        {
            if (n.Kind == ItemKind.Folder && n.Children.All(c => c.Kind == ItemKind.Family || c.Kind == ItemKind.Category || c.Kind == ItemKind.Folder))
            {
                n.IconKey = n.Children.Any(c => c.Kind == ItemKind.Family) ? "boxes" : "folder";
            }
            foreach (var c in n.Children) if (c.IsFolder) MarkCategories(c);
        }

        private List<BrowserItem> BuildFamilies(BuildContext ctx)
        {
            var doc = ctx.Doc;
            var recs = new Dictionary<string, FamRec>();
            var counts = ctx.InstanceCounts;
            var countEnabled = ctx.S.CountInstances;

            // loadable + in-place families
            var famById = new Dictionary<int, Family>();
            foreach (Family f in new FilteredElementCollector(doc).OfClass(typeof(Family)))
            {
                famById[f.Id.IntegerValue] = f;
            }

            foreach (ElementType et in new FilteredElementCollector(doc).WhereElementIsElementType())
            {
                Category cat;
                try { cat = et.Category; } catch { continue; }
                if (cat == null) continue;
                if (et is ViewFamilyType) continue;
                string famName; string key; FamRec rec;
                var sym = et as FamilySymbol;
                if (sym != null)
                {
                    Family fam = null;
                    try { fam = sym.Family; } catch { }
                    if (fam == null) continue;
                    key = "F" + fam.Id.IntegerValue;
                    if (!recs.TryGetValue(key, out rec))
                    {
                        rec = new FamRec
                        {
                            Category = cat.Name, FamilyName = fam.Name, FamilyId = fam.Id, FamilyUid = fam.UniqueId,
                            IsInPlace = fam.IsInPlace, IsSystem = false,
                            Kind = fam.IsInPlace ? L.T("kind.inplace") : L.T("kind.loadable"),
                            CategoryType = CategoryTypeName(cat),
                        };
                        recs[key] = rec;
                    }
                }
                else
                {
                    famName = SafeFamilyName(et);
                    if (string.IsNullOrEmpty(famName)) continue;
                    key = "S" + cat.Id.IntegerValue + "|" + famName;
                    if (!recs.TryGetValue(key, out rec))
                    {
                        rec = new FamRec
                        {
                            Category = cat.Name, FamilyName = famName, FamilyId = ElementId.InvalidElementId, IsSystem = true,
                            Kind = L.T("kind.system"), CategoryType = CategoryTypeName(cat),
                        };
                        recs[key] = rec;
                    }
                }
                rec.Types.Add(et);
            }

            var result = new List<BrowserItem>(recs.Count);
            foreach (var rec in recs.Values)
            {
                var fi = new BrowserItem
                {
                    Kind = ItemKind.Family, Name = rec.FamilyName, Id = rec.FamilyId, UniqueId = rec.FamilyUid,
                    IconKey = rec.IsInPlace ? "component" : rec.IsSystem ? "blocks" : "package",
                    IsSystemFamily = rec.IsSystem, IsInPlace = rec.IsInPlace,
                };
                fi.IsFavorite = ctx.Fav.IsFavorite(rec.FamilyUid);
                int famInstances = 0;
                foreach (var t in rec.Types.OrderBy(t => t.Name, NaturalComparer.Instance))
                {
                    var ti = new BrowserItem
                    {
                        Kind = ItemKind.FamilyType, Name = t.Name, Id = t.Id, UniqueId = t.UniqueId, IconKey = "square",
                        IsSystemFamily = rec.IsSystem, IsInPlace = rec.IsInPlace, IsPlaceable = !rec.IsInPlace, NeedsPreview = true,
                    };
                    ti.IsFavorite = ctx.Fav.IsFavorite(t.UniqueId);
                    var n = counts.TryGetValue(t.Id.IntegerValue, out var c) ? c : 0;
                    ti.Instances = countEnabled ? n : -1;
                    ti.IsUnused = countEnabled && n == 0;
                    famInstances += n;
                    ti.Badges = countEnabled ? (n == 0 ? L.T("badge.unused") : n + " " + L.T("badge.inst")) : null;
                    var mark = BuildContext.Param(t, BuiltInParameter.ALL_MODEL_TYPE_MARK);
                    var desc = BuildContext.Param(t, BuiltInParameter.ALL_MODEL_DESCRIPTION);
                    ti.Tooltip = string.Join("\n", new[] { rec.FamilyName + " : " + t.Name, rec.Category, mark != null ? L.T("tip.typeMark") + ": " + mark : null, desc != null ? desc : null, countEnabled ? L.T("tip.instances") + ": " + n : null, "Id: " + t.Id.IntegerValue }.Where(x => x != null));
                    ti.SearchText = string.Join(" ", new[] { rec.FamilyName, rec.Category, mark, desc }.Where(x => x != null));
                    fi.Add(ti);
                }
                fi.Instances = countEnabled ? famInstances : -1;
                fi.IsUnused = countEnabled && famInstances == 0;
                fi.Badges = ViewHelpers.BuildBadges(rec.Types.Count + " " + L.T("badge.types"), countEnabled ? (famInstances == 0 ? L.T("badge.unused") : famInstances + " " + L.T("badge.inst")) : null, rec.IsInPlace ? L.T("badge.inplace") : null);
                fi.Tooltip = string.Join("\n", new[] { rec.FamilyName, rec.Category + " · " + rec.Kind, L.T("tip.types") + ": " + rec.Types.Count, countEnabled ? L.T("tip.instances") + ": " + famInstances : null, rec.FamilyId != ElementId.InvalidElementId ? "Id: " + rec.FamilyId.IntegerValue : null }.Where(x => x != null));
                fi.SearchText = rec.Category + " " + rec.Kind;
                fi.GroupPaths["category"] = new[] { rec.Category };
                fi.GroupPaths["kind"] = new[] { rec.Kind, rec.Category };
                fi.GroupPaths["discipline"] = new[] { rec.CategoryType, rec.Category };
                fi.GroupPaths["flat"] = Array.Empty<string>();
                result.Add(fi);
            }
            return result;
        }

        private static string SafeFamilyName(ElementType et)
        {
            try { return et.FamilyName; } catch { return null; }
        }

        private static string CategoryTypeName(Category c)
        {
            try
            {
                switch (c.CategoryType)
                {
                    case CategoryType.Model: return L.T("cattype.model");
                    case CategoryType.Annotation: return L.T("cattype.annotation");
                    case CategoryType.AnalyticalModel: return L.T("cattype.analytical");
                    case CategoryType.Internal: return L.T("cattype.internal");
                }
            }
            catch { }
            return "—";
        }
    }
}
