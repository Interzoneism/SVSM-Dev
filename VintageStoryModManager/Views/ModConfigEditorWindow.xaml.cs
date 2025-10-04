using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VintageStoryModManager.ViewModels;
using WpfMessageBox = System.Windows.MessageBox;

namespace VintageStoryModManager.Views;

public partial class ModConfigEditorWindow : Window
{
    public ModConfigEditorWindow(ModConfigEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(EnsureFilePathDoesNotOverlapButtons));
    }

    private void EnsureFilePathDoesNotOverlapButtons()
    {
        if (FilePathTextBlock is null || ActionButtonsPanel is null)
        {
            return;
        }

        UpdateLayout();

        const int maxIterations = 3;
        for (int i = 0; i < maxIterations; i++)
        {
            Rect filePathBounds = GetElementBounds(FilePathTextBlock);
            Rect buttonsBounds = GetElementBounds(ActionButtonsPanel);

            if (filePathBounds.Right <= buttonsBounds.Left)
            {
                break;
            }

            double overlap = filePathBounds.Right - buttonsBounds.Left;
            if (overlap <= 0)
            {
                break;
            }

            double additionalWidth = overlap + 24; // Add some spacing to separate the elements.
            Width = Math.Max(Width + additionalWidth, MinWidth);

            UpdateLayout();
        }
    }

    private Rect GetElementBounds(FrameworkElement element)
    {
        if (!element.IsLoaded)
        {
            element.UpdateLayout();
        }

        GeneralTransform transform = element.TransformToAncestor(this);
        return transform.TransformBounds(new Rect(element.RenderSize));
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModConfigEditorViewModel viewModel)
        {
            return;
        }

        try
        {
            viewModel.Save();
            DialogResult = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        {
            WpfMessageBox.Show(this,
                $"Failed to save the configuration:\n{ex.Message}",
                "Edit Config",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void TreeView_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (sender is not DependencyObject dependencyObject)
        {
            return;
        }

        var scrollViewer = FindAncestorScrollViewer(dependencyObject);
        if (scrollViewer is null)
        {
            return;
        }

        if (e.Delta > 0)
        {
            scrollViewer.LineUp();
        }
        else if (e.Delta < 0)
        {
            scrollViewer.LineDown();
        }

        e.Handled = true;
    }

    private static ScrollViewer? FindAncestorScrollViewer(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void TreeViewItem_OnExpanded(object sender, RoutedEventArgs e)
    {
        if (sender is not TreeViewItem item)
        {
            return;
        }

        if (item.DataContext is not ModConfigArrayNodeViewModel)
        {
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => ExpandArrayChildren(item)));
    }

    private void TreeViewItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeViewItem item)
        {
            return;
        }

        if (item.DataContext is not ModConfigArrayNodeViewModel)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source)
        {
            if (FindAncestor<ToggleButton>(source) is not null)
            {
                return;
            }

            if (FindAncestor<System.Windows.Controls.Primitives.TextBoxBase>(source) is not null)
            {
                return;
            }
        }

        item.IsSelected = true;
        item.IsExpanded = !item.IsExpanded;
        e.Handled = true;
    }

    private void ExpandArrayChildren(TreeViewItem arrayItem)
    {
        if (arrayItem.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
        {
            arrayItem.UpdateLayout();

            if (arrayItem.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            {
                arrayItem.ItemContainerGenerator.StatusChanged += OnStatusChanged;
                return;
            }
        }

        foreach (object child in arrayItem.Items)
        {
            if (arrayItem.ItemContainerGenerator.ContainerFromItem(child) is TreeViewItem childItem
                && childItem.DataContext is ModConfigContainerNodeViewModel)
            {
                childItem.IsExpanded = true;
                ExpandArrayChildren(childItem);
            }
        }

        void OnStatusChanged(object? sender, EventArgs e)
        {
            if (arrayItem.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                arrayItem.ItemContainerGenerator.StatusChanged -= OnStatusChanged;
                ExpandArrayChildren(arrayItem);
            }
        }
    }

    private static T? FindAncestor<T>(DependencyObject current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            DependencyObject? parent = VisualTreeHelper.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent;
        }

        return null;
    }
}
