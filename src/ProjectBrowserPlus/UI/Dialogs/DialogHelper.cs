using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.UI;

namespace ProjectBrowserPlus.UI.Dialogs
{
    public static class DialogHelper
    {
        /// <summary>Makes Revit's main window the owner so the dialog stays on top and centred.</summary>
        public static void Own(Window w, UIApplication app)
        {
            try
            {
                if (app != null) new WindowInteropHelper(w) { Owner = app.MainWindowHandle };
                else new WindowInteropHelper(w) { Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle };
            }
            catch { }
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
    }
}
