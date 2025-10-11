using System;
using System.Globalization;
using System.Windows;

namespace VintageStoryModManager.Views.Dialogs;

public partial class InstallIncludedConfigsDialog : Window
{
    public InstallIncludedConfigsDialog(string snapshotType, string snapshotName, int configCount)
    {
        InitializeComponent();

        string typeDisplay = string.IsNullOrWhiteSpace(snapshotType)
            ? "preset"
            : snapshotType;
        string nameDisplay = string.IsNullOrWhiteSpace(snapshotName)
            ? typeDisplay
            : snapshotName;

        Title = "Install Config Files";
        MessageTextBlock.Text = string.Create(
            CultureInfo.CurrentCulture,
            $"The {typeDisplay} \"{nameDisplay}\" includes {configCount} mod config file(s). Do you want to install them?");
    }

    public IncludedConfigInstallDecision Decision { get; private set; } = IncludedConfigInstallDecision.None;

    private void SetDecision(IncludedConfigInstallDecision decision)
    {
        Decision = decision;
        DialogResult = true;
    }

    private void OnYesClick(object sender, RoutedEventArgs e) => SetDecision(IncludedConfigInstallDecision.Yes);

    private void OnNoClick(object sender, RoutedEventArgs e) => SetDecision(IncludedConfigInstallDecision.No);

    private void OnYesAlwaysClick(object sender, RoutedEventArgs e) => SetDecision(IncludedConfigInstallDecision.YesAlways);

    private void OnNoAlwaysClick(object sender, RoutedEventArgs e) => SetDecision(IncludedConfigInstallDecision.NoAlways);

    protected override void OnClosed(EventArgs e)
    {
        if (Decision == IncludedConfigInstallDecision.None)
        {
            Decision = IncludedConfigInstallDecision.No;
        }

        base.OnClosed(e);
    }
}
