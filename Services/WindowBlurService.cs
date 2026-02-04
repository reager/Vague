using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Vague.Models;

namespace Vague.Services
{
    public class WindowBlurService
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int LWA_ALPHA = 0x00000002;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [StructLayout(LayoutKind.Sequential)]
        public struct DWM_BLURBEHIND
        {
            public int dwFlags;
            public bool fEnable;
            public IntPtr hRgnBlur;
            public bool fTransitionOnMaximized;
        }

        private const int DWM_BB_ENABLE = 0x00000001;
        private const int DWM_BB_BLURREGION = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private readonly object _stateLock = new object();
        private readonly Dictionary<IntPtr, WindowBlurState> _blurStates = new();

        public void ApplyBlur(IntPtr hWnd, int blurLevel)
        {
            if (hWnd == IntPtr.Zero) return;

            try
            {
                int originalStyle;
                lock (_stateLock)
                {
                    originalStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

                    if (!_blurStates.ContainsKey(hWnd))
                    {
                        _blurStates[hWnd] = new WindowBlurState
                        {
                            OriginalStyle = originalStyle,
                            IsBlurred = false,
                            BlurLevel = blurLevel
                        };
                    }
                }

                SetWindowLong(hWnd, GWL_EXSTYLE, originalStyle | WS_EX_LAYERED);

                var alpha = (byte)(255 - (blurLevel * 255 / 100));
                SetLayeredWindowAttributes(hWnd, 0, alpha, LWA_ALPHA);

                var blurBehind = new DWM_BLURBEHIND
                {
                    dwFlags = DWM_BB_ENABLE,
                    fEnable = true,
                    hRgnBlur = IntPtr.Zero,
                    fTransitionOnMaximized = false
                };

                DwmEnableBlurBehindWindow(hWnd, ref blurBehind);

                lock (_stateLock)
                {
                    _blurStates[hWnd].IsBlurred = true;
                    _blurStates[hWnd].BlurLevel = blurLevel;
                }

                InvalidateRect(hWnd, IntPtr.Zero, true);
            }
            catch
            {
            }
        }

        public void RemoveBlur(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            try
            {
                WindowBlurState? state;
                lock (_stateLock)
                {
                    _blurStates.TryGetValue(hWnd, out state);
                }

                if (state != null)
                {
                    SetWindowLong(hWnd, GWL_EXSTYLE, state.OriginalStyle);

                    SetLayeredWindowAttributes(hWnd, 0, 255, LWA_ALPHA);

                    var blurBehind = new DWM_BLURBEHIND
                    {
                        dwFlags = DWM_BB_ENABLE,
                        fEnable = false,
                        hRgnBlur = IntPtr.Zero,
                        fTransitionOnMaximized = false
                    };

                    DwmEnableBlurBehindWindow(hWnd, ref blurBehind);

                    InvalidateRect(hWnd, IntPtr.Zero, true);

                    lock (_stateLock)
                    {
                        _blurStates.Remove(hWnd);
                    }
                }
            }
            catch
            {
            }
        }

        public void RemoveAllBlurs()
        {
            List<IntPtr> handles;
            lock (_stateLock)
            {
                handles = new List<IntPtr>(_blurStates.Keys);
            }
            foreach (var hWnd in handles)
            {
                RemoveBlur(hWnd);
            }
        }

        public bool IsWindowBlurred(IntPtr hWnd)
        {
            lock (_stateLock)
            {
                return _blurStates.ContainsKey(hWnd) && _blurStates[hWnd].IsBlurred;
            }
        }

        public void UpdateBlurLevel(IntPtr hWnd, int blurLevel)
        {
            if (hWnd == IntPtr.Zero) return;

            try
            {
                lock (_stateLock)
                {
                    if (_blurStates.ContainsKey(hWnd))
                    {
                        var alpha = (byte)(255 - (blurLevel * 255 / 100));
                        SetLayeredWindowAttributes(hWnd, 0, alpha, LWA_ALPHA);
                        _blurStates[hWnd].BlurLevel = blurLevel;
                    }
                }
            }
            catch
            {
            }
        }

        private class WindowBlurState
        {
            public int OriginalStyle { get; set; }
            public bool IsBlurred { get; set; }
            public int BlurLevel { get; set; }
        }
    }
}
