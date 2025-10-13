using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace VintageStoryModManager.Views.Dialogs;

public partial class CloudModlistDetailsDialog : Window
{
    public CloudModlistDetailsDialog(Window owner, string? suggestedName, IEnumerable<CloudModConfigOption>? configOptions)
    {
        ConfigOptions = new ObservableCollection<CloudModConfigOption>(
            (configOptions ?? Enumerable.Empty<CloudModConfigOption>())
                .Where(option => option is not null)
                .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase));

        InitializeComponent();

        Owner = owner;
        NameTextBox.Text = string.IsNullOrWhiteSpace(suggestedName)
            ? string.Empty
            : suggestedName;
        NameTextBox.SelectAll();
        UpdateConfirmButtonState();
    }

    public ObservableCollection<CloudModConfigOption> ConfigOptions { get; }

    public bool HasConfigOptions => ConfigOptions.Count > 0;

    public string ModlistName => NameTextBox.Text.Trim();

    public string? ModlistDescription => string.IsNullOrWhiteSpace(DescriptionTextBox.Text)
        ? null
        : DescriptionTextBox.Text.Trim();

    public string? ModlistVersion => string.IsNullOrWhiteSpace(VersionTextBox.Text)
        ? null
        : VersionTextBox.Text.Trim();

    public IReadOnlyList<CloudModConfigOption> GetSelectedConfigOptions()
    {
        return ConfigOptions.Where(option => option.IsSelected).ToList();
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            return;
        }

        DialogResult = true;
    }

    private void NameTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateConfirmButtonState();
    }

    private void UpdateConfirmButtonState()
    {
        ConfirmButton.IsEnabled = !string.IsNullOrWhiteSpace(NameTextBox.Text);
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        NameTextBox.Focus();
        NameTextBox.SelectAll();
    }

    public sealed class CloudModConfigOption : INotifyPropertyChanged
    {
        private bool _isSelected;

        public CloudModConfigOption(string modId, string displayName, string configPath, bool isSelected)
        {
            ModId = modId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ModId : displayName;
            ConfigPath = configPath ?? string.Empty;
            _isSelected = isSelected;
        }

        public string ModId { get; }

        public string DisplayName { get; }

        public string ConfigPath { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
