using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;
using PrivacyFilter.Models;

namespace PrivacyFilter.Services
{
    public class WindowMonitorService
    {
        private const int ForegroundDebounceMs = 200;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        private const uint GA_ROOT = 2;
        private const uint GA_ROOTOWNER = 3;

        private const uint EVENT_SYSTEM_FOREGROUND = 3;
        private const uint EVENT_SYSTEM_MINIMIZESTART = 6;
        private const uint EVENT_SYSTEM_MINIMIZEEND = 7;
        private const uint WINEVENT_OUTOFCONTEXT = 0;

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private IntPtr _hook;
        private readonly WindowBlurService _blurService;
        private readonly List<ProcessInfo> _privateProcesses;
        private WinEventDelegate? _winEventDelegate;
        private readonly object _lockObject = new object();

        private readonly object _debounceLock = new object();
        private Timer? _debounceTimer;
        private int _foregroundEventToken;

        public WindowMonitorService(WindowBlurService blurService)
        {
            _blurService = blurService;
            _privateProcesses = new List<ProcessInfo>();
        }

        public void StartMonitoring()
        {
            if (_hook != IntPtr.Zero)
                return;

            _winEventDelegate = new WinEventDelegate(WinEventProc);
            _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_MINIMIZEEND, IntPtr.Zero, 
                _winEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        public void StopMonitoring()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWinEvent(_hook);
                _hook = IntPtr.Zero;
            }

            lock (_debounceLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }
        }

        public void SetPrivateProcesses(List<ProcessInfo> processes)
        {
            lock (_lockObject)
            {
                _privateProcesses.Clear();
                _privateProcesses.AddRange(processes);
            }
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                if (eventType == EVENT_SYSTEM_FOREGROUND)
                {
                    ScheduleDebouncedForegroundHandling();
                }
            }
            catch
            {
            }
        }

        private void HandleWindowActivated(IntPtr activatedWindow)
        {
            HandleWindowActivated(activatedWindow, Volatile.Read(ref _foregroundEventToken));
        }

        private void ScheduleDebouncedForegroundHandling()
        {
            Interlocked.Increment(ref _foregroundEventToken);

            lock (_debounceLock)
            {
                _debounceTimer ??= new Timer(_ => DebouncedForegroundCallback(), null, Timeout.Infinite, Timeout.Infinite);
                _debounceTimer.Change(ForegroundDebounceMs, Timeout.Infinite);
            }
        }

        private void DebouncedForegroundCallback()
        {
            var token = Volatile.Read(ref _foregroundEventToken);

            try
            {
                if (token != Volatile.Read(ref _foregroundEventToken))
                    return;

                var hwnd = GetForegroundWindow();
                if (token != Volatile.Read(ref _foregroundEventToken))
                    return;

                HandleWindowActivated(hwnd, token);
            }
            catch
            {
            }
        }

        private IntPtr NormalizeToRootWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return IntPtr.Zero;

            var root = GetAncestor(hwnd, GA_ROOTOWNER);
            if (root == IntPtr.Zero)
                root = GetAncestor(hwnd, GA_ROOT);
            if (root == IntPtr.Zero)
                root = hwnd;

            return root;
        }

        private void HandleWindowActivated(IntPtr activatedWindow, int expectedToken)
        {
            try
            {
                if (activatedWindow == IntPtr.Zero)
                    return;

                var activatedRoot = NormalizeToRootWindow(activatedWindow);

                GetWindowThreadProcessId(activatedWindow, out var activatedProcessId);
                var activatedPid = (int)activatedProcessId;

                List<ProcessInfo> processesCopy;
                lock (_lockObject)
                {
                    processesCopy = new List<ProcessInfo>(_privateProcesses);
                }

                var pidMatchCount = 0;
                if (activatedPid != 0)
                {
                    foreach (var p in processesCopy)
                    {
                        if (p.Id == activatedPid)
                            pidMatchCount++;
                    }
                }

                foreach (var process in processesCopy)
                {
                    if (expectedToken != Volatile.Read(ref _foregroundEventToken))
                        return;

                    var processRoot = NormalizeToRootWindow(process.MainWindowHandle);

                    var isActive = processRoot != IntPtr.Zero && processRoot == activatedRoot
                        || (activatedPid != 0 && pidMatchCount == 1 && process.Id == activatedPid);

                    if (isActive)
                    {
                        if (process.AutoUnblurOnFocus)
                        {
                            _blurService.RemoveBlur(process.MainWindowHandle);
                        }
                        process.IsActive = true;
                    }
                    else if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        _blurService.ApplyBlur(process.MainWindowHandle, process.BlurLevel);
                        process.IsActive = false;
                    }
                }
            }
            catch
            {
            }
        }
    }
}
