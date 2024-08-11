using Microsoft.UI.Composition;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using WinRT;

namespace MouseMiddleButtonScroll.WinUI3.Helpers
{
    internal static class ScrollCursorHelper
    {
        private static ResourceManager resourceManager;
        private static IObjectReference obj_IInputCursorStaticsInterop;
        private static Dictionary<string, CustomCursor> customInputCursors = new Dictionary<string, CustomCursor>();

        [ThreadStatic]
        private static Dictionary<string, CompositionSurfaceBrush> inputCursorImageBrushes = new Dictionary<string, CompositionSurfaceBrush>();

        public static InputCursor ScrollNS => EnsureCustomCursor("ScrollNS", SystemCursorResourceId.ScrollNSCursor);
        public static InputCursor ScrollWE => EnsureCustomCursor("ScrollWE", SystemCursorResourceId.ScrollWECursor);
        public static InputCursor ScrollAll => EnsureCustomCursor("ScrollAll", SystemCursorResourceId.ScrollAllCursor);
        public static InputCursor ScrollN => EnsureCustomCursor("ScrollN", SystemCursorResourceId.ScrollNCursor);
        public static InputCursor ScrollS => EnsureCustomCursor("ScrollS", SystemCursorResourceId.ScrollSCursor);
        public static InputCursor ScrollW => EnsureCustomCursor("ScrollW", SystemCursorResourceId.ScrollWCursor);
        public static InputCursor ScrollE => EnsureCustomCursor("ScrollE", SystemCursorResourceId.ScrollECursor);
        public static InputCursor ScrollNW => EnsureCustomCursor("ScrollNW", SystemCursorResourceId.ScrollNWCursor);
        public static InputCursor ScrollNE => EnsureCustomCursor("ScrollNE", SystemCursorResourceId.ScrollNECursor);
        public static InputCursor ScrollSW => EnsureCustomCursor("ScrollSW", SystemCursorResourceId.ScrollSWCursor);
        public static InputCursor ScrollSE => EnsureCustomCursor("ScrollSE", SystemCursorResourceId.ScrollSECursor);


        public static CompositionBrush ScrollAllImage => EnsureInputCursorImage("ScrollAll");
        public static CompositionBrush ScrollNSImage => EnsureInputCursorImage("ScrollNS");
        public static CompositionBrush ScrollWEImage => EnsureInputCursorImage("ScrollWE");


        #region Ensure Resources

        private static InputCursor EnsureSystemInputCursor(int resourceId)
        {
            if (!SystemCursorResourceId.IsSystemCursorResourceId(resourceId))
                ArgumentNullException.ThrowIfNull((object)null, nameof(resourceId));

            lock (customInputCursors)
            {
                if (!customInputCursors.TryGetValue($"#{resourceId}", out var customCursor))
                {
                    var cursor = CreateSystemCursor(resourceId, out var hCursor);

                    customInputCursors[$"#{resourceId}"] = new CustomCursor(hCursor, cursor, resourceId);
                }
                return customCursor?.InputCursor;
            }
        }

        private static InputCursor EnsureCustomCursor(string resource, int defaultCursorResourceId)
        {
            lock (customInputCursors)
            {
                if (!customInputCursors.TryGetValue(resource, out var customCursor))
                {
                    var cursor = CreateCursorFromResource(resource, out var hCursor);
                    if (cursor != null)
                    {
                        customCursor = new CustomCursor(hCursor, cursor, 0);
                    }
                    else if (SystemCursorResourceId.IsSystemCursorResourceId(defaultCursorResourceId))
                    {
                        customCursor = new CustomCursor(default, EnsureSystemInputCursor(defaultCursorResourceId), defaultCursorResourceId);
                    }
                    customInputCursors[resource] = customCursor;
                }

                return customCursor?.InputCursor;
            }
        }

        private static CompositionSurfaceBrush EnsureInputCursorImage(string resource)
        {
            lock (inputCursorImageBrushes)
            {
                if (!inputCursorImageBrushes.TryGetValue(resource, out var brush))
                {
                    brush = CreateInputCursorImageBrush(resource);
                    inputCursorImageBrushes[resource] = brush;
                }

                return brush;
            }

        }

        #endregion Ensure Resources

        #region Create Resources

        private static CompositionSurfaceBrush CreateInputCursorImageBrush(
            string resource)
        {
            try
            {
                var bytes = GetResourceData($"/Files/{typeof(ScrollCursorHelper).Assembly.GetName().Name}/Assets/CursorImages/{resource}.png");
                if (bytes != null)
                {
                    var compositor = CompositionTarget.GetCompositorForCurrentThread();
                    var brush = compositor.CreateSurfaceBrush();
                    brush.Stretch = CompositionStretch.UniformToFill;
                    brush.HorizontalAlignmentRatio = 0.5f;
                    brush.VerticalAlignmentRatio = 0.5f;

                    compositor.DispatcherQueue.TryEnqueue(async () =>
                    {
                        var stream = new InMemoryRandomAccessStream();
                        await stream.WriteAsync(bytes.AsBuffer());
                        stream.Seek(0);

                        var surface = LoadedImageSurface.StartLoadFromStream(stream);
                        brush.Surface = surface;

                        surface.LoadCompleted += (s, a) =>
                        {
                            stream.Dispose();
                        };
                    });

                    return brush;
                }
            }
            catch { }
            return null;
        }

