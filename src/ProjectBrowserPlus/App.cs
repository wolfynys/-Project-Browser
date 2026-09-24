using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using ProjectBrowserPlus.Core;

namespace ProjectBrowserPlus
{
    /// <summary>
    /// Normal entry point (.addin file): ribbon tab + dockable pane registered at startup.
    /// </summary>
    public class App : IExternalApplication
    {
        public static App Instance { get; private set; }

        public Result OnStartup(UIControlledApplication app)
        {
            Instance = this;
            AppDomain.CurrentDomain.AssemblyResolve += ResolveSelf;
            try { EnsurePackScheme(); } catch (Exception ex) { Log.Warn("pack scheme: " + ex.Message); }
            try { RevitTask.Instance.EnsureCreated(); } catch (Exception ex) { Log.Error("ExternalEvent", ex); }
            try { RevitEvents.Attach(app); } catch (Exception ex) { Log.Error("events", ex); }
            try { BuildRibbon(app); } catch (Exception ex) { Log.Error("ribbon", ex); }
            // the pane's WPF control is created inside RegisterDockablePane, keep it last and isolated
            try { PaneManager.TryRegister(app); }
            catch (Exception ex)
            {
                Log.Error("OnStartup: pane registration failed", ex);
                TaskDialog.Show("Project Browser+", "Не удалось зарегистрировать панель: " + ex.Message + "\nПодробности: " + Log.Dir + "\\log.txt");
            }
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;

        /// <summary>
        /// pack:// URIs (used for the XAML resource dictionaries) need the "pack" scheme registered and,
        /// in a host that is not a WPF Application, a resource assembly hint. Revit normally has both,
        /// but Add-in Manager / early startup paths are cheap to make robust.
        /// </summary>
        internal static void EnsurePackScheme()
        {
            var _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        }

        /// <summary>
        /// When the DLL is loaded by Add-in Manager (Assembly.LoadFile into a temp copy), WPF's pack URIs
        /// may ask the AppDomain for "ProjectBrowserPlus" by name; answer with the already loaded assembly.
        /// </summary>
        internal static Assembly ResolveSelf(object sender, ResolveEventArgs args)
        {
            var self = typeof(App).Assembly;
            var name = new AssemblyName(args.Name).Name;
            return string.Equals(name, self.GetName().Name, StringComparison.OrdinalIgnoreCase) ? self : null;
        }

        private static void BuildRibbon(UIControlledApplication app)
        {
            const string tab = "Диспетчер+";
            try { app.CreateRibbonTab(tab); } catch { }
            var panel = app.CreateRibbonPanel(tab, "Project Browser+");
            var asm = typeof(App).Assembly.Location;

            var show = new PushButtonData("PBP_Show", "Диспетчер\nпроекта+", asm, typeof(Commands.ShowBrowserCommand).FullName)
            {
                ToolTip = "Показать / скрыть панель «Диспетчер проекта+»",
                LongDescription = "Диспетчер проекта на стероидах: вкладки по категориям, поиск, группировки, превью, избранное, пакетные операции.",
                LargeImage = Icon("browser32.png"),
                Image = Icon("browser16.png"),
            };
            var settings = new PushButtonData("PBP_Settings", "Настройки", asm, typeof(Commands.SettingsCommand).FullName)
            {
                ToolTip = "Настройки Project Browser+",
                LargeImage = Icon("settings32.png"),
                Image = Icon("settings16.png"),
            };
            var about = new PushButtonData("PBP_About", "О плагине", asm, typeof(Commands.AboutCommand).FullName)
            {
                ToolTip = "Версия, лицензии, журнал",
                LargeImage = Icon("about32.png"),
                Image = Icon("about16.png"),
            };
            panel.AddItem(show);
            panel.AddStackedItems(settings, about);
        }

        internal static BitmapImage Icon(string file)
        {
            try
            {
                var asm = typeof(App).Assembly;
                var res = asm.GetManifestResourceNames();
                foreach (var r in res)
                {
                    if (!r.EndsWith(file, StringComparison.OrdinalIgnoreCase)) continue;
                    using (var s = asm.GetManifestResourceStream(r))
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = s;
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    }
                }
            }
            catch (Exception ex) { Log.Warn("icon " + file + ": " + ex.Message); }
            return null;
        }
    }
}
