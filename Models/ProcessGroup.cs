using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Vague.Models
{
    public class ProcessGroup : INotifyPropertyChanged
    {
        private int _blurLevel = 95;
        private bool _autoUnblurOnFocus = true;
        private bool _blurAllWindows = true;

        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public ObservableCollection<ProcessInfo> ChildWindows { get; set; }

        public int BlurLevel
        {
            get => _blurLevel;
            set
            {
                if (_blurLevel != value)
                {
                    _blurLevel = value;
                    OnPropertyChanged();
                    if (BlurAllWindows)
                    {
                        foreach (var child in ChildWindows)
                        {
                            child.BlurLevel = value;
                        }
                    }
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
                    if (BlurAllWindows)
                    {
                        foreach (var child in ChildWindows)
                        {
                            child.AutoUnblurOnFocus = value;
                        }
                    }
                }
            }
        }

        public bool BlurAllWindows
        {
            get => _blurAllWindows;
            set
            {
                if (_blurAllWindows != value)
                {
                    _blurAllWindows = value;
                    OnPropertyChanged();
                    if (value)
                    {
                        foreach (var child in ChildWindows)
                        {
                            child.BlurLevel = BlurLevel;
                            child.AutoUnblurOnFocus = AutoUnblurOnFocus;
                            child.BlurAllWindows = true;
                            child.OnPropertyChanged(nameof(child.CanEditIndividually));
                        }
                    }
                    else
                    {
                        foreach (var child in ChildWindows)
                        {
                            child.BlurAllWindows = false;
                            child.OnPropertyChanged(nameof(child.CanEditIndividually));
                        }
                    }
                }
            }
        }

        public int WindowCount => ChildWindows.Count;

        public string DisplayText => $"{ProcessName} (PID: {ProcessId}) - {WindowCount} window(s)";

        public ProcessGroup()
        {
            ChildWindows = new ObservableCollection<ProcessInfo>();
            ChildWindows.CollectionChanged += (s, e) => OnPropertyChanged(nameof(WindowCount));
            ChildWindows.CollectionChanged += (s, e) => OnPropertyChanged(nameof(DisplayText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
