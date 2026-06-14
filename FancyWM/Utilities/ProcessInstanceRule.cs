using System;

namespace FancyWM.Utilities
{
    internal static class ProcessInstanceRule
    {
        public static string Format(string processName, int processId)
            => $"{processName}:{processId}";

        public static bool TryParse(string entry, out string processName, out int processId)
        {
            processName = string.Empty;
            processId = 0;

            if (string.IsNullOrWhiteSpace(entry))
            {
                return false;
            }

            var separator = entry.LastIndexOf(':');
            if (separator <= 0 || separator >= entry.Length - 1)
            {
                return false;
            }

            if (!int.TryParse(entry.AsSpan(separator + 1), out processId))
            {
                return false;
            }

            processName = entry[..separator];
            return processName.Length > 0;
        }
    }
}
