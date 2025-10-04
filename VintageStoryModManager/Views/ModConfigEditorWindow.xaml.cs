using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
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
}
