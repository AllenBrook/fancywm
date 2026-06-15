using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using FancyWM.Utilities;

using WinMan;
using FancyWM.Layouts.Tiling;
using FancyWM.Layouts;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Reactive.Linq;
using System.Diagnostics;

namespace FancyWM
{
    internal partial class TilingService
    {
        private void RestoreOriginalLayout()
        {
            using (m_backendLock.EnterScope())
            {
                foreach (var desktop in m_workspace.VirtualDesktopManager.Desktops)
                {
                    try
                    {
                        var tree = m_backend.GetTree(desktop);
                        if (tree == null)
                            continue;

                        foreach (var window in tree.Root!.Windows)
                        {
                            var originalPosition = m_backend.GetOriginalPosition(window.WindowReference);
                            try
                            {
                                window.WindowReference.SetPosition(originalPosition);
                            }
                            catch (InvalidWindowReferenceException)
                            {
                                continue;
                            }
                            catch (InvalidOperationException) when (window.WindowReference.State != WindowState.Restored)
                            {
                                continue;
                            }
                        }
                    }
                    catch (KeyNotFoundException)
                    {
                        continue;
                    }
                    catch (InvalidOperationException e)
                    {
                        m_logger.Warning(e, "Exception thrown while restoring the original window layout!");
                    }
                }
            }
        }

        private TimeSpan m_lastUpdateLayout = TimeSpan.Zero;

        private void UpdateTree(DesktopTree tree)
        {
            tree.WorkArea = m_display.WorkArea;

            bool constraintsSatisfied = false;
            while (!constraintsSatisfied)
            {
                tree.Measure();
                try
                {
                    tree.Arrange();
                    constraintsSatisfied = true;
                }
                catch (UnsatisfiableFlexConstraintsException)
                {
                    var largestWindow = tree.Root!.Windows.OrderByDescending(x => x.GenerationID).First();
                    m_logger.Warning($"The arrange pass failed! Floating window {largestWindow.WindowReference.DebugString()} in an attempt to find a permissible arrangement!");
                    using (m_floatingSetLock.EnterScope())
                    {
                        m_floatingSet.Add(largestWindow.WindowReference);
                    }
                    DetectChanges(largestWindow.WindowReference);
                    PlacementFailed?.Invoke(this, new TilingFailedEventArgs(TilingError.NoValidPlacementExists, largestWindow.WindowReference));
                }
            }
        }

        private async Task UpdateLayoutAsync()
        {
            if (!Active)
                return;

            if (m_currentInteraction != UserInteraction.None && m_sw.Elapsed - m_lastUpdateLayout <= TimeSpan.FromSeconds(1.0 / m_display.RefreshRate))
            {
                return;
            }
            m_lastUpdateLayout = m_sw.Elapsed;

            IVirtualDesktop desktop = m_workspace.VirtualDesktopManager.CurrentDesktop;

            List<TilingNode> snapshot;
            IReadOnlyCollection<TilingNode> focusedPath;
            TilingNode? focusedNode;
            DesktopTree tree;

            using (m_backendLock.EnterScope())
            {
                try
                {
                    var treeOrNull = m_backend.GetTree(desktop);
                    if (treeOrNull == null)
                        return;
                    tree = treeOrNull;
                }
                catch (KeyNotFoundException)
                {
                    m_logger.Warning($"Current desktop {desktop} is not registered with backend, aborting...");
                    return;
                }

                UpdateTree(tree);

                snapshot = tree.Root!.Nodes.Skip(1).ToList();
                focusedNode = m_backend.GetFocus(desktop);
                focusedPath = (IReadOnlyCollection<TilingNode>?)focusedNode?.PathToRoot?.ToList() ?? [];
            }

            async ValueTask RepositionAsync()
            {
                try
                {
                    Freeze();
                    IList<WindowNode> snapshotWindows;
                    using (m_ignoreRepositionSetLock.EnterScope())
                    {
                        snapshotWindows = snapshot.OfType<WindowNode>().Where(x => !m_ignoreRepositionSet.Contains(x.WindowReference)).ToList();
                    }

                    bool useSmoothing = m_animateWindowMovement && m_currentInteraction != UserInteraction.Resizing;
                    await UpdateWindowPositionsAsync(snapshotWindows, useSmoothing);
                }
                finally
                {
                    Unfreeze();
                }
            }

            m_gui.FocusRectangle = GetFocusRectangle(focusedNode);

            var repositionTask = RepositionAsync();

            m_gui.UpdateOverlay(snapshot, focusedPath);
            m_gui.PreviewRectangle = GetPreviewRectangle();

            if (m_showPreviewFocus)
            {
                // TODO: Can we just use focusedNode here?
                var previewWindows = m_workspace.VirtualDesktopManager.Desktops
                    .Select(desktop => m_backend.GetFocus(desktop))
                    .OfType<WindowNode>()
                    .Select(x => x.WindowReference)
                    .ToHashSet();
                m_gui.PreviewWindows = previewWindows;
            }
            else
            {
                m_gui.PreviewWindows = EmptyWindowSet;
            }

            await repositionTask;
        }

        private async Task UpdateWindowPositionsAsync(IEnumerable<WindowNode> snapshot, bool useSmoothing)
        {
            var targets = CalculateRepositionTargets(snapshot);
            foreach (var target in targets)
            {
                if (target.OriginalPosition != target.ComputedPosition)
                {
                    m_logger.Information("Relocating window {Window} from {OriginalPosition} to {ComputedPosition}",
                        target.Window.DebugString(),
                        target.OriginalPosition, target.ComputedPosition);
                }
                else
                {
                    m_logger.Information("Window {Window} location is {ComputedPosition}",
                        target.Window.DebugString(),
                        target.ComputedPosition);
                }
            }

            HashSet<IWindow>? newWindows = null;
            using (m_newWindowSetLock.EnterScope())
            {
                if (m_newWindowSet.Count > 0)
                {
                    newWindows = [.. m_newWindowSet];
                    m_newWindowSet.Clear();
                }
            }

            if (useSmoothing)
            {
                var focusRectangle = m_gui.FocusRectangle;
                m_gui.FocusRectangle = null;

                TransitionTargetGroup transitionGroup;
                if (newWindows != null)
                {
                    await TransitionTargetGroup.PerformTransitionAsync(targets.Where(x => newWindows!.Contains(x.Window)).ToList());
                    transitionGroup = new TransitionTargetGroup(m_animationThread, targets.Where(x => !newWindows!.Contains(x.Window)));
                }
                else
                {
                    transitionGroup = new TransitionTargetGroup(m_animationThread, targets);
                }
                await transitionGroup.PerformSmoothTransitionAsync(TimeSpan.FromMilliseconds(100));

                m_gui.FocusRectangle = focusRectangle;
            }
            else
            {
                await TransitionTargetGroup.PerformTransitionAsync(targets);
            }
        }

        private List<TransitionTarget> CalculateRepositionTargets(IEnumerable<WindowNode> snapshot)
        {
            var targets = new List<TransitionTarget>();
            foreach (var window in snapshot)
            {
                try
                {
                    var currentPosition = window.WindowReference.Position;
                    if (!window.WindowReference.CanResize)
                    {
                        m_logger.Warning("Unresizable window {Window} will be moved only", window.WindowReference.DebugString());
                        var targetRect = ShrinkTo(window.ComputedRectangle, currentPosition.Width, currentPosition.Height);
                        if (targetRect == currentPosition)
                        {
                            continue;
                        }
                        targets.Add(new TransitionTarget(window.WindowReference, currentPosition, targetRect));
                    }
                    else
                    {
                        m_logger.Debug("Updating position of window {Window}", window.WindowReference.DebugString());
                        var rect = window.ComputedRectangle;
                        var frame = window.WindowReference.FrameMargins;
                        var adjustedRect = new Rectangle(
                            left: rect.Left - frame.Left,
                            top: rect.Top - frame.Top,
                            right: rect.Right + frame.Right,
                            bottom: rect.Bottom + frame.Bottom);

                        if (adjustedRect == currentPosition)
                        {
                            continue;
                        }

                        targets.Add(new TransitionTarget(window.WindowReference, currentPosition, adjustedRect));

                        var minSize = window.WindowReference.MinSize;
                        if (minSize.HasValue)
                        {
                            if (minSize.Value.X > adjustedRect.Width)
                            {
                                m_logger.Warning("New width for {Window} is smaller than the value reported by WM_GETMINMAXINFO ({ComputedWidth} < {MinimumWidth})",
                                    window.WindowReference.DebugString(), adjustedRect.Width, minSize.Value.X);
                            }
                            if (minSize.Value.Y > adjustedRect.Height)
                            {
                                m_logger.Warning("New height for {Window} is smaller than the value reported by WM_GETMINMAXINFO ({ComputedHeight} < {MinimumHeight})",
                                    window.WindowReference.DebugString(), adjustedRect.Height, minSize.Value.Y);
                            }
                        }
                    }
                }
                catch (InvalidWindowReferenceException)
                {
                    // Ignore
                }
                catch (Win32Exception e)
                {
                    m_logger.Error(e, "Failed to calculate reposition targets");
                }
            }
            return targets;
        }

        private bool CanShowFocusRectangle()
        {
            return m_showFocus && m_currentInteraction == UserInteraction.None && m_movingPanelNode == null;
        }

        private Rectangle? GetFocusRectangle(TilingNode? focusedNode)
        {
            if (focusedNode is WindowNode focusedWindow && CanShowFocusRectangle())
            {
                return focusedWindow.ComputedRectangle;
            }
            return null;
        }

        private Rectangle? GetPreviewRectangle()
        {
            if (m_currentInteraction == UserInteraction.Moving && m_delayReposition || m_movingPanelNode != null)
            {
                try
                {
                    var isSwapping = IsSwapModifierPressed();
                    var pt = m_workspace.CursorLocation;

                    if (m_movingPanelNode == null)
                    {
                        var window = m_workspace.FocusedWindow;
                        if (window == null)
                        {
                            return null;
                        }

                        using (m_backendLock.EnterScope())
                        {
                            if (m_backend.HasWindow(window))
                            {
                                return m_backend.MockMoveWindow(window, pt, allowNesting: !isSwapping).preArrange;
                            }
                        }
                    }
                    else
                    {
                        using (m_backendLock.EnterScope())
                        {
                            var rect = m_backend.MockMoveNode(m_movingPanelNode, pt, allowNesting: !isSwapping).preArrange;
                            var padding = GetPanelPaddingRect();
                            var spacing = GetPanelSpacing();
                            return new Rectangle(
                                rect.Left - padding.Left - spacing / 2,
                                rect.Top - padding.Top - spacing / 2,
                                rect.Right + padding.Right + spacing / 2,
                                rect.Bottom + padding.Bottom + spacing / 2);
                        }
                    }
                }
                catch (TilingFailedException)
                {
                }
                catch (InvalidWindowReferenceException)
                {
                }
            }
            return null;
        }

