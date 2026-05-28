using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MagicSearch.Models;
using MagicSearch.Services;
using MagicSearch.Views;

namespace MagicSearch.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SettingsService _settingsService = new();
        private readonly IndexService _indexService = new();
        private readonly IndexCacheService _indexCacheService = new();
        private readonly SearchService _searchService = new();
        private readonly LaunchService _launchService = new();
        private readonly HotkeyService _hotkeyService = new();
        private readonly ObservableCollection<SearchResult> _results = [];
        private readonly ObservableCollection<SearchFilter> _filters =
        [
            new() { Name = "All", Key = "all" },
            new() { Name = "Apps", Key = "apps" },
            new() { Name = "Files", Key = "files" },
            new() { Name = "Folders", Key = "folders" },
            new() { Name = "Images", Key = "images" },
            new() { Name = "Videos", Key = "videos" },
            new() { Name = "Audio", Key = "audio" },
            new() { Name = "Documents", Key = "documents" },
            new() { Name = "Archives", Key = "archives" },
            new() { Name = "Code", Key = "code" },
            new() { Name = "Executables", Key = "executables" }
        ];

        private IReadOnlyList<IndexedItem> _indexedItems = [];
        private CancellationTokenSource? _indexCancellation;
        private Window? _owner;
        private string _query = string.Empty;
        private string _statusText = "Idle";
        private string _lastIndexedText = "Not indexed yet";
        private bool _isIndexing;
        private SearchFilter _activeFilter;
        private SearchResult? _selectedResult;

        public MainViewModel()
        {
            _activeFilter = _filters[0];
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            RebuildIndexCommand = new RelayCommand(() => _ = RefreshIndexAsync());
            CancelIndexCommand = new RelayCommand(CancelIndex, () => IsIndexing);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? RequestHide;
        public event EventHandler? RequestShow;
        public event EventHandler? RequestFocusSearch;

        public ObservableCollection<SearchResult> Results => _results;
        public ObservableCollection<SearchFilter> Filters => _filters;
        public ICommand OpenSettingsCommand { get; }
        public ICommand RebuildIndexCommand { get; }
        public ICommand CancelIndexCommand { get; }
        public ICommand SelectFilterCommand { get; }

        public string Query
        {
            get => _query;
            set
            {
                if (SetField(ref _query, value))
                {
                    UpdateResults();
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetField(ref _statusText, value);
        }

        public string LastIndexedText
        {
            get => _lastIndexedText;
            private set => SetField(ref _lastIndexedText, value);
        }

        public bool IsIndexing
        {
            get => _isIndexing;
            private set
            {
                if (SetField(ref _isIndexing, value) && CancelIndexCommand is RelayCommand command)
                {
                    command.RaiseCanExecuteChanged();
                }
            }
        }

        public SearchFilter ActiveFilter
        {
            get => _activeFilter;
            set
            {
                if (value is not null && SetField(ref _activeFilter, value))
                {
                    UpdateResults();
                }
            }
        }

        public SearchResult? SelectedResult
        {
            get => _selectedResult;
            set => SetField(ref _selectedResult, value);
        }


        public async void Initialize(Window owner)
        {
            _owner = owner;
            _hotkeyService.HotkeyPressed += (_, _) => ToggleOverlay();

            var settings = await _settingsService.LoadAsync();

            if (!_hotkeyService.Register(owner, settings.HotkeyModifiers, settings.HotkeyKey))
            {
                StatusText = $"{settings.HotkeyDisplay} is already in use by another app.";
            }
            SelectFilterCommand = new RelayCommand<SearchFilter>(filter =>
            {
                if (filter is not null)
                    ActiveFilter = filter;
            });

            _ = InitializeIndexAsync();
        }

        public void LaunchSelected(bool openContainingFolder)
        {
            if (SelectedResult is null)
            {
                return;
            }

            try
            {
                if (openContainingFolder)
                {
                    _launchService.OpenContainingFolder(SelectedResult.Path);
                }
                else
                {
                    _launchService.Launch(SelectedResult.Path);
                }

                RequestHide?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                StatusText = $"Launch failed: {ex.Message}";
            }
        }

        public void Dispose()
        {
            CancelIndex();
            _hotkeyService.Dispose();
        }

        private async Task InitializeIndexAsync()
        {
            try
            {
                StatusText = "Loading cached index...";
                _indexedItems = await _indexCacheService.LoadAsync();

                if (_indexedItems.Count > 0)
                {
                    StatusText = $"Loaded {_indexedItems.Count:N0} cached items";
                    LastIndexedText = "Loaded from cache";
                    UpdateResults();
                    return;
                }
            }
            catch
            {
                StatusText = "No cache found";
            }

            await RefreshIndexAsync();
        }

        private async Task RefreshIndexAsync()
        {
            CancelIndex();
            var cancellation = new CancellationTokenSource();
            _indexCancellation = cancellation;

            try
            {
                IsIndexing = true;
                StatusText = "Indexing...";
                var settings = await _settingsService.LoadAsync();
                var progress = new Progress<IndexProgress>(update =>
                {
                    StatusText = update.IsComplete
                        ? $"Indexed {update.IndexedCount:N0} items"
                        : $"Indexing... {update.IndexedCount:N0} items";
                });

                _indexedItems = await _indexService.BuildIndexAsync(settings.IndexedFolders, progress, cancellation.Token);
                await _indexCacheService.SaveAsync(_indexedItems);

                var indexedAt = DateTime.Now;

                StatusText = $"Finished indexing {_indexedItems.Count:N0} items";
                StatusText = $"Indexed {_indexedItems.Count:N0} items";
                LastIndexedText = $"Last indexed {indexedAt:g}";
                UpdateResults();
            }
            catch (OperationCanceledException)
            {
                StatusText = _indexedItems.Count == 0
                    ? "Indexing canceled"
                    : $"Indexing canceled. Using {_indexedItems.Count:N0} previous items";
            }
            catch (Exception ex)
            {
                StatusText = $"Indexing failed: {ex.Message}";
            }
            finally
            {
                if (_indexCancellation == cancellation)
                {
                    IsIndexing = false;
                    _indexCancellation.Dispose();
                    _indexCancellation = null;
                }

                IsIndexing = false;
            }
        }

        private void UpdateResults()
        {
            var selectedPath = SelectedResult?.Path;
            var matches = _searchService.Search(_indexedItems, Query, ActiveFilter.Key);

            _results.Clear();
            foreach (var result in matches)
            {
                _results.Add(result);
            }

            SelectedResult = _results.FirstOrDefault(result => result.Path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase))
                ?? _results.FirstOrDefault();

            if (!IsIndexing)
            {
                StatusText = string.IsNullOrWhiteSpace(Query)
                    ? $"Indexed {_indexedItems.Count:N0} items"
                    : $"{_results.Count:N0} results";
            }
        }

        private void CancelIndex()
        {
            _indexCancellation?.Cancel();
        }

        private void ToggleOverlay()
        {
            if (_owner?.IsVisible == true)
            {
                RequestHide?.Invoke(this, EventArgs.Empty);
                return;
            }

            RequestShow?.Invoke(this, EventArgs.Empty);
            RequestFocusSearch?.Invoke(this, EventArgs.Empty);
        }

        private void OpenSettings()
        {
            if (_owner is null)
            {
                return;
            }

            var settingsWindow = new SettingsWindow(_settingsService, IsIndexing)
            {
                Owner = _owner
            };

            if (settingsWindow.ShowDialog() == true)
            {
                RestartApplication();
            }
        }

        private void RestartApplication()
        {
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

                if (string.IsNullOrWhiteSpace(exePath))
                {
                    StatusText = "Could not restart application.";
                    return;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });

                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                StatusText = $"Restart failed: {ex.Message}";
            }
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
