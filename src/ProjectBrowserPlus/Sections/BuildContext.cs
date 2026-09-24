using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ProjectBrowserPlus.Core;

namespace ProjectBrowserPlus.Sections
{
    /// <summary>
    /// Everything a section builder needs, computed once per rebuild inside the API context.
    /// Expensive lookups (view→sheet map, instance counts) are lazy and cached for the rebuild.
    /// </summary>
    public sealed class BuildContext
    {
        public Document Doc { get; }
        public UIDocument UiDoc { get; }
        public Favorites Fav { get; }
        public Core.Settings S => Core.Settings.Current;
        public HashSet<int> OpenViewIds { get; } = new HashSet<int>();
        public int ActiveViewId { get; }

        private Dictionary<int, List<string>> _viewSheets;
        private Dictionary<int, int> _instanceCounts;
        private Dictionary<int, string> _worksets;
        private BrowserOrganization _viewOrg, _sheetOrg;
        private Dictionary<int, string> _titleBlocks;

        public BuildContext(UIDocument uidoc)
        {
            UiDoc = uidoc;
            Doc = uidoc.Document;
            Fav = Favorites.For(Doc);
            try
            {
                ActiveViewId = uidoc.ActiveGraphicalView?.Id.IntegerValue ?? uidoc.ActiveView?.Id.IntegerValue ?? -1;
                foreach (var uv in uidoc.GetOpenUIViews()) OpenViewIds.Add(uv.ViewId.IntegerValue);
            }
            catch { }
        }

        /// <summary>view id → sheet numbers it is placed on (legends may be on many).</summary>
        public Dictionary<int, List<string>> ViewSheets
        {
            get
            {
                if (_viewSheets != null) return _viewSheets;
                _viewSheets = new Dictionary<int, List<string>>();
                var sheets = new FilteredElementCollector(Doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                    .ToDictionary(s => s.Id.IntegerValue, s => s.SheetNumber);
                void add(int vid, ElementId sheetId)
                {
                    if (!sheets.TryGetValue(sheetId.IntegerValue, out var num)) return;
                    if (!_viewSheets.TryGetValue(vid, out var l)) _viewSheets[vid] = l = new List<string>();
                    if (!l.Contains(num)) l.Add(num);
                }
                foreach (Viewport vp in new FilteredElementCollector(Doc).OfClass(typeof(Viewport)))
                    add(vp.ViewId.IntegerValue, vp.SheetId);
                foreach (ScheduleSheetInstance si in new FilteredElementCollector(Doc).OfClass(typeof(ScheduleSheetInstance)))
                    if (!si.IsTitleblockRevisionSchedule) add(si.ScheduleId.IntegerValue, si.OwnerViewId);
                return _viewSheets;
            }
        }

        /// <summary>type id → number of instances in the model.</summary>
        public Dictionary<int, int> InstanceCounts
        {
            get
            {
                if (_instanceCounts != null) return _instanceCounts;
                _instanceCounts = new Dictionary<int, int>();
                if (!S.CountInstances) return _instanceCounts;
                foreach (var e in new FilteredElementCollector(Doc).WhereElementIsNotElementType())
                {
                    ElementId t;
                    try { t = e.GetTypeId(); } catch { continue; }
                    if (t == null || t == ElementId.InvalidElementId) continue;
                    var k = t.IntegerValue;
                    _instanceCounts[k] = _instanceCounts.TryGetValue(k, out var c) ? c + 1 : 1;
                }
                return _instanceCounts;
            }
        }

        public string WorksetName(Element e)
        {
            try
            {
                if (!Doc.IsWorkshared) return null;
                if (_worksets == null)
                    _worksets = new FilteredWorksetCollector(Doc).OfKind(WorksetKind.UserWorkset).ToDictionary(w => w.Id.IntegerValue, w => w.Name);
                return _worksets.TryGetValue(e.WorksetId.IntegerValue, out var n) ? n : null;
            }
            catch { return null; }
        }

        public BrowserOrganization ViewOrganization => _viewOrg ?? (_viewOrg = BrowserOrganization.GetCurrentBrowserOrganizationForViews(Doc));
        public BrowserOrganization SheetOrganization => _sheetOrg ?? (_sheetOrg = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(Doc));

        public string[] StandardFolders(BrowserOrganization org, ElementId id)
        {
            try
            {
                return org.GetFolderItems(id).Select(f => f.Name).Where(n => !string.IsNullOrEmpty(n)).ToArray();
            }
            catch { return Array.Empty<string>(); }
        }

        public string TitleBlockName(ElementId sheetId)
        {
            if (_titleBlocks == null)
            {
                _titleBlocks = new Dictionary<int, string>();
                foreach (FamilyInstance tb in new FilteredElementCollector(Doc).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType())
                {
                    var sid = tb.OwnerViewId.IntegerValue;
                    if (!_titleBlocks.ContainsKey(sid)) _titleBlocks[sid] = tb.Symbol?.Name;
                }
            }
            return _titleBlocks.TryGetValue(sheetId.IntegerValue, out var n) ? n : null;
        }

        public static string Param(Element e, BuiltInParameter bip)
        {
            try
            {
                var p = e.get_Parameter(bip);
                if (p == null || !p.HasValue) return null;
                return p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
            }
            catch { return null; }
        }

        public static string Param(Element e, string name)
        {
            try
            {
                var p = e.LookupParameter(name);
                if (p == null || !p.HasValue) return null;
                return p.StorageType == StorageType.String ? p.AsString() : p.AsValueString();
            }
            catch { return null; }
        }

        public string FormatLength(double feet)
        {
            try { return UnitFormatUtils.Format(Doc.GetUnits(), SpecTypeId.Length, feet, false); }
            catch { return (feet * 304.8).ToString("0") + " mm"; }
        }
    }
}
