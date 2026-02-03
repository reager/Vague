using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using PrivacyFilter.Models;

namespace PrivacyFilter.Services
{
    public class ProcessService
    {
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public List<ProcessInfo> GetRunningProcesses()
        {
            var processes = new List<ProcessInfo>();
            var seenWindows = new HashSet<string>();

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    GetWindowThreadProcessId(hWnd, out uint processId);
                    try
                    {
                        var process = Process.GetProcessById((int)processId);
                        var windowTitle = GetWindowTitle(hWnd);
                        
                        if (process != null && !string.IsNullOrEmpty(windowTitle))
                        {
                            var isBrowser = IsBrowserProcess(process.ProcessName);
                            string uniqueKey;
                            
                            if (isBrowser)
                            {
                                uniqueKey = $"{process.ProcessName}|{windowTitle}|{hWnd}";
                            }
                            else
                            {
                                uniqueKey = process.Id.ToString();
                            }
                            
                            if (!seenWindows.Contains(uniqueKey))
                            {
                                seenWindows.Add(uniqueKey);
                                var processInfo = new ProcessInfo(process, windowTitle)
                                {
                                    MainWindowHandle = hWnd
                                };
                                processes.Add(processInfo);
                            }
                        }
                    }
                    catch
                    {
                    }
                }
                return true;
            }, IntPtr.Zero);

            return processes;
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            var length = (int)GetWindowTextLength(hWnd);
            if (length == 0) return string.Empty;
            
            var builder = new StringBuilder(length + 1);
            GetWindowText(hWnd, builder, builder.Capacity);
            return builder.ToString();
        }

        public string GetWindowTitleByHandle(IntPtr hWnd)
        {
            try
            {
                return GetWindowTitle(hWnd);
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool IsBrowserProcess(string processName)
        {
            var browserNames = new[] { "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "iexplore" };
            return browserNames.Any(b => processName.ToLower().Contains(b));
        }

        public ProcessInfo? GetProcessByWindowHandle(IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out uint processId);
            try
            {
                var process = Process.GetProcessById((int)processId);
                var windowTitle = GetWindowTitle(hWnd);
                
                if (process != null && !string.IsNullOrEmpty(windowTitle))
                {
                    return new ProcessInfo(process, windowTitle)
                    {
                        MainWindowHandle = hWnd
                    };
                }
            }
            catch
            {
            }
            
            return null;
        }
    }
}
