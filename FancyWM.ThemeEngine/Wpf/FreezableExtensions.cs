using System.Windows;

namespace FancyWM.ThemeEngine.Wpf
{
    internal static class FreezableExtensions
    {
        public static void FreezeIfPossible(this Freezable freezable)
        {
            if (!freezable.IsFrozen && freezable.CanFreeze)
            {
                freezable.Freeze();
            }
        }
    }
}
