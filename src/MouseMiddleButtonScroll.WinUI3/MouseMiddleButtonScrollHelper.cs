using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Content;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.Foundation;
using WinRT;

namespace MouseMiddleButtonScroll.WinUI3
{
    internal class MouseMiddleButtonScrollHelper : IDisposable
    {
        private readonly object locker = new object();
        private ScrollViewer scrollViewer;
        private UIElement element;
        private bool disposeValue;
        private AppWindowListener appWindowListener;
        private Point startPoint;
        private bool hasScrolled;
        private DispatcherTimer timer;
        private double scrollStartThreshold = 20;
        private bool showCursorAtStartPoint = false;
        private Pointer capturedPointer;
        private CursorAdorner cursorAdorner;

        public MouseMiddleButtonScrollHelper(UIElement element)
        {
            this.element = element;
            this.element.PointerPressed += Element_PointerPressed;
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

        public bool InScrollMode => appWindowListener != null;

        private bool EnterScrollMode(Pointer pointer)
        {
            lock (locker)
            {
                if (scrollViewer == null) return false;
                if (appWindowListener != null) return true;

                if (scrollViewer.IsLoaded
                    && scrollViewer.XamlRoot != null
                    && (scrollViewer.ScrollableWidth > 0 || scrollViewer.ScrollableHeight > 0)
                    && scrollViewer.CapturePointer(pointer))
                {
                    capturedPointer = pointer;

                    appWindowListener = AppWindowListener.TryCreate(scrollViewer);
                }

                if (appWindowListener != null)
                {
                    if (timer == null)
                    {
                        timer = new DispatcherTimer()
                        {
                            Interval = TimeSpan.FromMilliseconds(33)
                        };
                        timer.Tick += (s, a) => UpdateScrollStates();
                    }

                    scrollViewer.Unloaded += ScrollViewer_Unloaded;

                    appWindowListener.InputKeyboardSource.KeyDown += InputKeyboardSource_KeyDown;

                    appWindowListener.InputPointerSource.PointerWheelChanged += InputPointerSource_PointerWheelChanged;
                    appWindowListener.InputPointerSource.PointerPressed += InputPointerSource_PointerPressed;
                    appWindowListener.InputPointerSource.PointerReleased += InputPointerSource_PointerReleased;
                    appWindowListener.InputPointerSource.PointerMoved += InputPointerSource_PointerMoved;
                    appWindowListener.InputPointerSource.PointerCaptureLost += InputPointerSource_PointerCaptureLost;

                    appWindowListener.AppWindow.Changed += AppWindow_Changed;
                    appWindowListener.AppWindow.Destroying += AppWindow_Destroying;

                    appWindowListener.WindowMessageMonitor.MessageReceived += WindowMessageMonitor_MessageReceived;

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

        private void ExitScrollMode()
        {
            lock (locker)
            {
                var appWindowListener = this.appWindowListener;
                if (appWindowListener == null) return;

                startPoint = default;
                hasScrolled = false;

                RemoveScrollStartCursor();
                timer?.Stop();

                if (capturedPointer != null)
                {
                    scrollViewer.ReleasePointerCapture(capturedPointer);
                }
                capturedPointer = null;

                scrollViewer.Unloaded -= ScrollViewer_Unloaded;

                this.appWindowListener = null;
                this.scrollViewer = null;

                if (appWindowListener != null)
                {
                    UIElementCursorHelper.SetCursor(element, null);

                    appWindowListener.InputKeyboardSource.KeyDown -= InputKeyboardSource_KeyDown;

                    appWindowListener.InputPointerSource.PointerWheelChanged -= InputPointerSource_PointerWheelChanged;
                    appWindowListener.InputPointerSource.PointerPressed -= InputPointerSource_PointerPressed;
                    appWindowListener.InputPointerSource.PointerReleased -= InputPointerSource_PointerReleased;
                    appWindowListener.InputPointerSource.PointerMoved -= InputPointerSource_PointerMoved;
                    appWindowListener.InputPointerSource.PointerCaptureLost -= InputPointerSource_PointerCaptureLost;

                    appWindowListener.AppWindow.Changed -= AppWindow_Changed;
                    appWindowListener.AppWindow.Destroying -= AppWindow_Destroying;

                    appWindowListener.WindowMessageMonitor.MessageReceived -= WindowMessageMonitor_MessageReceived;

                    appWindowListener.Dispose();
                }

                return;
            }
        }

        public void Dispose()
        {
            if (!disposeValue)
            {
                disposeValue = true;

                element.PointerPressed -= Element_PointerPressed;
                element = null;

                ExitScrollMode();
            }
        }


        #region ScrollViewer Events

        private void InputPointerSource_PointerMoved(InputPointerSource sender, PointerEventArgs args)
        {
            lock (locker)
            {
                if (appWindowListener == null) return;
                var curPos = MouseEx.GetPosition(scrollViewer);
                var cursor = GetCursor(startPoint, curPos, out var scrollOffsetX, out var scrollOffsetY);
                UIElementCursorHelper.SetCursor(element, cursor);
            }
        }


        private void Element_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
            {
                var point = e.GetCurrentPoint((UIElement)sender);
                if (point.Properties.IsMiddleButtonPressed)
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

                    e.Handled = EnterScrollMode(e.Pointer);
                }
            }
        }

        private void ScrollViewer_Unloaded(object sender, RoutedEventArgs e)
        {
            ExitScrollMode();
        }


        private void InputPointerSource_PointerReleased(InputPointerSource sender, PointerEventArgs args)
        {
            if (args.CurrentPoint.PointerDeviceType == PointerDeviceType.Mouse
                && !args.CurrentPoint.Properties.IsMiddleButtonPressed)
            {
                var curPos = MouseEx.GetPosition(scrollViewer);
                if (Math.Abs(curPos.X - startPoint.X) > 10 || Math.Abs(curPos.Y - startPoint.Y) > 10 || hasScrolled)
                {
                    // In press mode, exit
                    args.Handled = true;
                    ExitScrollMode();
                }
            }
        }

        private void InputPointerSource_PointerCaptureLost(InputPointerSource sender, PointerEventArgs args)
        {
            ExitScrollMode();
        }

        #endregion ScrollViewer Events


        #region Exit Scroll Events


        private void InputActivationListener_InputActivationChanged(InputActivationListener sender, InputActivationListenerActivationChangedEventArgs args)
        {
            if (sender.State != InputActivationState.Activated)
            {
                ExitScrollMode();
            }
        }

        private void InputPointerSource_PointerPressed(InputPointerSource sender, PointerEventArgs args)
        {
            args.Handled = true;
            ExitScrollMode();
        }

        private void InputPointerSource_PointerWheelChanged(InputPointerSource sender, PointerEventArgs args)
        {
            args.Handled = true;
        }

        private void InputKeyboardSource_KeyDown(InputKeyboardSource sender, KeyEventArgs args)
        {
            args.Handled = true;
            ExitScrollMode();
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPositionChange
                || args.DidPresenterChange
                || args.DidSizeChange
                || args.DidVisibilityChange
                || args.DidZOrderChange)
            {
                ExitScrollMode();
            }
        }

        private void AppWindow_Destroying(AppWindow sender, object args)
        {
            ExitScrollMode();
        }

        private void WindowMessageMonitor_MessageReceived(WindowMessageMonitor sender, WindowMessageMonitor.MessageReceivedEventArgs args)
        {
            const int WM_NCLBUTTONDOWN = 0x00A1;
            const int WM_NCRBUTTONDOWN = 0x00A4;
            const int WM_NCMBUTTONDOWN = 0x00A7;
            const int WM_NCXBUTTONDOWN = 0x00AB;
            const int WM_ACTIVATE = 0x0006;

            if (args.MessageId == WM_NCLBUTTONDOWN
                || args.MessageId == WM_NCRBUTTONDOWN
                || args.MessageId == WM_NCMBUTTONDOWN
                || args.MessageId == WM_NCXBUTTONDOWN)
            {
                ExitScrollMode();
            }
            else if (args.MessageId == WM_ACTIVATE
                && (args.WParam & 0x0000FFFF) == 0)
            {
                ExitScrollMode();
            }
        }


        #endregion Exit Scroll Events


        #region Update States

        private void UpdateScrollStates()
        {
            lock (locker)
            {
                if (appWindowListener == null) return;

                var curPos = MouseEx.GetPosition(scrollViewer);

                UpdateScrollStartCursor();
                var cursor = GetCursor(startPoint, curPos, out var scrollOffsetX, out var scrollOffsetY);
                UIElementCursorHelper.SetCursor(element, cursor);

                if (scrollOffsetX != 0 || scrollOffsetY != 0)
                {
                    hasScrolled = true;
                    var scrollX = Math.Min(scrollViewer.HorizontalOffset + scrollOffsetX, scrollViewer.ScrollableWidth);
                    var scrollY = Math.Min(scrollViewer.VerticalOffset + scrollOffsetY, scrollViewer.ScrollableHeight);
                    scrollViewer.ChangeView(scrollX, scrollY, null, true);
                }
            }
        }

        private void UpdateScrollStartCursor()
        {
            lock (locker)
            {

                if (appWindowListener == null)
                {
                    RemoveScrollStartCursor();
                    return;
                }

                var sv = scrollViewer;
                if (sv == null) return;

                var adorner = cursorAdorner;

                if (ShowCursorAtStartPoint)
                {
                    if (adorner == null)
                    {
                        cursorAdorner = adorner = new CursorAdorner(sv);
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
        }

        private void RemoveScrollStartCursor()
        {
            lock (locker)
            {
                var sv = scrollViewer;
                if (sv == null) return;

                var adorner = cursorAdorner;
                cursorAdorner = null;

                adorner?.Dispose();
            }
        }

        private InputCursor GetCursor(in Point startPoint, in Point currentPoint, out double scrollOffsetX, out double scrollOffsetY)
        {
            scrollOffsetX = 0;
            scrollOffsetY = 0;

            var offsetX = currentPoint.X - startPoint.X;
            var offsetY = currentPoint.Y - startPoint.Y;

            bool canHorizontallyScroll = scrollViewer.ScrollableWidth > 0;
            bool canVerticallyScroll = scrollViewer.ScrollableHeight > 0;

            InputCursor defaultCursor = null;

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

        private class CursorAdorner : IDisposable
        {
            private UIElement adornedElement;
            private Compositor compositor;
            private SpriteVisual visual;
            private Point startPoint;
            private bool canHorizontallyScroll;
            private bool canVerticallyScroll;

            public CursorAdorner(UIElement adornedElement)
            {
                this.adornedElement = adornedElement;
                compositor = ElementCompositionPreview.GetElementVisual(adornedElement).Compositor;
                visual = compositor.CreateSpriteVisual();
                visual.Size = new System.Numerics.Vector2(32, 32);

                ElementCompositionPreview.SetElementChildVisual(adornedElement, visual);
            }


            public Point StartPoint
            {
                get => startPoint;
                set
                {
                    if (startPoint != value)
                    {
                        startPoint = value;
                        visual.Offset = new System.Numerics.Vector3((float)(startPoint.X - 16), (float)(startPoint.Y - 16), 0);
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
                        UpdateVisual();
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
                        UpdateVisual();
                    }
                }
            }

            private void UpdateVisual()
            {
                visual.Brush = GetCursorImageBrush();
            }

            private CompositionBrush GetCursorImageBrush()
            {
                if (CanHorizontallyScroll && CanVerticallyScroll) return ScrollCursorHelper.ScrollAllImage;
                else if (CanHorizontallyScroll) return ScrollCursorHelper.ScrollWEImage;
                else if (CanVerticallyScroll) return ScrollCursorHelper.ScrollNSImage;
                else return null;
            }

            public void Dispose()
            {
                if (visual != null)
                {
                    visual.Brush = null;
                    if (adornedElement != null)
                    {
                        ElementCompositionPreview.SetElementChildVisual(adornedElement, null);
                        adornedElement = null;
                    }
                    visual.Dispose();
                    visual = null;
                }
            }
        }

        private class AppWindowListener : IDisposable
        {
            public AppWindow AppWindow { get; private set; }

            public InputPointerSource InputPointerSource { get; private set; }

            public InputKeyboardSource InputKeyboardSource { get; private set; }

            public WindowMessageMonitor WindowMessageMonitor { get; private set; }

            public XamlRoot XamlRoot { get; private set; }

            public ContentIsland ContentIsland { get; private set; }

            public static AppWindowListener TryCreate(UIElement element)
            {
                var xamlRoot = element?.XamlRoot;
                if (xamlRoot != null)
                {
                    var appWindow = AppWindow.GetFromWindowId(xamlRoot.ContentIslandEnvironment.AppWindowId);

                    if (appWindow != null)
                    {
                        var contentIsland = ContentIsland.GetByVisual(ElementCompositionPreview.GetElementVisual(element));
                        if (contentIsland == null)
                        {
                            contentIsland = ContentIsland.FindAllForCurrentThread()
                                .FirstOrDefault(c => c.Environment.AppWindowId == appWindow.Id);
                        }
                        if (contentIsland != null)
                        {

                            var inputPointerSource = InputPointerSource.GetForIsland(contentIsland);
                            var inputKeyboardSource = InputKeyboardSource.GetForIsland(contentIsland);

                            if (inputPointerSource != null && inputKeyboardSource != null)
                            {
                                return new AppWindowListener()
                                {
                                    AppWindow = appWindow,
                                    InputKeyboardSource = inputKeyboardSource,
                                    InputPointerSource = inputPointerSource,
                                    ContentIsland = contentIsland,
                                    XamlRoot = xamlRoot,
                                    WindowMessageMonitor = new WindowMessageMonitor(appWindow.Id)
                                };
                            }
                        }
                    }
                }

                return null;
            }

            public void Dispose()
            {
                AppWindow = null;
                InputKeyboardSource = null;
                InputPointerSource = null;
                ContentIsland = null;
                XamlRoot = null;
                WindowMessageMonitor?.Dispose();
                WindowMessageMonitor = null;
            }
        }

        private static class MouseEx
        {
            public static Point GetPosition(UIElement element)
            {
                if (element.XamlRoot != null && Windows.Win32.PInvoke.GetCursorPos(out var point))
                {
                    Windows.Win32.PInvoke.ScreenToClient(
                        (Windows.Win32.Foundation.HWND)Win32Interop.GetWindowFromWindowId(element.XamlRoot.ContentIslandEnvironment.AppWindowId),
                        ref point);

                    var scale = element.XamlRoot.RasterizationScale;
                    return element.XamlRoot.Content.TransformToVisual(element).TransformPoint(new Point(
                        x: point.X / scale,
                        y: point.Y / scale));
                }
                return default;
            }
        }

        private static class UIElementCursorHelper
        {
            public static InputCursor GetCursor(UIElement element)
            {
                using var reference = ((IWinRTObject)element).NativeObject.As(new Guid("8f69b9e9-1f00-5834-9bf1-a9257bed39f0"));
                return get_ProtectedCursor(reference);
            }

            public static void SetCursor(UIElement element, InputCursor value)
            {
                using var reference = ((IWinRTObject)element).NativeObject.As(new Guid("8f69b9e9-1f00-5834-9bf1-a9257bed39f0"));
                set_ProtectedCursor(reference, value);
            }

            private unsafe static global::Microsoft.UI.Input.InputCursor get_ProtectedCursor(IObjectReference _obj)
            {
                IntPtr thisPtr = _obj.ThisPtr;
                IntPtr intPtr = default(IntPtr);
                try
                {
                    ExceptionHelpers.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<IntPtr, out IntPtr, int>)(*(IntPtr*)((nint)(*(IntPtr*)(void*)thisPtr) + (nint)6 * (nint)sizeof(delegate* unmanaged[Stdcall]<IntPtr, out IntPtr, int>))))(thisPtr, out intPtr));
                    return ABI.Microsoft.UI.Input.InputCursor.FromAbi(intPtr);
                }
                finally
                {
                    ABI.Microsoft.UI.Input.InputCursor.DisposeAbi(intPtr);
                }
            }

            private unsafe static void set_ProtectedCursor(IObjectReference _obj, global::Microsoft.UI.Input.InputCursor value)
            {
                IntPtr thisPtr = _obj.ThisPtr;
                ObjectReferenceValue value2 = default(ObjectReferenceValue);
                try
                {
                    value2 = ABI.Microsoft.UI.Input.InputCursor.CreateMarshaler2(value);
                    ExceptionHelpers.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)(*(IntPtr*)((nint)(*(IntPtr*)(void*)thisPtr) + (nint)7 * (nint)sizeof(delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>))))(thisPtr, MarshalInspectable<object>.GetAbi(value2)));
                }
                finally
                {
                    MarshalInspectable<object>.DisposeMarshaler(value2);
                }
            }
        }

