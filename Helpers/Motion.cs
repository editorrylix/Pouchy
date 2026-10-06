using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Pouchy.Helpers
{
    /// <summary>
    /// App-wide animation switches, plus an attached property that fades/scales an
    /// element in when it first appears (used for newly added tiles).
    /// </summary>
    public static class Motion
    {
        /// <summary>False when the user turned on "Reduce motion".</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>Speed multiplier from settings; 2 halves every duration.</summary>
        public static double Speed { get; set; } = 1.0;

        public static Duration Duration(double milliseconds) =>
            TimeSpan.FromMilliseconds(milliseconds / Math.Clamp(Speed, 0.25, 4));

        public static readonly DependencyProperty AnimateOnLoadProperty = DependencyProperty.RegisterAttached(
            "AnimateOnLoad", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnAnimateOnLoadChanged));

        public static bool GetAnimateOnLoad(DependencyObject element) => (bool)element.GetValue(AnimateOnLoadProperty);

        public static void SetAnimateOnLoad(DependencyObject element, bool value) => element.SetValue(AnimateOnLoadProperty, value);

        private static void OnAnimateOnLoadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && e.NewValue is true)
            {
                element.Loaded += OnLoaded;
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            var element = (FrameworkElement)sender;
            element.Loaded -= OnLoaded; // Only the first appearance.
            if (!Enabled) return;

            var scale = new ScaleTransform(0.9, 0.9);
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = scale;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, Duration(220)) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.9, 1, Duration(260)) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.9, 1, Duration(260)) { EasingFunction = ease });
        }
    }
}
