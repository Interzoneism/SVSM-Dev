using System;
using System.Windows;
using WpfPanel = System.Windows.Controls.Panel;
using Size = System.Windows.Size;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace VintageStoryModManager.Views;

public class OverlappingTagPanel : WpfPanel
{
    public OverlappingTagPanel()
    {
        ClipToBounds = true;
    }

    public static readonly DependencyProperty MaxSpacingProperty = DependencyProperty.Register(
        nameof(MaxSpacing),
        typeof(double),
        typeof(OverlappingTagPanel),
        new FrameworkPropertyMetadata(6d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double MaxSpacing
    {
        get => (double)GetValue(MaxSpacingProperty);
        set => SetValue(MaxSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double totalWidth = 0d;
        double maxHeight = 0d;

        foreach (UIElement child in InternalChildren)
        {
            if (child is null)
            {
                continue;
            }

            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            totalWidth += child.DesiredSize.Width;
            if (child.DesiredSize.Height > maxHeight)
            {
                maxHeight = child.DesiredSize.Height;
            }
        }

        int gapCount = Math.Max(InternalChildren.Count - 1, 0);
        double desiredWidth = totalWidth + (MaxSpacing * gapCount);
        double constrainedWidth = double.IsInfinity(availableSize.Width) ? desiredWidth : Math.Min(desiredWidth, availableSize.Width);
        double constrainedHeight = double.IsInfinity(availableSize.Height) ? maxHeight : Math.Min(maxHeight, availableSize.Height);

        return new Size(constrainedWidth, constrainedHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int childCount = InternalChildren.Count;
        if (childCount == 0)
        {
            return finalSize;
        }

        double[] widths = new double[childCount];
        double totalWidth = 0d;
        double maxHeight = 0d;

        for (int i = 0; i < childCount; i++)
        {
            UIElement child = InternalChildren[i];
            if (child is null)
            {
                continue;
            }

            widths[i] = child.DesiredSize.Width;
            totalWidth += widths[i];
            if (child.DesiredSize.Height > maxHeight)
            {
                maxHeight = child.DesiredSize.Height;
            }
        }

        double availableWidth = double.IsInfinity(finalSize.Width)
            ? totalWidth + (MaxSpacing * Math.Max(childCount - 1, 0))
            : finalSize.Width;

        double spacing = childCount > 1 ? (availableWidth - totalWidth) / (childCount - 1) : 0d;
        if (double.IsNaN(spacing) || double.IsInfinity(spacing))
        {
            spacing = 0d;
        }

        spacing = Math.Min(MaxSpacing, spacing);

        double x = 0d;
        for (int i = 0; i < childCount; i++)
        {
            UIElement child = InternalChildren[i];
            if (child is null)
            {
                continue;
            }

            double childWidth = widths[i];
            double childHeight = child.DesiredSize.Height;
            double y = Math.Max(0d, (finalSize.Height - childHeight) / 2d);

            child.Arrange(new Rect(new Point(x, y), new Size(childWidth, childHeight)));
            WpfPanel.SetZIndex(child, childCount - i);

            if (i < childCount - 1)
            {
                double nextX = x + childWidth + spacing;
                x = Math.Max(x, nextX);
            }
        }

        return finalSize;
    }
}
