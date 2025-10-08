using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace VintageStoryModManager.Views.Behaviors;

public static class ScrollViewerAnimator
{
    public static readonly DependencyProperty AnimatedVerticalOffsetProperty = DependencyProperty.RegisterAttached(
        "AnimatedVerticalOffset",
        typeof(double),
        typeof(ScrollViewerAnimator),
        new PropertyMetadata(0d, OnAnimatedVerticalOffsetChanged));

    public static void SetAnimatedVerticalOffset(DependencyObject element, double value)
    {
        element.SetValue(AnimatedVerticalOffsetProperty, value);
    }

    public static double GetAnimatedVerticalOffset(DependencyObject element)
    {
        return (double)element.GetValue(AnimatedVerticalOffsetProperty);
    }

    public static void AnimateToOffset(ScrollViewer scrollViewer, double targetOffset, TimeSpan duration, IEasingFunction? easingFunction)
    {
        if (scrollViewer is null)
        {
            throw new ArgumentNullException(nameof(scrollViewer));
        }

        double currentOffset = scrollViewer.VerticalOffset;
        if (Math.Abs(currentOffset - targetOffset) < double.Epsilon)
        {
            return;
        }

        SetAnimatedVerticalOffset(scrollViewer, currentOffset);

        var animation = new DoubleAnimation
        {
            From = currentOffset,
            To = targetOffset,
            Duration = duration,
            EasingFunction = easingFunction,
        };

        scrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private static void OnAnimatedVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer scrollViewer && e.NewValue is double newOffset && !double.IsNaN(newOffset) && !double.IsInfinity(newOffset))
        {
            scrollViewer.ScrollToVerticalOffset(newOffset);
        }
    }
}
