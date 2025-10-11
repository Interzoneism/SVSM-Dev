using System.Linq;
using System.Windows;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Services;

public static class ModManagerMessageBox
{
    public static MessageBoxResult Show(
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageDialogExtraButton? extraButton = null)
    {
        return ShowInternal(null, messageBoxText, caption, button, icon, extraButton);
    }

    public static MessageBoxResult Show(
        Window owner,
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageDialogExtraButton? extraButton = null)
    {
        return ShowInternal(owner, messageBoxText, caption, button, icon, extraButton);
    }

    private static MessageBoxResult ShowInternal(
        Window? owner,
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageDialogExtraButton? extraButton)
    {
        Window? resolvedOwner = owner ?? GetActiveWindow();

        var dialog = new MessageDialogWindow();

        if (IsEligibleOwner(resolvedOwner))
        {
            dialog.Owner = resolvedOwner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Topmost = true;
        }

        dialog.Initialize(messageBoxText, caption, button, icon, extraButton);
        _ = dialog.ShowDialog();
        return dialog.Result;
    }

    private static bool IsEligibleOwner(Window? window)
    {
        return window is { IsLoaded: true };
    }

    private static Window? GetActiveWindow()
    {
        if (System.Windows.Application.Current == null)
        {
            return null;
        }

        return System.Windows.Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? System.Windows.Application.Current.MainWindow;
    }
}
