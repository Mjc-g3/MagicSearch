namespace MagicSearch.Models
{
    public sealed class IndexProgress
    {
        public string CurrentPath { get; init; } = string.Empty;
        public int IndexedCount { get; init; }
        public bool IsComplete { get; init; }
    }
}
