using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;
using Vague.Models;

namespace Vague.Services
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

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

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
                if (activatedRoot == IntPtr.Zero)
                    return;

                GetWindowThreadProcessId(activatedWindow, out var activatedProcessId);
                var activatedPid = (int)activatedProcessId;

                List<ProcessInfo> processesCopy;
                lock (_lockObject)
                {
                    processesCopy = new List<ProcessInfo>(_privateProcesses);
                }

                if (processesCopy.Count == 0)
                    return;

                ProcessInfo? matchedProcess = null;

                foreach (var process in processesCopy)
                {
                    if (expectedToken != Volatile.Read(ref _foregroundEventToken))
                        return;

                    if (!IsValidWindow(process.MainWindowHandle))
                        continue;

                    var processRoot = NormalizeToRootWindow(process.MainWindowHandle);
                    if (processRoot == IntPtr.Zero)
                        continue;

                    if (processRoot == activatedRoot)
                    {
                        matchedProcess = process;
                        break;
                    }
                }

                foreach (var process in processesCopy)
                {
                    if (expectedToken != Volatile.Read(ref _foregroundEventToken))
                        return;

                    if (!IsValidWindow(process.MainWindowHandle))
                        continue;

                    bool isThisWindowActive = process == matchedProcess;

                    if (isThisWindowActive)
                    {
                        process.IsActive = true;
                        if (process.AutoUnblurOnFocus)
                        {
                            _blurService.RemoveBlur(process.MainWindowHandle);
                        }
                        else
                        {
                            _blurService.ApplyBlur(process.MainWindowHandle, process.BlurLevel);
                        }
                    }
                    else
                    {
                        process.IsActive = false;
                        _blurService.ApplyBlur(process.MainWindowHandle, process.BlurLevel);
                    }
                }
            }
            catch
            {
            }
        }

        private bool IsValidWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return false;

            if (!IsWindow(hWnd))
                return false;

            if (!IsWindowVisible(hWnd))
                return false;

            if (IsIconic(hWnd))
                return false;

            return true;
        }
    }
}
