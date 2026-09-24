using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ProjectBrowserPlus.Core;

namespace ProjectBrowserPlus.Commands
{
    /// <summary>
    /// Entry point for Add-in Manager ("Run" this command): registers the pane on first run
    /// (falls back to a floating window if Revit refuses late registration) and toggles it.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ShowBrowserCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            AppDomain.CurrentDomain.AssemblyResolve -= App.ResolveSelf;
            AppDomain.CurrentDomain.AssemblyResolve += App.ResolveSelf;
            try
            {
                try { App.EnsurePackScheme(); } catch (Exception ex) { Log.Warn("pack scheme: " + ex.Message); }
                PaneManager.Show(data.Application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error("ShowBrowserCommand", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class SettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            RevitTask.Instance.EnsureCreated();
            RevitTask.UIApp = data.Application;
            var dlg = new UI.Dialogs.SettingsDialog();
            UI.Dialogs.DialogHelper.Own(dlg, data.Application);
            if (dlg.ShowDialog() == true)
                PaneManager.Pane.ViewModel.OnSettingsChanged();
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class AboutCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var v = typeof(App).Assembly.GetName().Version;
            var td = new TaskDialog("Project Browser+")
            {
                MainInstruction = "Project Browser+  v" + v,
                MainContent = "Диспетчер проекта на стероидах для Revit 2022.\n\n" +
                              "Иконки: Lucide (ISC). Журнал и настройки: " + Log.Dir,
                CommonButtons = TaskDialogCommonButtons.Close,
            };
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Открыть папку настроек и журнала");
            if (td.Show() == TaskDialogResult.CommandLink1)
            {
                try { System.IO.Directory.CreateDirectory(Log.Dir); System.Diagnostics.Process.Start("explorer.exe", Log.Dir); } catch { }
            }
            return Result.Succeeded;
        }
    }
}
