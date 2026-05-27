using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using MagicSearch.Models;
using MagicSearch.Services;

namespace MagicSearch.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly ObservableCollection<string> _folders = [];

        public SettingsWindow(SettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            FoldersList.ItemsSource = _folders;
            Loaded += SettingsWindow_Loaded;
        }

        private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = await _settingsService.LoadAsync();
            foreach (var folder in settings.IndexedFolders)
            {
                _folders.Add(folder);
            }
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
            await _settingsService.SaveAsync(new AppSettings
            {
                IndexedFolders = _folders.ToList()
            });

            DialogResult = true;
        }
    }
}
