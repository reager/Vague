using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Windows.Threading;
using Vague.Models;
using Vague.Services;

namespace Vague.ViewModels
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
        public ObservableCollection<ProcessGroup> PrivateProcessGroups { get; set; }

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

        private object? _selectedPrivateItem;
        public object? SelectedPrivateItem
        {
            get => _selectedPrivateItem;
            set
            {
                _selectedPrivateItem = value;
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
            PrivateProcessGroups = new ObservableCollection<ProcessGroup>();

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

            LoadProcesses();
            LoadSavedSettings();
            _monitorService.StartMonitoring();
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
            foreach (var group in PrivateProcessGroups)
            {
                foreach (var process in group.ChildWindows)
                {
                    var title = _processService.GetWindowTitleByHandle(process.MainWindowHandle);
                    if (!string.IsNullOrEmpty(title))
                    {
                        process.CurrentWindowTitle = title;
                    }
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
            if (SelectedProcess == null)
                return;

            var existingGroup = PrivateProcessGroups.FirstOrDefault(g => g.ProcessId == SelectedProcess.Id);
            
            if (existingGroup != null)
                return;

            var allWindowsFromProcess = Processes
                .Where(p => p.Id == SelectedProcess.Id)
                .ToList();

            if (allWindowsFromProcess.Count == 0)
                return;

            var newGroup = new ProcessGroup
            {
                ProcessId = SelectedProcess.Id,
                ProcessName = SelectedProcess.Name,
                BlurLevel = SelectedProcess.BlurLevel,
                AutoUnblurOnFocus = SelectedProcess.AutoUnblurOnFocus,
                BlurAllWindows = SelectedProcess.BlurAllWindows
            };

            foreach (var process in allWindowsFromProcess)
            {
                process.IsPrivate = true;
                process.IsActive = false;
                process.BlurLevel = newGroup.BlurLevel;
                process.AutoUnblurOnFocus = newGroup.AutoUnblurOnFocus;
                process.BlurAllWindows = newGroup.BlurAllWindows;
                process.ParentGroup = newGroup;
                newGroup.ChildWindows.Add(process);
                _blurService.ApplyBlur(process.MainWindowHandle, process.BlurLevel);
            }

            PrivateProcessGroups.Add(newGroup);
            UpdateMonitorService();
            SaveSettings();
        }

        public void RemoveFromPrivate()
        {
            if (SelectedPrivateItem is ProcessGroup group)
            {
                foreach (var child in group.ChildWindows.ToList())
                {
                    child.IsPrivate = false;
                    child.ParentGroup = null;
                    _blurService.RemoveBlur(child.MainWindowHandle);
                }
                PrivateProcessGroups.Remove(group);
                UpdateMonitorService();
                SaveSettings();
            }
            else if (SelectedPrivateItem is ProcessInfo process && process.ParentGroup != null)
            {
                process.IsPrivate = false;
                process.ParentGroup.ChildWindows.Remove(process);
                process.ParentGroup = null;
                _blurService.RemoveBlur(process.MainWindowHandle);
                
                if (process.ParentGroup?.ChildWindows.Count == 0)
                {
                    PrivateProcessGroups.Remove(process.ParentGroup);
                }
                
                UpdateMonitorService();
                SaveSettings();
            }
        }

        public void RemoveFromPrivate(ProcessInfo processToRemove)
        {
            if (processToRemove == null || processToRemove.ParentGroup == null)
                return;

            processToRemove.IsPrivate = false;
            var parentGroup = processToRemove.ParentGroup;
            parentGroup.ChildWindows.Remove(processToRemove);
            processToRemove.ParentGroup = null;

            if (parentGroup.ChildWindows.Count == 0)
            {
                PrivateProcessGroups.Remove(parentGroup);
            }

            UpdateMonitorService();
            _blurService.RemoveBlur(processToRemove.MainWindowHandle);
            SaveSettings();
        }

        public void UpdateBlurLevel(int level)
        {
            if (SelectedPrivateItem is ProcessInfo process)
            {
                process.BlurLevel = level;
                if (_blurService.IsWindowBlurred(process.MainWindowHandle))
                {
                    _blurService.UpdateBlurLevel(process.MainWindowHandle, level);
                }

                _settingsSaveTimer.Stop();
                _settingsSaveTimer.Start();
            }
            else if (SelectedPrivateItem is ProcessGroup group)
            {
                group.BlurLevel = level;
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

        public void UpdateBlurLevel(ProcessGroup group, int level)
        {
            if (group == null)
                return;

            group.BlurLevel = level;
            
            foreach (var child in group.ChildWindows)
            {
                if (_blurService.IsWindowBlurred(child.MainWindowHandle))
                {
                    _blurService.UpdateBlurLevel(child.MainWindowHandle, level);
                }
            }

            _settingsSaveTimer.Stop();
            _settingsSaveTimer.Start();
        }

        private void UpdateMonitorService()
        {
            var allPrivateProcesses = PrivateProcessGroups
                .SelectMany(g => g.ChildWindows)
                .ToList();
            _monitorService.SetPrivateProcesses(allPrivateProcesses);
        }

        private void LoadSavedSettings()
        {
            var settings = _settingsService.LoadSettings();
            MinimizeToTrayOnStartup = settings.MinimizeToTrayOnStartup;
            var allProcesses = _processService.GetRunningProcesses();
            
            var groupedByProcessId = settings.PrivateProcesses
                .GroupBy(p => p.ProcessName)
                .ToList();

            foreach (var savedGroup in groupedByProcessId)
            {
                var firstSaved = savedGroup.First();
                var matchingProcesses = allProcesses
                    .Where(p => p.Name.Equals(firstSaved.ProcessName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matchingProcesses.Count == 0)
                    continue;

                var newGroup = new ProcessGroup
                {
                    ProcessId = matchingProcesses[0].Id,
                    ProcessName = firstSaved.ProcessName,
                    BlurLevel = firstSaved.BlurLevel,
                    AutoUnblurOnFocus = firstSaved.AutoUnblurOnFocus,
                    BlurAllWindows = firstSaved.BlurAllWindows
                };

                foreach (var matchingProcess in matchingProcesses)
                {
                    var savedWindow = savedGroup.FirstOrDefault(s => 
                        s.WindowTitle.Equals(matchingProcess.MainWindowTitle, StringComparison.OrdinalIgnoreCase));

                    if (savedWindow != null)
                    {
                        matchingProcess.BlurLevel = savedWindow.BlurLevel;
                        matchingProcess.AutoUnblurOnFocus = savedWindow.AutoUnblurOnFocus;
                        matchingProcess.BlurAllWindows = savedWindow.BlurAllWindows;
                    }
                    else
                    {
                        matchingProcess.BlurLevel = newGroup.BlurLevel;
                        matchingProcess.AutoUnblurOnFocus = newGroup.AutoUnblurOnFocus;
                        matchingProcess.BlurAllWindows = newGroup.BlurAllWindows;
                    }

                    matchingProcess.IsPrivate = true;
                    matchingProcess.IsActive = false;
                    matchingProcess.ParentGroup = newGroup;
                    newGroup.ChildWindows.Add(matchingProcess);
                    _blurService.ApplyBlur(matchingProcess.MainWindowHandle, matchingProcess.BlurLevel);
                }

                PrivateProcessGroups.Add(newGroup);
            }
            
            UpdateMonitorService();
        }

        private void SaveSettings()
        {
            var settings = new VagueSettings
            {
                PrivateProcesses = PrivateProcessGroups
                    .SelectMany(g => g.ChildWindows.Select(p => new SavedProcessInfo
                    {
                        ProcessName = p.Name,
                        WindowTitle = p.MainWindowTitle,
                        BlurLevel = p.BlurLevel,
                        AutoUnblurOnFocus = p.AutoUnblurOnFocus,
                        BlurAllWindows = p.BlurAllWindows
                    }))
                    .ToList(),
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
