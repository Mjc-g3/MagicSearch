using System.Windows.Media;

namespace MagicSearch.Models
{
    public sealed class SearchResult
    {
        public required string Name { get; init; }
        public required string Path { get; init; }
        public required string Type { get; init; }
        public string Extension { get; init; } = string.Empty;
        public string Drive { get; init; } = string.Empty;
        public DateTime? LastModified { get; init; }
        public long? Size { get; init; }
        public string SizeText => Size is null ? string.Empty : FormatSize(Size.Value);
        public string DetailText
        {
            get
            {
                var parts = new[] { Type, Extension, SizeText }
                    .Where(part => !string.IsNullOrWhiteSpace(part));

                return string.Join("  ", parts);
            }
        }
        public ImageSource? Icon { get; init; }
        public int Score { get; init; }

        private static string FormatSize(long bytes)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            var size = (double)bytes;
            var unit = 0;

            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return unit == 0 ? $"{bytes} B" : $"{size:0.#} {units[unit]}";
        }
    }
}