        private static unsafe InputCursor CreateCursorFromResource(string resource, out Windows.Win32.UI.WindowsAndMessaging.HCURSOR hCursor)
        {
            hCursor = default;
            var bytes = GetResourceData($"/Files/{typeof(ScrollCursorHelper).Assembly.GetName().Name}/Assets/Cursors/{resource}.cur");
            if (bytes != null)
            {
                try
                {
                    string filePath;
                    using (var fileStream = FileHelper.CreateAndOpenTemporaryFile(out filePath))
                    {
                        fileStream.Write(bytes, 0, bytes.Length);
                    }
                    var filePath2 = filePath + "\0";
                    fixed (char* filePathPtr = filePath2)
                    {
                        hCursor = (Windows.Win32.UI.WindowsAndMessaging.HCURSOR)Windows.Win32.PInvoke.LoadImage(
                            default,
                            filePathPtr,
                            Windows.Win32.UI.WindowsAndMessaging.GDI_IMAGE_TYPE.IMAGE_CURSOR,
                            0, 0,
                            Windows.Win32.UI.WindowsAndMessaging.IMAGE_FLAGS.LR_DEFAULTCOLOR
                            | Windows.Win32.UI.WindowsAndMessaging.IMAGE_FLAGS.LR_LOADFROMFILE
                            | Windows.Win32.UI.WindowsAndMessaging.IMAGE_FLAGS.LR_DEFAULTSIZE).Value;

                        return CreateInputCursorFromHCursor(hCursor);
                    }
                }
                finally { }
            }

            return null;
        }

        private static unsafe InputCursor CreateSystemCursor(int resourceId, out Windows.Win32.UI.WindowsAndMessaging.HCURSOR hCursor)
        {
            hCursor = Windows.Win32.PInvoke.LoadCursor(default, (char*)resourceId);
            return CreateInputCursorFromHCursor(hCursor);
        }

        private static unsafe InputCursor CreateInputCursorFromHCursor(nint hCursor)
        {
            if (obj_IInputCursorStaticsInterop == null)
            {
                using var obj_IInputCursorStatics = InputCursor.As<IWinRTObject>().NativeObject.As(new Guid("92F6A552-099F-55FB-8C31-E450284C9643"));
                obj_IInputCursorStaticsInterop = obj_IInputCursorStatics.As(new Guid("ac6f5065-90c4-46ce-beb7-05e138e54117"));
            }

            nint abi = 0;
            var thisPtr = obj_IInputCursorStaticsInterop.ThisPtr;
            var vtable = *(void***)thisPtr;

            // IInputCursorStaticsInterop.CreateFromHCursor
            var hr = ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)vtable[6])(thisPtr, hCursor, &abi);
            if (hr >= 0) return InputCursor.FromAbi(abi);