        private void MoveToParentPanel(TilingNode node)
        {
            try
            {
                using (m_backendLock.EnterScope())
                {
                    m_backend.PullUp(node);
                }
                InvalidateLayout();
            }
            catch (TilingFailedException e)
            {
                m_logger.Error(e, "Attempted pull up of {Node} failed", node);
                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(e.FailReason));
            }
        }

        private void WrapInSplitPanel(TilingNode node, bool vertical)
        {
            try
            {
                using (m_backendLock.EnterScope())
                {
                    m_backend.WrapInSplitPanel(node, vertical);
                    m_backend.SetFocus(node);

                    node.Parent!.Padding = GetPanelPaddingRect();
                    node.Parent!.Spacing = GetPanelSpacing();

                    if (m_allocateNewPanelSpace)
                    {
                        node.Parent!.Attach(new PlaceholderNode());
                    }

                    InvalidateLayout();
                }
            }
            catch (TilingFailedException ex)
            {
                m_logger.Error(ex, "Attempted split of {Node} failed", node);
                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(ex.FailReason));
            }
        }

        private void WrapInStackPanel(TilingNode node)
        {
            try
            {
                using (m_backendLock.EnterScope())
                {
                    ApplyStackLayout(m_workspace.VirtualDesktopManager.CurrentDesktop, node);
                }
            }
            catch (TilingFailedException ex)
            {
                m_logger.Error(ex, "Attempted stack of {Node} failed", node);
                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(ex.FailReason));
            }
        }

        private void ApplyStackLayout(IVirtualDesktop desktop, TilingNode focusedNode)
        {
            var tree = m_backend.GetTree(desktop);
            if (tree?.Root == null)
                throw new TilingFailedException(TilingError.MissingTarget);

            if (tree.Root.Windows.Count() > 1)
            {
                m_backend.StackAllWindows(desktop);
            }
            else
            {
                m_backend.WrapInStackPanel(focusedNode);
                focusedNode.Parent!.Padding = GetPanelPaddingRect();
                focusedNode.Parent!.Spacing = GetPanelSpacing();
            }

            foreach (var panel in tree.Root.Nodes.OfType<PanelNode>())
            {
                panel.Padding = GetPanelPaddingRect();
                panel.Spacing = GetPanelSpacing();
            }

            m_backend.SetFocus(focusedNode);
            InvalidateLayout();
        }

        private void UnstackLayout(StackPanelNode stack, TilingNode focusedNode)
        {
            var parent = stack.Parent;
            if (parent == null)
                throw new TilingFailedException(TilingError.InvalidTarget);

            var children = stack.Children.ToList();
            if (children.Count == 0)
                throw new TilingFailedException(TilingError.MissingTarget);

            var index = parent.IndexOf(stack);
            if (children.Count == 1)
            {
                var child = children[0];
                stack.Detach(child);
                parent.Attach(index, child);
                parent.Detach(stack);
            }
            else
            {
                var replacement = new SplitPanelNode
                {
                    Orientation = PanelOrientation.Horizontal,
                    Padding = GetPanelPaddingRect(),
                    Spacing = GetPanelSpacing(),
                };

                parent.Attach(index, replacement);
                foreach (var child in children)
                {
                    stack.Detach(child);
                    replacement.Attach(child);
                }
                parent.Detach(stack);
            }

            parent.Cleanup(collapse: m_backend.AutoCollapse);
            m_backend.SetFocus(focusedNode);
            InvalidateLayout();
        }

        private void SetPanelStackCore()
        {
            m_logger.Information("Setting panel stack for visible windows on display {Display}", m_display);
            DiscoverWindows();

            var desktop = m_workspace.VirtualDesktopManager.CurrentDesktop;
            var candidates = new HashSet<IWindow>();
            foreach (var window in m_workspace.GetCurrentDesktopSnapshot())
            {
                if (window.State != WindowState.Minimized)
                {
                    candidates.Add(window);
                }
            }

            using (m_windowSetLock.EnterScope())
            {
                foreach (var window in m_windowSet)
                {
                    if (window.State != WindowState.Minimized)
                    {
                        candidates.Add(window);
                    }
                }
            }

            var visibleWindows = candidates
                .Where(IsWindowOnThisDisplay)
                .Where(w => w.Position.Width > 0 && w.Position.Height > 0)
                .ToList();

            foreach (var window in visibleWindows)
            {
                if (AuxiliaryWindowRules.IsAuxiliaryApplicationWindow(window, visibleWindows))
                {
                    using (m_floatingSetLock.EnterScope())
                    {
                        m_floatingSet.Add(window);
                    }

                    continue;
                }

                if (!CanManage(window, ignoreFloating: true))
                {
                    continue;
                }

                if (window.State == WindowState.Maximized)
                {
                    try
                    {
                        window.SetState(WindowState.Restored);
                    }
                    catch (Exception ex) when (ex is Win32Exception or InvalidWindowReferenceException or InvalidOperationException)
                    {
                        m_logger.Debug(
                            "Could not restore {Window} before panel stack: {Message}",
                            window.DebugString(), ex.Message);
                        continue;
                    }
                }

                using (m_floatingSetLock.EnterScope())
                {
                    m_floatingSet.Remove(window);
                }
            }

            using (m_backendLock.EnterScope())
            {
                var tree = m_backend.GetTree(desktop);
                if (tree?.Root == null)
                {
                    return;
                }

                var stack = tree.Root.Children.OfType<StackPanelNode>().FirstOrDefault();
                if (stack == null)
                {
                    stack = new StackPanelNode();
                    tree.Root.Attach(stack);
                }

                foreach (var window in visibleWindows)
                {
                    if (AuxiliaryWindowRules.IsAuxiliaryApplicationWindow(window, visibleWindows))
                    {
                        continue;
                    }

                    if (!CanManage(window, ignoreFloating: true) || m_backend.HasWindow(window))
                    {
                        continue;
                    }

                    try
                    {
                        var node = m_backend.RegisterWindow(window, stack);
                        node.Parent!.Padding = GetPanelPaddingRect();
                        node.Parent!.Spacing = GetPanelSpacing();
                    }
                    catch (NoValidPlacementExistsException)
                    {
                        PlacementFailed?.Invoke(this, new TilingFailedEventArgs(
                            TilingError.NoValidPlacementExists, window));
                    }
                    catch (WindowAlreadyRegisteredException)
                    {
                    }
                    catch (InvalidWindowReferenceException)
                    {
                    }
                }

                if (!tree.Root.Windows.Any())
                {
                    return;
                }

                m_backend.StackAllWindows(desktop);

                if (tree.Root.Children.OfType<StackPanelNode>().FirstOrDefault() is StackPanelNode stackPanel)
                {
                    foreach (var windowNode in stackPanel.Windows.ToList())
                    {
                        if (!ShouldManageVisualBasicWindow(windowNode.WindowReference))
                        {
                            m_backend.UnregisterWindow(windowNode.WindowReference);
                        }
                    }
                }

                foreach (var panel in tree.Root.Nodes.OfType<PanelNode>())
                {
                    panel.Padding = GetPanelPaddingRect();
                    panel.Spacing = GetPanelSpacing();
                }
            }

            InvalidateLayout();
        }

        private int CountTiledManagedWindows()
        {
            using (m_windowSetLock.EnterScope())
            using (m_floatingSetLock.EnterScope())
            {
                var desktop = m_workspace.VirtualDesktopManager.CurrentDesktop;
                return m_windowSet.Count(w =>
                    CanManage(w)
                    && !m_floatingSet.Contains(w)
                    && desktop.HasWindow(w));
            }
        }

        private void SaveShowDesktopLayoutSnapshotCore(IVirtualDesktop desktop)
        {
            var tree = m_backend.GetTree(desktop);
            if (tree?.Root == null || !tree.Root.Windows.Any())
                return;

            m_showDesktopSnapshot = new ShowDesktopLayoutSnapshot
            {
                RootClone = (PanelNode)tree.Root.Clone(),
                WindowRects = tree.Root.Windows.ToDictionary(
                    w => w.WindowReference.Handle,
                    w => w.ComputedRectangle),
                FocusedWindowHandle = (m_backend.GetFocus(desktop) as WindowNode)?.WindowReference.Handle,
            };
        }

        private void RestoreShowDesktopLayoutSnapshot()
        {
            if (m_showDesktopSnapshot == null)
                return;

            try
            {
                using (m_backendLock.EnterScope())
                {
                    var desktop = m_workspace.VirtualDesktopManager.CurrentDesktop;
                    var tree = m_backend.GetTree(desktop);
                    if (tree == null)
                        return;

                    var snapshot = m_showDesktopSnapshot;
                    m_showDesktopSnapshot = null;

                    var validWindows = GetRestoredTiledWindows(desktop);
                    if (validWindows.Count == 0)
                        return;

                    tree.Root = snapshot.RootClone;

                    foreach (var windowNode in tree.Root!.Windows.ToList())
                    {
                        if (!validWindows.Contains(windowNode.WindowReference))
                            windowNode.Remove(cleanup: true, collapse: false);
                    }

                    foreach (var panel in tree.Root.Nodes.OfType<PanelNode>())
                    {
                        panel.Padding = GetPanelPaddingRect();
                        panel.Spacing = GetPanelSpacing();
                    }

                    try
                    {
                        tree.Measure();
                        tree.Arrange();
                    }
                    catch (UnsatisfiableFlexConstraintsException)
                    {
                    }

                    foreach (var windowNode in tree.Root.Windows)
                    {
                        if (!snapshot.WindowRects.TryGetValue(windowNode.WindowReference.Handle, out var rect))
                            continue;

                        if (windowNode.Parent is GridLikeNode gridNode)
                        {
                            if (gridNode.CanResizeInOrientation(PanelOrientation.Horizontal))
                                gridNode.ResizeTo(windowNode, rect.Width, GrowDirection.Both);
                            else
                                gridNode.ResizeTo(windowNode, rect.Height, GrowDirection.Both);
                        }
                    }

                    if (snapshot.FocusedWindowHandle is IntPtr focusedHandle)
                    {
                        var focusedWindow = validWindows.FirstOrDefault(w => w.Handle == focusedHandle);
                        if (focusedWindow != null)
                        {
                            var focusNode = tree.FindNode(focusedWindow);
                            if (focusNode != null)
                                m_backend.SetFocus(focusNode);
                        }
                    }

                    m_showDesktopAwaitingRestore = false;
                    InvalidateLayout();
                }
            }
            catch (Exception ex)
            {
                m_showDesktopAwaitingRestore = false;
                m_logger.Error(ex, "Failed to restore show-desktop layout snapshot");
            }
        }

        private bool ShouldDeferShowDesktopRestore(IWindow window)
            => m_showDesktopAwaitingRestore
                && m_showDesktopSnapshot?.WindowRects.ContainsKey(window.Handle) == true;

        private bool AreAllShowDesktopWindowsReady(IVirtualDesktop desktop)
        {
            if (m_showDesktopSnapshot == null)
                return false;

            var restoredHandles = GetRestoredTiledWindows(desktop).Select(w => w.Handle).ToHashSet();
            return m_showDesktopSnapshot.WindowRects.Keys.All(restoredHandles.Contains);
        }

        private HashSet<IWindow> GetRestoredTiledWindows(IVirtualDesktop desktop)
        {
            using (m_windowSetLock.EnterScope())
            using (m_floatingSetLock.EnterScope())
            {
                return m_windowSet
                    .Where(w => w.State == WindowState.Restored
                        && CanManage(w)
                        && !m_floatingSet.Contains(w)
                        && desktop.HasWindow(w))
                    .ToHashSet();
            }
        }

        private async void ScheduleRestoreShowDesktopLayout()
        {
            var generation = ++m_showDesktopRestoreGeneration;
            await Task.Delay(150);
            if (generation != m_showDesktopRestoreGeneration || !m_active)
                return;

            RestoreShowDesktopLayoutSnapshot();
        }

        private IntPtr GetOverlayAnchor()
        {
            var desktop = m_workspace.VirtualDesktopManager.CurrentDesktop;
            using (m_backendLock.EnterScope())
            {
                try
                {
                    var focusedNode = m_backend.GetFocus(desktop);
                    if (focusedNode is WindowNode window)
                        return window.WindowReference.Handle;
                }
                catch (ArgumentException)
                {
                    return new IntPtr(0);
                }
            }

            var comparer = m_workspace.CreateSnapshotZOrderComparer();
            using (m_backendLock.EnterScope())
            {
                var tree = m_backend.GetTree(desktop);
                if (tree == null)
                    return new IntPtr(0);
                var topWindow = tree.Root!.Windows
                    .OrderByDescending(x => x.WindowReference, comparer)
                    .FirstOrDefault();

                if (topWindow != null)
                    return topWindow.WindowReference.Handle;

                return new IntPtr(0);
            }
        }

        private void ToggleFloat(IWindow window)
        {
            bool floated;
            using (m_floatingSetLock.EnterScope())
            {
                if (m_floatingSet.Contains(window))
                {
                    floated = false;
                    m_floatingSet.Remove(window);
                }
                else
                {
                    floated = true;
                    m_floatingSet.Add(window);
                }
            }

            DetectChanges(window, manualRegistration: true);
            if (floated)
            {
                OnWindowFloated(window);
            }
            else
            {
                try
                {
                    using (m_backendLock.EnterScope())
                    {
                        m_backend.SetFocus(window);
                    }
                }
                catch
                {
                }
            }

            InvalidateLayout();
        }

        private void OnDisplayScalingChanged(object? sender, DisplayScalingChangedEventArgs e)
        {
            PropagatePanelHeightChange();
        }

        private void OnPlacementFailed(object? sender, TilingFailedEventArgs e)
        {
            if (e.FailReason == TilingError.NoValidPlacementExists && e.FailSource != null)
            {
                using (m_floatingSetLock.EnterScope())
                {
                    m_floatingSet.Add(e.FailSource);
                }
                OnWindowFloated(e.FailSource);
            }
        }

        private void OnWindowFloated(IWindow window)
        {
            Rectangle? originalPosition;
            try
            {
                using (m_backendLock.EnterScope())
                {
                    originalPosition = m_backend.GetOriginalPosition(window);
                }
            }
            catch
            {
                originalPosition = null;
            }
            try
            {
                originalPosition ??= GetOptimalRestoredSize(window);

                var originalDisplay = m_workspace.DisplayManager.Displays.FirstOrDefault(x => x.Bounds.Contains(originalPosition.Value.Center));
                originalDisplay ??= m_workspace.DisplayManager.PrimaryDisplay;

                var displayBounds = originalDisplay.Bounds;

                var centeredPosition = Rectangle.OffsetAndSize(
                    displayBounds.Left + displayBounds.Width / 2 - originalPosition.Value.Width / 2,
                    displayBounds.Top + displayBounds.Height / 2 - originalPosition.Value.Height / 2,
                    originalPosition.Value.Width,
                    originalPosition.Value.Height);

                window.SetPosition(centeredPosition);
                FocusHelper.ForceActivate(window.Handle);
            }
            catch (Exception e) when (e is InvalidWindowReferenceException || e is InvalidOperationException && window.State != WindowState.Restored)
            {
                // ignore
            }
        }

        private Rectangle GetOptimalRestoredSize(IWindow window)
        {
            var screenSize = m_display.WorkArea.Size;
            var minSize = window.MinSize ?? new Point(0, 0);
            var maxSize = window.MaxSize ?? new Point(screenSize.X, screenSize.Y);
            var pos = window.Position;

            return Rectangle.OffsetAndSize(
                pos.Left,
                pos.Top,
                Math.Max(minSize.X, Math.Min(maxSize.X, Math.Min(screenSize.X, (screenSize.X + minSize.X) / 2))),
                Math.Max(minSize.Y, Math.Min(maxSize.Y, Math.Min(screenSize.Y, (screenSize.Y + minSize.Y) / 2))));
        }


        private void OnCursorLocationChanged(object? sender, CursorLocationChangedEventArgs e)
        {
            if (PendingIntent == null)
                return;

            m_dispatcher.BeginInvoke(() =>
            {
                if (PendingIntent is GroupWithIntent gwi)
                {
                    if (Mouse.LeftButton != MouseButtonState.Pressed)
                    {
                        PendingIntent.Cancel();
                        PendingIntent = null;
                    }

                    using (m_backendLock.EnterScope())
                    {
                        if (m_backend.NodeAtPoint(m_workspace.VirtualDesktopManager.CurrentDesktop, e.NewLocation) is WindowNode targetNode)
                        {
                            var newSet = new HashSet<IWindow> { gwi.Source.WindowReference, targetNode.WindowReference };
                            if (!m_gui.PreviewWindows.SetEquals(newSet))
                            {
                                m_gui.PreviewWindows = newSet;
                            }
                        }
                    }
                }
            });
        }

        private void OnPendingIntentChanged(object? sender, EventArgs e)
        {
            if (PendingIntent == null)
            {
                _ = m_dispatcher.BeginInvoke(() =>
                {
                    m_gui.PreviewWindows = EmptyWindowSet;
                });
            }
            else
            {
                if (App.Current.Services.GetService<LowLevelMouseHook>() is LowLevelMouseHook mshk)
                {
                    var startPt = m_workspace.CursorLocation;
                    bool dispatched = false;
                    void onMouseButtonChanged(object? sender, ref LowLevelMouseHook.ButtonStateChangedEventArgs e)
                    {
                        mshk.ButtonStateChanged -= onMouseButtonChanged;
                        if (e.Button == LowLevelMouseHook.MouseButton.Left && e.IsPressed == false)
                        {
                            var pt = new Point(e.X, e.Y);
                            if (Math.Abs(pt.X - startPt.X) > 5 || Math.Abs(pt.Y - startPt.Y) > 5)
                            {
                                if (!dispatched)
                                {
                                    dispatched = true;
                                    m_dispatcher.BeginInvoke(() =>
                                    {
                                        HitTestCompletePendingIntent(pt);
                                    });
                                }
                            }
                        }
                        else
                        {
                            if (!dispatched)
                            {
                                dispatched = true;
                                m_dispatcher.BeginInvoke(() =>
                                {
                                    PendingIntent?.Cancel();
                                    PendingIntent = null;
                                });
                            }
                        }
                    }
                    mshk.ButtonStateChanged += onMouseButtonChanged;
                }
            }
        }

        private void HitTestCompletePendingIntent(Point cursorPosition)
        {
            if (m_pendingIntent is GroupWithIntent intent && m_display.Bounds.Contains(cursorPosition))
            {
                PendingIntent = null;

                WindowNode sourceNode;
                PanelNode panel;
                using (m_backendLock.EnterScope())
                {
                    var node = m_backend.NodeAtPoint(m_workspace.VirtualDesktopManager.CurrentDesktop, cursorPosition);
                    if (node is not WindowNode targetNode)
                    {
                        intent.Cancel();
                        return;
                    }
                    if (targetNode.WindowReference.Equals(intent.Source.WindowReference))
                    {
                        intent.Cancel();
                        return;
                    }

                    switch (intent.Type)
                    {
                        case GroupWithIntent.GroupType.HorizontalPanel:
                            if (!CanSplit(targetNode))
                            {
                                intent.Cancel();
                                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(TilingError.NestingInStackPanel, targetNode.WindowReference));
                                return;
                            }
                            break;
                        case GroupWithIntent.GroupType.VerticalPanel:
                            if (!CanSplit(targetNode))
                            {
                                intent.Cancel();
                                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(TilingError.NestingInStackPanel, targetNode.WindowReference));
                                return;
                            }
                            break;
                        case GroupWithIntent.GroupType.StackPanel:
                            if (!CanStack(targetNode))
                            {
                                intent.Cancel();
                                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(TilingError.NestingInStackPanel, targetNode.WindowReference));
                                return;
                            }
                            break;
                    }

                    // Must complete before doing anything with the intent data.
                    intent.Complete();
                    sourceNode = intent.Source;

                    switch (intent.Type)
                    {
                        case GroupWithIntent.GroupType.HorizontalPanel:
                            m_backend.WrapInSplitPanel(targetNode, vertical: false);
                            break;
                        case GroupWithIntent.GroupType.VerticalPanel:
                            m_backend.WrapInSplitPanel(targetNode, vertical: true);
                            break;
                        case GroupWithIntent.GroupType.StackPanel:
                            m_backend.WrapInStackPanel(targetNode);
                            break;
                    }
                    panel = targetNode.Parent!;
                    panel.Spacing = GetPanelSpacing();
                    panel.Padding = GetPanelPaddingRect();
                }


                BindEventHandlers(sourceNode.WindowReference);
                using (m_windowSetLock.EnterScope())
                {
                    m_windowSet.Add(sourceNode.WindowReference);
                }
                if (CanManage(sourceNode.WindowReference))
                {
                    //m_logger.Information("Window {Handle}={ProcessName} can be managed, registering with backend", e.Source.Handle, e.Source.GetCachedProcessName());
                    try
                    {
                        try
                        {
                            using (m_backendLock.EnterScope())
                            {
                                var node = m_backend.RegisterWindow(sourceNode.WindowReference, panel);
                                m_backend.SetFocus(node);
                            }
                        }
                        catch (NoValidPlacementExistsException)
                        {
                            PlacementFailed?.Invoke(this, new TilingFailedEventArgs(
                                TilingError.NoValidPlacementExists, sourceNode.WindowReference));
                        }
                    }
                    catch
                    {
                        return;
                    }

                    InvalidateLayout();
                }
            }
            else
            {
                m_pendingIntent?.Cancel();
            }
        }

        private void OnBeginHorizontalWithRequestedAsync(object? sender, WindowNode e)
        {
            m_gui.PreviewWindows = new HashSet<IWindow> { e.WindowReference };
            PendingIntent = new GroupWithIntent(GroupWithIntent.GroupType.HorizontalPanel, e,
                complete: () =>
                {
                    m_gui.PreviewWindows = EmptyWindowSet;
                    OnWindowRemoved(this, new WindowChangedEventArgs(e.WindowReference));
                },
                cancel: () =>
                {
                    m_gui.PreviewWindows = EmptyWindowSet;
                });
        }

        private void OnBeginVerticalWithRequested(object? sender, WindowNode e)
        {
            m_gui.PreviewWindows = new HashSet<IWindow> { e.WindowReference };
            PendingIntent = new GroupWithIntent(GroupWithIntent.GroupType.VerticalPanel, e,
                complete: () =>
                {
                    m_gui.PreviewWindows = EmptyWindowSet;
                    OnWindowRemoved(this, new WindowChangedEventArgs(e.WindowReference));
                },
                cancel: () =>
                {
                    m_gui.PreviewWindows = EmptyWindowSet;
                });
        }

        private void OnBeginStackWithRequested(object? sender, WindowNode e)
        {
            m_gui.PreviewWindows = new HashSet<IWindow> { e.WindowReference };
            PendingIntent = new GroupWithIntent(GroupWithIntent.GroupType.StackPanel, e,
                complete: () =>
                {
                    m_gui.PreviewWindows = EmptyWindowSet;
                    OnWindowRemoved(this, new WindowChangedEventArgs(e.WindowReference));
                },
                cancel: () =>
                {
                    m_gui.PreviewWindows = EmptyWindowSet;
                });
        }

        private void OnWindowVerticalSplitRequested(object? sender, TilingNode e)
        {
            WrapInSplitPanel(e, true);
        }

        private void OnWindowStackRequested(object? sender, TilingNode e)
        {
            WrapInStackPanel(e);
        }

        private void OnWindowPullUpRequested(object? sender, TilingNode e)
        {
            MoveToParentPanel(e);
        }

        private void OnWindowHorizontalSplitRequested(object? sender, TilingNode e)
        {
            WrapInSplitPanel(e, false);
        }

        private void OnWindowFloatRequested(object? sender, WindowNode e)
        {
            ToggleFloat(e.WindowReference);
        }

        private void OnWindowIgnoreProcessRequested(object? sender, WindowNode e)
        {
            var processName = e.WindowReference.GetCachedProcessName();
            var instanceKey = ProcessInstanceRule.Format(processName, e.WindowReference.GetCachedProcessId());
            App.Current.AppState.Settings.SaveAsync(x =>
            {
                if (x.ProcessInstanceIncludeList.Contains(instanceKey))
                {
                    return x;
                }

                return x with { ProcessInstanceIncludeList = [.. x.ProcessInstanceIncludeList, instanceKey] };
            });
        }
        private void OnWindowIgnoreClassRequested(object? sender, WindowNode e)
        {
            SaveClassIncludeRule(e.WindowReference);
        }

        private void SaveClassIncludeRule(IWindow window)
        {
            var className = ((WinMan.Windows.Win32Window)window).ClassName;
            App.Current.AppState.Settings.SaveAsync(x =>
            {
                if (x.ClassIncludeList.Contains(className))
                    return x;
                return x with { ClassIncludeList = [.. x.ClassIncludeList, className] };
            });
        }

        private void OnTilingPanelMoving(object? sender, PanelNode panel)
        {
            m_currentInteraction = UserInteraction.Moving;
            m_movingPanelNode = panel;
            InvalidateLayout();
        }

        private void OnTilingPanelMoveRequested(object? sender, PanelNode panel)
        {
            m_logger.Information("Panel {Panel} move ended", panel);
            m_currentInteraction = UserInteraction.None;
            m_movingPanelNode = null;

            try
            {
                var isSwapping = IsSwapModifierPressed();
                var pt = m_workspace.CursorLocation;
                using (m_backendLock.EnterScope())
                {
                    // Check that panel hasn't disappeared during the move.
                    if (panel.Desktop == null)
                    {
                        return;
                    }
                    m_backend.MoveNode(panel, pt, allowNesting: !isSwapping);
                }

                InvalidateLayout();
            }
            catch (InvalidWindowReferenceException)
            {
                return;
            }
            catch (TilingFailedException e)
            {
                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(e.FailReason));
            }
        }

        private void OnStackTabReorderRequested(object? sender, Controls.StackTabReorderRoutedEventArgs e)
        {
            using (m_backendLock.EnterScope())
            {
                var childCount = e.Stack.Children.Count;
                if (e.FromIndex < 0
                    || e.FromIndex >= childCount
                    || childCount <= 1)
                {
                    return;
                }

                var toIndex = Math.Clamp(e.ToIndex, 0, childCount - 1);
                if (e.FromIndex != toIndex)
                {
                    e.Stack.Move(e.FromIndex, toIndex);
                }
            }

            InvalidateLayout();
        }

        private void OnTilingNodePullUpRequested(object? sender, TilingNode node)
        {
            MoveToParentPanel(node);
        }

        private void OnDesktopAdded(object? sender, DesktopChangedEventArgs e)
        {
            m_logger.Information("Desktop {Desktop} added to workspace", e.Source);
            var orientation = m_display.Bounds.Width >= m_display.Bounds.Height ? PanelOrientation.Horizontal : PanelOrientation.Vertical;
            using (m_backendLock.EnterScope())
            {
                m_backend.RegisterDesktop(e.Source, m_display.WorkArea, orientation);
            }
        }

        private void OnDesktopRemoved(object? sender, DesktopChangedEventArgs e)
        {
            m_logger.Information("Desktop {Desktop} removed from workspace", e.Source);
            using (m_backendLock.EnterScope())
            {
                m_backend.UnregisterDesktop(e.Source);
            }
        }

        private void OnCurrentDesktopChanged(object? sender, CurrentDesktopChangedEventArgs e)
        {
            Refresh();
            InvalidateLayout();
        }

        private void OnWindowGotFocus(object? sender, WindowFocusChangedEventArgs e)
        {
            m_dispatcher.BeginInvoke(() =>
            {
                m_logger.Information("Got focus on {Window}", e.Source.DebugString());
                try
                {
                    bool hideMaximised = false;
                    using (m_backendLock.EnterScope())
                    {
                        if (m_backend.HasWindow(e.Source))
                        {
                            m_logger.Debug("Window {Window} is managed by backend, need to hide all obstructing windows", e.Source.DebugString());
                            // Focused restored windows that are in the tree cause all maximised windows
                            // to be send to the back
                            hideMaximised = true;
                            m_backend.SetFocus(e.Source);
                        }
                        else
                        {
                            m_logger.Debug("Window {Window} is not managed by backend", e.Source.DebugString());
                            return;
                        }
                    }

                    if (hideMaximised)
                    {
                        m_logger.Debug("Moving all obstructing maximised windows to back");
                        var comparer = m_workspace.CreateSnapshotZOrderComparer();
                        foreach (var maximisedWindow in m_workspace.GetCurrentDesktopSnapshot()
                            .Where(x => x.State == WindowState.Maximized && m_display.Bounds.Contains(x.Position.Center))
                            .OrderBy(x => x, comparer))
                        {
                            m_logger.Information("Moving maximised window {Window} to back", maximisedWindow.DebugString());
                            try
                            {
                                if (maximisedWindow.CanReorder)
                                {
                                    maximisedWindow.SendToBack();
                                }
                            }
                            catch (InvalidWindowReferenceException)
                            {
                                continue;
                            }
                            catch (Win32Exception ex)
                            {
                                m_logger.Error(ex, "Moving window {Window} to back failed ({@Metadata})", maximisedWindow.DebugString(), maximisedWindow.GetMetadata());
                                continue;
                            }
                        }
                    }
                    InvalidateLayout();
                }
                catch (InvalidWindowReferenceException)
                {
                    return;
                }
            }, System.Windows.Threading.DispatcherPriority.DataBind);
        }

        private void OnWindowLostFocus(object? sender, WindowFocusChangedEventArgs e)
        {
            // This delay is needed to handle the case where the previously focused window
            // loses focus because another window was just created and the OnWindowAdded event
            // observes the new window as focused.
            //m_logger.Information("Lost focus on {Handle}={ProcessName}", e.Source.Handle, e.Source.GetCachedProcessName());
            //await Task.Delay(250);

            //SilenceExceptionIfDead(() =>
            //{
            //    using (m_backendLock.EnterScope())
            //    {
            //        if (m_backend.HasWindow(e.Source))
            //        {
            //            m_logger.Information("Removing focus from {Handle}={ProcessName}", e.Source.Handle, e.Source.GetCachedProcessName());
            //            m_backend.UnsetFocus(e.Source);
            //            InvalidateLayout();
            //        }
            //    }
            //});
            m_currentInteraction = UserInteraction.None;
        }

        private void OnWindowAdded(object? sender, WindowChangedEventArgs e)
        {
            m_logger.Debug("Window {Window} added to workspace", e.Source.DebugString());
            try
            {
                BindEventHandlers(e.Source);
                using (m_windowSetLock.EnterScope())
                {
                    m_windowSet.Add(e.Source);
                }
                using (m_newWindowSetLock.EnterScope())
                {
                    m_newWindowSet.Add(e.Source);
                }

                using (m_floatingSetLock.EnterScope())
                {
                    m_floatingSet.Add(e.Source);
                }

                if (ShouldAutoTile(e.Source) && !ShouldKeepAuxiliaryFloating(e.Source))
                {
                    using (m_floatingSetLock.EnterScope())
                    {
                        m_floatingSet.Remove(e.Source);
                    }
                }

                if (!AutoRegisterWindows || m_showDesktopAwaitingRestore)
                {
                    return;
                }

                if (CanManage(e.Source) && ShouldAutoTile(e.Source) && e.Source.State == WindowState.Restored)
                {
                    m_logger.Information("Window {Window} can be managed, registering with backend ({Display})", e.Source.DebugString(), m_display);
                    m_dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            try
                            {
                                using (m_backendLock.EnterScope())
                                {
                                    if (m_showDesktopAwaitingRestore || m_backend.HasWindow(e.Source))
                                    {
                                        return;
                                    }

                                    if (!TryRegisterAutoTiledWindow(e.Source, out _))
                                    {
                                        return;
                                    }
                                }
                            }
                            catch (NoValidPlacementExistsException)
                            {
                                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(
                                    TilingError.NoValidPlacementExists, e.Source));
                            }
                        }
                        catch
                        {
                            return;
                        }

                        InvalidateLayout();
                    }, System.Windows.Threading.DispatcherPriority.DataBind);
                }
            }
            catch (InvalidWindowReferenceException)
            {
                return;
            }
        }

        private void OnWindowRemoved(object? sender, WindowChangedEventArgs e)
        {
            m_logger.Information("Window {Window} removed from workspace", e.Source.DebugString());

            UnbindEventHandlers(e.Source);
            using (m_savedLocationsLock.EnterScope())
            {
                m_savedLocations.Remove(e.Source);
            }
            lock (s_crossDisplayStackTransfersLock)
            {
                s_crossDisplayStackTransfers.Remove(e.Source);
            }
            using (m_ignoreRepositionSetLock.EnterScope())
            {
                m_ignoreRepositionSet.Remove(e.Source);
            }
            using (m_backendLock.EnterScope())
            {
                if (m_backend.HasWindow(e.Source))
                {
                    m_logger.Debug("Unregistering window {Window} from backend", e.Source.DebugString());
                    m_backend.UnregisterWindow(e.Source);
                    InvalidateLayout();
                }
            }
            using (m_floatingSetLock.EnterScope())
            {
                m_floatingSet.Remove(e.Source);
            }
            using (m_newWindowSetLock.EnterScope())
            {
                m_newWindowSet.Remove(e.Source);
            }
            using (m_windowSetLock.EnterScope())
            {
                m_windowSet.Remove(e.Source);
            }
        }

        private void DoWindowMove(IWindow window)
        {
            var isSwapping = IsSwapModifierPressed();
            var pt = m_workspace.CursorLocation;
            using (m_backendLock.EnterScope())
            {
                if (m_backend.HasWindow(window))
                {
                    m_logger.Debug("Window {Window} size is unchanged, attempting to insert window at {Position}", window.DebugString(), pt);
                    m_backend.MoveWindow(window, pt, allowNesting: !isSwapping);
                    m_backend.SetFocus(window);
                }
            }
        }

        private void OnWindowPositionChangeEnd(object? sender, WindowPositionChangedEventArgs e)
        {
            if (!m_active)
                return;

            if (m_delayReposition && m_currentInteraction == UserInteraction.Moving && IsWindowOnThisDisplay(e.Source))
            {
                try
                {
                    DoWindowMove(e.Source);
                }
                catch (InvalidWindowReferenceException)
                {
                }
                catch (TilingFailedException ex)
                {
                    PlacementFailed?.Invoke(this, new TilingFailedEventArgs(ex.FailReason, e.Source));
                }
            }

            m_logger.Information("Window {Window} move ended", e.Source.DebugString());

            try
            {
                if (IsWindowOnThisDisplay(e.Source))
                {
                    TryAcceptCrossDisplayStackWindow(e.Source);
                }
                else
                {
                    FinalizeCrossDisplayLeave(e.Source);
                }
            }
            catch (InvalidWindowReferenceException)
            {
            }

            InvalidateLayout();
            using (m_ignoreRepositionSetLock.EnterScope())
            {
                m_ignoreRepositionSet.Remove(e.Source);
            }
            m_currentInteraction = UserInteraction.None;
        }

        private TimeSpan m_lastPlacementFailed = TimeSpan.Zero;
        private TimeSpan m_lastWindowPositionChanged = TimeSpan.Zero;

        private void OnWindowPositionChanged(object? sender, WindowPositionChangedEventArgs e)
        {
            if (!m_active)
                return;

            if (m_sw.Elapsed - m_lastPlacementFailed <= TimeSpan.FromMilliseconds(100))
            {
                return;
            }

            if (m_currentInteraction != UserInteraction.None && m_sw.Elapsed - m_lastWindowPositionChanged <= TimeSpan.FromSeconds(1.0 / m_display.RefreshRate))
            {
                return;
            }
            m_lastWindowPositionChanged = m_sw.Elapsed;

            try
            {
                MaybeRememberCrossDisplayStackTransfer(e.Source);
            }
            catch (InvalidWindowReferenceException)
            {
                return;
            }

            using (m_ignoreRepositionSetLock.EnterScope())
            {
                if (!m_ignoreRepositionSet.Contains(e.Source))
                {
                    // Some other event might have resulted in the movement of the window.
                    // Do not call DetectChanges under the lock, to avoid deadlock.
                    m_dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            MaybeRememberCrossDisplayStackTransfer(e.Source);
                            DetectChanges(e.Source, manualRegistration: HasCrossDisplayStackTransfer(e.Source));
                        }
                        catch (InvalidWindowReferenceException)
                        {
                        }
                    });
                    return;
                }
            }

            using (m_backendLock.EnterScope())
            {
                if (!m_backend.HasWindow(e.Source))
                {
                    if (IsWindowOnThisDisplay(e.Source))
                    {
                        TryAcceptCrossDisplayStackWindow(e.Source);
                    }

                    return;
                }
            }

            if (m_currentInteraction == UserInteraction.Starting)
            {
                if (e.OldPosition.Size == e.NewPosition.Size)
                {
                    m_currentInteraction = UserInteraction.Moving;
                }
                else
                {
                    m_currentInteraction = UserInteraction.Resizing;
                }
            }

            try
            {
                DetectChanges(e.Source);

                if (e.NewPosition == e.OldPosition)
                {
                    return;
                }

                if (e.NewPosition.Width == e.OldPosition.Width && e.NewPosition.Height == e.OldPosition.Height)
                {
                    if (!m_delayReposition)
                    {
                        DoWindowMove(e.Source);
                    }
                }
                else
                {
                    using (m_backendLock.EnterScope())
                    {
                        if (m_backend.HasWindow(e.Source))
                        {
                            var node = m_backend.FindWindow(e.Source);
                            var oldPosition = node!.ComputedContentRectangle;
                            var frame = e.Source.FrameMargins;
                            var adjustedRect = new Rectangle(
                                left: oldPosition.Left - frame.Left,
                                top: oldPosition.Top - frame.Top,
                                right: oldPosition.Right + frame.Right,
                                bottom: oldPosition.Bottom + frame.Bottom);

                            m_logger.Debug("Window {Window} size is different, attempting to resize window from {OldPosition} to {NewPosition}", e.Source.DebugString(), adjustedRect, e.NewPosition);
                            m_backend.ResizeWindow(e.Source, e.NewPosition, adjustedRect);
                            UpdateTree(node.Desktop!);
                        }
                    }
                }
                InvalidateLayout();
            }
            catch (InvalidWindowReferenceException)
            {
                return;
            }
            catch (TilingFailedException ex)
            {
                if (m_sw.Elapsed - m_lastPlacementFailed <= TimeSpan.FromSeconds(1))
                {
                    return;
                }
                m_lastPlacementFailed = m_sw.Elapsed;
                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(ex.FailReason, e.Source));
            }
            finally
            {
                Unfreeze();
            }
        }

        private void OnWindowTopmostChanged(object? sender, WindowTopmostChangedEventArgs e)
        {
            if (!m_active)
                return;

            try
            {
                m_logger.Verbose("Changed topmost of window {Window}", e.Source.DebugString());
                DetectChanges(e.Source);
            }
            catch (InvalidWindowReferenceException)
            {
                return;
            }
        }

        private void OnWindowStateChanged(object? sender, WindowStateChangedEventArgs e)
        {
            if (!m_active)
                return;

            void UnregisterAndSaveLocation()
            {
                using (m_backendLock.EnterScope())
                {
                    var window = m_backend.FindWindow(e.Source);
                    if (window != null)
                    {
                        bool isFirstSaved;
                        using (m_savedLocationsLock.EnterScope())
                        {
                            // Be resilient to multiple OnWindowStateChanged events happening one after the other
                            isFirstSaved = m_savedLocations.Count == 0;
                            m_savedLocations[e.Source] = new NodeLocation(window);

                            var tiledCount = CountTiledManagedWindows();
                            if (tiledCount > 0 && m_savedLocations.Count >= tiledCount)
                                m_showDesktopAwaitingRestore = true;
                        }

                        if (isFirstSaved)
                            SaveShowDesktopLayoutSnapshotCore(m_workspace.VirtualDesktopManager.CurrentDesktop);

                        InvalidateLayout();
                        m_backend.UnregisterWindow(e.Source);
                    }
                }
                DetectChanges(e.Source);
            }

            void RegisterAndRestoreLocation()
            {
                if (ShouldDeferShowDesktopRestore(e.Source))
                {
                    using (m_savedLocationsLock.EnterScope())
                    {
                        m_savedLocations.Remove(e.Source);
                    }

                    if (AreAllShowDesktopWindowsReady(m_workspace.VirtualDesktopManager.CurrentDesktop))
                        ScheduleRestoreShowDesktopLayout();

                    return;
                }

                NodeLocation? savedLocation;
                if (!m_showDesktopAwaitingRestore)
                    m_showDesktopSnapshot = null;

                using (m_savedLocationsLock.EnterScope())
                {
                    if (m_savedLocations.TryGetValue(e.Source, out savedLocation))
                    {
                        m_savedLocations.Remove(e.Source);
                    }
                }

                void RegisterInTopLevelPanel()
                {
                    try
                    {
                        if (!TryRegisterAutoTiledWindow(e.Source, out _))
                        {
                            return;
                        }
                    }
                    catch (WindowAlreadyRegisteredException)
                    {
                        // Window might be already registered!
                        var registered = m_backend.FindWindow(e.Source);
                        if (registered == null)
                        {
                            throw;
                        }
                        // This is clearly a race condition with DetectChanges dirty checking.
                    }
                }

                void RegisterInSavedPanel()
                {
                    WindowNode window;
                    try
                    {
                        window = m_backend.RegisterWindow(e.Source, savedLocation.Parent);
                        window.Parent!.Padding = GetPanelPaddingRect();
                        window.Parent!.Spacing = GetPanelSpacing();
                    }
                    catch (WindowAlreadyRegisteredException)
                    {
                        // Window might be already registered!
                        var registered = m_backend.FindWindow(e.Source);
                        if (registered == null)
                        {
                            throw;
                        }
                        // This is clearly a race condition with DetectChanges dirty checking.
                        window = registered;
                    }

                    window.Parent!.Detach(window);
                    int childCount = savedLocation.Parent.Children.Count;
                    int index = m_stackAppendRestoredTabsToEnd && savedLocation.Parent is StackPanelNode
                        ? childCount
                        : Math.Min(savedLocation.Index, childCount);
                    savedLocation.Parent.Attach(index, window);

                    RepairRootStackLayout(m_workspace.VirtualDesktopManager.CurrentDesktop);

                    // Restore size
                    if (window.Parent is GridLikeNode gridNode)
                    {
                        if (m_backend.GetTree(m_workspace.VirtualDesktopManager.CurrentDesktop) is DesktopTree tree)
                        {
                            // Assign ComputedRectangle to that Resize will work.
                            try
                            {
                                tree.Measure();
                                tree.Arrange();
                            }
                            catch (UnsatisfiableFlexConstraintsException)
                            {
                            }
                            if (gridNode.CanResizeInOrientation(PanelOrientation.Horizontal))
                            {
                                gridNode.ResizeTo(window, savedLocation.ComputedRectangle.Width, GrowDirection.Both);
                            }
                            else
                            {
                                gridNode.ResizeTo(window, savedLocation.ComputedRectangle.Height, GrowDirection.Both);
                            }
                        }
                    }
                }

                try
                {
                    using (m_backendLock.EnterScope())
                    {
                        if (savedLocation?.Parent?.Desktop != null)
                        {
                            try
                            {
                                RegisterInSavedPanel();
                            }
                            catch (NoValidPlacementExistsException)
                            {
                                RegisterInTopLevelPanel();
                            }
                        }
                        else
                        {
                            RegisterInTopLevelPanel();
                        }
                    }
                }
                catch (NoValidPlacementExistsException)
                {
                    PlacementFailed?.Invoke(this, new TilingFailedEventArgs(
                        TilingError.NoValidPlacementExists, e.Source));
                }
                DetectChanges(e.Source);
            }

            try
            {
                m_logger.Information("Changed state of window {Window} to {NewState}", e.Source.DebugString(), e.NewState);

                try
                {
                    // Window is now minimized or maximized but was restored
                    if ((e.NewState == WindowState.Maximized || e.NewState == WindowState.Minimized)
                        && e.OldState == WindowState.Restored)
                    {
                        UnregisterAndSaveLocation();
                    }
                    // Window is now restored
                    else if (e.NewState == WindowState.Restored
                        && (e.OldState == WindowState.Maximized || e.OldState == WindowState.Minimized))
                    {
                        if (!CanManage(e.Source))
                        {
                            return;
                        }
                        RegisterAndRestoreLocation();
                    }
                    else
                    {
                        DetectChanges(e.Source);
                    }
                }
                catch (InvalidWindowReferenceException)
                {
                    return;
                }
                catch (WindowAlreadyRegisteredException)
                {
                    return;
                }

                if (Equals(m_workspace.FocusedWindow, sender))
                {
                    m_logger.Debug("Window {Window} is also focused, calling OnWindowGotFocus", e.Source.DebugString());
                    // This is to update focus when a maximised window is restored.
                    OnWindowGotFocus(e.Source, new WindowFocusChangedEventArgs(e.Source, true));
                }
            }
            catch (InvalidWindowReferenceException)
            {
                return;
            }
        }

        private void OnWindowPositionChangeStart(object? sender, WindowPositionChangedEventArgs e)
        {
            if (!m_active)
                return;

            using (m_ignoreRepositionSetLock.EnterScope())
            {
                m_ignoreRepositionSet.Add(e.Source);
            }
            m_currentInteraction = UserInteraction.Starting;
        }

        private void OnTilingNodeFocusRequested(object? sender, TilingNode e)
        {
            using (m_backendLock.EnterScope())
            {
                var windowNode = e.Windows.FirstOrDefault();
                try
                {
                    if (windowNode != null)
                    {
                        if (FocusHelper.ForceActivate(windowNode.WindowReference.Handle))
                        {
                            m_backend.SetFocus(windowNode.WindowReference);
                        }
                    }
                }
                catch (InvalidWindowReferenceException)
                {
                    return;
                }
            }
        }

        private void OnTilingNodeCloseRequested(object? sender, TilingNode e)
        {
            foreach (var window in e.Windows.ToList())
            {
                try
                {
                    if (window.WindowReference.CanClose)
                    {
                        window.WindowReference.Close();
                    }
                }
                catch (InvalidWindowReferenceException)
                {
                    // Ignore
                }
                catch (Win32Exception)
                {
                    // Ignore
                    // TODO: Show toast
                }
            }
        }

        private void BindEventHandlers(IWindow window)
        {
            window.StateChanged += OnWindowStateChanged;
            window.PositionChangeStart += OnWindowPositionChangeStart;
            window.PositionChangeEnd += OnWindowPositionChangeEnd;
            window.PositionChanged += OnWindowPositionChanged;
            window.GotFocus += OnWindowGotFocus;
            window.LostFocus += OnWindowLostFocus;
            window.TopmostChanged += OnWindowTopmostChanged;
        }

        private void UnbindEventHandlers(IWindow window)
        {
            window.StateChanged -= OnWindowStateChanged;
            window.PositionChangeStart -= OnWindowPositionChangeStart;
            window.PositionChangeEnd -= OnWindowPositionChangeEnd;
            window.PositionChanged -= OnWindowPositionChanged;
            window.GotFocus -= OnWindowGotFocus;
            window.LostFocus -= OnWindowLostFocus;
            window.TopmostChanged -= OnWindowTopmostChanged;
        }

        private bool IsSwapModifierPressed()
        {
            static bool GetState() => Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            if (m_dispatcher.CheckAccess())
            {
                return GetState();
            }
            else
            {
                return m_dispatcher.Invoke(GetState, System.Windows.Threading.DispatcherPriority.Send);
            }
        }

        private bool ShouldKeepAuxiliaryFloating(IWindow window)
        {
            return AuxiliaryWindowRules.IsAuxiliaryApplicationWindow(window, GetSameProcessPeersOnDisplay(window));
        }

        private IReadOnlyList<IWindow> GetSameProcessPeersOnDisplay(IWindow window)
        {
            int processId;
            try
            {
                processId = window.GetCachedProcessId();
            }
            catch (InvalidWindowReferenceException)
            {
                return [];
            }

            using (m_windowSetLock.EnterScope())
            {
                return m_windowSet
                    .Where(w => w != window && IsWindowOnThisDisplay(w))
                    .Where(w =>
                    {
                        try
                        {
                            return w.GetCachedProcessId() == processId;
                        }
                        catch (InvalidWindowReferenceException)
                        {
                            return false;
                        }
                    })
                    .ToList();
            }
        }

        private bool ShouldFloatNewWindowInStackMode(IWindow window, IVirtualDesktop desktop)
        {
            if (!m_backend.IsStackModeActive(desktop))
            {
                return false;
            }

            if (ShouldKeepAuxiliaryFloating(window))
            {
                return true;
            }

            bool isNewWindow;
            using (m_newWindowSetLock.EnterScope())
            {
                isNewWindow = m_newWindowSet.Contains(window);
            }

            if (!isNewWindow)
            {
                return false;
            }

            try
            {
                var processId = window.GetCachedProcessId();

                using (m_windowSetLock.EnterScope())
                using (m_floatingSetLock.EnterScope())
                using (m_savedLocationsLock.EnterScope())
                {
                    foreach (var other in m_windowSet)
                    {
                        if (other == window || !IsWindowOnThisDisplay(other))
                        {
                            continue;
                        }

                        try
                        {
                            if (other.GetCachedProcessId() != processId)
                            {
                                continue;
                            }
                        }
                        catch (InvalidWindowReferenceException)
                        {
                            continue;
                        }

                        if (m_floatingSet.Contains(other))
                        {
                            continue;
                        }

                        if (m_savedLocations.TryGetValue(other, out var saved)
                            && saved.Parent is StackPanelNode)
                        {
                            return false;
                        }
                    }
                }

                var tree = m_backend.GetTree(desktop);
                if (tree?.Root != null)
                {
                    var processInStack = tree.Root.Nodes.OfType<StackPanelNode>()
                        .SelectMany(stack => stack.Windows)
                        .Any(stackWindow =>
                        {
                            try
                            {
                                return stackWindow.WindowReference.GetCachedProcessId() == processId;
                            }
                            catch (InvalidWindowReferenceException)
                            {
                                return false;
                            }
                        });

                    if (processInStack)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (InvalidWindowReferenceException)
            {
                return true;
            }
        }

        private void RepairRootStackLayout(IVirtualDesktop desktop)
        {
            if (!m_backend.IsStackModeActive(desktop))
            {
                return;
            }

            var root = m_backend.GetTree(desktop)?.Root;
            if (root == null)
            {
                return;
            }

            var hasRootOrphans = root.Children.Any(child => child is not StackPanelNode);
            if (hasRootOrphans || root.Windows.Any())
            {
                m_backend.StackAllWindows(desktop);
            }

            foreach (var panel in root.Nodes.OfType<PanelNode>())
            {
                panel.Padding = GetPanelPaddingRect();
                panel.Spacing = GetPanelSpacing();
            }
        }

        private void MaybeRememberCrossDisplayStackTransfer(IWindow window)
        {
            if (IsWindowOnThisDisplay(window))
            {
                return;
            }

            using (m_backendLock.EnterScope())
            {
                if (!m_backend.HasWindow(window))
                {
                    return;
                }

                var node = m_backend.FindWindow(window);
                if (node == null || !node.PathToRoot.OfType<StackPanelNode>().Any())
                {
                    return;
                }
            }

            RememberCrossDisplayStackTransfer(window);
        }

        private bool TryRegisterAutoTiledWindow(IWindow window, out WindowNode? node, int maxTreeWidth = 100)
        {
            node = null;
            var desktop = m_workspace.VirtualDesktopManager.CurrentDesktop;
            bool crossDisplayStack = HasCrossDisplayStackTransfer(window);

            if (!crossDisplayStack && ShouldKeepAuxiliaryFloating(window))
            {
                using (m_floatingSetLock.EnterScope())
                {
                    m_floatingSet.Add(window);
                }

                m_logger.Information(
                    "Window {Window} left floating (auxiliary popup, original size)",
                    window.DebugString());
                return false;
            }

            if (!crossDisplayStack && ShouldFloatNewWindowInStackMode(window, desktop))
            {
                using (m_floatingSetLock.EnterScope())
                {
                    m_floatingSet.Add(window);
                }

                m_logger.Information(
                    "Window {Window} left floating in stack mode (new process, original size)",
                    window.DebugString());
                return false;
            }

            using (m_floatingSetLock.EnterScope())
            {
                m_floatingSet.Remove(window);
            }

            return TryRegisterAutoTiledWindowCore(window, out node, maxTreeWidth);
        }

        private bool TryRegisterAutoTiledWindowCore(IWindow window, out WindowNode? node, int maxTreeWidth = 100)
        {
            node = null;
            var desktop = m_workspace.VirtualDesktopManager.CurrentDesktop;

            if (m_backend.IsStackModeActive(desktop))
            {
                var stack = m_backend.GetOrCreateRootStackPanel(desktop);
                node = m_backend.RegisterWindow(window, stack);
            }
            else
            {
                var restoreToStack = TakeCrossDisplayStackTransfer(window);
                node = m_backend.RegisterWindow(window, maxTreeWidth);
                if (restoreToStack && node.Parent is not StackPanelNode)
                {
                    TryRestoreCrossDisplayStack(node);
                }
            }

            node.Parent!.Padding = GetPanelPaddingRect();
            node.Parent!.Spacing = GetPanelSpacing();
            RepairRootStackLayout(desktop);
            return true;
        }

        private bool IsRegisteredWithBackend(IWindow window)
        {
            using (m_backendLock.EnterScope())
            {
                return m_backend.HasWindow(window);
            }
        }

        private bool DetectChanges(IWindow window, bool manualRegistration = false)
        {
            m_logger.Verbose("Dirty checking for changes with window {Window}", window.DebugString());
            try
            {
                if (window.State == WindowState.Restored && CanManage(window))
                {
                    if (!AutoRegisterWindows || m_showDesktopAwaitingRestore)
                    {
                        return false;
                    }

                    bool crossDisplayStack = HasCrossDisplayStackTransfer(window);
                    bool allowManagedRegistration = manualRegistration || crossDisplayStack;

                    if (!allowManagedRegistration && !ShouldAutoTile(window))
                    {
                        if (!IsRegisteredWithBackend(window))
                        {
                            using (m_floatingSetLock.EnterScope())
                            {
                                m_floatingSet.Add(window);
                            }
                        }

                        return false;
                    }

                    if (!allowManagedRegistration && ShouldKeepAuxiliaryFloating(window))
                    {
                        if (!IsRegisteredWithBackend(window))
                        {
                            using (m_floatingSetLock.EnterScope())
                            {
                                m_floatingSet.Add(window);
                            }
                        }

                        return false;
                    }

                    if (!crossDisplayStack && ShouldFloatNewWindowInStackMode(window, m_workspace.VirtualDesktopManager.CurrentDesktop))
                    {
                        using (m_floatingSetLock.EnterScope())
                        {
                            m_floatingSet.Add(window);
                        }
                        return false;
                    }

                    try
                    {
                        using (m_backendLock.EnterScope())
                        {
                            try
                            {
                                if (!m_backend.HasWindow(window))
                                {
                                    m_logger.Debug("Window {Window} can be managed, but is not registered with backend, registering now", window.DebugString());
                                    if (!TryRegisterAutoTiledWindow(window, out _))
                                    {
                                        return false;
                                    }
                                    InvalidateLayout();
                                    return true;
                                }
                            }
                            catch (InvalidWindowReferenceException)
                            {
                                if (m_backend.HasWindow(window))
                                    m_backend.UnregisterWindow(window);
                            }
                        }
                    }
                    catch (NoValidPlacementExistsException)
                    {
                        PlacementFailed?.Invoke(this, new TilingFailedEventArgs(
                            TilingError.NoValidPlacementExists, window));
                    }
                }
                else
                {
                    using (m_backendLock.EnterScope())
                    {
                        if (m_backend.HasWindow(window) && !CanManage(window, ignoreFloating: true))
                        {
                            m_logger.Verbose("Window {Window} can no longer be managed, but is registered with backend, unregistering now", window.DebugString());
                            RememberCrossDisplayStackTransfer(window);
                            m_backend.UnregisterWindow(window);

                            InvalidateLayout();
                            return true;
                        }
                    }
                }
            }
            catch (WindowAlreadyRegisteredException)
            {
                return false;
            }
            // TODO: Is the following catch block necessary?
            catch (InvalidOperationException)
            {
                return false;
            }
            return false;
        }

        private void TryAcceptCrossDisplayStackWindow(IWindow window)
        {
            if (!IsWindowOnThisDisplay(window))
            {
                return;
            }

            bool crossDisplayStack = HasCrossDisplayStackTransfer(window);

            using (m_floatingSetLock.EnterScope())
            {
                m_floatingSet.Remove(window);
            }

            if (!IsRegisteredWithBackend(window))
            {
                DetectChanges(window, manualRegistration: crossDisplayStack);
                return;
            }

            if (!crossDisplayStack)
            {
                return;
            }

            using (m_backendLock.EnterScope())
            {
                var node = m_backend.FindWindow(window);
                if (node == null)
                {
                    return;
                }

                if (node.Parent is not StackPanelNode)
                {
                    TryRestoreCrossDisplayStack(node);
                }

                TakeCrossDisplayStackTransfer(window);
                RepairRootStackLayout(m_workspace.VirtualDesktopManager.CurrentDesktop);
            }

            InvalidateLayout();
        }

        private void FinalizeCrossDisplayLeave(IWindow window)
        {
            if (IsWindowOnThisDisplay(window))
            {
                return;
            }

            using (m_backendLock.EnterScope())
            {
                if (!m_backend.HasWindow(window))
                {
                    return;
                }

                RememberCrossDisplayStackTransfer(window);
                m_backend.UnregisterWindow(window);
            }

            InvalidateLayout();
        }

        internal void FinalizeCrossDisplayLeaves()
        {
            List<IWindow> windows;
            using (m_windowSetLock.EnterScope())
            {
                windows = [.. m_windowSet];
            }

            foreach (var window in windows)
            {
                try
                {
                    FinalizeCrossDisplayLeave(window);
                }
                catch (InvalidWindowReferenceException)
                {
                }
            }
        }

        internal void AdmitCrossDisplayWindowsOnDisplay()
        {
            List<IWindow> windows;
            using (m_windowSetLock.EnterScope())
            {
                windows = [.. m_windowSet];
            }

            foreach (var window in windows)
            {
                try
                {
                    if (!IsWindowOnThisDisplay(window))
                    {
                        continue;
                    }

                    TryAcceptCrossDisplayStackWindow(window);
                }
                catch (InvalidWindowReferenceException)
                {
                }
            }
        }

        private static bool HasCrossDisplayStackTransfer(IWindow window)
        {
            lock (s_crossDisplayStackTransfersLock)
            {
                return s_crossDisplayStackTransfers.ContainsKey(window);
            }
        }

        private void RememberCrossDisplayStackTransfer(IWindow window)
        {
            var node = m_backend.FindWindow(window);
            if (node == null || !node.PathToRoot.OfType<StackPanelNode>().Any())
            {
                return;
            }

            lock (s_crossDisplayStackTransfersLock)
            {
                s_crossDisplayStackTransfers[window] = true;
            }

            CrossDisplayStackTransferReady?.Invoke(window);
        }

        internal void AdmitCrossDisplayStackWindow(IWindow window)
        {
            TryAcceptCrossDisplayStackWindow(window);
        }

        private static bool TakeCrossDisplayStackTransfer(IWindow window)
        {
            lock (s_crossDisplayStackTransfersLock)
            {
                return s_crossDisplayStackTransfers.Remove(window);
            }
        }

        private void TryRestoreCrossDisplayStack(WindowNode node)
        {
            var desktop = m_workspace.VirtualDesktopManager.CurrentDesktop;
            var tree = m_backend.GetTree(desktop);
            if (tree?.Root == null || node.Parent == null)
            {
                return;
            }

            var stack = tree.Root.Nodes.OfType<StackPanelNode>().FirstOrDefault();
            if (stack != null)
            {
                if (node.Parent != stack)
                {
                    node.Parent.Detach(node);
                    stack.Attach(node);
                }
                return;
            }

            m_backend.WrapInStackPanel(node);
            node.Parent!.Padding = GetPanelPaddingRect();
            node.Parent!.Spacing = GetPanelSpacing();
        }

        private void ResetLayoutCore()
        {
            m_logger.Information("Resetting window layout for display {Display}", m_display);

            m_pendingIntent?.Cancel();
            m_pendingIntent = null;

            m_showDesktopSnapshot = null;
            m_showDesktopAwaitingRestore = false;
            ++m_showDesktopRestoreGeneration;

            using (m_savedLocationsLock.EnterScope())
            {
                m_savedLocations.Clear();
            }

            using (m_ignoreRepositionSetLock.EnterScope())
            {
                m_ignoreRepositionSet.Clear();
            }

            using (m_backendLock.EnterScope())
            {
                foreach (var desktop in m_workspace.VirtualDesktopManager.Desktops.ToList())
                {
                    var tree = m_backend.GetTree(desktop);
                    if (tree?.Root != null)
                    {
                        foreach (var windowNode in tree.Root.Windows.ToList())
                        {
                            try
                            {
                                m_backend.UnregisterWindow(windowNode.WindowReference);
                            }
                            catch (Exception ex)
                            {
                                m_logger.Warning(ex, "Failed to unregister window during layout reset");
                            }
                        }
                    }

                    try
                    {
                        m_backend.UnregisterDesktop(desktop);
                    }
                    catch (ArgumentException)
                    {
                    }

                    var orientation = m_display.Bounds.Width >= m_display.Bounds.Height
                        ? PanelOrientation.Horizontal
                        : PanelOrientation.Vertical;
                    m_backend.RegisterDesktop(desktop, m_display.WorkArea, orientation);
                }
            }

            using (m_floatingSetLock.EnterScope())
            using (m_windowSetLock.EnterScope())
            {
                foreach (var window in m_windowSet.Where(IsWindowOnThisDisplay))
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

            DiscoverWindows();
            Refresh();
            InvalidateLayout();
        }

        private static long IntersectionArea(Rectangle a, Rectangle b)
        {
            int left = Math.Max(a.Left, b.Left);
            int top = Math.Max(a.Top, b.Top);
            int right = Math.Min(a.Right, b.Right);
            int bottom = Math.Min(a.Bottom, b.Bottom);
            if (right <= left || bottom <= top)
            {
                return 0;
            }

            return (long)(right - left) * (bottom - top);
        }

        private IDisplay? GetOwningDisplay(IWindow window)
        {
            var center = window.Position.Center;
            var byCenter = m_workspace.DisplayManager.Displays
                .FirstOrDefault(d => d.Bounds.Contains(center));
            if (byCenter != null)
            {
                return byCenter;
            }

            IDisplay? best = null;
            long bestArea = 0;
            var rect = window.Position;
            foreach (var display in m_workspace.DisplayManager.Displays)
            {
                var area = IntersectionArea(display.Bounds, rect);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = display;
                }
            }

            return bestArea > 0 ? best : null;
        }

        private bool IsWindowOnThisDisplay(IWindow window)
        {
            var owner = GetOwningDisplay(window);
            return owner != null && owner.Equals(m_display);
        }

        private bool CanManage(IWindow x, bool ignoreFloating = false)
        {
            bool IsOnCurrentDisplay() => IsWindowOnThisDisplay(x);
            bool IsFloating()
            {
                using (m_floatingSetLock.EnterScope())
                {
                    return m_floatingSet.Contains(x);
                }
            }

            // Cheap boolean read
            if (x.IsTopmost)
            {
                return false;
            }

            // Set lookup
            if (!ignoreFloating && IsFloating())
            {
                return false;
            }

            // GetWindowPos + Lookup
            if (!IsOnCurrentDisplay())
            {
                return false;
            }

            // OpenProcess (expensive). Move-only windows (e.g. fixed-size VB6 forms) are
            // still supported: the layout engine repositions them without resizing.
            if (!x.CanMove)
            {
                return false;
            }

            // Virtual Desktop stuff is very expensive
            if (m_workspace.VirtualDesktopManager.IsWindowPinned(x))
            {
                return false;
            }

            if (!ShouldManageVisualBasicWindow(x))
            {
                return false;
            }

            return true;
        }

        private IEnumerable<IWindow> GetVisualBasicPeersOnDisplay(int processId)
        {
            var peers = new HashSet<IWindow>();
            foreach (var window in m_workspace.GetSnapshot())
            {
                if (!IsWindowOnThisDisplay(window))
                {
                    continue;
                }

                try
                {
                    if (window.GetCachedProcessId() == processId)
                    {
                        peers.Add(window);
                    }
                }
                catch (InvalidWindowReferenceException)
                {
                }
            }

            using (m_windowSetLock.EnterScope())
            {
                foreach (var window in m_windowSet)
                {
                    if (!IsWindowOnThisDisplay(window))
                    {
                        continue;
                    }

                    try
                    {
                        if (window.GetCachedProcessId() == processId)
                        {
                            peers.Add(window);
                        }
                    }
                    catch (InvalidWindowReferenceException)
                    {
                    }
                }
            }

            return peers;
        }

        private bool ShouldManageVisualBasicWindow(IWindow window)
        {
            try
            {
                if (!VisualBasicWindowRules.IsVisualBasicProcess(window.GetCachedProcessName()))
                {
                    return true;
                }

                return VisualBasicWindowRules.ShouldManage(window, GetVisualBasicPeersOnDisplay(window.GetCachedProcessId()));
            }
            catch (InvalidWindowReferenceException)
            {
                return true;
            }
        }

        private void InvalidateLayout()
        {
            if (!m_active)
            {
                return;
            }

            m_dirty = true;
            if (m_frozen.IsPositive())
            {
                return;
            }
            m_dispatcher.InvokeAsync(new Action(() =>
            {
                if (!m_dirty || m_frozen.IsPositive())
                    return;
                m_dirty = false;
                _ = UpdateLayoutAsync();
            }), System.Windows.Threading.DispatcherPriority.DataBind);
        }

        private void Freeze()
        {
            m_frozen.Increment();
        }

        private void Unfreeze()
        {
            if (m_frozen.DecrementIfPositive())
            {
                if (m_dirty)
                {
                    InvalidateLayout();
                }
            }
        }

        private static Rectangle ShrinkTo(Rectangle container, int width, int height)
        {
            int wdiff = container.Width - width;
            int hdiff = container.Height - height;
            return new Rectangle(
                container.Left + wdiff / 2,
                container.Top + hdiff / 2,
                container.Right - wdiff / 2,
                container.Height - wdiff / 2
            );
        }

        private int GetPanelSpacing()
        {
            double scaling = m_display.Scaling;
            return (int)(m_windowPadding * scaling);
        }

        private Rectangle GetPanelPaddingRect()
        {
            double scaling = m_display.Scaling;
            return new Rectangle(0, (int)((m_panelHeight + m_windowPadding) * scaling), 0, 0);
        }

        private static System.Windows.Thickness ToThickness(Rectangle rc)
        {
            return new System.Windows.Thickness(rc.Left, rc.Top, rc.Right, rc.Bottom);
        }

        private void UpdateGuiNodeOptions()
        {
            m_dispatcher.Invoke(() =>
            {
                m_gui.PanelSpacing = GetPanelSpacing();
                m_gui.PanelPadding = ToThickness(GetPanelPaddingRect());
                m_gui.InvalidateView();
                InvalidateLayout();
            });
        }

        private void PropagatePaddingChange()
        {
            using (m_backendLock.EnterScope())
            {
                foreach (var panel in m_backend.Trees.SelectMany(x => x.Root!.Nodes).OfType<PanelNode>())
                {
                    panel.Spacing = GetPanelSpacing();
                }
            }
            UpdateGuiNodeOptions();
        }

        private void PropagatePanelHeightChange()
        {
            using (m_backendLock.EnterScope())
            {
                foreach (var panel in m_backend.Trees.SelectMany(x => x.Root!.Nodes).OfType<PanelNode>())
                {
                    panel.Padding = GetPanelPaddingRect();
                    panel.Spacing = GetPanelSpacing();
                }
            }
            UpdateGuiNodeOptions();
        }

        private void PropagateShowFocusChange()
        {
            InvalidateLayout();
        }

        private void PropagateShowPreviewFocusChange()
        {
            InvalidateLayout();
        }

        private bool ShouldAutoTile(IWindow window)
        {
            return m_inclusionMatchers.Any(x => x.Matches(window));
        }

        private void EnsureWindowTiled(IWindow window)
        {
            if (!CanManage(window, ignoreFloating: true))
                return;

            bool shouldRegister;
            using (m_floatingSetLock.EnterScope())
            {
                shouldRegister = m_floatingSet.Remove(window);
            }

            if (!shouldRegister)
            {
                using (m_backendLock.EnterScope())
                {
                    if (m_backend.HasWindow(window))
                        return;
                }
                shouldRegister = true;
            }

            if (shouldRegister)
                DetectChanges(window, manualRegistration: true);
        }

        private TilingNode? GetFocusedTilingNode(bool ensureManaged = false)
        {
            var window = m_workspace.FocusedWindow;
            if (window != null && ensureManaged)
                EnsureWindowTiled(window);

            if (window != null)
            {
                using (m_backendLock.EnterScope())
                {
                    var node = m_backend.FindWindow(window);
                    if (node != null)
                        return node;
                }

                if (!ensureManaged)
                    return null;
            }

            using (m_backendLock.EnterScope())
            {
                return m_backend.GetFocus(m_workspace.VirtualDesktopManager.CurrentDesktop);
            }
        }

        bool HasFocusAndAdjacentWindow(TilingDirection direction)
        {
            try
            {
                var focusedNode = GetFocusedTilingNode(ensureManaged: false);
                if (focusedNode == null)
                    return false;

                using (m_backendLock.EnterScope())
                {
                    _ = focusedNode.GetAdjacentWindow(direction) ?? throw new TilingFailedException(TilingError.MissingAdjacentWindow);
                    return true;
                }
            }
            catch (TilingFailedException e) when (e.FailReason == TilingError.MissingTarget || e.FailReason == TilingError.MissingAdjacentWindow)
            {
                return false;
            }
        }
    }
}
