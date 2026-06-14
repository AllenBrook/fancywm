using System.Windows;

using FancyWM.Layouts.Tiling;

namespace FancyWM.Controls
{
    public class StackTabReorderRoutedEventArgs : RoutedEventArgs
    {
        public StackTabReorderRoutedEventArgs(RoutedEvent routedEvent, object source, StackPanelNode stack, int fromIndex, int toIndex)
            : base(routedEvent, source)
        {
            Stack = stack;
            FromIndex = fromIndex;
            ToIndex = toIndex;
        }

        public StackPanelNode Stack { get; }

        public int FromIndex { get; }

        public int ToIndex { get; }
    }
}
