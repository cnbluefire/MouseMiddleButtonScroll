using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MouseMiddleButtonScroll.Wpf
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

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
    }
}
