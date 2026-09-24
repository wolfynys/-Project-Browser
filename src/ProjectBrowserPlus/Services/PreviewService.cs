using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Settings = ProjectBrowserPlus.Core.Settings;
using Autodesk.Revit.UI;
using ProjectBrowserPlus.Core;
using ProjectBrowserPlus.Model;

namespace ProjectBrowserPlus.Services
{
    /// <summary>
    /// Type previews come from ElementType.GetPreviewImage (fast, what the Type Selector shows).
    /// View previews are rendered on demand with Document.ExportImage and cached on disk.
    /// </summary>
    public static class PreviewService
    {
        private static readonly Dictionary<string, ImageSource> Cache = new Dictionary<string, ImageSource>();
        private static readonly HashSet<string> Failed = new HashSet<string>();
        private static readonly List<BrowserItem> Queue = new List<BrowserItem>();
        private static bool _scheduled;

        public static string CacheDir => Path.Combine(Path.GetTempPath(), "ProjectBrowserPlus", "previews");

        public static void ClearMemory() { Cache.Clear(); Failed.Clear(); }

        private static string KeyOf(Document doc, BrowserItem it, int size) => Favorites.KeyFor(doc) + ":" + it.IdValue + ":" + size;

        /// <summary>Called from the UI thread when a row with NeedsPreview is realized.</summary>
        public static void Request(BrowserItem it)
        {
            if (it == null || !it.NeedsPreview || it.Preview != null) return;
            lock (Queue)
            {
                if (Queue.Contains(it)) return;
                Queue.Add(it);
                if (_scheduled) return;
                _scheduled = true;
            }
            RevitTask.Run(app => ProcessQueue(app));
        }

        private static void ProcessQueue(UIApplication app)
        {
            List<BrowserItem> batch;
            lock (Queue) { batch = Queue.ToList(); Queue.Clear(); _scheduled = false; }
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null) return;
            var size = Math.Max(48, Settings.Current.PreviewSize);
            foreach (var it in batch)
            {
                try
                {
                    if (it.Preview != null) continue;
                    var key = KeyOf(doc, it, size);
                    if (Cache.TryGetValue(key, out var img)) { it.Preview = img; continue; }
                    if (Failed.Contains(key)) continue;
                    var src = TypePreview(doc, it.Id, size);
                    if (src == null) { Failed.Add(key); it.NeedsPreview = false; continue; }
                    Cache[key] = src;
                    it.Preview = src;
                }
                catch (Exception ex) { Log.Warn("preview failed: " + ex.Message); }
            }
        }

        public static ImageSource TypePreview(Document doc, ElementId id, int size)
        {
            var e = doc.GetElement(id);
            ElementType t = e as ElementType;
            if (t == null && e is Family f)
            {
                var first = f.GetFamilySymbolIds().FirstOrDefault();
                if (first != null) t = doc.GetElement(first) as ElementType;
            }
            if (t == null) return null;
            using (var bmp = t.GetPreviewImage(new System.Drawing.Size(size, size)))
            {
                if (bmp == null) return null;
                return ToSource(bmp);
            }
        }

        public static ImageSource ToSource(Bitmap bmp)
        {
            var h = bmp.GetHbitmap();
            try
            {
                var src = Imaging.CreateBitmapSourceFromHBitmap(h, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally { DeleteObject(h); }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        /// <summary>Render a view/sheet/legend to PNG (slow: seconds). Cached by element id + version.</summary>
        public static ImageSource ViewPreview(Document doc, View view, int pixelSize, bool force)
        {
            var dir = Path.Combine(CacheDir, Favorites.KeyFor(doc));
            Directory.CreateDirectory(dir);
            string ver = "";
            try { ver = doc.GetElement(view.Id).VersionGuid.ToString("N").Substring(0, 8); } catch { }
            var file = Path.Combine(dir, view.Id.IntegerValue + "_" + ver + "_" + pixelSize + ".png");
            if (force || !File.Exists(file))
            {
                foreach (var old in Directory.GetFiles(dir, view.Id.IntegerValue + "_*.png")) { try { File.Delete(old); } catch { } }
                var tmp = Path.Combine(dir, "export_" + view.Id.IntegerValue);
                var opts = new ImageExportOptions
                {
                    ExportRange = ExportRange.SetOfViews,
                    FilePath = tmp,
                    FitDirection = FitDirectionType.Horizontal,
                    HLRandWFViewsFileType = ImageFileType.PNG,
                    ShadowViewsFileType = ImageFileType.PNG,
                    ImageResolution = ImageResolution.DPI_72,
                    ZoomType = ZoomFitType.FitToPage,
                    PixelSize = pixelSize,
                };
                opts.SetViewsAndSheets(new List<ElementId> { view.Id });
                doc.ExportImage(opts);
                var produced = Directory.GetFiles(dir, "export_" + view.Id.IntegerValue + "*.png").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
                if (produced == null) return null;
                if (File.Exists(file)) File.Delete(file);
                File.Move(produced, file);
                foreach (var junk in Directory.GetFiles(dir, "export_" + view.Id.IntegerValue + "*")) { try { File.Delete(junk); } catch { } }
            }
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(file);
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
    }
}
