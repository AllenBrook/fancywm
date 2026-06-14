using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using FancyWM.Layouts.Tiling;
using FancyWM.Utilities;
using FancyWM.ViewModels;

namespace FancyWM.Controls
{
    public partial class TabBar : UserControl
    {
        public delegate void StackTabReorderRequestedEventHandler(object sender, StackTabReorderRoutedEventArgs e);

        public static readonly RoutedEvent TabReorderRequestedEvent = EventManager.RegisterRoutedEvent(
            "TabReorderRequested",
            RoutingStrategy.Bubble,
            typeof(StackTabReorderRequestedEventHandler),
            typeof(TabBar));

        public static void AddTabReorderRequestedHandler(UIElement element, StackTabReorderRequestedEventHandler handler)
            => element.AddHandler(TabReorderRequestedEvent, handler);

        public static void RemoveTabReorderRequestedHandler(UIElement element, StackTabReorderRequestedEventHandler handler)
            => element.RemoveHandler(TabReorderRequestedEvent, handler);

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<TilingNodeViewModel>),
            typeof(TabBar));

        private const double DragThreshold = 4;

        private TilingNodeTab? m_dragSourceTab;
        private int m_dragSourceIndex = -1;
        private Point m_dragStartPoint;
        private bool m_isDragging;

        public ObservableCollection<TilingNodeViewModel> ItemsSource
        {
            get => (ObservableCollection<TilingNodeViewModel>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public TabBar()
        {
            InitializeComponent();
            PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
            PreviewMouseRightButtonUp += OnPreviewMouseRightButtonUp;
            MouseMove += OnMouseMove;
            LostMouseCapture += OnLostMouseCapture;
        }

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsOnCloseButton(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (FindParent<TilingNodeTab>(e.OriginalSource as DependencyObject) is not TilingNodeTab tab)
            {
                return;
            }

            if (DataContext is not TilingPanelViewModel panelViewModel || panelViewModel.Node is not StackPanelNode)
            {
                return;
            }

            m_dragSourceTab = tab;
            m_dragSourceIndex = GetTabIndex(tab, panelViewModel);
            m_dragStartPoint = e.GetPosition(this);
            m_isDragging = false;
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (m_dragSourceTab == null || e.RightButton != MouseButtonState.Pressed)
            {
                return;
            }

            var position = e.GetPosition(this);
            if (!m_isDragging)
            {
                var delta = position - m_dragStartPoint;
                if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
                {
                    return;
                }

                m_isDragging = true;
                CaptureMouse();
                m_dragSourceTab.Cursor = Cursors.SizeWE;
                Panel.SetZIndex(m_dragSourceTab, 100);
                m_dragSourceTab.RenderTransform = new TranslateTransform();
            }

            if (m_dragSourceTab.RenderTransform is TranslateTransform transform)
            {
                transform.X = position.X - m_dragStartPoint.X;
                transform.Y = 0;
            }

            e.Handled = true;
        }

        private void OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (m_dragSourceTab == null)
            {
                return;
            }

            if (m_isDragging
                && DataContext is TilingPanelViewModel panelViewModel
                && panelViewModel.Node is StackPanelNode stack
                && m_dragSourceIndex >= 0)
            {
                var insertBeforeIndex = GetInsertIndex(e.GetPosition(this));
                var targetIndex = insertBeforeIndex;
                if (m_dragSourceIndex < targetIndex)
                {
                    targetIndex--;
                }

                if (m_dragSourceIndex != targetIndex)
                {
                    RaiseEvent(new StackTabReorderRoutedEventArgs(
                        TabReorderRequestedEvent,
                        this,
                        stack,
                        m_dragSourceIndex,
                        targetIndex));
                }
            }

            e.Handled = true;
            ResetDragState();
        }

        private void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            ResetDragState();
        }

        private void ResetDragState()
        {
            if (m_dragSourceTab != null)
            {
                m_dragSourceTab.Cursor = null;
                Panel.SetZIndex(m_dragSourceTab, 0);
                m_dragSourceTab.RenderTransform = null;
            }

            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }

            m_dragSourceTab = null;
            m_dragSourceIndex = -1;
            m_isDragging = false;
        }

        private int GetTabIndex(TilingNodeTab tab, TilingPanelViewModel panelViewModel)
        {
            if (tab.DataContext is not TilingNodeViewModel tabViewModel)
            {
                return -1;
            }

            return panelViewModel.ChildNodes.IndexOf(tabViewModel);
        }

        private int GetInsertIndex(Point position)
        {
            var tabs = GetVisibleTabs();
            if (tabs.Count == 0)
            {
                return 0;
            }

            for (var i = 0; i < tabs.Count; i++)
            {
                var topLeft = tabs[i].TransformToAncestor(this).Transform(new Point(0, 0));
                var midX = topLeft.X + tabs[i].ActualWidth / 2;
                if (position.X < midX)
                {
                    return i;
                }
            }

            return tabs.Count;
        }

        private System.Collections.Generic.List<TilingNodeTab> GetVisibleTabs()
        {
            var tabs = new System.Collections.Generic.List<TilingNodeTab>();
            CollectTabs(TabsItemsControl, tabs);
            return tabs;
        }

        private static void CollectTabs(DependencyObject parent, System.Collections.Generic.List<TilingNodeTab> tabs)
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is TilingNodeTab tab)
                {
                    tabs.Add(tab);
                }
                else
                {
                    CollectTabs(child, tabs);
                }
            }
        }

        private static bool IsOnCloseButton(DependencyObject? source)
        {
            for (var current = source; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is Button { Parent: Grid parent } button && Grid.GetColumn(button) == 3)
                {
                    return true;
                }
            }

            return false;
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
            => child?.FindParent<T>();
    }
}
