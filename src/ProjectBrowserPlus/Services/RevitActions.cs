using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Settings = ProjectBrowserPlus.Core.Settings;
using Autodesk.Revit.UI;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.Services
{
    /// <summary>
    /// All model-changing / UI-changing operations. Every method runs inside the API context
    /// (they are invoked through <see cref="RevitTask"/>).
    /// </summary>
    public static class RevitActions
    {
        public const string TxPrefix = "PB+: ";

        // ---------- navigation ----------

        public static void OpenView(UIApplication app, ElementId id)
        {
            var uidoc = app.ActiveUIDocument;
            var view = uidoc?.Document.GetElement(id) as View;
            if (view == null) return;
            if (view.IsTemplate) return;
            try { uidoc.ActiveView = view; }
            catch { uidoc.RequestViewChange(view); }
            try { Favorites.For(uidoc.Document).PushRecent(view.UniqueId); } catch { }
            if (Settings.Current.CloseOthersOnOpen) CloseOtherViews(app, view.Id);
        }

        public static void CloseOtherViews(UIApplication app, ElementId keep)
        {
            var uidoc = app.ActiveUIDocument;
            if (uidoc == null) return;
            foreach (var uv in uidoc.GetOpenUIViews())
            {
                if (uv.ViewId == keep) continue;
                try { uv.Close(); } catch { }
            }
        }

        public static void CloseViews(UIApplication app, IEnumerable<ElementId> ids)
        {
            var uidoc = app.ActiveUIDocument;
            if (uidoc == null) return;
            var set = new HashSet<int>(ids.Select(i => i.IntegerValue));
            foreach (var uv in uidoc.GetOpenUIViews())
                if (set.Contains(uv.ViewId.IntegerValue)) { try { uv.Close(); } catch { } }
        }

        /// <summary>Open the (first) sheet a view is placed on.</summary>
        public static bool OpenSheetOf(UIApplication app, ElementId viewId)
        {
            var doc = app.ActiveUIDocument.Document;
            foreach (Viewport vp in new FilteredElementCollector(doc).OfClass(typeof(Viewport)))
                if (vp.ViewId == viewId) { OpenView(app, vp.SheetId); return true; }
            foreach (ScheduleSheetInstance si in new FilteredElementCollector(doc).OfClass(typeof(ScheduleSheetInstance)))
                if (si.ScheduleId == viewId) { OpenView(app, si.OwnerViewId); return true; }
            return false;
        }

        public static void ShowElements(UIApplication app, ICollection<ElementId> ids)
        {
            if (ids.Count == 0) return;
            try { app.ActiveUIDocument.ShowElements(ids); } catch { }
        }

        public static void Select(UIApplication app, ICollection<ElementId> ids)
        {
            try { app.ActiveUIDocument.Selection.SetElementIds(ids); } catch { }
        }

        // ---------- selection of instances ----------

        public static ICollection<ElementId> InstancesOfType(Document doc, ElementId typeId, View inView)
        {
            var col = inView != null ? new FilteredElementCollector(doc, inView.Id) : new FilteredElementCollector(doc);
            col.WhereElementIsNotElementType();
            var type = doc.GetElement(typeId) as ElementType;
            if (type is FamilySymbol)
                return col.WherePasses(new FamilyInstanceFilter(doc, typeId)).ToElementIds();
            if (type?.Category != null) col.OfCategoryId(type.Category.Id);
            return col.Where(e => { try { return e.GetTypeId() == typeId; } catch { return false; } }).Select(e => e.Id).ToList();
        }

        public static ICollection<ElementId> InstancesOfFamily(Document doc, ElementId familyId, View inView)
        {
            var fam = doc.GetElement(familyId) as Family;
            if (fam == null) return new List<ElementId>();
            var ids = new List<ElementId>();
            foreach (var sid in fam.GetFamilySymbolIds()) ids.AddRange(InstancesOfType(doc, sid, inView));
            return ids;
        }

        public static ICollection<ElementId> InstancesOfGroup(Document doc, ElementId groupTypeId, View inView)
        {
            var col = inView != null ? new FilteredElementCollector(doc, inView.Id) : new FilteredElementCollector(doc);
            return col.OfClass(typeof(Group)).Where(g => g.GetTypeId() == groupTypeId).Select(g => g.Id).ToList();
        }

        public static ICollection<ElementId> InstancesOfLink(Document doc, ElementId linkTypeId)
        {
            return new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Where(i => i.GetTypeId() == linkTypeId).Select(i => i.Id).ToList();
        }

        public static ICollection<ElementId> InstancesOf(Document doc, BrowserItem it, View inView)
        {
            switch (it.Kind)
            {
                case ItemKind.FamilyType: return InstancesOfType(doc, it.Id, inView);
                case ItemKind.Family: return it.Id == ElementId.InvalidElementId ? SystemFamilyInstances(doc, it, inView) : InstancesOfFamily(doc, it.Id, inView);
                case ItemKind.Group: return InstancesOfGroup(doc, it.Id, inView);
                case ItemKind.Link: return InstancesOfLink(doc, it.Id);
                default: return new List<ElementId>();
            }
        }

        private static ICollection<ElementId> SystemFamilyInstances(Document doc, BrowserItem fam, View inView)
        {
            var ids = new List<ElementId>();
            foreach (var t in fam.Children) ids.AddRange(InstancesOfType(doc, t.Id, inView));
            return ids;
        }

        // ---------- placement ----------

        public static string PlaceType(UIApplication app, ElementId typeId)
        {
            var uidoc = app.ActiveUIDocument;
            var doc = uidoc.Document;
            var type = doc.GetElement(typeId) as ElementType;
            if (type == null) return L.T("msg.notFound");
            if (type is FamilySymbol fs && !fs.IsActive)
            {
                using (var t = new Transaction(doc, TxPrefix + L.T("tx.activateType")))
                {
                    t.Start(); fs.Activate(); t.Commit();
                }
            }
            try
            {
                uidoc.PostRequestForElementTypePlacement(type);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ---------- rename / renumber (draft application) ----------

        public static string ApplyChanges(UIApplication app, IList<PendingChange> changes)
        {
            var doc = app.ActiveUIDocument.Document;
            var errors = new List<string>();
            using (var tg = new TransactionGroup(doc, TxPrefix + L.T("tx.applyDraft")))
            {
                tg.Start();
                // pass 1: sheet numbers get temporary unique values, so A→B, B→A swaps work
                var renumbers = changes.Where(c => c.Kind == ChangeKind.Renumber).ToList();
                if (renumbers.Count > 0)
                {
                    using (var t = new Transaction(doc, TxPrefix + L.T("tx.renumber")))
                    {
                        t.Start();
                        var tmp = new Dictionary<PendingChange, string>();
                        foreach (var c in renumbers)
                        {
                            var s = doc.GetElement(c.Id) as ViewSheet;
                            if (s == null) continue;
                            var tv = "~" + Guid.NewGuid().ToString("N").Substring(0, 10);
                            try { s.SheetNumber = tv; tmp[c] = tv; } catch (Exception ex) { errors.Add(c.OldValue + ": " + ex.Message); }
                        }
                        foreach (var c in renumbers)
                        {
                            var s = doc.GetElement(c.Id) as ViewSheet;
                            if (s == null || !tmp.ContainsKey(c)) continue;
                            try { s.SheetNumber = c.NewValue; }
                            catch (Exception ex) { errors.Add(c.OldValue + " → " + c.NewValue + ": " + ex.Message); try { s.SheetNumber = c.OldValue; } catch { } }
                        }
                        t.Commit();
                    }
                }
                var renames = changes.Where(c => c.Kind == ChangeKind.Rename).ToList();
                if (renames.Count > 0)
                {
                    using (var t = new Transaction(doc, TxPrefix + L.T("tx.rename")))
                    {
                        t.Start();
                        foreach (var c in renames)
                        {
                            var e = doc.GetElement(c.Id);
                            if (e == null) { errors.Add(c.OldValue + ": " + L.T("msg.notFound")); continue; }
                            try { e.Name = c.NewValue; }
                            catch (Exception ex) { errors.Add(c.OldValue + " → " + c.NewValue + ": " + ex.Message); }
                        }
                        t.Commit();
                    }
                }
                tg.Assimilate();
            }
            return errors.Count == 0 ? null : string.Join("\n", errors);
        }

        // ---------- duplicate ----------

        public static string DuplicateViews(UIApplication app, IList<ElementId> ids, ViewDuplicateOption option, int copies, string suffix)
        {
            var doc = app.ActiveUIDocument.Document;
            var errors = new List<string>();
            int made = 0;
            using (var t = new Transaction(doc, TxPrefix + L.T("tx.duplicate")))
            {
                t.Start();
                foreach (var id in ids)
                {
                    var v = doc.GetElement(id) as View;
                    if (v == null) continue;
                    var opt = option;
                    if (!v.CanViewBeDuplicated(opt))
                    {
                        if (v.CanViewBeDuplicated(ViewDuplicateOption.Duplicate)) opt = ViewDuplicateOption.Duplicate;
                        else { errors.Add(v.Name + ": " + L.T("msg.cannotDuplicate")); continue; }
                    }
                    for (int i = 0; i < copies; i++)
                    {
                        try
                        {
                            var nid = v.Duplicate(opt);
                            var nv = doc.GetElement(nid) as View;
                            if (nv != null && !string.IsNullOrEmpty(suffix))
                            {
                                var baseName = v.Name + suffix.Replace("{n}", (i + 1).ToString());
                                nv.Name = UniqueViewName(doc, baseName);
                            }
                            made++;
                        }
                        catch (Exception ex) { errors.Add(v.Name + ": " + ex.Message); }
                    }
                }
                t.Commit();
            }
            var msg = string.Format(L.T("msg.duplicated"), made);
            return errors.Count == 0 ? msg : msg + "\n" + string.Join("\n", errors);
        }

        public static string UniqueViewName(Document doc, string name)
        {
            var existing = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(View)).Select(v => v.Name), StringComparer.OrdinalIgnoreCase);
            if (!existing.Contains(name)) return name;
            for (int i = 2; i < 1000; i++)
            {
                var n = name + " (" + i + ")";
                if (!existing.Contains(n)) return n;
            }
            return name + " " + Guid.NewGuid().ToString("N").Substring(0, 4);
        }

        // ---------- delete ----------

        public static string Delete(UIApplication app, IList<ElementId> ids)
        {
            var doc = app.ActiveUIDocument.Document;
            var active = app.ActiveUIDocument.ActiveView?.Id;
            var toDelete = ids.Where(i => i != active).ToList();
            int n = 0;
            using (var t = new Transaction(doc, TxPrefix + L.T("tx.delete")))
            {
                t.Start();
                var fh = t.GetFailureHandlingOptions();
                fh.SetFailuresPreprocessor(new WarningSwallower());
                t.SetFailureHandlingOptions(fh);
                foreach (var id in toDelete)
                {
                    try { n += doc.Delete(id).Count > 0 ? 1 : 0; } catch (Exception ex) { Log.Warn("delete " + id.IntegerValue + ": " + ex.Message); }
                }
                t.Commit();
            }
            var msg = string.Format(L.T("msg.deleted"), n);
            if (toDelete.Count != ids.Count) msg += "\n" + L.T("msg.activeSkipped");
            return msg;
        }

        private class WarningSwallower : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor a)
            {
                foreach (var f in a.GetFailureMessages())
                    if (f.GetSeverity() == FailureSeverity.Warning) a.DeleteWarning(f);
                return FailureProcessingResult.Continue;
            }
        }

        // ---------- templates ----------

        public static List<KeyValuePair<ElementId, string>> ViewTemplates(Document doc, View forView)
        {
            var list = new List<KeyValuePair<ElementId, string>>();
            foreach (View v in new FilteredElementCollector(doc).OfClass(typeof(View)))
            {
                if (!v.IsTemplate) continue;
                if (forView != null) { try { if (!forView.IsValidViewTemplate(v.Id)) continue; } catch { } }
                list.Add(new KeyValuePair<ElementId, string>(v.Id, v.Name));
            }
            return list.OrderBy(k => k.Value, NaturalComparer.Instance).ToList();
        }

        public static string ApplyTemplate(UIApplication app, IList<ElementId> viewIds, ElementId templateId)
        {
            var doc = app.ActiveUIDocument.Document;
            var errors = new List<string>(); int n = 0;
            using (var t = new Transaction(doc, TxPrefix + L.T("tx.template")))
            {
                t.Start();
                foreach (var id in viewIds)
                {
                    var v = doc.GetElement(id) as View;
                    if (v == null) continue;
                    try
                    {
                        if (templateId == ElementId.InvalidElementId || v.IsValidViewTemplate(templateId)) { v.ViewTemplateId = templateId; n++; }
                        else errors.Add(v.Name + ": " + L.T("msg.templateIncompatible"));
                    }
                    catch (Exception ex) { errors.Add(v.Name + ": " + ex.Message); }
                }
                t.Commit();
            }
            var msg = string.Format(L.T("msg.templateApplied"), n);
            return errors.Count == 0 ? msg : msg + "\n" + string.Join("\n", errors);
        }

        // ---------- sheets ----------

        public static List<KeyValuePair<ElementId, string>> TitleBlockTypes(Document doc)
        {
            return new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType().Cast<FamilySymbol>()
                .Select(s => new KeyValuePair<ElementId, string>(s.Id, s.Family.Name + " : " + s.Name))
                .OrderBy(k => k.Value, NaturalComparer.Instance).ToList();
        }

        public static string CreateSheets(UIApplication app, ElementId titleBlockId, string startNumber, string name, int count, bool placeholder, out List<ElementId> created)
        {
            var doc = app.ActiveUIDocument.Document;
            created = new List<ElementId>();
            var existing = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Select(s => s.SheetNumber));
            var errors = new List<string>();
            using (var t = new Transaction(doc, TxPrefix + L.T("tx.createSheets")))
            {
                t.Start();
                var number = startNumber;
                for (int i = 0; i < count; i++)
                {
                    while (existing.Contains(number)) number = Increment(number);
                    try
                    {
                        var s = placeholder ? ViewSheet.CreatePlaceholder(doc) : ViewSheet.Create(doc, titleBlockId ?? ElementId.InvalidElementId);
                        s.SheetNumber = number;
                        if (!string.IsNullOrWhiteSpace(name)) s.Name = name.Replace("{n}", (i + 1).ToString()).Replace("{num}", number);
                        created.Add(s.Id);
                        existing.Add(number);
                    }
                    catch (Exception ex) { errors.Add(number + ": " + ex.Message); }
                    number = Increment(number);
                }
                t.Commit();
            }
            var msg = string.Format(L.T("msg.sheetsCreated"), created.Count);
            return errors.Count == 0 ? msg : msg + "\n" + string.Join("\n", errors);
        }

        /// <summary>"A-101" → "A-102", "05" → "06", "AR" → "AR1".</summary>
        public static string Increment(string number)
        {
            if (string.IsNullOrEmpty(number)) return "1";
            int end = number.Length, start = end;
            while (start > 0 && char.IsDigit(number[start - 1])) start--;
            if (start == end) return number + "1";
            var digits = number.Substring(start);
            var val = long.Parse(digits) + 1;
            return number.Substring(0, start) + val.ToString().PadLeft(digits.Length, '0');
        }

        public static string PlaceOnSheets(UIApplication app, IList<ElementId> viewIds, IList<ElementId> sheetIds, XYZ point, bool sameAsReference)
        {
            var doc = app.ActiveUIDocument.Document;
            var errors = new List<string>(); int n = 0;
            using (var t = new Transaction(doc, TxPrefix + L.T("tx.placeOnSheets")))
            {
                t.Start();
                foreach (var vid in viewIds)
                {
                    var view = doc.GetElement(vid) as View;
                    if (view == null) continue;
                    XYZ pt = point;
                    if (sameAsReference)
                    {
                        var refPt = ExistingPlacement(doc, vid);
                        if (refPt != null) pt = refPt;
                    }
                    foreach (var sid in sheetIds)
                    {
                        var sheet = doc.GetElement(sid) as ViewSheet;
                        if (sheet == null) continue;
                        var p = pt ?? SheetCenter(doc, sheet);
                        try
                        {
                            if (view is ViewSchedule)
                            {
                                ScheduleSheetInstance.Create(doc, sid, vid, p); n++;
                            }
                            else if (Viewport.CanAddViewToSheet(doc, sid, vid))
                            {
                                Viewport.Create(doc, sid, vid, p); n++;
                            }
                            else errors.Add(view.Name + " → " + sheet.SheetNumber + ": " + L.T("msg.cannotPlace"));
                        }
                        catch (Exception ex) { errors.Add(view.Name + " → " + sheet.SheetNumber + ": " + ex.Message); }
                    }
                }
                t.Commit();
            }
            var msg = string.Format(L.T("msg.placed"), n);
            return errors.Count == 0 ? msg : msg + "\n" + string.Join("\n", errors);
        }

        private static XYZ ExistingPlacement(Document doc, ElementId viewId)
        {
            foreach (Viewport vp in new FilteredElementCollector(doc).OfClass(typeof(Viewport)))
                if (vp.ViewId == viewId) { try { return vp.GetBoxCenter(); } catch { } }
            foreach (ScheduleSheetInstance si in new FilteredElementCollector(doc).OfClass(typeof(ScheduleSheetInstance)))
                if (si.ScheduleId == viewId) { try { return si.Point; } catch { } }
            return null;
        }

        public static XYZ SheetCenter(Document doc, ViewSheet sheet)
        {
            try
            {
                var tb = new FilteredElementCollector(doc, sheet.Id).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType().FirstOrDefault();
                var bb = tb?.get_BoundingBox(sheet);
                if (bb != null) return (bb.Min + bb.Max) / 2;
                var o = sheet.Outline;
                return new XYZ((o.Min.U + o.Max.U) / 2, (o.Min.V + o.Max.V) / 2, 0);
            }
            catch { return XYZ.Zero; }
        }

        public static string CreateSheetForViews(UIApplication app, IList<ElementId> viewIds, ElementId titleBlockId)
        {
            var doc = app.ActiveUIDocument.Document;
            int n = 0; var errors = new List<string>();
            using (var t = new Transaction(doc, TxPrefix + L.T("tx.createSheets")))
            {
                t.Start();
                foreach (var vid in viewIds)
                {
                    var view = doc.GetElement(vid) as View;
                    if (view == null) continue;
                    try
                    {
                        var sheet = ViewSheet.Create(doc, titleBlockId ?? ElementId.InvalidElementId);
                        try { sheet.Name = view.Name; } catch { }
                        var c = SheetCenter(doc, sheet);
                        if (view is ViewSchedule) ScheduleSheetInstance.Create(doc, sheet.Id, vid, c);
                        else if (Viewport.CanAddViewToSheet(doc, sheet.Id, vid)) Viewport.Create(doc, sheet.Id, vid, c);
                        n++;
                    }
                    catch (Exception ex) { errors.Add(view.Name + ": " + ex.Message); }
                }
                t.Commit();
            }
            var msg = string.Format(L.T("msg.sheetsCreated"), n);
            return errors.Count == 0 ? msg : msg + "\n" + string.Join("\n", errors);
        }

        // ---------- views from levels ----------

        public static List<KeyValuePair<ElementId, string>> PlanViewTypes(Document doc)
        {
            return new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .Where(t => t.ViewFamily == ViewFamily.FloorPlan || t.ViewFamily == ViewFamily.CeilingPlan || t.ViewFamily == ViewFamily.StructuralPlan || t.ViewFamily == ViewFamily.AreaPlan)
                .Select(t => new KeyValuePair<ElementId, string>(t.Id, Sections.ViewHelpers.ViewFamilyLabel(t.ViewFamily) + " : " + t.Name))
                .OrderBy(k => k.Value, NaturalComparer.Instance).ToList();
        }

        public static string CreatePlans(UIApplication app, IList<ElementId> levelIds, ElementId viewTypeId, string nameTemplate, ElementId templateId)
        {
            var doc = app.ActiveUIDocument.Document;
            int n = 0; var errors = new List<string>();
            var vft = doc.GetElement(viewTypeId) as ViewFamilyType;
            if (vft == null) return L.T("msg.notFound");
            using (var t = new Transaction(doc, TxPrefix + L.T("tx.createViews")))
            {
                t.Start();
                foreach (var lid in levelIds)
                {
                    var level = doc.GetElement(lid) as Level;
                    if (level == null) continue;
                    try
                    {
                        ViewPlan vp;
                        if (vft.ViewFamily == ViewFamily.AreaPlan)
                        {
                            var scheme = new FilteredElementCollector(doc).OfClass(typeof(AreaScheme)).FirstOrDefault();
                            if (scheme == null) { errors.Add(level.Name + ": " + L.T("msg.noAreaScheme")); continue; }
                            vp = ViewPlan.CreateAreaPlan(doc, scheme.Id, lid);
                        }
                        else vp = ViewPlan.Create(doc, viewTypeId, lid);
                        if (!string.IsNullOrWhiteSpace(nameTemplate))
                            vp.Name = UniqueViewName(doc, nameTemplate.Replace("{level}", level.Name).Replace("{type}", vft.Name));
                        if (templateId != null && templateId != ElementId.InvalidElementId && vp.IsValidViewTemplate(templateId)) vp.ViewTemplateId = templateId;
                        n++;
                    }
                    catch (Exception ex) { errors.Add(level.Name + ": " + ex.Message); }
                }
                t.Commit();
            }
            var msg = string.Format(L.T("msg.viewsCreated"), n);
            return errors.Count == 0 ? msg : msg + "\n" + string.Join("\n", errors);
        }

        // ---------- links ----------

        public static string ReloadLink(UIApplication app, ElementId id)
        {
            var doc = app.ActiveUIDocument.Document;
            var e = doc.GetElement(id);
            try
            {
                if (e is RevitLinkType rl) { rl.Reload(); return L.T("msg.reloaded"); }
                if (e is CADLinkType cl) { cl.Reload(); return L.T("msg.reloaded"); }
                if (e is ImageType im) { using (var t = new Transaction(doc, TxPrefix + L.T("msg.reloaded"))) { t.Start(); im.Reload(); t.Commit(); } return L.T("msg.reloaded"); }
            }
            catch (Exception ex) { return ex.Message; }
            return L.T("msg.notSupported");
        }

        public static string UnloadLink(UIApplication app, ElementId id)
        {
            var doc = app.ActiveUIDocument.Document;
            try
            {
                if (doc.GetElement(id) is RevitLinkType rl) { rl.Unload(null); return L.T("msg.unloaded"); }
            }
            catch (Exception ex) { return ex.Message; }
            return L.T("msg.notSupported");
        }

        public static string OpenLinkFolder(UIApplication app, ElementId id)
        {
            var doc = app.ActiveUIDocument.Document;
            try
            {
                string path = null;
                var e = doc.GetElement(id);
                if (e is RevitLinkType rl) path = ModelPathUtils.ConvertModelPathToUserVisiblePath(rl.GetExternalFileReference().GetAbsolutePath());
                else if (e is CADLinkType cl && cl.IsExternalFileReference()) path = ModelPathUtils.ConvertModelPathToUserVisiblePath(cl.GetExternalFileReference().GetAbsolutePath());
                else if (e is ImageType im) path = im.Path;
                if (string.IsNullOrEmpty(path)) return L.T("msg.noPath");
                if (File.Exists(path)) System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else if (Directory.Exists(Path.GetDirectoryName(path))) System.Diagnostics.Process.Start("explorer.exe", Path.GetDirectoryName(path));
                else return L.T("msg.noPath") + ": " + path;
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ---------- families ----------

        public static string EditFamily(UIApplication app, ElementId familyId)
        {
            var uidoc = app.ActiveUIDocument;
            var doc = uidoc.Document;
            var fam = doc.GetElement(familyId) as Family;
            if (fam == null) return L.T("msg.notFound");
            if (!fam.IsEditable) return L.T("msg.familyNotEditable");
            try
            {
                var famDoc = doc.EditFamily(fam);
                var dir = Path.Combine(Path.GetTempPath(), "ProjectBrowserPlus", "families");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, SafeFile(fam.Name) + ".rfa");
                famDoc.SaveAs(path, new SaveAsOptions { OverwriteExistingFile = true });
                famDoc.Close(false);
                app.OpenAndActivateDocument(path);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        private static string SafeFile(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        public static string SaveFamily(UIApplication app, ElementId familyId, string folder)
        {
            var doc = app.ActiveUIDocument.Document;
            var fam = doc.GetElement(familyId) as Family;
            if (fam == null || !fam.IsEditable) return L.T("msg.familyNotEditable");
            try
            {
                var famDoc = doc.EditFamily(fam);
                var path = Path.Combine(folder, SafeFile(fam.Name) + ".rfa");
                famDoc.SaveAs(path, new SaveAsOptions { OverwriteExistingFile = true });
                famDoc.Close(false);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ---------- parameters ----------

        public static List<string> CommonParameters(Document doc, IList<ElementId> ids)
        {
            HashSet<string> common = null;
            foreach (var id in ids)
            {
                var e = doc.GetElement(id);
                if (e == null) continue;
                var names = new HashSet<string>();
                foreach (Parameter p in e.Parameters)
                {
                    if (p.IsReadOnly) continue;
                    if (p.StorageType == StorageType.ElementId) continue;
                    names.Add(p.Definition.Name);
                }
                if (common == null) common = names; else common.IntersectWith(names);
            }
            return (common ?? new HashSet<string>()).OrderBy(n => n, NaturalComparer.Instance).ToList();
        }

        public static string SetParameter(UIApplication app, IList<ElementId> ids, string paramName, string value)
        {
            var doc = app.ActiveUIDocument.Document;
            int n = 0; var errors = new List<string>();
            using (var t = new Transaction(doc, TxPrefix + L.T("tx.setParam")))
            {
                t.Start();
                foreach (var id in ids)
                {
                    var e = doc.GetElement(id);
                    var p = e?.LookupParameter(paramName);
                    if (p == null || p.IsReadOnly) continue;
                    try
                    {
                        switch (p.StorageType)
                        {
                            case StorageType.String: p.Set(value ?? ""); break;
                            case StorageType.Integer:
                                if (int.TryParse(value, out var iv)) p.Set(iv);
                                else if (value != null && (value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value.Equals("да", StringComparison.OrdinalIgnoreCase) || value == "true")) p.Set(1);
                                else if (value != null && (value.Equals("no", StringComparison.OrdinalIgnoreCase) || value.Equals("нет", StringComparison.OrdinalIgnoreCase) || value == "false")) p.Set(0);
                                else if (!p.SetValueString(value)) throw new Exception(L.T("msg.badValue"));
                                break;
                            case StorageType.Double:
                                if (!p.SetValueString(value)) throw new Exception(L.T("msg.badValue"));
                                break;
                        }
                        n++;
                    }
                    catch (Exception ex) { errors.Add(e.Name + ": " + ex.Message); }
                }
                t.Commit();
            }
            var msg = string.Format(L.T("msg.paramSet"), n);
            return errors.Count == 0 ? msg : msg + "\n" + string.Join("\n", errors);
        }

        // ---------- misc ----------

        public static string SheetsList(Document doc, out List<KeyValuePair<ElementId, string>> sheets)
        {
            sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Where(s => !s.IsTemplate && !s.IsPlaceholder)
                .OrderBy(s => s.SheetNumber, NaturalComparer.Instance)
                .Select(s => new KeyValuePair<ElementId, string>(s.Id, s.SheetNumber + " - " + s.Name)).ToList();
            return null;
        }
    }
}
