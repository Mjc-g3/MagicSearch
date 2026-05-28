using System.Windows;
using System.Windows.Input;
using MagicSearch.Models;
using MagicSearch.ViewModels;

namespace MagicSearch
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel { get; }
        private readonly System.Windows.Forms.NotifyIcon _trayIcon = new();

        public MainWindow()
        {
            InitializeComponent();

            ViewModel = new MainViewModel();
            DataContext = ViewModel;

            SetupTrayIcon();

            ViewModel.RequestHide += (_, _) => HideOverlay();
            ViewModel.RequestShow += (_, _) => ShowOverlay();
            ViewModel.RequestFocusSearch += (_, _) => FocusSearch();
            ViewModel.Initialize(this);
        }

        private void SetupTrayIcon()
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);

            _trayIcon.Icon = icon;
            _trayIcon.Text = "MagicSearch";
            _trayIcon.Visible = true;

            _trayIcon.MouseClick += (_, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    ShowOverlay();
                }
            };

            var menu = new System.Windows.Forms.ContextMenuStrip();

            var openItem = new System.Windows.Forms.ToolStripMenuItem("Open MagicSearch");
            openItem.Click += (_, _) => ShowOverlay();

            var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings");
            settingsItem.Click += (_, _) => ViewModel.OpenSettingsCommand.Execute(null);

            var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
            exitItem.Click += (_, _) =>
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                System.Windows.Application.Current.Shutdown();
            };

            menu.Items.Add(openItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(exitItem);

            _trayIcon.ContextMenuStrip = menu;
        }

        protected override void OnClosed(EventArgs e)
        {
            _trayIcon.Visible = false;
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

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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

        private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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
            if (sender is System.Windows.Controls.Button button &&
                button.Tag is SearchFilter filter)
            {
                ViewModel.ActiveFilter = filter;
                ResultsList.SelectedIndex = ViewModel.Results.Count > 0 ? 0 : -1;
                e.Handled = true;
            }
        }
    }
}