        private class WindowMessageMonitor : IDisposable
        {
            private bool disposeValue;
            private nuint id;
            private nint hWnd;
            private object locker = new object();
            private GCHandle thisHandle;

            public WindowMessageMonitor(WindowId windowId)
            {
                hWnd = Win32Interop.GetWindowFromWindowId(windowId);
            }

            private MessageReceivedEventHandler messageReceived;

            public event MessageReceivedEventHandler MessageReceived
            {
                add
                {
                    lock (locker)
                    {
                        messageReceived += value;
                        UpdateSubClass();
                    }
                }
                remove
                {
                    lock (locker)
                    {
                        messageReceived -= value;
                        UpdateSubClass();
                    }
                }
            }

            private unsafe void UpdateSubClass()
            {
                lock (locker)
                {
                    if (disposeValue || messageReceived == null)
                    {
                        if (id != 0)
                        {
                            if (RemoveWindowSubclass(
                                (Windows.Win32.Foundation.HWND)hWnd,
                                new SUBCLASSPROC()
                                {
                                    Func = &SubClassProcStatic
                                },
                                id))
                            {
                                id = 0;
                                thisHandle.Free();
                                thisHandle = default;
                            }
                            else
                            {
                                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                            }
                        }
                    }
                    else if (id == 0)
                    {
                        nuint subClassId = 200;
                        var gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);

                        while (true)
                        {
                            var res = SetWindowSubclass(
                                (Windows.Win32.Foundation.HWND)hWnd,
                                new SUBCLASSPROC()
                                {
                                    Func = &SubClassProcStatic
                                },
                                subClassId,
                                (nuint)GCHandle.ToIntPtr(gcHandle));

                            if (res)
                            {
                                id = subClassId;
                                thisHandle = gcHandle;
                                break;
                            }

                            subClassId++;
                        }
                    }
                }
            }

