using Microsoft.UI.Xaml;

namespace MouseMiddleButtonScroll.WinUI3
{
    public class MouseMiddleButtonScrollExtensions
    {
        public static bool GetShowCursorAtStartPoint(UIElement obj)
        {
            return (bool)obj.GetValue(ShowCursorAtStartPointProperty);
        }

        public static void SetShowCursorAtStartPoint(UIElement obj, bool value)
        {
            obj.SetValue(ShowCursorAtStartPointProperty, value);
        }

        public static readonly DependencyProperty ShowCursorAtStartPointProperty =
            DependencyProperty.RegisterAttached("ShowCursorAtStartPoint", typeof(bool), typeof(MouseMiddleButtonScrollExtensions), new PropertyMetadata(false, (s, a) =>
            {
                if (s is UIElement sender && !Equals(a.NewValue, a.OldValue))
                {
                    var helper = (MouseMiddleButtonScrollHelper)sender.GetValue(MouseMiddleButtonScrollHelperProperty);
                    if (helper != null)
                    {
                        helper.ShowCursorAtStartPoint = a.NewValue is true;
                    }
                }
            }));



        public static bool GetIsEnabled(UIElement obj)
        {
            return (bool)obj.GetValue(IsEnabledProperty);
        }

        public static void SetIsEnabled(UIElement obj, bool value)
        {
            obj.SetValue(IsEnabledProperty, value);
        }

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(MouseMiddleButtonScrollExtensions), new PropertyMetadata(false, (s, a) =>
            {
                if (s is UIElement sender && !Equals(a.NewValue, a.OldValue))
                {
                    sender.SetValue(MouseMiddleButtonScrollHelperProperty, null);
                    if (a.NewValue is true)
                    {
                        sender.SetValue(MouseMiddleButtonScrollHelperProperty, new MouseMiddleButtonScrollHelper(sender)
                        {
                            ShowCursorAtStartPoint = GetShowCursorAtStartPoint(sender),
                        });
                    }
                }
            }));


        private static readonly DependencyProperty MouseMiddleButtonScrollHelperProperty =
            DependencyProperty.RegisterAttached("MouseMiddleButtonScrollHelper", typeof(MouseMiddleButtonScrollHelper), typeof(MouseMiddleButtonScrollExtensions), new PropertyMetadata(null, (s, a) =>
            {
                if (a.OldValue is MouseMiddleButtonScrollHelper helper)
                {
                    helper.Dispose();
                }
            }));
    }
}
