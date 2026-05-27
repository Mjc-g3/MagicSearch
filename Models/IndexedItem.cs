using System.Windows.Media;

namespace MagicSearch.Models
{
    public sealed class IndexedItem
    {
        public required string Name { get; init; }
        public required string Path { get; init; }
        public required string Type { get; init; }
        public string Extension { get; init; } = string.Empty;
        public string Drive { get; init; } = string.Empty;
        public DateTime? LastModified { get; init; }
        public long? Size { get; init; }
        public ImageSource? Icon { get; init; }
    }
}
