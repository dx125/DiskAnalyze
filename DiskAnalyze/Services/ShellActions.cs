using System.Diagnostics;
using System.IO;

namespace DiskAnalyze.Services
{
    public static class ShellActions
    {
        /// <summary>
        /// Opens a folder in Explorer, or reveals a file selected inside its parent folder.
        /// Best effort: failures (e.g. the path no longer exists) are ignored.
        /// </summary>
        public static void ShowInExplorer(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                var arguments = Directory.Exists(path) ? $"\"{path}\"" : $"/select,\"{path}\"";
                Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                Debug.WriteLine($"Could not open Explorer for {path}: {ex.Message}");
            }
        }
    }
}
