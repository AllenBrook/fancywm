using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

using FancyWM.Layouts.Tiling;
using FancyWM.Utilities;
using WinMan;
using System;
using FancyWM.Models;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using Serilog;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;

#if DEBUG
using Lock = FancyWM.Utilities.DebugLock;
#else
using Lock = System.Threading.Lock;
#endif

namespace FancyWM
{
    /// <summary>
    /// Manages the layout of window in the workspace
    /// </summary>
    internal partial class TilingService : ITilingService, IDisposable
    {
        private enum UserInteraction
        {
            None,
            Starting,
            Moving,
            Resizing,
        }

        private class NodeLocation(TilingNode node)
        {
            public PanelNode Parent = node.Parent ?? throw new ArgumentException(nameof(node));
            public int Index = node.Parent.IndexOf(node);
            public Rectangle ComputedRectangle = node.ComputedRectangle;
        }

        private sealed class ShowDesktopLayoutSnapshot
        {
            public required PanelNode RootClone { get; init; }
            public required Dictionary<IntPtr, Rectangle> WindowRects { get; init; }
            public IntPtr? FocusedWindowHandle { get; init; }
        }

        public event EventHandler<TilingFailedEventArgs>? PlacementFailed;
        public event EventHandler<EventArgs>? PendingIntentChanged;

        /// <summary>
        /// Current active state.
        /// <see cref="Start"/>
        /// <see cref="Stop"/>
        /// </summary>
        public bool Active
        {
            get => m_active;
        }

        public bool AutoRegisterWindows { get; internal set; }

        private bool m_allocateNewPanelSpace;

        private bool m_stackAppendRestoredTabsToEnd = true;

        private bool m_animateWindowMovement;

        private int m_autoSplitCount = 100;

        private bool m_delayReposition = false;

        private void SetAutoCollapse(bool value)
        {
            m_backend.AutoCollapse = value;
        }

        private void SetWindowPadding(int value)
        {
            m_windowPadding = value;
            PropagatePaddingChange();
        }

        private void SetPanelHeight(int value)
        {
            m_panelHeight = value;
            PropagatePanelHeightChange();
        }

        private void SetShowFocus(bool value)
        {
            m_showFocus = value;
            PropagateShowFocusChange();
        }

        public bool ShowPreviewFocus
        {
            get => m_showPreviewFocus;
            set
            {
                m_showPreviewFocus = value;
                PropagateShowPreviewFocusChange();
            }
        }

        public IWorkspace Workspace => m_workspace;

        public IReadOnlyCollection<IWindowMatcher> InclusionMatchers
        {
            get => m_inclusionMatchers;
            set
            {
                m_inclusionMatchers = [.. value];

                using (m_windowSetLock.EnterScope())
                {
                    foreach (var window in m_windowSet)
                    {
                        using (m_floatingSetLock.EnterScope())
                        {
                            if (ShouldAutoTile(window))
                            {
                                m_floatingSet.Remove(window);
                            }
                            else
                            {
                                m_floatingSet.Add(window);
                            }
                        }
                    }
                }
                Refresh();
            }
        }

