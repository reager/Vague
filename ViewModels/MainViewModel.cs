using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Windows.Threading;
using PrivacyFilter.Models;
using PrivacyFilter.Services;

namespace PrivacyFilter.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ProcessService _processService;
        private readonly WindowBlurService _blurService;
        private readonly WindowMonitorService _monitorService;
        private readonly SettingsService _settingsService;

        private readonly DispatcherTimer _titleRefreshTimer;
        private readonly DispatcherTimer _settingsSaveTimer;

        private bool _minimizeToTrayOnStartup = true;

        public bool MinimizeToTrayOnStartup
        {
            get => _minimizeToTrayOnStartup;
            set
            {
                if (_minimizeToTrayOnStartup != value)
                {
                    _minimizeToTrayOnStartup = value;
                    OnPropertyChanged();
                    ScheduleSettingsSave();
                }
            }
        }

        public ObservableCollection<ProcessInfo> Processes { get; set; }
        public ObservableCollection<ProcessInfo> PrivateProcesses { get; set; }

        private ProcessInfo? _selectedProcess;
        public ProcessInfo? SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                _selectedProcess = value;
                OnPropertyChanged();
            }
        }

        private ProcessInfo? _selectedPrivateProcess;
        public ProcessInfo? SelectedPrivateProcess
        {
            get => _selectedPrivateProcess;
            set
            {
                _selectedPrivateProcess = value;
                OnPropertyChanged();
            }
        }

        public MainViewModel()
        {
            _processService = new ProcessService();
            _blurService = new WindowBlurService();
            _monitorService = new WindowMonitorService(_blurService);
            _settingsService = new SettingsService();

            Processes = new ObservableCollection<ProcessInfo>();
            PrivateProcesses = new ObservableCollection<ProcessInfo>();

            LoadProcesses();
            LoadSavedSettings();
            _monitorService.StartMonitoring();

            _titleRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _titleRefreshTimer.Tick += (_, __) => RefreshPrivateWindowTitles();
            _titleRefreshTimer.Start();

            _settingsSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _settingsSaveTimer.Tick += (_, __) => FlushDebouncedSettingsSave();
        }

        private void FlushDebouncedSettingsSave()
        {
            _settingsSaveTimer.Stop();
            SaveSettings();
        }

        private void ScheduleSettingsSave()
        {
            _settingsSaveTimer.Stop();
            _settingsSaveTimer.Start();
        }

        private void RefreshPrivateWindowTitles()
        {
            foreach (var process in PrivateProcesses)
            {
                var title = _processService.GetWindowTitleByHandle(process.MainWindowHandle);
                if (!string.IsNullOrEmpty(title))
                {
                    process.CurrentWindowTitle = title;
                }
            }
        }

        private void LoadProcesses()
        {
            Processes.Clear();
            var processes = _processService.GetRunningProcesses();
            
            foreach (var process in processes)
            {
                Processes.Add(process);
            }
        }

        public void RefreshProcesses()
        {
            LoadProcesses();
        }

        public void AddToPrivate()
        {
            if (SelectedProcess != null && !PrivateProcesses.Any(p => p.MainWindowHandle == SelectedProcess.MainWindowHandle))
            {
                SelectedProcess.IsPrivate = true;
                SelectedProcess.IsActive = false;
                PrivateProcesses.Add(SelectedProcess);
                _blurService.ApplyBlur(SelectedProcess.MainWindowHandle, SelectedProcess.BlurLevel);
                UpdateMonitorService();
                SaveSettings();
            }
        }

        public void RemoveFromPrivate()
        {
            if (SelectedPrivateProcess != null)
            {
                var processToRemove = SelectedPrivateProcess;
                
                SelectedPrivateProcess.IsPrivate = false;
                
                PrivateProcesses.Remove(processToRemove);
                UpdateMonitorService();
                
                _blurService.RemoveBlur(processToRemove.MainWindowHandle);
                SaveSettings();
            }
        }

        public void RemoveFromPrivate(ProcessInfo processToRemove)
        {
            if (processToRemove == null)
                return;

            processToRemove.IsPrivate = false;

            PrivateProcesses.Remove(processToRemove);
            UpdateMonitorService();

            _blurService.RemoveBlur(processToRemove.MainWindowHandle);
            SaveSettings();
        }

        public void UpdateBlurLevel(int level)
        {
            if (SelectedPrivateProcess != null)
            {
                SelectedPrivateProcess.BlurLevel = level;
                if (_blurService.IsWindowBlurred(SelectedPrivateProcess.MainWindowHandle))
                {
                    _blurService.UpdateBlurLevel(SelectedPrivateProcess.MainWindowHandle, level);
                }

                _settingsSaveTimer.Stop();
                _settingsSaveTimer.Start();
            }
        }

        public void UpdateBlurLevel(ProcessInfo process, int level)
        {
            if (process == null)
                return;

            process.BlurLevel = level;
            if (_blurService.IsWindowBlurred(process.MainWindowHandle))
            {
                _blurService.UpdateBlurLevel(process.MainWindowHandle, level);
            }

            _settingsSaveTimer.Stop();
            _settingsSaveTimer.Start();
        }

        private void UpdateMonitorService()
        {
            _monitorService.SetPrivateProcesses(PrivateProcesses.ToList());
        }

        private void LoadSavedSettings()
        {
            var settings = _settingsService.LoadSettings();
            MinimizeToTrayOnStartup = settings.MinimizeToTrayOnStartup;
            var allProcesses = _processService.GetRunningProcesses();
            
            foreach (var savedProcess in settings.PrivateProcesses)
            {
                var matchingProcess = allProcesses.FirstOrDefault(p => 
                    p.Name.Equals(savedProcess.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                    p.MainWindowTitle.Equals(savedProcess.WindowTitle, StringComparison.OrdinalIgnoreCase));
                
                if (matchingProcess != null)
                {
                    matchingProcess.BlurLevel = savedProcess.BlurLevel;
                    matchingProcess.AutoUnblurOnFocus = savedProcess.AutoUnblurOnFocus;
                    matchingProcess.IsPrivate = true;
                    matchingProcess.IsActive = false;
                    PrivateProcesses.Add(matchingProcess);
                    _blurService.ApplyBlur(matchingProcess.MainWindowHandle, matchingProcess.BlurLevel);
                }
            }
            
            UpdateMonitorService();
        }

        private void SaveSettings()
        {
            var settings = new PrivacyFilterSettings
            {
                PrivateProcesses = PrivateProcesses.Select(p => new SavedProcessInfo
                {
                    ProcessName = p.Name,
                    WindowTitle = p.MainWindowTitle,
                    BlurLevel = p.BlurLevel,
                    AutoUnblurOnFocus = p.AutoUnblurOnFocus
                }).ToList(),
                MinimizeToTrayOnStartup = MinimizeToTrayOnStartup
            };
            
            _settingsService.SaveSettings(settings);
        }

        public void SaveSettingsFromUI()
        {
            ScheduleSettingsSave();
        }

        public void Cleanup()
        {
            SaveSettings();
            _monitorService.StopMonitoring();
            _blurService.RemoveAllBlurs();

            _titleRefreshTimer.Stop();
            _settingsSaveTimer.Stop();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
