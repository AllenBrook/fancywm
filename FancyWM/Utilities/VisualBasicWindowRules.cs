using System;
using System.Collections.Generic;
using System.Linq;

using WinMan;
using WinMan.Windows;

namespace FancyWM.Utilities
{
    internal static class VisualBasicWindowRules
    {
        public static bool IsVisualBasicProcess(string processName)
        {
            return processName.Equals("VB6", StringComparison.OrdinalIgnoreCase)
                || processName.Equals("VB5", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsExcludedAuxiliaryWindow(IWindow window)
        {
            if (window is not Win32Window win32Window)
            {
                return false;
            }

            var className = win32Window.ClassName;
            if (className.Equals("IDEOwner", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (className.Equals("ThunderMain", StringComparison.OrdinalIgnoreCase)
                || className.Equals("ThunderRT6Main", StringComparison.OrdinalIgnoreCase)
                || className.Equals("ThunderRT5Main", StringComparison.OrdinalIgnoreCase)
                || className.Equals("ThunderRTMain", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static IWindow? SelectPrimaryWindow(IEnumerable<IWindow> windows)
        {
            var candidates = windows
                .Where(w => !IsExcludedAuxiliaryWindow(w))
                .Where(w => w.Position.Width > 0 && w.Position.Height > 0)
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            var ideSurface = candidates
                .OfType<Win32Window>()
                .FirstOrDefault(w => w.ClassName.Equals("wndclass_desked_gsk", StringComparison.OrdinalIgnoreCase));

            if (ideSurface != null)
            {
                return ideSurface;
            }

            return candidates
                .OrderByDescending(w => (long)w.Position.Width * w.Position.Height)
                .First();
        }

        public static bool ShouldManage(IWindow window, IEnumerable<IWindow> peers)
        {
            try
            {
                if (!IsVisualBasicProcess(window.GetCachedProcessName()))
                {
                    return true;
                }

                if (IsExcludedAuxiliaryWindow(window))
                {
                    return false;
                }

                var primary = SelectPrimaryWindow(peers);
                return primary != null && primary.Equals(window);
            }
            catch (InvalidWindowReferenceException)
            {
                return true;
            }
        }
    }
}
