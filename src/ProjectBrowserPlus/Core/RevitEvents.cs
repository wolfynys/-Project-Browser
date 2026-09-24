using System;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace ProjectBrowserPlus.Core
{
    /// <summary>
    /// Bridges Revit application events to the pane. Handlers run in the API context; the pane
    /// only stores flags and refreshes later through RevitTask.
    /// </summary>
    public static class RevitEvents
    {
        public static event Action<int> ViewActivated;        // active view element id
        public static event Action DocumentChanged;           // model changed (debounced by UI)
        public static event Action DocumentSwitched;          // opened / closed / activated
        private static bool _attachedApp, _attachedCtrl;

        public static void Attach(UIControlledApplication app)
        {
            if (_attachedCtrl) return;
            _attachedCtrl = true;
            app.ViewActivated += OnViewActivated;
            app.ControlledApplication.DocumentChanged += OnDocumentChanged;
            app.ControlledApplication.DocumentOpened += (s, e) => DocumentSwitched?.Invoke();
            app.ControlledApplication.DocumentClosed += (s, e) => DocumentSwitched?.Invoke();
            app.ControlledApplication.DocumentSynchronizedWithCentral += (s, e) => DocumentChanged?.Invoke();
        }

        public static void Attach(UIApplication app)
        {
            if (_attachedApp || _attachedCtrl) return;
            _attachedApp = true;
            app.ViewActivated += OnViewActivated;
            app.Application.DocumentChanged += OnDocumentChanged;
            app.Application.DocumentOpened += (s, e) => DocumentSwitched?.Invoke();
            app.Application.DocumentClosed += (s, e) => DocumentSwitched?.Invoke();
        }

        private static void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            try
            {
                if (e.Document != null && e.PreviousActiveView != null && e.PreviousActiveView.Document != null && !e.PreviousActiveView.Document.Equals(e.Document))
                    DocumentSwitched?.Invoke();
                ViewActivated?.Invoke(e.CurrentActiveView?.Id.IntegerValue ?? -1);
            }
            catch (Exception ex) { Log.Warn("ViewActivated: " + ex.Message); }
        }

        private static void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            try
            {
                var names = e.GetTransactionNames();
                // ignore our own preview/selection-only operations
                DocumentChanged?.Invoke();
            }
            catch (Exception ex) { Log.Warn("DocumentChanged: " + ex.Message); }
        }
    }
}
