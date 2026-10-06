using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Pouchy.Helpers;

namespace Pouchy.Views
{
    public enum MascotMood
    {
        /// <summary>Blinks and gently bobs.</summary>
        Idle,

        /// <summary>Something is being dragged over the pouch: mouth open, bouncing.</summary>
        Excited,

        /// <summary>Just got fed: ^ ^ eyes and a hop.</summary>
        Happy,

        /// <summary>Search found nothing.</summary>
        Puzzled,
    }

    /// <summary>Pouchy the mascot. Set <see cref="Mood"/>; animations respect "Reduce motion".</summary>
    public partial class Mascot : UserControl
    {
        public static readonly DependencyProperty MoodProperty = DependencyProperty.Register(
            nameof(Mood), typeof(MascotMood), typeof(Mascot), new PropertyMetadata(MascotMood.Idle, (d, _) => ((Mascot)d).ApplyMood()));

        /// <summary>When false the mascot holds still (used for the tiny header logo).</summary>
        public static readonly DependencyProperty AnimatedProperty = DependencyProperty.Register(
            nameof(Animated), typeof(bool), typeof(Mascot), new PropertyMetadata(true));

        private static readonly Random Random = new();
        private readonly DispatcherTimer _blinkTimer = new();

        public Mascot()
        {
            InitializeComponent();
            _blinkTimer.Tick += (_, _) => Blink();
            Loaded += (_, _) =>
            {
                ApplyMood();
                ScheduleBlink();
            };
            Unloaded += (_, _) => _blinkTimer.Stop();
            IsVisibleChanged += (_, _) =>
            {
                if (IsVisible) ApplyMood();
            };
        }

        public MascotMood Mood
        {
            get => (MascotMood)GetValue(MoodProperty);
            set => SetValue(MoodProperty, value);
        }

        public bool Animated
        {
            get => (bool)GetValue(AnimatedProperty);
            set => SetValue(AnimatedProperty, value);
        }

        private bool CanAnimate => Animated && Motion.Enabled && IsVisible;

        private void ApplyMood()
        {
            var mood = Mood;
            OpenEyes.Visibility = mood is MascotMood.Happy ? Visibility.Collapsed : Visibility.Visible;
            HappyEyes.Visibility = mood is MascotMood.Happy ? Visibility.Visible : Visibility.Collapsed;
            SmileMouth.Visibility = mood is MascotMood.Idle ? Visibility.Visible : Visibility.Collapsed;
            BigSmileMouth.Visibility = mood is MascotMood.Happy ? Visibility.Visible : Visibility.Collapsed;
            OpenMouth.Visibility = mood is MascotMood.Excited ? Visibility.Visible : Visibility.Collapsed;
            FlatMouth.Visibility = mood is MascotMood.Puzzled ? Visibility.Visible : Visibility.Collapsed;
            QuestionMark.Visibility = mood is MascotMood.Puzzled ? Visibility.Visible : Visibility.Collapsed;
            Sparkles.Visibility = mood is MascotMood.Excited ? Visibility.Visible : Visibility.Collapsed;

            StopMotion();
            if (!CanAnimate) return;

            switch (mood)
            {
                case MascotMood.Idle:
                    // A slow breathing bob.
                    Hop.BeginAnimation(TranslateTransform.YProperty, Loop(0, -2.5, 1300));
                    break;
                case MascotMood.Excited:
                    Hop.BeginAnimation(TranslateTransform.YProperty, Loop(0, -6, 260));
                    Squash.BeginAnimation(ScaleTransform.ScaleYProperty, Loop(0.94, 1.04, 260));
                    break;
                case MascotMood.Happy:
                    var hop = new DoubleAnimationUsingKeyFrames { Duration = Motion.Duration(700) };
                    hop.KeyFrames.Add(new EasingDoubleKeyFrame(-12, KeyTime.FromPercent(0.35), new CubicEase { EasingMode = EasingMode.EaseOut }));
                    hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0.7), new BounceEase { Bounces = 1, Bounciness = 3 }));
                    hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1)));
                    Hop.BeginAnimation(TranslateTransform.YProperty, hop);
                    Tilt.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-6, 0, Motion.Duration(700))
                    {
                        EasingFunction = new ElasticEase { Oscillations = 2, Springiness = 4 },
                    });
                    break;
                case MascotMood.Puzzled:
                    Tilt.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, -8, Motion.Duration(400))
                    {
                        EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut },
                    });
                    break;
            }
        }

        private void StopMotion()
        {
            Hop.BeginAnimation(TranslateTransform.YProperty, null);
            Squash.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            Tilt.BeginAnimation(RotateTransform.AngleProperty, null);
            Hop.Y = 0;
            Squash.ScaleY = 1;
            Tilt.Angle = 0;
        }

        private static DoubleAnimation Loop(double from, double to, double milliseconds) => new(from, to, Motion.Duration(milliseconds))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };

        private void ScheduleBlink()
        {
            _blinkTimer.Interval = TimeSpan.FromMilliseconds(Random.Next(2400, 5600));
            _blinkTimer.Start();
        }

        private void Blink()
        {
            _blinkTimer.Stop();
            if (CanAnimate && Mood is MascotMood.Idle or MascotMood.Excited)
            {
                var blink = new DoubleAnimation(1, 0.1, TimeSpan.FromMilliseconds(80)) { AutoReverse = true };
                LeftBlink.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
                RightBlink.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
            }
            ScheduleBlink();
        }
    }
}
