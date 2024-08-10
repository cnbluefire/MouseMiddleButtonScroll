using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MouseMiddleButtonScroll.Wpf
{
    internal class MouseMiddleButtonScrollHelper : IDisposable
    {
        private readonly object locker = new object();
        private ScrollViewer scrollViewer;
        private UIElement element;
        private bool disposeValue;
        private Window window;
        private Point startPoint;
        private bool hasScrolled;
        private DispatcherTimer timer;
        private double scrollStartThreshold = 20;
        private bool showCursorAtStartPoint = false;

        public MouseMiddleButtonScrollHelper(UIElement element)
        {
            this.element = element;
            this.element.MouseDown += Element_MouseDown;
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

        public bool ShowCursorAtStartPoint
        {
            get => showCursorAtStartPoint;
            set
            {
                if (showCursorAtStartPoint != value)
                {
                    showCursorAtStartPoint = value;
                    UpdateScrollStates();
                }
            }
        }

        public bool InScrollMode => window != null;

        public bool EnterScrollMode()
        {
            lock (locker)
            {
                if (scrollViewer == null) return false;
                if (window != null) return true;

                if (scrollViewer.IsLoaded
                    && (scrollViewer.ScrollableWidth > 0 || scrollViewer.ScrollableHeight > 0)
                    && scrollViewer.CaptureMouse())
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

                    startPoint = MouseEx.GetPosition(scrollViewer);
                    hasScrolled = false;

                    UpdateScrollStates();
                    timer.Start();

                    return true;
                }

                ExitScrollMode();
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
                hasScrolled = false;

                RemoveScrollStartCursor();
                scrollViewer.ReleaseMouseCapture();
                timer?.Stop();
                Mouse.OverrideCursor = null;

                scrollViewer.Unloaded -= ScrollViewer_Unloaded;

                this.window = null;
                this.scrollViewer = null;

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

                element.MouseDown -= Element_MouseDown;
                element = null;

                ExitScrollMode();
            }
        }


        #region ScrollViewer Events


        private void Element_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle
                && e.MiddleButton == MouseButtonState.Pressed)
            {
                ExitScrollMode();

                scrollViewer = sender as ScrollViewer;
                if (scrollViewer == null)
                {
                    var p = e.OriginalSource as DependencyObject;
                    while (p != null && !(p is ScrollViewer) && p != element)
                    {
                        p = (p as FrameworkElement)?.Parent ?? VisualTreeHelper.GetParent(p);
                    }

                    if (p is ScrollViewer sv)
                    {
                        scrollViewer = sv;
                    }
                }

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
                if (Math.Abs(curPos.X - startPoint.X) > 10 || Math.Abs(curPos.Y - startPoint.Y) > 10 || hasScrolled)
                {
                    // In press mode, exit
                    e.Handled = true;
                    ExitScrollMode();
                }
            }
        }

        #endregion ScrollViewer Events


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


        #region Update States

        private void UpdateScrollStates()
        {
            lock (locker)
            {
                if (window == null) return;

                var curPos = MouseEx.GetPosition(scrollViewer);

                UpdateScrollStartCursor();
                Mouse.OverrideCursor = GetCursor(startPoint, curPos, out var scrollOffsetX, out var scrollOffsetY);

                if (scrollOffsetX != 0)
                {
                    hasScrolled = true;
                    var scrollX = Math.Min(scrollViewer.HorizontalOffset + scrollOffsetX, scrollViewer.ScrollableWidth);
                    scrollViewer.ScrollToHorizontalOffset(scrollX);
                }

                if (scrollOffsetY != 0)
                {
                    hasScrolled = true;
                    var scrollY = Math.Min(scrollViewer.VerticalOffset + scrollOffsetY, scrollViewer.ScrollableHeight);
                    scrollViewer.ScrollToVerticalOffset(scrollY);
                }
            }
        }

        private void UpdateScrollStartCursor()
        {
            if (window == null)
            {
                RemoveScrollStartCursor();
                return;
            }

            var sv = scrollViewer;
            if (sv == null) return;

            var layer = AdornerLayer.GetAdornerLayer(sv);
            var adorner = layer.GetAdorners(sv)?.OfType<CursorAdorner>().FirstOrDefault();

            if (ShowCursorAtStartPoint)
            {
                if (adorner == null)
                {
                    adorner = new CursorAdorner(sv);
                    layer.Add(adorner);
                }

                adorner.StartPoint = startPoint;
                adorner.CanHorizontallyScroll = scrollViewer.ScrollableWidth > 0;
                adorner.CanVerticallyScroll = scrollViewer.ScrollableHeight > 0;
            }
            else
            {
                RemoveScrollStartCursor();
            }
        }

        private void RemoveScrollStartCursor()
        {
            var sv = scrollViewer;
            if (sv == null) return;

            var layer = AdornerLayer.GetAdornerLayer(sv);
            var adorner = layer?.GetAdorners(sv)?.OfType<CursorAdorner>().FirstOrDefault();

            if (adorner != null)
            {
                layer.Remove(adorner);
            }
        }

        private Cursor GetCursor(in Point startPoint, in Point currentPoint, out double scrollOffsetX, out double scrollOffsetY)
        {
            scrollOffsetX = 0;
            scrollOffsetY = 0;

            var offsetX = currentPoint.X - startPoint.X;
            var offsetY = currentPoint.Y - startPoint.Y;

            bool canHorizontallyScroll = scrollViewer.ScrollableWidth > 0;
            bool canVerticallyScroll = scrollViewer.ScrollableHeight > 0;

            Cursor defaultCursor = null;

            if (canHorizontallyScroll && canVerticallyScroll) defaultCursor = ScrollCursorHelper.ScrollAll;
            else if (canHorizontallyScroll) defaultCursor = ScrollCursorHelper.ScrollWE;
            else defaultCursor = ScrollCursorHelper.ScrollNS;

            if (Math.Abs(offsetX) >= ScrollStartThreshold || Math.Abs(offsetY) >= ScrollStartThreshold)
            {
                if (!canHorizontallyScroll) offsetX = 0;
                if (!canVerticallyScroll) offsetY = 0;

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
            }

            return defaultCursor;
        }

        #endregion Update States


        #region Nested Classes

        private class CursorAdorner : Adorner
        {
            public CursorAdorner(UIElement adornedElement) : base(adornedElement)
            {
            }

            private Point startPoint;
            private bool canHorizontallyScroll;
            private bool canVerticallyScroll;

            public Point StartPoint
            {
                get => startPoint;
                set
                {
                    if (startPoint != value)
                    {
                        startPoint = value;
                        InvalidateVisual();
                    }
                }
            }

            public bool CanHorizontallyScroll
            {
                get => canHorizontallyScroll;
                set
                {
                    if (canHorizontallyScroll != value)
                    {
                        canHorizontallyScroll = value;
                        InvalidateVisual();
                    }
                }
            }

            public bool CanVerticallyScroll
            {
                get => canVerticallyScroll;
                set
                {
                    if (canVerticallyScroll != value)
                    {
                        canVerticallyScroll = value;
                        InvalidateVisual();
                    }
                }
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                base.OnRender(drawingContext);

                var image = GetCursorImage();

                if (image != null)
                {
                    drawingContext.DrawImage(image, new Rect(startPoint.X - 16, startPoint.Y - 16, 32, 32));
                }
            }

            private ImageSource GetCursorImage()
            {
                if (CanHorizontallyScroll && CanVerticallyScroll) return ScrollCursorHelper.ScrollAllImage;
                else if (CanHorizontallyScroll) return ScrollCursorHelper.ScrollWEImage;
                else if (CanVerticallyScroll) return ScrollCursorHelper.ScrollNSImage;
                else return null;
            }
        }

        private static class MouseEx
        {
            public static Point GetPosition(UIElement element)
            {
                if (GetCursorPos(out var point))
                {
                    return element.PointFromScreen(new Point(point.X, point.Y));
                }
                return Mouse.GetPosition(element);
            }


            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool GetCursorPos(out _POINT lpPoint);

            [StructLayout(LayoutKind.Sequential)]
            private struct _POINT
            {
                public int X;
                public int Y;
            }
        }

        private static class ScrollCursorHelper
        {
            private static readonly Dictionary<string, Cursor> cursors = new Dictionary<string, Cursor>();
            private static readonly Dictionary<string, WriteableBitmap> cursorImages = new Dictionary<string, WriteableBitmap>();
            private static Func<Cursor, SafeHandle> cursorHandleGetter;

            public static Cursor ScrollAll => EnsureCursor("Assets.Cursors.ScrollAll.cur", Cursors.ScrollAll);
            public static Cursor ScrollE => EnsureCursor("Assets.Cursors.ScrollE.cur", Cursors.ScrollE);
            public static Cursor ScrollN => EnsureCursor("Assets.Cursors.ScrollN.cur", Cursors.ScrollN);
            public static Cursor ScrollNE => EnsureCursor("Assets.Cursors.ScrollNE.cur", Cursors.ScrollNE);
            public static Cursor ScrollNS => EnsureCursor("Assets.Cursors.ScrollNS.cur", Cursors.ScrollNS);
            public static Cursor ScrollNW => EnsureCursor("Assets.Cursors.ScrollNW.cur", Cursors.ScrollNW);
            public static Cursor ScrollS => EnsureCursor("Assets.Cursors.ScrollS.cur", Cursors.ScrollS);
            public static Cursor ScrollSE => EnsureCursor("Assets.Cursors.ScrollSE.cur", Cursors.ScrollSE);
            public static Cursor ScrollSW => EnsureCursor("Assets.Cursors.ScrollSW.cur", Cursors.ScrollSW);
            public static Cursor ScrollW => EnsureCursor("Assets.Cursors.ScrollW.cur", Cursors.ScrollW);
            public static Cursor ScrollWE => EnsureCursor("Assets.Cursors.ScrollWE.cur", Cursors.ScrollWE);

            public static ImageSource ScrollAllImage => EnsureCursorImage("Assets.ScrollAll.cur", Cursors.ScrollAll);
            public static ImageSource ScrollNSImage => EnsureCursorImage("Assets.ScrollNS.cur", Cursors.ScrollNS);
            public static ImageSource ScrollWEImage => EnsureCursorImage("Assets.ScrollWE.cur", Cursors.ScrollWE);

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

            private static ImageSource EnsureCursorImage(string fileName, Cursor fallbackCursor)
            {
                lock (cursorImages)
                {
                    if (cursorImages.TryGetValue(fileName, out var image)) return image;

                    var cursor = EnsureCursor(fileName, fallbackCursor);
                    image = GetCursorImage(cursor);
                    if (image == null && cursor != fallbackCursor) image = GetCursorImage(fallbackCursor);

                    if (image == null) image = new WriteableBitmap(32, 32, 96, 96, PixelFormats.Bgra32, null);
                    cursorImages[fileName] = image;

                    return image;
                }
            }

            private unsafe static WriteableBitmap GetCursorImage(Cursor cursor)
            {
                if (cursorHandleGetter == null)
                {
                    lock (cursors)
                    {
                        if (cursorHandleGetter == null)
                        {
                            try
                            {
                                var propertyInfo = typeof(Cursor).GetProperty("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                                if (propertyInfo != null)
                                {
                                    var p = System.Linq.Expressions.Expression.Parameter(typeof(Cursor), "c");
                                    var propertyAccess = System.Linq.Expressions.Expression.Property(p, propertyInfo);
                                    var cast = System.Linq.Expressions.Expression.Convert(propertyAccess, typeof(SafeHandle));

                                    cursorHandleGetter = System.Linq.Expressions.Expression.Lambda<Func<Cursor, SafeHandle>>(cast, p).Compile();
                                }
                            }
                            catch { }

                            if (cursorHandleGetter == null) cursorHandleGetter = _ => null;
                        }
                    }
                }

                var handle = cursorHandleGetter.Invoke(cursor);
                if (handle != null && !handle.IsInvalid)
                {
                    bool success = false;
                    handle.DangerousAddRef(ref success);
                    if (success)
                    {
                        try
                        {
                            var hIcon = handle.DangerousGetHandle();
                            using (var icon = System.Drawing.Icon.FromHandle(hIcon))
                            using (var bitmap = icon.ToBitmap())
                            using (var convertBitmap = new System.Drawing.Bitmap(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                            {
                                using (var g = System.Drawing.Graphics.FromImage(convertBitmap))
                                {
                                    g.DrawImageUnscaled(bitmap, 0, 0);
                                }
                                var data = convertBitmap.LockBits(
                                    new System.Drawing.Rectangle(0, 0, convertBitmap.Width, convertBitmap.Height),
                                    System.Drawing.Imaging.ImageLockMode.ReadWrite,
                                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                                if (data != null)
                                {
                                    try
                                    {
                                        var writeableBitmap = new WriteableBitmap(convertBitmap.Width, convertBitmap.Height, 96, 96, PixelFormats.Bgra32, null);
                                        if (writeableBitmap.TryLock(Duration.Forever))
                                        {
                                            try
                                            {
                                                var size = data.Stride * data.Height;
                                                Buffer.MemoryCopy((void*)data.Scan0, (void*)writeableBitmap.BackBuffer, size, size);
                                                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, data.Width, data.Height));
                                            }
                                            finally
                                            {
                                                writeableBitmap.Unlock();
                                            }

                                            return writeableBitmap;
                                        }
                                    }
                                    finally
                                    {
                                        convertBitmap.UnlockBits(data);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            handle.DangerousRelease();
                        }
                    }
                }

                return null;
            }
        }

        #endregion Nested Classes

    }
}
