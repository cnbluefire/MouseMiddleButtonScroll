using Microsoft.UI;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MouseMiddleButtonScroll.WinUI3.Helpers
{
    internal class WindowMessageMonitor : IDisposable
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


        public delegate void MessageReceivedEventHandler(WindowMessageMonitor sender, MessageReceivedEventArgs args);

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
}