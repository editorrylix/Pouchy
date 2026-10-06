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
        /// <summary>Breathes, blinks, glances around, wiggles now and then.</summary>
        Idle,

        /// <summary>Something is being dragged over the pouch: mouth open, bouncing, sparkles.</summary>
        Excited,

        /// <summary>Just got fed: a chomp, ^ ^ eyes, a hop and a floating heart.</summary>
        Happy,

        /// <summary>Search found nothing: head tilt and a question mark.</summary>
        Puzzled,
    }

    /// <summary>Pouchy the mascot. Set <see cref="Mood"/>; animations respect "Reduce motion".</summary>
    public partial class Mascot : UserControl
    {
        public static readonly DependencyProperty MoodProperty = DependencyProperty.Register(
            nameof(Mood), typeof(MascotMood), typeof(Mascot), new PropertyMetadata(MascotMood.Idle, (d, _) => ((Mascot)d).OnMoodChanged()));

        /// <summary>When false the mascot holds still.</summary>
        public static readonly DependencyProperty AnimatedProperty = DependencyProperty.Register(
            nameof(Animated), typeof(bool), typeof(Mascot), new PropertyMetadata(true));

        private static readonly Random Random = new();
        private readonly DispatcherTimer _idleTimer = new();
        private readonly DispatcherTimer _chompTimer = new() { Interval = TimeSpan.FromMilliseconds(170) };
        private int _idleTicks;

        public Mascot()
        {
            InitializeComponent();
            _idleTimer.Tick += (_, _) => IdleFidget();
            _chompTimer.Tick += (_, _) =>
            {
                _chompTimer.Stop();
                ShowFace(MascotMood.Happy);
            };
            Loaded += (_, _) =>
            {
                ApplyMood(pop: false);
                ScheduleIdle();
            };
            Unloaded += (_, _) =>
            {
                _idleTimer.Stop();
                _chompTimer.Stop();
            };
            IsVisibleChanged += (_, _) =>
            {
                if (IsVisible) ApplyMood(pop: false);
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

        /// <summary>A springy pop-in, e.g. when the drop overlay appears.</summary>
        public void PopIn()
        {
            if (!CanAnimate) return;
            var ease = new ElasticEase { Oscillations = 1, Springiness = 5, EasingMode = EasingMode.EaseOut };
            Squash.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.4, 1, Motion.Duration(450)) { EasingFunction = ease });
            Squash.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.4, 1, Motion.Duration(450)) { EasingFunction = ease },
                HandoffBehavior.SnapshotAndReplace);
        }

        private void OnMoodChanged() => ApplyMood(pop: true);

        private void ApplyMood(bool pop)
        {
            StopMotion();
            _chompTimer.Stop();

            // Fed: show an open mouth for a moment (the "chomp"), then the happy face.
            if (Mood == MascotMood.Happy && CanAnimate)
            {
                ShowFace(MascotMood.Excited);
                Sparkles.Visibility = Visibility.Collapsed;
                _chompTimer.Start();
            }
            else
            {
                ShowFace(Mood);
            }

            if (!CanAnimate) return;
            if (pop) Pulse();

            switch (Mood)
            {
                case MascotMood.Idle:
                    Hop.BeginAnimation(TranslateTransform.YProperty, Loop(0, -2, 1400));
                    break;
                case MascotMood.Excited:
                    BounceForever();
                    Sparkles.BeginAnimation(OpacityProperty, Loop(0.35, 1, 300));
                    break;
                case MascotMood.Happy:
                    HappyHop();
                    FloatHeart();
                    break;
                case MascotMood.Puzzled:
                    Tilt.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, -9, Motion.Duration(450))
                    {
                        EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 },
                    });
                    Glance.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, -2, Motion.Duration(450)));
                    QuestionMark.BeginAnimation(Canvas.TopProperty, Loop(4, 1, 700));
                    break;
            }
        }

        private void ShowFace(MascotMood mood)
        {
            OpenEyes.Visibility = mood is MascotMood.Happy ? Visibility.Collapsed : Visibility.Visible;
            HappyEyes.Visibility = mood is MascotMood.Happy ? Visibility.Visible : Visibility.Collapsed;
            SmileMouth.Visibility = mood is MascotMood.Idle ? Visibility.Visible : Visibility.Collapsed;
            BigSmileMouth.Visibility = mood is MascotMood.Happy ? Visibility.Visible : Visibility.Collapsed;
            OpenMouth.Visibility = mood is MascotMood.Excited ? Visibility.Visible : Visibility.Collapsed;
            FlatMouth.Visibility = mood is MascotMood.Puzzled ? Visibility.Visible : Visibility.Collapsed;
            QuestionMark.Visibility = mood is MascotMood.Puzzled ? Visibility.Visible : Visibility.Collapsed;
            Sparkles.Visibility = mood is MascotMood.Excited ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---------------------------------------------------------------- Animations

        /// <summary>A quick squash when the mood changes, so every change feels alive.</summary>
        private void Pulse()
        {
            var ease = new ElasticEase { Oscillations = 2, Springiness = 6, EasingMode = EasingMode.EaseOut };
            Squash.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.14, 1, Motion.Duration(420)) { EasingFunction = ease });
            Squash.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.86, 1, Motion.Duration(420)) { EasingFunction = ease });
        }

        /// <summary>Hopping on the spot with squash on landing and stretch in the air.</summary>
        private void BounceForever()
        {
            var period = Motion.Duration(440);
            Hop.BeginAnimation(TranslateTransform.YProperty, KeyFrames(period, true, (0, 0), (0.15, 0), (0.5, -9), (0.85, 0), (1, 0)));
            Squash.BeginAnimation(ScaleTransform.ScaleYProperty, KeyFrames(period, true, (0, 0.9), (0.15, 1.0), (0.5, 1.06), (0.85, 1.0), (1, 0.9)));
            Squash.BeginAnimation(ScaleTransform.ScaleXProperty, KeyFrames(period, true, (0, 1.1), (0.15, 1.0), (0.5, 0.95), (0.85, 1.0), (1, 1.1)));
        }

        private void HappyHop()
        {
            var period = Motion.Duration(900);
            Hop.BeginAnimation(TranslateTransform.YProperty, KeyFrames(period, false, (0, 0), (0.2, 0), (0.45, -14), (0.7, 0), (0.82, -3), (1, 0)));
            Squash.BeginAnimation(ScaleTransform.ScaleYProperty, KeyFrames(period, false, (0, 1), (0.2, 0.85), (0.45, 1.08), (0.7, 0.88), (0.85, 1.02), (1, 1)));
            Squash.BeginAnimation(ScaleTransform.ScaleXProperty, KeyFrames(period, false, (0, 1), (0.2, 1.15), (0.45, 0.94), (0.7, 1.12), (0.85, 0.99), (1, 1)));
            Tilt.BeginAnimation(RotateTransform.AngleProperty, KeyFrames(period, false, (0, 0), (0.45, -5), (0.7, 3), (1, 0)));
        }

        private void FloatHeart()
        {
            var period = Motion.Duration(1100);
            Heart.BeginAnimation(OpacityProperty, KeyFrames(period, false, (0, 0), (0.25, 0), (0.4, 1), (0.8, 1), (1, 0)));
            HeartFloat.BeginAnimation(TranslateTransform.YProperty, KeyFrames(period, false, (0, 6), (0.25, 6), (1, -16)));
        }

        /// <summary>Every few seconds while idle: blink, sometimes glance or wiggle.</summary>
        private void IdleFidget()
        {
            _idleTimer.Stop();
            if (CanAnimate && Mood is MascotMood.Idle or MascotMood.Excited)
            {
                _idleTicks++;
                Blink();
                if (Mood == MascotMood.Idle && _idleTicks % 3 == 0) Look();
                if (Mood == MascotMood.Idle && _idleTicks % 5 == 0) Wiggle();
            }
            ScheduleIdle();
        }

        private void Blink()
        {
            var blink = new DoubleAnimation(1, 0.1, TimeSpan.FromMilliseconds(70)) { AutoReverse = true };
            LeftBlink.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
            RightBlink.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
        }

        private void Look()
        {
            double direction = Random.Next(2) == 0 ? -2.2 : 2.2;
            var period = Motion.Duration(1400);
            Glance.BeginAnimation(TranslateTransform.XProperty, KeyFrames(period, false, (0, 0), (0.15, direction), (0.8, direction), (1, 0)));
        }

        private void Wiggle()
        {
            Tilt.BeginAnimation(RotateTransform.AngleProperty,
                KeyFrames(Motion.Duration(600), false, (0, 0), (0.2, -6), (0.45, 5), (0.7, -3), (1, 0)));
        }

        private void ScheduleIdle()
        {
            _idleTimer.Interval = TimeSpan.FromMilliseconds(Random.Next(2200, 4800));
            _idleTimer.Start();
        }

        private void StopMotion()
        {
            Hop.BeginAnimation(TranslateTransform.YProperty, null);
            Squash.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            Squash.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            Tilt.BeginAnimation(RotateTransform.AngleProperty, null);
            Glance.BeginAnimation(TranslateTransform.XProperty, null);
            Sparkles.BeginAnimation(OpacityProperty, null);
            QuestionMark.BeginAnimation(Canvas.TopProperty, null);
            Heart.BeginAnimation(OpacityProperty, null);
            HeartFloat.BeginAnimation(TranslateTransform.YProperty, null);
            Hop.Y = 0;
            Squash.ScaleX = Squash.ScaleY = 1;
            Tilt.Angle = 0;
            Glance.X = 0;
            Sparkles.Opacity = 1;
            Heart.Opacity = 0;
        }

        private static DoubleAnimation Loop(double from, double to, double milliseconds) => new(from, to, Motion.Duration(milliseconds))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };

        /// <summary>Smooth keyframes at (fraction of the duration, value) pairs.</summary>
        private static DoubleAnimationUsingKeyFrames KeyFrames(Duration duration, bool forever, params (double At, double Value)[] frames)
        {
            var animation = new DoubleAnimationUsingKeyFrames { Duration = duration };
            if (forever) animation.RepeatBehavior = RepeatBehavior.Forever;
            foreach (var (at, value) in frames)
            {
                animation.KeyFrames.Add(new EasingDoubleKeyFrame(value, KeyTime.FromPercent(at), new SineEase { EasingMode = EasingMode.EaseInOut }));
            }
            return animation;
        }
    }
}
