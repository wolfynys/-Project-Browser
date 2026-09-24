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
                var docked = PaneManager.Show(data.Application);
                if (!docked) InstallCommand.OfferInstall(data.Application, false);
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

    /// <summary>
    /// Run once from Add-in Manager: installs the add-in for the current user so that after a Revit
    /// restart the browser is a real dockable pane (next to the standard Project Browser) with a ribbon tab.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class InstallCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            OfferInstall(data.Application, true);
            return Result.Succeeded;
        }

        internal static void OfferInstall(UIApplication app, bool explicitRequest)
        {
            var ver = app.Application.VersionNumber;
            if (Installer.RunningFromInstall(ver)) { if (explicitRequest) TaskDialog.Show("Project Browser+", "Плагин уже установлен и загружен из:\n" + Installer.TargetDll(ver)); return; }
            var installed = Installer.IsInstalled(ver);
            if (!explicitRequest && installed)
            {
                TaskDialog.Show("Project Browser+",
                    "Плагин уже установлен, но в этой сессии Revit запущен через Add-in Manager, поэтому открыт в отдельном окне.\n\n" +
                    "Перезапустите Revit и откройте панель кнопкой «Диспетчер проекта+» на вкладке ленты «Диспетчер+». " +
                    "Add-in Manager для этого плагина больше не нужен.");
                return;
            }
            var td = new TaskDialog("Project Browser+")
            {
                MainInstruction = explicitRequest ? "Установить Project Browser+?" : "Панель открыта в отдельном окне",
                MainContent = (explicitRequest ? "" :
                    "Revit разрешает встраивать панели только при своём запуске, а Add-in Manager загружает плагин уже после старта. " +
                    "Поэтому сейчас панель открыта окном.\n\n") +
                    "Установка скопирует DLL в\n" + Installer.TargetDir(ver) + "\nи создаст файл " + Installer.ManifestPath(ver) + ".\n\n" +
                    "После перезапуска Revit появится вкладка ленты «Диспетчер+», а панель будет встроена рядом со стандартным «Диспетчером проекта».",
                CommonButtons = TaskDialogCommonButtons.Close,
            };
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, installed ? "Обновить установленную версию" : "Установить", "Для текущего пользователя, права администратора не нужны");
            if (!explicitRequest) td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Продолжить в окне", "Без установки, только в этой сессии");
            if (td.Show() != TaskDialogResult.CommandLink1) return;
            var err = Installer.Install(ver);
            TaskDialog.Show("Project Browser+", err == null
                ? "Готово. Перезапустите Revit.\n\nПосле перезапуска: вкладка ленты «Диспетчер+» → «Диспетчер проекта+». Панель откроется вкладкой рядом со стандартным диспетчером; её можно перетаскивать и объединять с другими панелями.\n\nЗапускать плагин через Add-in Manager больше не нужно."
                : "Не удалось установить:\n" + err);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class UninstallCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var ver = data.Application.Application.VersionNumber;
            var err = Installer.Uninstall(ver);
            TaskDialog.Show("Project Browser+", err == null ? "Файл .addin удалён. После перезапуска Revit плагин не загрузится.\nПапку с DLL можно удалить вручную:\n" + Installer.TargetDir(ver) : err);
            return Result.Succeeded;
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
