using System;

namespace MouseMiddleButtonScroll.Wpf
{
    internal class Program
    {
        [STAThread]
        public static void Main()
        {
            MouseMiddleButtonScroll.Wpf.App app = new MouseMiddleButtonScroll.Wpf.App();
            app.InitializeComponent();
            app.Run();
        }

    }
}