            public void Dispose()
            {
                if (!disposeValue)
                {
                    lock (locker)
                    {
                        if (!disposeValue)
                        {
                            disposeValue = true;

                            UpdateSubClass();
                        }
                    }
                }
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
            private static Windows.Win32.Foundation.LRESULT SubClassProcStatic(
                Windows.Win32.Foundation.HWND hWnd,
                uint uMsg,
                Windows.Win32.Foundation.WPARAM wParam,
                Windows.Win32.Foundation.LPARAM lParam,
                nuint uIdSubclass,
                nuint dwRefData)
            {
                if (dwRefData != 0)
                {
                    var gcHandle = GCHandle.FromIntPtr((nint)dwRefData);
                    if (gcHandle.Target is WindowMessageMonitor sender)
                    {
                        var handle = sender.messageReceived;
                        if (handle != null)
                        {
                            var args = new MessageReceivedEventArgs(uMsg, wParam.Value, lParam.Value);
                            handle.Invoke(sender, args);
                            if (args.Handled) return (Windows.Win32.Foundation.LRESULT)args.LResult;
                        }
                    }
                }

                return Windows.Win32.PInvoke.DefSubclassProc(hWnd, uMsg, wParam, lParam);
            }

            [DllImport("COMCTL32.dll", ExactSpelling = true)]
            private static extern Windows.Win32.Foundation.BOOL SetWindowSubclass(
                Windows.Win32.Foundation.HWND hWnd,
                SUBCLASSPROC pfnSubclass,
                nuint uIdSubclass,
                nuint dwRefData);

            [DllImport("COMCTL32.dll", ExactSpelling = true)]
            private static extern Windows.Win32.Foundation.BOOL RemoveWindowSubclass(
                Windows.Win32.Foundation.HWND hWnd,
                SUBCLASSPROC pfnSubclass,
                nuint uIdSubclass);

            private unsafe struct SUBCLASSPROC
            {
                public delegate* unmanaged[Stdcall]<Windows.Win32.Foundation.HWND, uint, Windows.Win32.Foundation.WPARAM, Windows.Win32.Foundation.LPARAM, nuint, nuint, Windows.Win32.Foundation.LRESULT> Func;
            }


            public delegate void MessageReceivedEventHandler(WindowMessageMonitor? sender, MessageReceivedEventArgs args);

            public class MessageReceivedEventArgs
            {
                public MessageReceivedEventArgs(uint messageId, nuint wParam, nint lParam)
                {
                    MessageId = messageId;
                    WParam = wParam;
                    LParam = lParam;
                }

                public uint MessageId { get; }

                public nuint WParam { get; }

                public nint LParam { get; }

                public nint LResult { get; set; }

                public bool Handled { get; set; }
            }
        }

        #endregion Nested Classes

    }
}
