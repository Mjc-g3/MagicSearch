using System.IO;
using MagicSearch.Models;

namespace MagicSearch.Services
{
    public sealed class IndexService
    {
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".ico", ".svg"
        };

        private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".bat", ".cmd", ".msi", ".lnk"
        };

        private static readonly HashSet<string> SkippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules", ".git", "bin", "obj"
        };

        private static readonly string[] SystemSkipSuffixes =
        [
            "Windows",
            "Program Files",
            "Program Files (x86)",
            "ProgramData",
            "$Recycle.Bin",
            "System Volume Information",
            Path.Combine("AppData", "Local", "Temp")
        ];

        public IReadOnlyList<DriveInfo> GetFixedDrives()
        {
            return DriveInfo.GetDrives()
                .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
                .ToList();
        }

        public Task<IReadOnlyList<IndexedItem>> BuildIndexAsync(
            IEnumerable<string> extraFolders,
            IProgress<IndexProgress>? progress,
            CancellationToken cancellationToken)
        {
            return Task.Run<IReadOnlyList<IndexedItem>>(() =>
            {
                var items = new List<IndexedItem>();
                IndexStartMenu(items, cancellationToken);

                foreach (var drive in GetFixedDrives())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    IndexFolder(drive.RootDirectory.FullName, items, progress, cancellationToken, skipSystemFolders: true);
                }

                foreach (var folder in extraFolders.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    IndexFolder(folder, items, progress, cancellationToken, skipSystemFolders: false);
                }

                progress?.Report(new IndexProgress
                {
                    IndexedCount = items.Count,
                    IsComplete = true
                });

                return items
                    .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();
            }, cancellationToken);
        }

        private static void IndexStartMenu(List<IndexedItem> items, CancellationToken cancellationToken)
        {
            var folders = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs")
            };

            foreach (var folder in folders.Where(Directory.Exists))
            {
                IEnumerable<string> shortcuts;

                try
                {
                    shortcuts = Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories);
                }
                catch (Exception ex) when (IsSafeToIgnore(ex))
                {
                    shortcuts = [];
                }

                foreach (var shortcut in shortcuts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    items.Add(CreateItem(shortcut, "app", includeIcon: true));
                }
            }
        }


        private static void IndexFolder(
    string folder,
    List<IndexedItem> items,
    IProgress<IndexProgress>? progress,
    CancellationToken cancellationToken,
    bool skipSystemFolders)
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            var pending = new Stack<string>();
            pending.Push(folder);

            var lastProgressCount = 0;

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var current = pending.Pop();

                if (ShouldSkipDirectory(current, skipSystemFolders))
                {
                    continue;
                }

                IEnumerable<string> directories;
                try
                {
                    directories = Directory.EnumerateDirectories(current);
                }
                catch (Exception ex) when (IsSafeToIgnore(ex))
                {
                    directories = [];
                }

                foreach (var directory in directories)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (ShouldSkipDirectory(directory, skipSystemFolders))
                    {
                        continue;
                    }

                    items.Add(CreateItem(directory, "folder", includeIcon: false));
                    pending.Push(directory);
                    ReportProgressIfNeeded(items, progress, directory, ref lastProgressCount);
                }

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(current);
                }
                catch (Exception ex) when (IsSafeToIgnore(ex))
                {
                    files = [];
                }

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    items.Add(CreateItem(file, GetFileType(file), includeIcon: false));
                    ReportProgressIfNeeded(items, progress, file, ref lastProgressCount);
                }
            }
        }

        private static IndexedItem CreateItem(string path, string type, bool includeIcon)
        {
            var isFile = File.Exists(path);
            var info = isFile ? new FileInfo(path) : null;
            var extension = isFile ? info?.Extension ?? string.Empty : string.Empty;

            return new IndexedItem
            {
                Name = isFile ? Path.GetFileName(path) : GetDirectoryName(path),
                Path = path,
                Type = type,
                Extension = extension,
                Drive = Path.GetPathRoot(path) ?? string.Empty,
                LastModified = GetLastWriteTime(path, isFile),
                Size = info?.Length,
                Icon = includeIcon ? IconService.GetSmallIcon(path) : null
            };
        }

        private static DateTime? GetLastWriteTime(string path, bool isFile)
        {
            try
            {
                return isFile ? File.GetLastWriteTime(path) : Directory.GetLastWriteTime(path);
            }
            catch
            {
                return null;
            }
        }

        private static string GetDirectoryName(string path)
        {
            var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFileName(trimmed).Length == 0 ? trimmed : Path.GetFileName(trimmed);
        }

        private static string GetFileType(string path)
        {
            var extension = Path.GetExtension(path);
            if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return "app";
            }

            if (ExecutableExtensions.Contains(extension))
            {
                return "exe";
            }

            return ImageExtensions.Contains(extension) ? "img" : "file";
        }

        private static void ReportProgressIfNeeded(
            List<IndexedItem> items,
            IProgress<IndexProgress>? progress,
            string currentPath,
            ref int lastProgressCount)
        {
            if (items.Count - lastProgressCount < 250)
            {
                return;
            }

            lastProgressCount = items.Count;
            progress?.Report(new IndexProgress
            {
                CurrentPath = currentPath,
                IndexedCount = items.Count
            });
        }

        private static IEnumerable<string> EnumerateFilesSafe(
            string folder,
            CancellationToken cancellationToken,
            bool skipSystemFolders = false)
        {
            var pending = new Stack<string>();
            pending.Push(folder);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = pending.Pop();
                if (ShouldSkipDirectory(current, skipSystemFolders))
                {
                    continue;
                }

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(current);
                }
                catch (Exception ex) when (IsSafeToIgnore(ex))
                {
                    files = [];
                }

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return file;
                }

                IEnumerable<string> directories;
                try
                {
                    directories = Directory.EnumerateDirectories(current);
                }
                catch (Exception ex) when (IsSafeToIgnore(ex))
                {
                    directories = [];
                }

                foreach (var directory in directories)
                {
                    if (!ShouldSkipDirectory(directory, skipSystemFolders))
                    {
                        pending.Push(directory);
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateDirectoriesSafe(
            string folder,
            CancellationToken cancellationToken,
            bool skipSystemFolders)
        {
            var pending = new Stack<string>();
            pending.Push(folder);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = pending.Pop();

                IEnumerable<string> directories;
                try
                {
                    directories = Directory.EnumerateDirectories(current);
                }
                catch (Exception ex) when (IsSafeToIgnore(ex))
                {
                    directories = [];
                }

                foreach (var directory in directories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ShouldSkipDirectory(directory, skipSystemFolders))
                    {
                        continue;
                    }

                    yield return directory;
                    pending.Push(directory);
                }
            }
        }

        private static bool ShouldSkipDirectory(string path, bool skipSystemFolders)
        {
            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (SkippedDirectoryNames.Contains(name))
            {
                return true;
            }

            if (!skipSystemFolders)
            {
                return false;
            }

            var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetPathRoot(normalized) ?? string.Empty;

            return SystemSkipSuffixes.Any(suffix =>
                normalized.Equals(Path.Combine(root, suffix), StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(Path.DirectorySeparatorChar + suffix, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSafeToIgnore(Exception ex)
        {
            return ex is UnauthorizedAccessException
                or IOException
                or PathTooLongException
                or DirectoryNotFoundException;
        }
    }
}
