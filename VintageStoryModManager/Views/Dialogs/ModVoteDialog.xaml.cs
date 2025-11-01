using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using WpfButton = System.Windows.Controls.Button;

namespace VintageStoryModManager.Views.Dialogs;

public partial class ModVoteDialog : Window
{
    private ModVersionVoteSummary _summary;
    private readonly Func<ModVersionVoteOption?, string?, Task<ModVersionVoteSummary?>> _submitVoteAsync;
    private bool _isSubmitting;

    public ModVoteDialog(
        ModListItemViewModel mod,
        ModVersionVoteSummary summary,
        Func<ModVersionVoteOption?, string?, Task<ModVersionVoteSummary?>> submitVoteAsync)
    {
        InitializeComponent();

        ArgumentNullException.ThrowIfNull(mod);
        _summary = summary ?? throw new ArgumentNullException(nameof(summary));
        _submitVoteAsync = submitVoteAsync ?? throw new ArgumentNullException(nameof(submitVoteAsync));

        Title = string.Format(CultureInfo.CurrentCulture, "User reports for {0}", mod.DisplayName);
        TitleTextBlock.Text = Title;
        ModVersionTextBlock.Text = string.IsNullOrWhiteSpace(mod.VersionDisplay)
            ? "Mod version: Unknown"
            : string.Format(CultureInfo.CurrentCulture, "Mod version: {0}", mod.VersionDisplay);

        string? gameVersion = _summary.VintageStoryVersion;
        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            GameVersionTextBlock.Text = "Vintage Story version: Unknown";
        }
        else
        {
            GameVersionTextBlock.Text = string.Format(CultureInfo.CurrentCulture, "Vintage Story version: {0}", gameVersion);
        }

        UpdateOptionButtons();
        CommentTextBox.Text = _summary.UserComment ?? string.Empty;
        StatusTextBlock.Text = BuildStatusText();
    }

    private void UpdateOptionButtons()
    {
        UpdateOptionButton(FullyFunctionalButton, ModVersionVoteOption.FullyFunctional);
        UpdateOptionButton(NoIssuesButton, ModVersionVoteOption.NoIssuesSoFar);
        UpdateOptionButton(SomeIssuesButton, ModVersionVoteOption.SomeIssuesButWorks);
        UpdateOptionButton(NotFunctionalButton, ModVersionVoteOption.NotFunctional);
        UpdateOptionButton(CrashesButton, ModVersionVoteOption.CrashesOrFreezesGame);
    }

    private void UpdateOptionButton(WpfButton button, ModVersionVoteOption option)
    {
        int count = _summary.Counts.GetCount(option);
        string countLabel = count == 1
            ? "1 vote"
            : string.Format(CultureInfo.CurrentCulture, "{0} votes", count);
        button.Content = string.Format(
            CultureInfo.CurrentCulture,
            "{0} — {1}",
            option.ToDisplayString(),
            countLabel);

        button.FontWeight = _summary.UserVote == option ? FontWeights.Bold : FontWeights.Normal;
        button.IsEnabled = !_isSubmitting;
        button.ToolTip = option.RequiresComment()
            ? "A comment is required for this vote."
            : null;
    }

    private string BuildStatusText()
    {
        int total = _summary.TotalVotes;
        string totalLabel = total == 1
            ? "1 total vote"
            : string.Format(CultureInfo.CurrentCulture, "{0} total votes", total);
        return string.Format(
            CultureInfo.CurrentCulture,
            "{0}. Click an option to submit or update your vote.",
            totalLabel);
    }

    private async void VoteButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button || button.Tag is not ModVersionVoteOption option)
        {
            return;
        }

        if (_isSubmitting)
        {
            return;
        }

        ModVersionVoteOption? requestedOption = option;
        bool isRemovingVote = _summary.UserVote == option;
        if (isRemovingVote)
        {
            requestedOption = null;
        }

        string? comment = requestedOption.HasValue ? CommentTextBox.Text : null;
        if (requestedOption.HasValue
            && requestedOption.Value.RequiresComment()
            && string.IsNullOrWhiteSpace(comment))
        {
            StatusTextBlock.Text = "Please describe why the mod is not functional or crashes before submitting.";
            _ = CommentTextBox.Focus();
            return;
        }

        _isSubmitting = true;
        UpdateOptionButtons();
        StatusTextBlock.Text = isRemovingVote ? "Removing vote…" : "Submitting vote…";

        try
        {
            ModVersionVoteSummary? result = await _submitVoteAsync(requestedOption, comment).ConfigureAwait(true);
            if (result is not null)
            {
                _summary = result;
                CommentTextBox.Text = _summary.UserComment ?? string.Empty;
            }

            string statusPrefix = requestedOption.HasValue
                ? "Your vote has been recorded."
                : "Your vote has been removed.";

            StatusTextBlock.Text = string.Format(
                CultureInfo.CurrentCulture,
                "{0} {1}",
                statusPrefix,
                BuildStatusText());
        }
        catch (InternetAccessDisabledException ex)
        {
            StatusTextBlock.Text = ex.Message;
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Failed to submit vote: {0}",
                ex.Message);
        }
        finally
        {
            _isSubmitting = false;
            UpdateOptionButtons();
        }
    }
}
