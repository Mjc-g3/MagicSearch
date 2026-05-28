using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using MagicSearch.Models;
using MagicSearch.Services;
using Microsoft.Win32;

namespace MagicSearch.Views
{
    public partial class SettingsWindow : Window
    {
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModWin = 0x0008;

        private readonly SettingsService _settingsService;
        private readonly ObservableCollection<string> _folders = [];

        private uint _hotkeyModifiers = 0x0002;
        private uint _hotkeyKey = 0x20;
        private string _hotkeyDisplay = "Ctrl + Space";

        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        private readonly bool _isIndexing;

        private bool _hotkeyChanged;

        public SettingsWindow(SettingsService settingsService, bool isIndexing)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _isIndexing = isIndexing;
            FoldersList.ItemsSource = _folders;
            Loaded += SettingsWindow_Loaded;
        }

        private static bool IsReservedWindowsHotkey(ModifierKeys modifiers, Key key)
        {
            // Windows window menu
            if (modifiers == ModifierKeys.Alt && key == Key.Space)
                return true;

            // Task Manager
            if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.Escape)
                return true;

            // Security screen
            if (modifiers == (ModifierKeys.Control | ModifierKeys.Alt) && key == Key.Delete)
                return true;

            // Common Windows shortcuts
            if (modifiers.HasFlag(ModifierKeys.Windows))
                return true;

            return false;
        }

        private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = await _settingsService.LoadAsync();

            foreach (var folder in settings.IndexedFolders)
            {
                _folders.Add(folder);
            }

            if (_isIndexing)
            {
                HotkeyBox.IsEnabled = false;
                HotkeyBox.Text = $"{_hotkeyDisplay} - wait until indexing is finished";
            }

            _hotkeyModifiers = settings.HotkeyModifiers;
            _hotkeyKey = settings.HotkeyKey;
            _hotkeyDisplay = settings.HotkeyDisplay;
            HotkeyBox.Text = _hotkeyDisplay;

            StartWithWindowsCheckBox.IsChecked = StartupService.IsEnabled();
        }

        private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;

            if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            {
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var modifiers = Keyboard.Modifiers;

            if (IsReservedWindowsHotkey(modifiers, key))
            {
                System.Windows.MessageBox.Show(
                    this,
                    "This hotkey is already reserved by Windows. Please choose another shortcut.",
                    "Hotkey unavailable",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            uint modifierValue = 0;

            if (modifiers.HasFlag(ModifierKeys.Control))
                modifierValue |= ModControl;

            if (modifiers.HasFlag(ModifierKeys.Alt))
                modifierValue |= ModAlt;

            if (modifiers.HasFlag(ModifierKeys.Shift))
                modifierValue |= ModShift;

            if (modifiers.HasFlag(ModifierKeys.Windows))
                modifierValue |= ModWin;

            if (modifierValue == 0)
            {
                System.Windows.MessageBox.Show(this, "Use at least one modifier key, for example Ctrl, Alt, Shift, or Win.", "Invalid hotkey");
                return;
            }

            _hotkeyModifiers = modifierValue;
            _hotkeyKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            _hotkeyDisplay = BuildHotkeyDisplay(modifiers, key);
            HotkeyBox.Text = _hotkeyDisplay;
            _hotkeyChanged = true;
        }

        private void ResetHotkey_Click(object sender, RoutedEventArgs e)
        {
            _hotkeyModifiers = ModControl;
            _hotkeyKey = 0x20;
            _hotkeyDisplay = "Ctrl + Space";
            HotkeyBox.Text = _hotkeyDisplay;
        }

        private static string BuildHotkeyDisplay(ModifierKeys modifiers, Key key)
        {
            var parts = new List<string>();

            if (modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("Ctrl");

            if (modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("Alt");

            if (modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("Shift");

            if (modifiers.HasFlag(ModifierKeys.Windows))
                parts.Add("Win");

            parts.Add(key == Key.Space ? "Space" : key.ToString());

            return string.Join(" + ", parts);
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Add indexed folder"
            };

            if (dialog.ShowDialog(this) == true &&
                !_folders.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase))
            {
                _folders.Add(dialog.FolderName);
            }
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FoldersList.SelectedItem is string folder)
            {
                _folders.Remove(folder);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_isIndexing && _hotkeyChanged)
            {
                System.Windows.MessageBox.Show(
                    this,
                    "You cannot change the global hotkey while indexing is running. Please wait until indexing is finished.",
                    "Indexing in progress",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            await _settingsService.SaveAsync(new AppSettings
            {
                IndexedFolders = _folders.ToList(),
                HotkeyModifiers = _hotkeyModifiers,
                HotkeyKey = _hotkeyKey,
                HotkeyDisplay = _hotkeyDisplay
            });

            StartupService.SetEnabled(StartWithWindowsCheckBox.IsChecked == true);

            DialogResult = true;
        }
    }
}