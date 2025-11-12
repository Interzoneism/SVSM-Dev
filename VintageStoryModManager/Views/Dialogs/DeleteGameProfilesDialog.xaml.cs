using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views.Dialogs;

public partial class DeleteGameProfilesDialog : Window
{
    private readonly ObservableCollection<GameProfileDeletionOption> _profiles = new();

    public DeleteGameProfilesDialog(IEnumerable<string> profileNames, string activeProfileName)
    {
        if (profileNames is null)
        {
            throw new ArgumentNullException(nameof(profileNames));
        }

        InitializeComponent();

        foreach (string profileName in profileNames)
        {
            bool isDefault = string.Equals(
                profileName,
                UserConfigurationService.DefaultProfileName,
                StringComparison.OrdinalIgnoreCase);

            bool isActive = string.Equals(profileName, activeProfileName, StringComparison.OrdinalIgnoreCase);

            var option = new GameProfileDeletionOption(profileName, isActive, isDefault);
            option.PropertyChanged += OnProfilePropertyChanged;
            _profiles.Add(option);
        }

        UpdateDeleteButtonState();
    }

    public ObservableCollection<GameProfileDeletionOption> Profiles => _profiles;

    public IReadOnlyList<string> SelectedProfileNames => _profiles
        .Where(profile => profile.IsSelectable && profile.IsSelected)
        .Select(profile => profile.Name)
        .ToArray();

    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameProfileDeletionOption.IsSelected))
        {
            UpdateDeleteButtonState();
        }
    }

    private void UpdateDeleteButtonState()
    {
        if (DeleteButton is null)
        {
            return;
        }

        DeleteButton.IsEnabled = _profiles.Any(profile => profile.IsSelectable && profile.IsSelected);
    }

    private void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    public sealed class GameProfileDeletionOption : INotifyPropertyChanged
    {
        private bool _isSelected;

        public GameProfileDeletionOption(string name, bool isActive, bool isDefault)
        {
            Name = name;
            IsActive = isActive;
            IsDefault = isDefault;
        }

        public string Name { get; }

        public bool IsActive { get; }

        public bool IsDefault { get; }

        public bool IsSelectable => !IsDefault;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                bool newValue = IsSelectable && value;
                if (_isSelected == newValue)
                {
                    return;
                }

                _isSelected = newValue;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
