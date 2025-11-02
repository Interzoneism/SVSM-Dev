using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views.Dialogs;

public partial class ModUsageNoIssuesDialog : Window
{
    private readonly List<ModUsageVoteCandidateViewModel> _candidates;
    private IReadOnlyList<ModUsageVoteCandidateViewModel> _selectedCandidates = Array.Empty<ModUsageVoteCandidateViewModel>();

    public ModUsageNoIssuesDialog(IEnumerable<ModUsageVoteCandidateViewModel> candidates)
    {
        InitializeComponent();

        if (candidates is null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        _candidates = candidates.Where(candidate => candidate is not null).ToList();
        DataContext = this;
    }

    public IReadOnlyList<ModUsageVoteCandidateViewModel> Candidates => _candidates;

    public IReadOnlyList<ModUsageVoteCandidateViewModel> SelectedCandidates => _selectedCandidates;

    private void SubmitButton_OnClick(object sender, RoutedEventArgs e)
    {
        List<ModUsageVoteCandidateViewModel> selected = _candidates.Where(candidate => candidate.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusTextBlock.Text = "Select at least one mod to submit votes.";
            return;
        }

        _selectedCandidates = selected;
        DialogResult = true;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
