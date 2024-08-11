using Microsoft.UI.Content;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using MouseMiddleButtonScroll.WinUI3.Helpers;
using System;
using System.Linq;

namespace MouseMiddleButtonScroll.WinUI3
{
    internal partial class MouseMiddleButtonScrollHelper
    {
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
    }
}
