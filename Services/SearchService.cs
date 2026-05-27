using MagicSearch.Models;

namespace MagicSearch.Services
{
    public sealed class SearchService
    {
        private static readonly HashSet<string> PrefixFilters = new(StringComparer.OrdinalIgnoreCase)
        {
            "app", "file", "folder", "exe", "img"
        };

        private static readonly Dictionary<string, HashSet<string>> ExtensionGroups = new(StringComparer.OrdinalIgnoreCase)
        {
            ["images"] = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".ico", ".svg" },
            ["videos"] = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm" },
            ["audio"] = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac" },
            ["documents"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".doc", ".docx", ".txt", ".rtf", ".xls", ".xlsx", ".ppt", ".pptx" },
            ["archives"] = new(StringComparer.OrdinalIgnoreCase) { ".zip", ".rar", ".7z", ".tar", ".gz" },
            ["code"] = new(StringComparer.OrdinalIgnoreCase) { ".cs", ".xaml", ".js", ".ts", ".html", ".css", ".json", ".xml", ".py", ".cpp", ".h", ".lua", ".gsc", ".cfg", ".ini" },
            ["executables"] = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".bat", ".cmd", ".msi", ".lnk" }
        };

        public IReadOnlyList<SearchResult> Search(
            IEnumerable<IndexedItem> items,
            string query,
            string activeFilter,
            int maxResults = 150)
        {
            var (prefixFilter, term) = ParseQuery(query);
            var effectiveFilter = prefixFilter ?? activeFilter;

            if (string.IsNullOrWhiteSpace(term) && string.Equals(effectiveFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            return items
                .Where(item => MatchesFilter(item, effectiveFilter))
                .Select(item => new SearchResult
                {
                    Name = item.Name,
                    Path = item.Path,
                    Type = item.Type,
                    Extension = item.Extension,
                    Drive = item.Drive,
                    LastModified = item.LastModified,
                    Size = item.Size,
                    Icon = item.Icon,
                    Score = Score(item, term)
                })
                .Where(result => string.IsNullOrWhiteSpace(term) || result.Score > 0)
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.Name)
                .Take(maxResults)
                .ToList();
        }

        private static (string? Filter, string Term) ParseQuery(string query)
        {
            var trimmed = query.Trim();
            var separator = trimmed.IndexOf(':');
            if (separator <= 0)
            {
                return (null, trimmed);
            }

            var prefix = trimmed[..separator];
            if (!PrefixFilters.Contains(prefix))
            {
                return (null, trimmed);
            }

            return (prefix.ToLowerInvariant(), trimmed[(separator + 1)..].Trim());
        }

        private static bool MatchesFilter(IndexedItem item, string filter)
        {
            if (string.Equals(filter, "all", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(filter, "apps", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, "app", StringComparison.OrdinalIgnoreCase))
            {
                return item.Type.Equals("app", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(filter, "files", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, "file", StringComparison.OrdinalIgnoreCase))
            {
                return item.Type.Equals("file", StringComparison.OrdinalIgnoreCase)
                    || item.Type.Equals("img", StringComparison.OrdinalIgnoreCase)
                    || item.Type.Equals("exe", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(filter, "folders", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, "folder", StringComparison.OrdinalIgnoreCase))
            {
                return item.Type.Equals("folder", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(filter, "img", StringComparison.OrdinalIgnoreCase))
            {
                return item.Type.Equals("img", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(filter, "exe", StringComparison.OrdinalIgnoreCase))
            {
                return item.Type.Equals("exe", StringComparison.OrdinalIgnoreCase);
            }

            return ExtensionGroups.TryGetValue(filter, out var extensions) && extensions.Contains(item.Extension);
        }

        private static int Score(IndexedItem item, string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return 1;
            }

            if (item.Name.Equals(term, StringComparison.OrdinalIgnoreCase))
            {
                return 1000;
            }

            if (item.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            {
                return 800;
            }

            if (item.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return 500;
            }

            if (item.Extension.Equals(term, StringComparison.OrdinalIgnoreCase))
            {
                return 350;
            }

            if (item.Path.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return 200;
            }

            return 0;
        }
    }
}