            return null;
        }

        #endregion Create Resources


        private static byte[] GetResourceData(string resource)
        {
            if (string.IsNullOrEmpty(resource)) return null;

            if (resourceManager == null)
            {
                resourceManager = new ResourceManager();
            }

            var resourceCandidate = resourceManager.MainResourceMap.TryGetValue(resource);

            if (resourceCandidate != null
                && (resourceCandidate.Kind == ResourceCandidateKind.FilePath
                    || resourceCandidate.Kind == ResourceCandidateKind.EmbeddedData))
            {
                return resourceCandidate.ValueAsBytes;
            }

            return null;
        }

        private class CustomCursor : IDisposable
        {
            public CustomCursor(
                Windows.Win32.UI.WindowsAndMessaging.HCURSOR hCursor,
                InputCursor inputCursor,
                int systemResourceId)
            {
                HCursor = hCursor;
                InputCursor = inputCursor;
                SystemResourceId = systemResourceId;
            }

            public Windows.Win32.UI.WindowsAndMessaging.HCURSOR HCursor { get; private set; }

            public InputCursor InputCursor { get; private set; }

            public int SystemResourceId { get; }

            public void Dispose()
            {
                InputCursor?.Dispose();
                InputCursor = null;

                if (!HCursor.IsNull)
                {
                    if (SystemResourceId == 0)
                    {
                        Windows.Win32.PInvoke.DestroyCursor(HCursor);
                    }
                    HCursor = default;
                }
            }
        }


        private static class FileHelper
        {
            private static bool? isPackagedApp;
            private static object locker = new object();

            internal static bool IsPackagedApp
            {
                get
                {
                    if (!isPackagedApp.HasValue)
                    {
                        lock (locker)
                        {
                            if (!isPackagedApp.HasValue)
                            {
                                var err = GetCurrentApplicationUserModelId(out _);
                                isPackagedApp = err != Windows.Win32.Foundation.WIN32_ERROR.APPMODEL_ERROR_NO_APPLICATION;
                            }
                        }
                    }
                    return isPackagedApp.Value;
                }
            }

            public static FileStream CreateAndOpenTemporaryFile(out string filePath)
            {
                const int MaxRetries = 5;
                int retries = MaxRetries;
                filePath = null;

                string folderPath = "";
                if (IsPackagedApp)
                {
                    try
                    {
                        folderPath = Windows.Storage.ApplicationData.Current.TemporaryFolder.Path;
                    }
                    catch { }
                }
                if (string.IsNullOrEmpty(folderPath)) folderPath = Path.GetTempPath();

                string subFolderPath = Path.Combine(folderPath, "ScrollCursorHelper");

                if (!Directory.Exists(subFolderPath))
                {
                    Directory.CreateDirectory(subFolderPath);
                }

                folderPath = subFolderPath;

                FileStream stream = null;
                while (stream == null)
                {
                    // build a candidate path name for the temp file
                    string path = Path.Combine(folderPath, Path.GetRandomFileName());

                    // try creating and opening the file
                    --retries;
                    try
                    {
                        const int DefaultBufferSize = 4096;    // so says FileStream doc
                                                               // mode must be CreateNew and share must be None, see discussion above
                        stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, DefaultBufferSize, FileOptions.None);

                        // success, report the path name to the caller
                        filePath = path;
                    }
                    catch (Exception e) when (retries > 0 && (e is IOException || e is UnauthorizedAccessException))
                    {
                        // failure - perhaps because a file with the candidate path name
                        // already exists.  Try again with a different candidate.
                        // If the failure happens too often, let the exception bubble out.
                    }
                }

                return stream;
            }

            private unsafe static Windows.Win32.Foundation.WIN32_ERROR GetCurrentApplicationUserModelId(out string applicationUserModelId)
            {
                applicationUserModelId = null;

                uint length = 0;

                var err = Windows.Win32.PInvoke.GetCurrentApplicationUserModelId(ref length, (char*)0);

                if (err == Windows.Win32.Foundation.WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
                {
                    var amuidBuffer = stackalloc char[(int)length];

                    err = Windows.Win32.PInvoke.GetCurrentApplicationUserModelId(ref length, amuidBuffer);

                    if (err == Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS)
                    {
                        applicationUserModelId = new string(amuidBuffer, 0, (int)length - 1);
                    }
                }

                return err;
            }
        }

        private static class SystemCursorResourceId
        {
            private static FrozenSet<int> allResourceIds = ((int[])
            [
                IDC_ARROW, IDC_IBEAM, IDC_WAIT, IDC_CROSS, IDC_UPARROW, IDC_SIZE, IDC_ICON, IDC_SIZENWSE, IDC_SIZENESW, IDC_SIZEWE, IDC_SIZENS, IDC_SIZEALL, IDC_NO, IDC_HAND, IDC_APPSTARTING, IDC_HELP, IDC_PIN, IDC_PERSON,
                PenCursor, ScrollNSCursor, ScrollWECursor, ScrollAllCursor, ScrollNCursor, ScrollSCursor, ScrollWCursor, ScrollECursor, ScrollNWCursor, ScrollNECursor, ScrollSWCursor, ScrollSECursor, ArrowCDCursor,
            ]).ToFrozenSet();

            public static bool IsSystemCursorResourceId(int resourceId) => allResourceIds.Contains(resourceId);

            public const int IDC_ARROW = 32512;
            public const int IDC_IBEAM = 32513;
            public const int IDC_WAIT = 32514;
            public const int IDC_CROSS = 32515;
            public const int IDC_UPARROW = 32516;
            public const int IDC_SIZE = 32640;
            public const int IDC_ICON = 32641;
            public const int IDC_SIZENWSE = 32642;
            public const int IDC_SIZENESW = 32643;
            public const int IDC_SIZEWE = 32644;
            public const int IDC_SIZENS = 32645;
            public const int IDC_SIZEALL = 32646;
            public const int IDC_NO = 32648;
            public const int IDC_HAND = 32649;
            public const int IDC_APPSTARTING = 32650;
            public const int IDC_HELP = 32651;
            public const int IDC_PIN = 32671;
            public const int IDC_PERSON = 32672;

            public const int PenCursor = IDC_ARROW + 119;
            public const int ScrollNSCursor = IDC_ARROW + 140;
            public const int ScrollWECursor = IDC_ARROW + 141;
            public const int ScrollAllCursor = IDC_ARROW + 142;
            public const int ScrollNCursor = IDC_ARROW + 143;
            public const int ScrollSCursor = IDC_ARROW + 144;
            public const int ScrollWCursor = IDC_ARROW + 145;
            public const int ScrollECursor = IDC_ARROW + 146;
            public const int ScrollNWCursor = IDC_ARROW + 147;
            public const int ScrollNECursor = IDC_ARROW + 148;
            public const int ScrollSWCursor = IDC_ARROW + 149;
            public const int ScrollSECursor = IDC_ARROW + 150;
            public const int ArrowCDCursor = IDC_ARROW + 151;
        }
    }
}
