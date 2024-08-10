using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.Win32.Foundation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MouseMiddleButtonScroll.WinUI3
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            var dpi = Windows.Win32.PInvoke.GetDpiForWindow((HWND)Win32Interop.GetWindowFromWindowId(this.AppWindow.Id));
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(
                (int)(500 * dpi / 96),
                (int)(500 * dpi / 96)));

            var rnd = new Random();

            var colors = typeof(Colors)
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(c => c.PropertyType == typeof(Color))
                .Select(c => (Color)c.GetValue(null))
                .OrderBy(c => rnd.Next())
                .ToArray();

            const int columnCount = 12;
            const double itemWidth = 100;
            const double itemHeight = 100;

            ContentCanvas.Width = 0;
            ContentCanvas.Height = 0;

            for (int i = 0; i < colors.Length; i++)
            {
                var x = i % columnCount * itemWidth;
                var y = i / columnCount * itemHeight;
                var rect = new Rectangle()
                {
                    Width = itemWidth,
                    Height = itemHeight,
                    Fill = new SolidColorBrush(colors[i])
                };
                ContentCanvas.Children.Add(rect);
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);

                ContentCanvas.Width = Math.Max(ContentCanvas.Width, x + itemWidth);
                ContentCanvas.Height = Math.Max(ContentCanvas.Height, y + itemHeight);
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ContentScrollViewer == null) return;
            switch (((ComboBox)sender).SelectedIndex)
            {
                case 1:
                    ContentScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
                    ContentScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    break;

                case 2:
                    ContentScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    ContentScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                    break;

                default:
                    ContentScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
                    ContentScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                    break;
            }
        }
    }
}
