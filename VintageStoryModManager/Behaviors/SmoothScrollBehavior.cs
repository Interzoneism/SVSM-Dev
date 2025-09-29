using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace VintageStoryModManager.Behaviors
{
    /// <summary>
    /// Eases mouse-wheel scrolling on a ScrollViewer (subtle and snappy).
    /// Attach via a Style (recommended) or per ScrollViewer.
    /// </summary>
    public static class SmoothScrollBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(SmoothScrollBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);
        public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ScrollViewer sv) return;

            if ((bool)e.NewValue)
            {
                sv.PreviewMouseWheel += OnPreviewMouseWheel;
                sv.CanContentScroll = false; // pixel-based for smooth anims
            }
            else
            {
                sv.PreviewMouseWheel -= OnPreviewMouseWheel;
            }
        }

        private sealed class OffsetAnimator : Animatable
        {
            public static readonly DependencyProperty ValueProperty =
                DependencyProperty.Register(nameof(Value), typeof(double), typeof(OffsetAnimator),
                    new PropertyMetadata(0.0, OnValueChanged));

            private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                if (d is not OffsetAnimator a || a._target is null) return;
                a._target.ScrollToVerticalOffset((double)e.NewValue);
            }

            public double Value
            {
                get => (double)GetValue(ValueProperty);
                set => SetValue(ValueProperty, value);
            }

            public void Attach(ScrollViewer sv) => _target = sv;
            private ScrollViewer? _target;

            // Required by Freezable/Animatable
            protected override Freezable CreateInstanceCore() => new OffsetAnimator();
        }


        private static readonly DependencyProperty AnimatorProperty =
            DependencyProperty.RegisterAttached(
                "Animator",
                typeof(OffsetAnimator),
                typeof(SmoothScrollBehavior));

        private static OffsetAnimator GetOrCreateAnimator(ScrollViewer sv)
        {
            var a = (OffsetAnimator?)sv.GetValue(AnimatorProperty);
            if (a == null)
            {
                a = new OffsetAnimator();
                a.Attach(sv);
                sv.SetValue(AnimatorProperty, a);
            }
            return a;
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;

            // One wheel notch ~120; keep motion small & quick
            double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 200 : 120;
            double current = sv.VerticalOffset;
            double target = current - Math.Sign(e.Delta) * step;
            target = Math.Max(0, Math.Min(target, sv.ScrollableHeight));

            var animator = GetOrCreateAnimator(sv);
            animator.Value = current;

            var anim = new DoubleAnimation
            {
                From = current,
                To = target,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            animator.BeginAnimation(OffsetAnimator.ValueProperty, anim, HandoffBehavior.SnapshotAndReplace);
            e.Handled = true;
        }
    }
}
