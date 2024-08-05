using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MouseMiddleButtonScroll.Wpf
{
    public class ScrollViewerExtensions
    {
        public static bool GetIsMouseMiddleButtonScrollEnabled(ScrollViewer obj)
        {
            return (bool)obj.GetValue(IsMouseMiddleButtonScrollEnabledProperty);
        }

        public static void SetIsMouseMiddleButtonScrollEnabled(ScrollViewer obj, bool value)
        {
            obj.SetValue(IsMouseMiddleButtonScrollEnabledProperty, value);
        }

        public static readonly DependencyProperty IsMouseMiddleButtonScrollEnabledProperty =
            DependencyProperty.RegisterAttached("IsMouseMiddleButtonScrollEnabled", typeof(bool), typeof(MouseMiddleButtonScrollHelper), new PropertyMetadata(false, (s, a) =>
            {
                if (s is ScrollViewer sender && !Equals(a.NewValue, a.OldValue))
                {
                    sender.SetValue(MouseMiddleButtonScrollHelperProperty, null);
                    if(a.NewValue is true)
                    {
                        sender.SetValue(MouseMiddleButtonScrollHelperProperty, new MouseMiddleButtonScrollHelper(sender));
                    }
                }
            }));


        private static readonly DependencyProperty MouseMiddleButtonScrollHelperProperty =
            DependencyProperty.RegisterAttached("MouseMiddleButtonScrollHelper", typeof(MouseMiddleButtonScrollHelper), typeof(ScrollViewerExtensions), new PropertyMetadata(null, (s, a) =>
            {
                if (a.OldValue is MouseMiddleButtonScrollHelper helper)
                {
                    helper.Dispose();
                }
            }));
    }
}
