namespace MagicSearch.Models
{
    public sealed class AppSettings
    {
        public List<string> IndexedFolders { get; set; } = [];

        public uint HotkeyModifiers { get; set; } = 0x0002; // Ctrl
        public uint HotkeyKey { get; set; } = 0x20; // Space
        public string HotkeyDisplay { get; set; } = "Ctrl + Space";
    }
}