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

        /// <summary>
        /// Grows the element by this factor while the mouse is over it (a tile "lifting" towards
        /// you), with a slight spring. Off when motion is reduced.
        /// </summary>
        public static readonly DependencyProperty HoverLiftProperty = DependencyProperty.RegisterAttached(
            "HoverLift", typeof(double), typeof(Motion), new PropertyMetadata(1.0, OnHoverLiftChanged));

        public static double GetHoverLift(DependencyObject element) => (double)element.GetValue(HoverLiftProperty);

        public static void SetHoverLift(DependencyObject element, double value) => element.SetValue(HoverLiftProperty, value);

        private static void OnHoverLiftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element) return;
            element.MouseEnter -= OnLiftEnter;
            element.MouseLeave -= OnLiftLeave;
            if (e.NewValue is double lift && Math.Abs(lift - 1) > 0.001)
            {
                element.MouseEnter += OnLiftEnter;
                element.MouseLeave += OnLiftLeave;
            }
        }

        private static void OnLiftEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (Enabled) Lift((FrameworkElement)sender, GetHoverLift((FrameworkElement)sender), 240, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 });
        }

        private static void OnLiftLeave(object sender, System.Windows.Input.MouseEventArgs e) =>
            Lift((FrameworkElement)sender, 1, 180, new CubicEase { EasingMode = EasingMode.EaseOut });

        private static void Lift(FrameworkElement element, double to, double ms, IEasingFunction ease)
        {
            if (element.RenderTransform is not ScaleTransform scale || scale.IsFrozen)
            {
                if (to == 1) return; // Never lifted.
                scale = new ScaleTransform(1, 1);
                element.RenderTransformOrigin = new Point(0.5, 0.5);
                element.RenderTransform = scale;
            }
            var duration = Enabled ? Duration(ms) : new Duration(TimeSpan.Zero);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, duration) { EasingFunction = ease });
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
