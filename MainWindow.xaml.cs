using System.Windows;
using System.Windows.Input;
using MagicSearch.ViewModels;
using MagicSearch.Models;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;

namespace MagicSearch
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext;
        private readonly TaskbarIcon _trayIcon = new();

        public MainWindow()
        {
            InitializeComponent();

            ViewModel.RequestHide += (_, _) => HideOverlay();
            ViewModel.RequestShow += (_, _) => ShowOverlay();
            ViewModel.RequestFocusSearch += (_, _) => FocusSearch();
            ViewModel.Initialize(this);
        }

        private void SetupTrayIcon()
        {
            _trayIcon.Icon = new System.Drawing.Icon("Assets/app.ico");
            _trayIcon.ToolTipText = "MagicSearch";

            _trayIcon.TrayLeftMouseUp += (_, _) => ShowOverlay();

            var menu = new System.Windows.Controls.ContextMenu();

            var openItem = new System.Windows.Controls.MenuItem
            {
                Header = "Open MagicSearch"
            };
            openItem.Click += (_, _) => ShowOverlay();

            var settingsItem = new System.Windows.Controls.MenuItem
            {
                Header = "Settings"
            };
            settingsItem.Click += (_, _) => ViewModel.OpenSettingsCommand.Execute(null);

            var exitItem = new System.Windows.Controls.MenuItem
            {
                Header = "Exit"
            };
            exitItem.Click += (_, _) =>
            {
                _trayIcon.Dispose();
                Application.Current.Shutdown();
            };

            menu.Items.Add(openItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(new System.Windows.Controls.Separator());
            menu.Items.Add(exitItem);

            _trayIcon.ContextMenu = menu;
        }

        protected override void OnClosed(EventArgs e)
        {
            _trayIcon.Dispose();
            ViewModel.Dispose();
            base.OnClosed(e);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            HideOverlay();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideOverlay();
                e.Handled = true;
            }
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ResultsList.SelectedIndex = ViewModel.Results.Count > 0 ? 0 : -1;
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                MoveSelection(1);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                MoveSelection(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                ViewModel.LaunchSelected(Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
                e.Handled = true;
            }
        }

        private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel.LaunchSelected(openContainingFolder: false);
        }

        private void MoveSelection(int delta)
        {
            if (ViewModel.Results.Count == 0)
            {
                return;
            }

            var next = ResultsList.SelectedIndex + delta;
            if (next < 0)
            {
                next = ViewModel.Results.Count - 1;
            }
            else if (next >= ViewModel.Results.Count)
            {
                next = 0;
            }

            ResultsList.SelectedIndex = next;
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
        }

        private void ShowOverlay()
        {
            Left = (SystemParameters.WorkArea.Width - Width) / 2 + SystemParameters.WorkArea.Left;
            Top = (SystemParameters.WorkArea.Height - Height) / 3 + SystemParameters.WorkArea.Top;
            Show();
            Activate();
            FocusSearch();
        }

        private void HideOverlay()
        {
            Hide();
            ViewModel.Query = string.Empty;
        }

        private void FocusSearch()
        {
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
            SearchBox.SelectAll();
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SearchFilter filter)
            {
                ViewModel.ActiveFilter = filter;
            }
        }
    }
}
