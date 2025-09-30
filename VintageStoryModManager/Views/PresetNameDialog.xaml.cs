using System;
using System.Windows;
using System.Windows.Controls;

namespace VintageStoryModManager.Views;

public partial class PresetNameDialog : Window
{
    public PresetNameDialog()
    {
        InitializeComponent();
        Loaded += PresetNameDialog_Loaded;
    }

    public string PresetName => PresetNameTextBox.Text.Trim();

    public void SetInitialName(string? name)
    {
        string initial = name ?? string.Empty;
        PresetNameTextBox.Text = initial;
        PresetNameTextBox.SelectAll();
        UpdateSaveButtonState();
    }

    private void PresetNameDialog_Loaded(object sender, RoutedEventArgs e)
    {
        PresetNameTextBox.Focus();
        UpdateSaveButtonState();
    }

    private void PresetNameTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSaveButtonState();
    }

    private void UpdateSaveButtonState()
    {
        if (SaveButton is null)
        {
            return;
        }

        SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(PresetNameTextBox.Text);
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
