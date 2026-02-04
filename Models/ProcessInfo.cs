using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Vague.Models
{
    public class ProcessInfo : INotifyPropertyChanged
    {
        private int _blurLevel = 95;
        private bool _isActive;
        private bool _autoUnblurOnFocus = true;
        private string _currentWindowTitle = string.Empty;

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MainWindowTitle { get; set; } = string.Empty;
        public bool IsPrivate { get; set; }
        
        public int BlurLevel 
        { 
            get => _blurLevel;
            set
            {
                if (_blurLevel != value)
                {
                    _blurLevel = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public bool IsActive 
        { 
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public bool AutoUnblurOnFocus
        {
            get => _autoUnblurOnFocus;
            set
            {
                if (_autoUnblurOnFocus != value)
                {
                    _autoUnblurOnFocus = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CurrentWindowTitle
        {
            get => _currentWindowTitle;
            set
            {
                if (_currentWindowTitle != value)
                {
                    _currentWindowTitle = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public IntPtr MainWindowHandle { get; set; }

        public ProcessInfo(Process process)
        {
            Id = process.Id;
            Name = process.ProcessName;
            MainWindowTitle = process.MainWindowTitle ?? string.Empty;
            CurrentWindowTitle = MainWindowTitle;
            MainWindowHandle = process.MainWindowHandle;
        }

        public ProcessInfo(Process process, string windowTitle)
        {
            Id = process.Id;
            Name = process.ProcessName;
            MainWindowTitle = windowTitle;
            CurrentWindowTitle = windowTitle;
            MainWindowHandle = process.MainWindowHandle;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
