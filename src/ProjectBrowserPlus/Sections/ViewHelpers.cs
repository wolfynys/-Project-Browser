using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.Sections
{
    internal static class ViewHelpers
    {
        public static bool IsBrowserView(View v)
        {
            if (v == null || v.IsTemplate) return false;
            switch (v.ViewType)
            {
                case ViewType.Internal:
                case ViewType.ProjectBrowser:
                case ViewType.SystemBrowser:
                case ViewType.Undefined:
                    return false;
            }
            return true;
        }

        public static bool IsGraphicalView(View v)
        {
            if (!IsBrowserView(v)) return false;
            return !(v is ViewSheet) && !(v is ViewSchedule) && v.ViewType != ViewType.Legend;
        }

        public static string IconFor(View v)
        {
            switch (v.ViewType)
            {
                case ViewType.FloorPlan: return "layout-grid";
                case ViewType.CeilingPlan: return "layout-dashboard";
                case ViewType.AreaPlan: return "land-plot";
                case ViewType.EngineeringPlan: return "grid-2x2";
                case ViewType.Elevation: return "panel-left";
                case ViewType.Section: return "columns-3";
                case ViewType.Detail: return "zoom-in";
                case ViewType.ThreeD: return "cuboid";
                case ViewType.DraftingView: return "square-pen";
                case ViewType.Legend: return "book-open";
                case ViewType.Schedule: return "table";
                case ViewType.ColumnSchedule: return "table-2";
                case ViewType.PanelSchedule: return "table-properties";
                case ViewType.DrawingSheet: return "sheet";
                case ViewType.Walkthrough: return "user";
                case ViewType.Rendering: return "image";
                case ViewType.CostReport:
                case ViewType.LoadsReport:
                case ViewType.PresureLossReport: return "scroll-text";
                default: return "eye";
            }
        }

        public static string TypeName(BuildContext ctx, View v)
        {
            try
            {
                var t = ctx.Doc.GetElement(v.GetTypeId()) as ElementType;
                return t?.Name;
            }
            catch { return null; }
        }

        public static string ViewFamilyName(BuildContext ctx, View v)
        {
            try
            {
                var t = ctx.Doc.GetElement(v.GetTypeId()) as ViewFamilyType;
                if (t != null) return ViewFamilyLabel(t.ViewFamily);
            }
            catch { }
            return v.ViewType.ToString();
        }

        public static string ViewFamilyLabel(ViewFamily f)
        {
            var key = "vf." + f;
            var s = L.T(key);
            return s == key ? f.ToString() : s;
        }

        public static string TemplateName(BuildContext ctx, View v)
        {
            try
            {
                var id = v.ViewTemplateId;
                if (id == null || id == ElementId.InvalidElementId) return null;
                return ctx.Doc.GetElement(id)?.Name;
            }
            catch { return null; }
        }

        public static string ScaleText(View v)
        {
            try
            {
                if (v.ViewType == ViewType.ThreeD && v is View3D v3 && v3.IsPerspective) return null;
                var s = v.Scale;
                return s > 0 ? "1:" + s : null;
            }
            catch { return null; }
        }

        public static string LevelName(View v)
        {
            try { return v.GenLevel?.Name; } catch { return null; }
        }

        public static void FillCommon(BuildContext ctx, BrowserItem it, View v)
        {
            it.UniqueId = v.UniqueId;
            it.IsFavorite = ctx.Fav.IsFavorite(v.UniqueId);
            it.IsCurrent = v.Id.IntegerValue == ctx.ActiveViewId;
            it.IsOpen = ctx.OpenViewIds.Contains(v.Id.IntegerValue);
            it.Note = ctx.Fav.GetNote(v.UniqueId);
            it.TagColor = ctx.Fav.GetColor(v.UniqueId);
            if (ctx.ViewSheets.TryGetValue(v.Id.IntegerValue, out var sheets))
            {
                it.IsOnSheet = true;
                it.SheetNumber = string.Join(", ", sheets);
            }
        }

        public static string BuildBadges(params string[] parts)
        {
            var l = parts.Where(p => !string.IsNullOrEmpty(p)).ToList();
            return l.Count == 0 ? null : string.Join("  ·  ", l);
        }
    }
}
