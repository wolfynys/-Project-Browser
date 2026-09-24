using System;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.UI;
using ProjectBrowserPlus.UI;

namespace ProjectBrowserPlus.Core
{
    /// <summary>Registers / shows the dockable pane and provides a floating-window fallback.</summary>
    public static class PaneManager
    {
        public static readonly DockablePaneId PaneId = new DockablePaneId(new Guid("A6C2E9F4-3B7D-4C1A-9E5F-8D2B6A4C7E10"));
        public const string PaneTitle = "Диспетчер проекта+";

        private static BrowserPane _pane;
        private static Window _floating;
        private static bool _registered;

        public static BrowserPane Pane => _pane ?? (_pane = new BrowserPane());

        private class Provider : IDockablePaneProvider
        {
            public void SetupDockablePane(DockablePaneProviderData data)
            {
                data.FrameworkElement = Pane;
                data.InitialState = new DockablePaneState
                {
                    DockPosition = DockPosition.Tabbed,
                    TabBehind = DockablePanes.BuiltInDockablePanes.ProjectBrowser,
                };
                data.VisibleByDefault = false;
            }
        }

        public static bool TryRegister(UIControlledApplication app)
        {
            if (_registered) return true;
            try
            {
                app.RegisterDockablePane(PaneId, PaneTitle, new Provider());
                _registered = true;
                return true;
            }
            catch (Exception ex) { Log.Error("RegisterDockablePane (startup) failed", ex); return false; }
        }

        public static bool TryRegister(UIApplication app)
        {
            if (_registered) return true;
            try
            {
                if (DockablePane.PaneIsRegistered(PaneId)) { _registered = true; return true; }
                app.RegisterDockablePane(PaneId, PaneTitle, new Provider());
                _registered = true;
                return true;
            }
            catch (Exception ex) { Log.Error("RegisterDockablePane (command) failed", ex); return false; }
        }

        /// <summary>Shows the dockable pane; falls back to a floating window when the pane cannot be registered.</summary>
        public static void Show(UIApplication app)
        {
            RevitTask.Instance.EnsureCreated();
            RevitTask.UIApp = app;
            RevitEvents.Attach(app);
            if (TryRegister(app))
            {
                try
                {
                    var pane = app.GetDockablePane(PaneId);
                    if (pane.IsShown()) pane.Hide(); else pane.Show();
                    Pane.ViewModel.RequestRefresh(true);
                    return;
                }
                catch (Exception ex) { Log.Error("GetDockablePane failed, using floating window", ex); }
            }
            ShowFloating(app);
        }

        public static void ShowFloating(UIApplication app)
        {
            if (_floating != null)
            {
                if (_floating.IsVisible) { _floating.Activate(); return; }
                _floating.Show();
                return;
            }
            var pane = Pane;
            if (pane.Parent is Window) { ((Window)pane.Parent).Content = null; }
            _floating = new Window
            {
                Title = PaneTitle,
                Content = pane,
                Width = 420,
                Height = 760,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };
            try { new WindowInteropHelper(_floating) { Owner = app.MainWindowHandle }; } catch { }
            _floating.Closed += (s, e) => { _floating.Content = null; _floating = null; };
            _floating.Show();
            pane.ViewModel.RequestRefresh(true);
        }
    }
}
