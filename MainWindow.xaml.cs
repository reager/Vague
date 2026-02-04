using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using Vague.Models;
using Vague.ViewModels;

namespace Vague
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        private const int BlurStep = 5;

        private NotifyIcon? _trayIcon;
        private ToolStripMenuItem? _trayMinimizeOnStartupItem;
        private bool _exitRequested;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;

            SetupTrayIcon();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Vague initialized. Select processes to add to private list.";

            if (_viewModel.MinimizeToTrayOnStartup)
            {
                WindowState = WindowState.Minimized;
                Hide();
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _trayIcon?.Dispose();
            _viewModel.Cleanup();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
        }

        private void SetupTrayIcon()
        {
            var icon = LoadTrayIcon() ?? System.Drawing.SystemIcons.Application;

            _trayIcon = new NotifyIcon
            {
                Visible = true,
                Text = "Privacy Filter",
                Icon = icon
            };

            _trayIcon.DoubleClick += (_, __) => Dispatcher.Invoke(ShowFromTray);

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open", null, (_, __) => Dispatcher.Invoke(ShowFromTray));

            _trayMinimizeOnStartupItem = new ToolStripMenuItem("Minimize to tray on startup")
            {
                Checked = _viewModel.MinimizeToTrayOnStartup,
                CheckOnClick = true
            };
            _trayMinimizeOnStartupItem.CheckedChanged += (_, __) =>
                Dispatcher.Invoke(() =>
                {
                    _viewModel.MinimizeToTrayOnStartup = _trayMinimizeOnStartupItem.Checked;
                    _viewModel.SaveSettingsFromUI();
                });
            menu.Items.Add(_trayMinimizeOnStartupItem);

            menu.Items.Add("Restart as Administrator", null, (_, __) => Dispatcher.Invoke(RestartAsAdmin));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, __) => Dispatcher.Invoke(ExitFromTray));

            _trayIcon.ContextMenuStrip = menu;
        }

        private static System.Drawing.Icon? LoadTrayIcon()
        {
            try
            {
                var streamInfo = System.Windows.Application.GetResourceStream(
                    new Uri("pack://application:,,,/gfx/icon.png", UriKind.Absolute));

                if (streamInfo?.Stream == null)
                    return null;

                using var bitmap = new System.Drawing.Bitmap(streamInfo.Stream);
                var hIcon = bitmap.GetHicon();
                try
                {
                    return (System.Drawing.Icon)System.Drawing.Icon.FromHandle(hIcon).Clone();
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
            catch
            {
                return null;
            }
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitFromTray()
        {
            _exitRequested = true;
            Close();
            System.Windows.Application.Current.Shutdown();
        }

        private void RestartAsAdmin()
        {
            try
            {
                var exePath = Assembly.GetEntryAssembly()?.Location;
                if (string.IsNullOrWhiteSpace(exePath))
                    return;

                var psi = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };

                if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    psi.FileName = "dotnet";
                    psi.Arguments = $"\"{exePath}\"";
                }
                else
                {
                    psi.FileName = exePath;
                }

                Process.Start(psi);
                ExitFromTray();
            }
            catch
            {
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RefreshProcesses();
            StatusText.Text = "Process list refreshed.";
        }

        private void ProcessesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.SelectedProcess != null)
            {
                var processName = _viewModel.SelectedProcess.Name;
                _viewModel.AddToPrivate();
                StatusText.Text = $"Added {processName} to private list.";
            }
        }

        private void PrivateProcessesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _viewModel.SelectedPrivateItem = e.NewValue;
        }

        private void PrivateProcessesTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.SelectedPrivateItem is ProcessGroup group)
            {
                _viewModel.RemoveFromPrivate();
                StatusText.Text = $"Removed {group.ProcessName} from private list.";
            }
            else if (_viewModel.SelectedPrivateItem is ProcessInfo process)
            {
                if (process.ParentGroup != null && !process.ParentGroup.BlurAllWindows)
                {
                    _viewModel.SetChildBlurToZero(process);
                    StatusText.Text = $"Blur set to 0% for {process.CurrentWindowTitle}.";
                }
            }
        }

        private void BlurIncrement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                if (element.DataContext is ProcessInfo process)
                {
                    var newValue = Math.Clamp(process.BlurLevel + BlurStep, 0, 100);
                    _viewModel.UpdateBlurLevel(process, newValue);
                    StatusText.Text = $"Blur level updated to {newValue}% for {process.Name}.";
                }
                else if (element.DataContext is ProcessGroup group)
                {
                    var newValue = Math.Clamp(group.BlurLevel + BlurStep, 0, 100);
                    _viewModel.UpdateBlurLevel(group, newValue);
                    StatusText.Text = $"Blur level updated to {newValue}% for {group.ProcessName}.";
                }
            }
        }

        private void BlurDecrement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                if (element.DataContext is ProcessInfo process)
                {
                    var newValue = Math.Clamp(process.BlurLevel - BlurStep, 0, 100);
                    _viewModel.UpdateBlurLevel(process, newValue);
                    StatusText.Text = $"Blur level updated to {newValue}% for {process.Name}.";
                }
                else if (element.DataContext is ProcessGroup group)
                {
                    var newValue = Math.Clamp(group.BlurLevel - BlurStep, 0, 100);
                    _viewModel.UpdateBlurLevel(group, newValue);
                    StatusText.Text = $"Blur level updated to {newValue}% for {group.ProcessName}.";
                }
            }
        }

        private void BlurTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitBlurTextBox(sender);
                e.Handled = true;

                if (sender is UIElement el)
                {
                    el.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            }
        }

        private void BlurTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitBlurTextBox(sender);
        }

        private void CommitBlurTextBox(object sender)
        {
            if (sender is not System.Windows.Controls.TextBox textBox)
                return;

            if (textBox.DataContext is ProcessInfo process)
            {
                if (!int.TryParse(textBox.Text, out var value))
                {
                    textBox.Text = process.BlurLevel.ToString(CultureInfo.InvariantCulture);
                    return;
                }

                var clamped = Math.Clamp(value, 0, 100);
                if (clamped != value)
                {
                    textBox.Text = clamped.ToString(CultureInfo.InvariantCulture);
                }

                _viewModel.UpdateBlurLevel(process, clamped);
                StatusText.Text = $"Blur level updated to {clamped}% for {process.Name}.";
            }
            else if (textBox.DataContext is ProcessGroup group)
            {
                if (!int.TryParse(textBox.Text, out var value))
                {
                    textBox.Text = group.BlurLevel.ToString(CultureInfo.InvariantCulture);
                    return;
                }

                var clamped = Math.Clamp(value, 0, 100);
                if (clamped != value)
                {
                    textBox.Text = clamped.ToString(CultureInfo.InvariantCulture);
                }

                _viewModel.UpdateBlurLevel(group, clamped);
                StatusText.Text = $"Blur level updated to {clamped}% for {group.ProcessName}.";
            }
        }

        private void AutoUnblur_Changed(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.SaveSettingsFromUI();
            }
        }

        private void MinimizeToTray_Changed(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                if (_trayMinimizeOnStartupItem != null)
                {
                    _trayMinimizeOnStartupItem.Checked = _viewModel.MinimizeToTrayOnStartup;
                }
                _viewModel.SaveSettingsFromUI();
            }
        }

        private void BlurAllWindows_Changed(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is ProcessGroup group)
            {
                foreach (var child in group.ChildWindows)
                {
                    _viewModel.UpdateBlurLevel(child, child.BlurLevel);
                }
                _viewModel.SaveSettingsFromUI();
            }
        }

        private void RestartAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            RestartAsAdmin();
        }
    }

    public class BoolToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive ? "Active" : "Blurred";
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