        public ITilingServiceIntent? PendingIntent
        {
            get => m_pendingIntent;
            set
            {
                if (m_pendingIntent != value)
                {
                    m_pendingIntent = value;
                    PendingIntentChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        private static readonly IReadOnlySet<IWindow> EmptyWindowSet = new HashSet<IWindow>();
        private static readonly TimeSpan LockThreshold = TimeSpan.FromMilliseconds(10);

        /// <summary>
        /// The dispatcher from the thread that created the <see cref="TilingService"/>
        /// </summary>
        private readonly Dispatcher m_dispatcher;
        private readonly IWorkspace m_workspace;
        private readonly ILogger m_logger = App.Current.Logger;
        private IReadOnlyCollection<IWindowMatcher> m_inclusionMatchers = [];

        private readonly TilingOverlayRenderer m_gui;
        private readonly IDisplay m_display;

        private readonly TilingWorkspace m_backend;
        private readonly Utilities.DebugLock m_backendLock = new(LockThreshold);

        private readonly HashSet<IWindow> m_newWindowSet = [];
        private readonly Utilities.DebugLock m_newWindowSetLock = new(LockThreshold);

        private readonly HashSet<IWindow> m_windowSet = [];
        private readonly Utilities.DebugLock m_windowSetLock = new(LockThreshold);

        private readonly HashSet<IWindow> m_floatingSet = [];
        private readonly Utilities.DebugLock m_floatingSetLock = new(LockThreshold);


        private readonly HashSet<IWindow> m_ignoreRepositionSet = [];
        private readonly Utilities.DebugLock m_ignoreRepositionSetLock = new(LockThreshold);

        private static readonly Dictionary<IWindow, bool> s_crossDisplayStackTransfers = new();
        private static readonly object s_crossDisplayStackTransfersLock = new();

        /// <summary>
        /// Raised after a window is marked for cross-display stack handoff so the target display can admit it.
        /// </summary>
        internal static Action<IWindow>? CrossDisplayStackTransferReady;

        private readonly Dictionary<IWindow, NodeLocation> m_savedLocations = [];
        private readonly Utilities.DebugLock m_savedLocationsLock = new(LockThreshold);

        private ShowDesktopLayoutSnapshot? m_showDesktopSnapshot;
        private bool m_showDesktopAwaitingRestore;
        private int m_showDesktopRestoreGeneration;

        private readonly CompositeDisposable m_subscriptions = [];
        private readonly IAnimationThread m_animationThread;
        private int m_panelHeight = 20;
        private int m_windowPadding = 2;
        private bool m_showFocus = false;
        private bool m_showPreviewFocus = false;

        private bool m_active = false;
        private bool m_dirty = true;
        private UserInteraction m_currentInteraction = UserInteraction.None;
        private PanelNode? m_movingPanelNode;
        private ITilingServiceIntent? m_pendingIntent;
        private readonly Counter m_frozen = new();
        private readonly Stopwatch m_sw = new();

        public TilingService(IWorkspace workspace, IDisplay display, IAnimationThread animationThread, IObservable<ITilingServiceSettings> settings, bool autoRegisterWindows)
        {
            m_logger.Information("Managing display {Display} (Bounds: {Bounds}, Scale: {Scaling})", display, display.Bounds, display.Scaling);
            m_dispatcher = Dispatcher.CurrentDispatcher;
            m_workspace = workspace;
            m_animationThread = animationThread;
            m_display = display;
            m_backend = new TilingWorkspace();
            m_gui = new TilingOverlayRenderer(display, GetOverlayAnchor)
            {
                PanelSpacing = GetPanelSpacing(),
                PanelPadding = ToThickness(GetPanelPaddingRect()),
            };
            m_gui.TilingNodeFocusRequested += OnTilingNodeFocusRequested;
            m_gui.TilingNodeCloseRequested += OnTilingNodeCloseRequested;
            m_gui.TilingNodePullUpRequested += OnTilingNodePullUpRequested;
            m_gui.TilingPanelMoving += OnTilingPanelMoving;
            m_gui.TilingPanelMoveRequested += OnTilingPanelMoveRequested;
            m_gui.BeginHorizontalWithRequested += OnBeginHorizontalWithRequestedAsync;
            m_gui.BeginVerticalWithRequested += OnBeginVerticalWithRequested;
            m_gui.BeginStackWithRequested += OnBeginStackWithRequested;
            m_gui.FloatRequested += OnWindowFloatRequested;
            m_gui.HorizontalSplitRequested += OnWindowHorizontalSplitRequested;
            m_gui.VerticalSplitRequested += OnWindowVerticalSplitRequested;
            m_gui.PullUpRequested += OnWindowPullUpRequested;
            m_gui.StackRequested += OnWindowStackRequested;
            m_gui.StackTabReorderRequested += OnStackTabReorderRequested;
            m_gui.IgnoreProcessRequested += OnWindowIgnoreProcessRequested;
            m_gui.IgnoreClassRequested += OnWindowIgnoreClassRequested;

            AutoRegisterWindows = autoRegisterWindows;

            foreach (var d in m_workspace.VirtualDesktopManager.Desktops)
            {
                OnDesktopAdded(this, new DesktopChangedEventArgs(d));
            }

            m_workspace.VirtualDesktopManager.DesktopAdded += OnDesktopAdded;
            m_workspace.VirtualDesktopManager.DesktopRemoved += OnDesktopRemoved;
            m_workspace.VirtualDesktopManager.CurrentDesktopChanged += OnCurrentDesktopChanged;
            m_workspace.CursorLocationChanged += OnCursorLocationChanged;

            m_display.ScalingChanged += OnDisplayScalingChanged;

            m_workspace.WindowAdded += OnWindowAdded;
            m_workspace.WindowRemoved += OnWindowRemoved;

            PlacementFailed += OnPlacementFailed;
            PendingIntentChanged += OnPendingIntentChanged;

            m_subscriptions.Add(m_gui);
            m_subscriptions.Add(settings.Subscribe(OnSettingsChanged));

            var currentDesktop = m_workspace.VirtualDesktopManager.CurrentDesktop;
            OnCurrentDesktopChanged(this, new CurrentDesktopChangedEventArgs(currentDesktop, currentDesktop));

            var tree = m_backend.GetTree(currentDesktop)!;
            foreach (var w in m_workspace.GetSnapshot())
            {
                OnWindowAdded(w, new WindowChangedEventArgs(w));
                if (m_backend.HasWindow(w))
                {
                    m_backend.SetFocus(w);
                    UpdateTree(tree);
                }
            }

            m_sw.Start();
        }

        private void OnSettingsChanged(ITilingServiceSettings x)
        {
            _ = m_dispatcher.RunAsync(() =>
            {
                m_allocateNewPanelSpace = x.AllocateNewPanelSpace;
                m_stackAppendRestoredTabsToEnd = x.StackAppendRestoredTabsToEnd;
                m_animateWindowMovement = x.AnimateWindowMovement;
                m_autoSplitCount = x.AutoSplitCount;
                m_delayReposition = x.DelayReposition;
                SetWindowPadding(x.WindowPadding);
                SetPanelHeight(x.PanelHeight);
                SetShowFocus(x.ShowFocus);
                SetAutoCollapse(x.AutoCollapsePanels);
            });
        }

        public void Start()
        {
            m_active = true;
            InvalidateLayout();
            m_gui.Show();
        }

        public void Stop()
        {
            m_active = false;
            RestoreOriginalLayout();
            m_gui.Hide();
        }

        public void ResetLayout()
        {
            if (m_dispatcher.CheckAccess())
            {
                ResetLayoutCore();
            }
            else
            {
                m_dispatcher.Invoke(ResetLayoutCore);
            }
        }

        public bool CanMoveFocus(TilingDirection direction)
        {
            return HasFocusAndAdjacentWindow(direction);
        }

        public void MoveFocus(TilingDirection direction)
        {
            var focusedNode = GetFocusedTilingNode(ensureManaged: true)
                ?? throw new TilingFailedException(TilingError.MissingTarget);

            using (m_backendLock.EnterScope())
            {
                var adjacentWindow = focusedNode.GetAdjacentWindow(direction)
                    ?? throw new TilingFailedException(TilingError.MissingAdjacentWindow);
                if (FocusHelper.ForceActivate(adjacentWindow.WindowReference.Handle))
                {
                    m_backend.SetFocus(adjacentWindow);
                }
            }

            InvalidateLayout();
        }

        public bool CanMoveWindow(TilingDirection direction)
        {
            return HasFocusAndAdjacentWindow(direction);
        }

        public void MoveWindow(TilingDirection direction)
        {
            var focusedNode = GetFocusedTilingNode(ensureManaged: true)
                ?? throw new TilingFailedException(TilingError.MissingTarget);

            using (m_backendLock.EnterScope())
            {
                WindowNode? adjacentWindow = focusedNode.GetAdjacentWindow(direction) ?? throw new TilingFailedException(TilingError.MissingAdjacentWindow);
                var adjancentWindowIndex = adjacentWindow.Parent!.IndexOf(adjacentWindow);

                if (adjacentWindow.Parent == focusedNode.Parent)
                {
                    var focusedNodeIndex = focusedNode.Parent.IndexOf(focusedNode);
                    var adjacentNodeIndex = focusedNode.Parent.IndexOf(adjacentWindow);
                    focusedNode.Parent.Move(focusedNodeIndex, adjacentNodeIndex);
                }
                else
                {

                    if (direction == TilingDirection.Left || direction == TilingDirection.Up)
                    {
                        m_backend.MoveAfter(focusedNode, adjacentWindow);
                    }
                    else
                    {
                        m_backend.MoveBefore(focusedNode, adjacentWindow);
                    }
                }
            }

            InvalidateLayout();
        }

        public bool CanSwapFocus(TilingDirection direction)
        {
            return HasFocusAndAdjacentWindow(direction);
        }

        public void SwapFocus(TilingDirection direction)
        {
            var focusedNode = GetFocusedTilingNode(ensureManaged: true)
                ?? throw new TilingFailedException(TilingError.MissingTarget);
            var adjacentWindow = focusedNode.GetAdjacentWindow(direction)
                ?? throw new TilingFailedException(TilingError.MissingAdjacentWindow);

            using (m_backendLock.EnterScope())
            {
                focusedNode.Swap(adjacentWindow);
            }
            InvalidateLayout();
        }

        public bool DiscoverWindows()
        {
            if (!AutoRegisterWindows)
            {
                return false;
            }

            List<IWindow> windows;
            using (m_windowSetLock.EnterScope())
            {
                windows = [.. m_windowSet];
            }

            bool anyChanges = false;
            foreach (var window in windows)
            {
                if (!m_backend.HasWindow(window) && window.State == WindowState.Restored && CanManage(window) && ShouldAutoTile(window))
                {
                    using (m_backendLock.EnterScope())
                    {
                        try
                        {
                            m_logger.Debug("Discovered window {Window}", window.DebugString());
                            if (TryRegisterAutoTiledWindow(window, out _))
                            {
                                anyChanges = true;
                            }
                        }
                        catch (NoValidPlacementExistsException)
                        {
                            PlacementFailed?.Invoke(this, new TilingFailedEventArgs(
                                TilingError.NoValidPlacementExists, window));
                        }
                        catch (InvalidWindowReferenceException)
                        {
                            if (m_backend.HasWindow(window))
                                m_backend.UnregisterWindow(window);
                        }
                    }
                }
            }

            if (anyChanges)
            {
                InvalidateLayout();
            }

            return anyChanges;
        }

        public void Refresh()
        {
            List<IWindow> windows;
            using (m_windowSetLock.EnterScope())
            {
                windows = [.. m_windowSet];
            }

            bool anyChanges = false;
            foreach (var window in windows)
            {
                if (!IsWindowOnThisDisplay(window))
                {
                    continue;
                }

                if (DetectChanges(window))
                {
                    anyChanges = true;
                }
            }

            if (anyChanges)
            {
                InvalidateLayout();
            }

            List<IWindow> movedWindows = [];

            using (m_backendLock.EnterScope())
            {
                foreach (var desktop in m_workspace.VirtualDesktopManager.Desktops)
                {
                    var tree = m_backend.GetTree(desktop);
                    if (tree == null)
                        continue;
                    foreach (var window in windows)
                    {
                        if (!IsWindowOnThisDisplay(window))
                        {
                            continue;
                        }

                        if (tree.FindNode(window) != null && !desktop.HasWindow(window))
                        {
                            movedWindows.Add(window);
                        }
                    }
                }
            }

            foreach (var movedWindow in movedWindows)
            {
                OnWindowRemoved(movedWindow, new WindowChangedEventArgs(movedWindow));
                OnWindowAdded(movedWindow, new WindowChangedEventArgs(movedWindow));
            }
        }

        private static bool CanSplit(TilingNode node)
        {
            return !node.PathToRoot.OfType<StackPanelNode>().Any();
        }

        public bool CanSplit(bool vertical)
        {
            var focusedNode = GetFocusedTilingNode(ensureManaged: false);
            if (focusedNode != null)
                return CanSplit(focusedNode);

            var window = m_workspace.FocusedWindow;
            return window != null && CanManage(window, ignoreFloating: true);
        }

        public void Split(bool vertical)
        {
            var focusedNode = GetFocusedTilingNode(ensureManaged: true)
                ?? throw new TilingFailedException(TilingError.MissingTarget);

            using (m_backendLock.EnterScope())
            {
                WrapInSplitPanel(focusedNode, vertical);
                m_backend.SetFocus(focusedNode);
            }
        }

        public bool CanFloat()
        {
            var window = m_workspace.FocusedWindow;
            return window != null && CanManage(window, ignoreFloating: true);
        }

        public void Float()
        {
            var window = m_workspace.FocusedWindow ?? throw new TilingFailedException(TilingError.MissingTarget);
            ToggleFloat(window);
        }

        private static bool CanStack(TilingNode node)
        {
            return !node.PathToRoot.OfType<StackPanelNode>().Any();
        }

        public bool CanStack()
        {
            // Win+Shift+F：焦点已在 stack 内可取消；否则可包裹焦点节点（单窗，非整屏）
            var focusedNode = GetFocusedTilingNode(ensureManaged: false);
            if (focusedNode != null)
            {
                if (focusedNode.PathToRoot.OfType<StackPanelNode>().Any())
                    return true;

                return CanStack(focusedNode);
            }

            var window = m_workspace.FocusedWindow;
            return window != null && CanManage(window, ignoreFloating: true);
        }

        /// <summary>
        /// 焦点窗是否位于 stack 内（Win+Shift+F 取消判定；不含「仅桌面处于 stack 模式但焦点不在 stack」）。
        /// </summary>
        public bool IsInStackLayout()
        {
            var focusedNode = GetFocusedTilingNode(ensureManaged: false);
            return focusedNode?.PathToRoot.OfType<StackPanelNode>().Any() == true;
        }

        /// <summary>
        /// Win+Shift+F：取当前激活窗口句柄，对该句柄做 stack 切换（见 StackWindow）。
        /// </summary>
        public void Stack()
        {
            var window = m_workspace.FocusedWindow
                ?? throw new TilingFailedException(TilingError.MissingTarget);
            StackWindow(window);
        }

        public void SetPanelStack()
        {
            if (m_dispatcher.CheckAccess())
            {
                SetPanelStackCore();
            }
            else
            {
                m_dispatcher.Invoke(SetPanelStackCore);
            }
        }

        public bool CanPullUp()
        {
            var focusedNode = GetFocusedTilingNode(ensureManaged: false);
            return focusedNode != null && focusedNode.Parent != focusedNode.Desktop!.Root;
        }

        public void PullUp()
        {
            var focusedNode = GetFocusedTilingNode(ensureManaged: true)
                ?? throw new TilingFailedException(TilingError.MissingTarget);

            using (m_backendLock.EnterScope())
            {
                MoveToParentPanel(focusedNode);
                m_backend.SetFocus(focusedNode);
            }
        }

        public async void ToggleDesktop()
        {
            if (m_active)
            {
                Stop();
                await Task.Delay(50);
                foreach (var window in m_workspace.GetCurrentDesktopSnapshot())
                {
                    try
                    {
                        if (window.CanMinimize)
                            window.SetState(WindowState.Minimized);
                    }
                    catch (Exception e) when (e is Win32Exception || e is InvalidWindowReferenceException)
                    {
                        // Ignore
                    }
                }
            }
            else
            {
                foreach (var window in m_workspace.GetCurrentDesktopSnapshot())
                {
                    try
                    {
                        if (window.CanMinimize)
                            window.SetState(WindowState.Restored);
                    }
                    catch (Exception e) when (e is Win32Exception || e is InvalidWindowReferenceException)
                    {
                        // Ignore
                    }
                }
                await Task.Delay(50);
                Start();
                Refresh();
            }
        }

        public void Dispose()
        {
            m_logger.Information("No longer managing display {Display}", m_display);

            m_active = false;
            m_subscriptions.Dispose();

            PlacementFailed = null;

            m_workspace.VirtualDesktopManager.DesktopAdded -= OnDesktopAdded;
            m_workspace.VirtualDesktopManager.DesktopRemoved -= OnDesktopRemoved;
            m_workspace.VirtualDesktopManager.CurrentDesktopChanged -= OnCurrentDesktopChanged;
            m_workspace.CursorLocationChanged -= OnCursorLocationChanged;

            m_workspace.WindowAdded -= OnWindowAdded;
            m_workspace.WindowRemoved -= OnWindowRemoved;

            m_display.ScalingChanged -= OnDisplayScalingChanged;

            // There is still the possibility that OnWindowAdded gets called, but hopefully that does not happen too often.
            using (m_windowSetLock.EnterScope())
            {
                foreach (var window in m_windowSet)
                {
                    UnbindEventHandlers(window);
                }
            }
        }

        public bool CanResize(PanelOrientation orientation, double displayPercentage)
        {
            var focusedNode = GetFocusedTilingNode(ensureManaged: false);
            if (focusedNode is not WindowNode focusedWindow)
                return false;

            using (m_backendLock.EnterScope())
            {

                var window = focusedWindow.WindowReference;
                var oldSize = window.Position;
                var display = m_workspace.DisplayManager.Displays.FirstOrDefault(x => x.WorkArea.Contains(window.Position.Center));
                if (display == null)
                    return false;

                var verticalDelta = (int)(display.WorkArea.Height * displayPercentage);
                var horizontalDelta = (int)(display.WorkArea.Width * displayPercentage);

                var grandparent = focusedNode.Ancestors
                        .Select(x => x as GridLikeNode)
                        .Where(x => x != null)
                        .FirstOrDefault(x => x!.CanResizeInOrientation(orientation));
                if (grandparent != null)
                {
                    double newSize;
                    switch (orientation)
                    {
                        case PanelOrientation.Horizontal:
                            newSize = oldSize.Width + horizontalDelta / 2;
                            return focusedNode.Parent!.GetMaxChildSize(focusedNode).X > newSize;
                        case PanelOrientation.Vertical:
                            newSize = oldSize.Height + verticalDelta / 2;
                            return focusedNode.Parent!.GetMaxChildSize(focusedNode).Y > newSize;
                        default:
                            throw new NotImplementedException();
                    }
                }

                return false;
            }
        }

        public void Resize(PanelOrientation orientation, double displayPercentage)
        {
            var focusedNode = GetFocusedTilingNode(ensureManaged: true);
            if (focusedNode is not WindowNode focusedWindow)
                throw new TilingFailedException(TilingError.MissingTarget);

            using (m_backendLock.EnterScope())
            {

                var window = focusedWindow.WindowReference;
                var oldSize = window.Position;
                var display = m_workspace.DisplayManager.Displays.FirstOrDefault(x => x.WorkArea.Contains(window.Position.Center)) ?? throw new TilingFailedException(TilingError.Failed);
                var verticalDelta = (int)(display.WorkArea.Height * displayPercentage);
                var horizontalDelta = (int)(display.WorkArea.Width * displayPercentage);
                var newSize = orientation switch
                {
                    PanelOrientation.Horizontal => new Rectangle(oldSize.Left - horizontalDelta / 2, oldSize.Top, oldSize.Right + horizontalDelta / 2, oldSize.Bottom),
                    PanelOrientation.Vertical => new Rectangle(oldSize.Left, oldSize.Top - verticalDelta / 2, oldSize.Right, oldSize.Bottom + verticalDelta / 2),
                    _ => throw new NotImplementedException(),
                };
                m_backend.ResizeWindow(window, newSize, oldSize);
            }
            InvalidateLayout();
        }

        public IWindow? GetFocus()
        {
            var focusedNode = GetFocusedTilingNode();
            if (focusedNode is not WindowNode focusedWindow)
                return null;

            return focusedWindow.WindowReference;
        }

        public Rectangle GetBounds()
        {
            return m_display.Bounds;
        }

        public IWindow? FindClosest(Point center)
        {
            static double Distance(Point point1, Point point2)
            {
                return Math.Pow(point1.X - point2.X, 2) + Math.Pow(point1.Y - point2.Y, 2);
            }

            using (m_backendLock.EnterScope())
            {
                var tree = m_backend.GetTree(m_workspace.VirtualDesktopManager.CurrentDesktop);
                var closestNode = tree!.Root!.Windows
                    .OrderBy(x => Distance(center, x.ComputedRectangle.Center))
                    .FirstOrDefault();

                if (closestNode != null)
                {
                    return closestNode.WindowReference;
                }
            }

            return null;
        }
    }
}
