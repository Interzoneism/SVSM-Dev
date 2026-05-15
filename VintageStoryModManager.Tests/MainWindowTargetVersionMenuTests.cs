using System.Windows.Controls;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class MainWindowTargetVersionMenuTests
{
    [Fact]
    public void ApplyTargetGameVersionMenuCheckedStateUnchecksSiblingVersions()
    {
        RunOnStaThread(() =>
        {
            var parent = new MenuItem();
            var autoItem = new MenuItem { Tag = "__target_auto__", IsCheckable = true, IsChecked = false };
            var latestItem = new MenuItem { Tag = TargetGameVersionPreference.Latest, IsCheckable = true, IsChecked = true };
            var oldVersionItem = new MenuItem { Tag = "1.21", IsCheckable = true, IsChecked = true };
            var newVersionItem = new MenuItem { Tag = "1.22", IsCheckable = true, IsChecked = true };
            var customItem = new MenuItem { Tag = "__target_custom__", IsCheckable = false, IsChecked = false };

            parent.Items.Add(autoItem);
            parent.Items.Add(latestItem);
            parent.Items.Add(new Separator());
            parent.Items.Add(oldVersionItem);
            parent.Items.Add(newVersionItem);
            parent.Items.Add(customItem);

            MainWindow.ApplyTargetGameVersionMenuCheckedState(newVersionItem);

            Assert.False(autoItem.IsChecked);
            Assert.False(latestItem.IsChecked);
            Assert.False(oldVersionItem.IsChecked);
            Assert.True(newVersionItem.IsChecked);
            Assert.False(customItem.IsChecked);
        });
    }

    [Fact]
    public void ApplyTargetGameVersionMenuCheckedStateCanSelectLatest()
    {
        RunOnStaThread(() =>
        {
            var parent = new MenuItem();
            var autoItem = new MenuItem { Tag = "__target_auto__", IsCheckable = true, IsChecked = true };
            var latestItem = new MenuItem { Tag = TargetGameVersionPreference.Latest, IsCheckable = true, IsChecked = false };
            var versionItem = new MenuItem { Tag = "1.22", IsCheckable = true, IsChecked = true };

            parent.Items.Add(autoItem);
            parent.Items.Add(latestItem);
            parent.Items.Add(versionItem);

            MainWindow.ApplyTargetGameVersionMenuCheckedState(latestItem);

            Assert.False(autoItem.IsChecked);
            Assert.True(latestItem.IsChecked);
            Assert.False(versionItem.IsChecked);
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null) throw exception;
    }
}
