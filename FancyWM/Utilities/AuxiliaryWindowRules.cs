using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using WinMan;
using WinMan.Windows;

namespace FancyWM.Utilities
{
    /// <summary>
    /// Distinguishes auxiliary popups (find/search dialogs, owned tool windows) from primary application windows.
    /// </summary>
    internal static class AuxiliaryWindowRules
    {
        private const uint GW_OWNER = 4;
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;

        private const uint WS_POPUP = 0x8000_0000;
        private const uint WS_CAPTION = 0x00C0_0000;

        private const uint WS_EX_DLGMODALFRAME = 0x0000_0001;
        private const uint WS_EX_TOOLWINDOW = 0x0000_0080;
        private const uint WS_EX_APPWINDOW = 0x0004_0000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public static bool IsAuxiliaryApplicationWindow(IWindow window, IEnumerable<IWindow>? peersOnDisplay = null)
        {
            if (VisualBasicWindowRules.IsExcludedAuxiliaryWindow(window))
            {
                return true;
            }

            if (window is not Win32Window win32Window)
            {
                return false;
            }

            if (IsAuxiliaryByWindowChrome(win32Window))
            {
                return true;
            }

            if (peersOnDisplay != null && IsSmallerSecondaryWindow(win32Window, peersOnDisplay))
            {
                return true;
            }

            return false;
        }

        private static bool IsAuxiliaryByWindowChrome(Win32Window window)
        {
            var className = window.ClassName;
            if (className.Equals("#32770", StringComparison.Ordinal))
            {
                return true;
            }

            var hwnd = window.Handle;
            var exStyle = (uint)GetWindowLong(hwnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_DLGMODALFRAME) != 0)
            {
                return true;
            }

            if ((exStyle & WS_EX_TOOLWINDOW) != 0 && (exStyle & WS_EX_APPWINDOW) == 0)
            {
                return true;
            }

            if (HasOwnerInSameProcess(hwnd))
            {
                return true;
            }

            if ((exStyle & WS_EX_APPWINDOW) == 0)
            {
                var style = (uint)GetWindowLong(hwnd, GWL_STYLE);
                bool isPopupDialog = (style & WS_POPUP) != 0
                    && (style & WS_CAPTION) != 0
                    && !window.CanMaximize
                    && !window.CanMinimize;
                if (isPopupDialog)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasOwnerInSameProcess(IntPtr hwnd)
        {
            var owner = GetWindow(hwnd, GW_OWNER);
            if (owner == IntPtr.Zero || owner == hwnd)
            {
                return false;
            }

            _ = GetWindowThreadProcessId(hwnd, out uint processId);
            _ = GetWindowThreadProcessId(owner, out uint ownerProcessId);
            return processId != 0 && processId == ownerProcessId;
        }

        private static bool IsSmallerSecondaryWindow(Win32Window window, IEnumerable<IWindow> peersOnDisplay)
        {
            var area = (long)window.Position.Width * window.Position.Height;
            if (area <= 0)
            {
                return false;
            }

            long largestPrimaryArea = 0;
            foreach (var peer in peersOnDisplay)
            {
                if (peer == window || peer is not Win32Window)
                {
                    continue;
                }

                try
                {
                    if (peer.GetCachedProcessId() != window.GetCachedProcessId())
                    {
                        continue;
                    }
                }
                catch (InvalidWindowReferenceException)
                {
                    continue;
                }

                if (IsAuxiliaryByWindowChrome((Win32Window)peer))
                {
                    continue;
                }

                var peerArea = (long)peer.Position.Width * peer.Position.Height;
                if (peerArea > largestPrimaryArea)
                {
                    largestPrimaryArea = peerArea;
                }
            }

            if (largestPrimaryArea <= 0)
            {
                return false;
            }

            // e.g. Notepad++/Excel find dialogs: much smaller than the main editor window.
            const long maxAuxiliaryArea = 500L * 400L;
            return area < maxAuxiliaryArea && area * 5 < largestPrimaryArea * 2;
        }
    }
}
