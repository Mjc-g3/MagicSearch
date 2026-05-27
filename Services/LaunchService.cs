using System.Diagnostics;
using System.IO;

namespace MagicSearch.Services
{
    public sealed class LaunchService
    {
        public void Launch(string path)
        {
            Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true
            });
        }

        public void OpenContainingFolder(string path)
        {
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return;
            }

            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                {
                    UseShellExecute = true
                });
            }
        }
    }
}
