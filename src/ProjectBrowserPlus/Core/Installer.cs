using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ProjectBrowserPlus.Core
{
    /// <summary>
    /// Installs the add-in for the current user: copies the running DLL into
    /// %AppData%\Autodesk\Revit\Addins\{version}\ProjectBrowserPlus\ and writes a .addin manifest.
    /// A dockable pane can only be registered while Revit starts, so after installing Revit must be restarted.
    /// </summary>
    public static class Installer
    {
        public const string AddinFileName = "ProjectBrowserPlus.addin";

        public static string AddinsRoot(string revitVersion) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "Revit", "Addins", revitVersion);

        public static string ManifestPath(string revitVersion) => Path.Combine(AddinsRoot(revitVersion), AddinFileName);
        public static string TargetDir(string revitVersion) => Path.Combine(AddinsRoot(revitVersion), "ProjectBrowserPlus");
        public static string TargetDll(string revitVersion) => Path.Combine(TargetDir(revitVersion), "ProjectBrowserPlus.dll");

        public static bool IsInstalled(string revitVersion) => File.Exists(ManifestPath(revitVersion)) && File.Exists(TargetDll(revitVersion));

        /// <summary>True when this assembly was loaded from the installed location (i.e. via the .addin at startup).</summary>
        public static bool RunningFromInstall(string revitVersion)
        {
            try { return string.Equals(Path.GetFullPath(typeof(App).Assembly.Location), Path.GetFullPath(TargetDll(revitVersion)), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        /// <returns>null on success, otherwise an error message.</returns>
        public static string Install(string revitVersion)
        {
            try
            {
                var src = typeof(App).Assembly.Location;
                if (string.IsNullOrEmpty(src) || !File.Exists(src)) return "Не найден исходный файл DLL: " + src;
                var dir = TargetDir(revitVersion);
                Directory.CreateDirectory(dir);
                var dst = TargetDll(revitVersion);
                if (!string.Equals(Path.GetFullPath(src), Path.GetFullPath(dst), StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Copy(src, dst, true); }
                    catch (IOException)
                    {
                        return "Файл " + dst + " занят: установленная версия уже загружена в Revit.\n" +
                               "Закройте Revit и замените файл вручную, либо удалите его и повторите установку.";
                    }
                    var pdb = Path.ChangeExtension(src, ".pdb");
                    if (File.Exists(pdb)) { try { File.Copy(pdb, Path.ChangeExtension(dst, ".pdb"), true); } catch { } }
                }
                Unblock(dst);
                var manifest =
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                    "<RevitAddIns>\r\n" +
                    "  <AddIn Type=\"Application\">\r\n" +
                    "    <Name>Project Browser+</Name>\r\n" +
                    "    <Assembly>" + System.Security.SecurityElement.Escape(dst) + "</Assembly>\r\n" +
                    "    <AddInId>7E5A3C1D-4B2F-4E8A-9C6D-2F1B8A7D5E31</AddInId>\r\n" +
                    "    <FullClassName>ProjectBrowserPlus.App</FullClassName>\r\n" +
                    "    <VendorId>PBPL</VendorId>\r\n" +
                    "    <VendorDescription>Project Browser+ (open source add-in)</VendorDescription>\r\n" +
                    "  </AddIn>\r\n" +
                    "</RevitAddIns>\r\n";
                File.WriteAllText(ManifestPath(revitVersion), manifest, new UTF8Encoding(false));
                Log.Info("Installed to " + dst);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error("Install failed", ex);
                return ex.Message;
            }
        }

        public static string Uninstall(string revitVersion)
        {
            try
            {
                if (File.Exists(ManifestPath(revitVersion))) File.Delete(ManifestPath(revitVersion));
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>Removes the "downloaded from the Internet" mark, otherwise .NET may refuse to load the DLL.</summary>
        private static void Unblock(string path)
        {
            try { DeleteFile(path + ":Zone.Identifier"); } catch { }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFile(string name);
    }
}
