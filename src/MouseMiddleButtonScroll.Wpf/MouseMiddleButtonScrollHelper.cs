using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MouseMiddleButtonScroll.Wpf
{
    internal class MouseMiddleButtonScrollHelper : IDisposable
    {
        private readonly object locker = new object();
        private readonly ScrollViewer scrollViewer;
        private bool disposeValue;
        private Window window;
        private Point startPoint;
        private DispatcherTimer timer;
        private double scrollStartThreshold = 60;

        public MouseMiddleButtonScrollHelper(ScrollViewer scrollViewer)
        {
            this.scrollViewer = scrollViewer;

            scrollViewer.MouseDown += ScrollViewer_MouseDown;
        }

        private void ScrollViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle
                && e.MiddleButton == MouseButtonState.Pressed)
            {
                ExitScrollMode();
                e.Handled = EnterScrollMode();
            }
        }

        private void ScrollViewer_Unloaded(object sender, RoutedEventArgs e)
        {
            ExitScrollMode();
        }

        private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle
                && e.MiddleButton == MouseButtonState.Released)
            {
                var curPos = e.GetPosition(scrollViewer);
                if (Math.Abs(curPos.X - startPoint.X) > 10 || Math.Abs(curPos.Y - startPoint.Y) > 10)
                {
                    // In press mode, exit
                    e.Handled = true;
                    ExitScrollMode();
                }
            }
        }

        #region Exit Scroll Events

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            ExitScrollMode();
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            ExitScrollMode();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ExitScrollMode();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            ExitScrollMode();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ExitScrollMode();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            ExitScrollMode();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            ExitScrollMode();
        }

        #endregion Exit Scroll Events

        private void UpdateScrollStates()
        {
            lock (locker)
            {
                if (window == null) return;

                var curPos = Mouse.GetPosition(scrollViewer);

                Mouse.OverrideCursor = GetCursor(startPoint, curPos, out var scrollOffsetX, out var scrollOffsetY);

                if (scrollOffsetX != 0)
                {
                    var scrollX = Math.Min(scrollViewer.HorizontalOffset + scrollOffsetX, scrollViewer.ScrollableWidth);
                    scrollViewer.ScrollToHorizontalOffset(scrollX);
                }

                if (scrollOffsetY != 0)
                {
                    var scrollY = Math.Min(scrollViewer.VerticalOffset + scrollOffsetY, scrollViewer.ScrollableHeight);
                    scrollViewer.ScrollToVerticalOffset(scrollY);
                }
            }
        }

        private Cursor GetCursor(in Point startPoint, in Point currentPoint, out double scrollOffsetX, out double scrollOffsetY)
        {
            scrollOffsetX = 0;
            scrollOffsetY = 0;

            var offsetX = currentPoint.X - startPoint.X;
            var offsetY = currentPoint.Y - startPoint.Y;

            Debug.WriteLine($"offsetX: {offsetX}, offsetY: {offsetY}");

            bool canHorizontallyScroll = scrollViewer.ScrollableWidth > 0;
            bool canVerticallyScroll = scrollViewer.ScrollableHeight > 0;

            if (Math.Abs(offsetX) < ScrollStartThreshold && Math.Abs(offsetY) < ScrollStartThreshold)
            {
                if (canHorizontallyScroll && canVerticallyScroll) return ScrollCursorHelper.ScrollAll;
                else if (canHorizontallyScroll) return ScrollCursorHelper.ScrollWE;
                else return ScrollCursorHelper.ScrollNS;
            }
            else
            {
                if (!canHorizontallyScroll) offsetX = 0;
                if (!canVerticallyScroll) offsetX = 0;

                const double ratio = 0.75d;

                if (Math.Abs(offsetX) > ScrollStartThreshold)
                {
                    scrollOffsetX = (offsetX > 0 ? offsetX - ScrollStartThreshold : offsetX + ScrollStartThreshold) * ratio;
                }

                if (Math.Abs(offsetY) > ScrollStartThreshold)
                {
                    scrollOffsetY = (offsetY > 0 ? offsetY - ScrollStartThreshold : offsetY + ScrollStartThreshold) * ratio;
                }

                if (offsetX <= -ScrollStartThreshold && offsetY <= -ScrollStartThreshold)
                {
                    return ScrollCursorHelper.ScrollNW;
                }
                else if (offsetX >= ScrollStartThreshold && offsetY <= -ScrollStartThreshold)
                {
                    return ScrollCursorHelper.ScrollNE;
                }
                else if (offsetX >= ScrollStartThreshold && offsetY >= ScrollStartThreshold)
                {
                    return ScrollCursorHelper.ScrollSE;
                }
                else if (offsetX <= -ScrollStartThreshold && offsetY >= ScrollStartThreshold)
                {
                    return ScrollCursorHelper.ScrollSW;
                }
                else if (offsetX <= -ScrollStartThreshold)
                {
                    return ScrollCursorHelper.ScrollW;
                }
                else if (offsetX >= ScrollStartThreshold)
                {
                    return ScrollCursorHelper.ScrollE;
                }
                else if (offsetY <= -ScrollStartThreshold)
                {
                    return ScrollCursorHelper.ScrollN;
                }
                else if (offsetY >= ScrollStartThreshold)
                {
                    return ScrollCursorHelper.ScrollS;
                }

                return null;
            }
        }

        public double ScrollStartThreshold
        {
            get => scrollStartThreshold;
            set
            {
                if (value != scrollStartThreshold)
                {
                    if (value < 0) throw new ArgumentException(nameof(ScrollStartThreshold));
                    scrollStartThreshold = value;
                    UpdateScrollStates();
                }
            }
        }

        public bool InScrollMode => window != null;

        public bool EnterScrollMode()
        {
            lock (locker)
            {
                if (window != null) return true;

                if (scrollViewer.IsLoaded && scrollViewer.CaptureMouse()
                    && (scrollViewer.ScrollableWidth > 0 || scrollViewer.ScrollableHeight > 0))
                {
                    window = Window.GetWindow(scrollViewer);
                }

                if (window != null)
                {
                    if (timer == null)
                    {
                        timer = new DispatcherTimer(DispatcherPriority.Render)
                        {
                            Interval = TimeSpan.FromMilliseconds(33)
                        };
                        timer.Tick += (s, a) => UpdateScrollStates();
                    }

                    scrollViewer.Unloaded += ScrollViewer_Unloaded;

                    window.PreviewKeyDown += Window_PreviewKeyDown;
                    window.PreviewMouseWheel += Window_PreviewMouseWheel;
                    window.PreviewMouseDown += Window_PreviewMouseDown;
                    window.PreviewMouseUp += Window_PreviewMouseUp;
                    window.StateChanged += Window_StateChanged;
                    window.Deactivated += Window_Deactivated;
                    window.IsVisibleChanged += Window_IsVisibleChanged;
                    window.Closed += Window_Closed;
                    window.SizeChanged += Window_SizeChanged;

                    startPoint = Mouse.GetPosition(scrollViewer);

                    UpdateScrollStates();
                    timer.Start();

                    return true;
                }

                return false;
            }
        }

        public void ExitScrollMode()
        {
            lock (locker)
            {
                var window = this.window;
                if (window == null) return;

                startPoint = default;

                scrollViewer.ReleaseMouseCapture();
                timer?.Stop();
                Mouse.OverrideCursor = null;

                scrollViewer.Unloaded -= ScrollViewer_Unloaded;

                this.window = null;

                if (window != null)
                {
                    window.PreviewKeyDown -= Window_PreviewKeyDown;
                    window.PreviewMouseWheel -= Window_PreviewMouseWheel;
                    window.PreviewMouseDown -= Window_PreviewMouseDown;
                    window.PreviewMouseUp -= Window_PreviewMouseUp;
                    window.StateChanged -= Window_StateChanged;
                    window.Deactivated -= Window_Deactivated;
                    window.IsVisibleChanged -= Window_IsVisibleChanged;
                    window.Closed -= Window_Closed;
                    window.SizeChanged -= Window_SizeChanged;
                }

                return;
            }
        }

        public void Dispose()
        {
            if (!disposeValue)
            {
                disposeValue = true;

                scrollViewer.MouseDown -= ScrollViewer_MouseDown;

                ExitScrollMode();
            }
        }

        private static class ScrollCursorHelper
        {
            private static readonly Dictionary<string, Cursor> cursors = new Dictionary<string, Cursor>();

            public static Cursor ScrollAll => EnsureCursor("Assets.ScrollAll.cur", Cursors.ScrollAll);
            public static Cursor ScrollE => EnsureCursor("Assets.ScrollE.cur", Cursors.ScrollE);
            public static Cursor ScrollN => EnsureCursor("Assets.ScrollN.cur", Cursors.ScrollN);
            public static Cursor ScrollNE => EnsureCursor("Assets.ScrollNE.cur", Cursors.ScrollNE);
            public static Cursor ScrollNS => EnsureCursor("Assets.ScrollNS.cur", Cursors.ScrollNS);
            public static Cursor ScrollNW => EnsureCursor("Assets.ScrollNW.cur", Cursors.ScrollNW);
            public static Cursor ScrollS => EnsureCursor("Assets.ScrollS.cur", Cursors.ScrollS);
            public static Cursor ScrollSE => EnsureCursor("Assets.ScrollSE.cur", Cursors.ScrollSE);
            public static Cursor ScrollSW => EnsureCursor("Assets.ScrollSW.cur", Cursors.ScrollSW);
            public static Cursor ScrollW => EnsureCursor("Assets.ScrollW.cur", Cursors.ScrollW);
            public static Cursor ScrollWE => EnsureCursor("Assets.ScrollWE.cur", Cursors.ScrollWE);

            private static Cursor EnsureCursor(string fileName, Cursor fallbackCursor)
            {
                lock (cursors)
                {
                    if (cursors.TryGetValue(fileName, out Cursor cursor)) return cursor;

                    using (var stream = typeof(ScrollCursorHelper).Assembly.GetManifestResourceStream($"MouseMiddleButtonScroll.Wpf.{fileName}"))
                    {
                        if (stream != null)
                        {
                            return (cursors[fileName] = new Cursor(stream, true));
                        }
                        return (cursors[fileName] = fallbackCursor);
                    }
                }

            }
        }
    }
}
