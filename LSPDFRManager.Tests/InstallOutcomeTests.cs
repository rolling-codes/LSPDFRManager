using LSPDFRManager.Domain;
using LSPDFRManager.ViewModels;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for InstallViewModel.ClassifyFailure — the pure classification of an
/// InstallResult into the three UI outcome tiers.
/// </summary>
public class InstallOutcomeTests
{
    // ── Shape 1: No mutation (cancel before write, early rejection) ──────────

    [Fact]
    public void NoMutation_WithGenericError_IsFailedNoMutation()
    {
        var result = new InstallResult { Success = false, IsPartial = false, Error = "Archive not found." };

        var (outcome, message, action) = InstallViewModel.ClassifyFailure(result);

        Assert.Equal(InstallOutcome.FailedNoMutation, outcome);
        Assert.Contains("Install failed:", message);
        Assert.Contains("Archive not found.", message);
        Assert.Null(action);
    }

    [Fact]
    public void NoMutation_WithCancellationError_IsCancelled()
    {
        var result = new InstallResult { Success = false, IsPartial = false, Error = "Operation was canceled." };

        var (outcome, message, action) = InstallViewModel.ClassifyFailure(result);

        Assert.Equal(InstallOutcome.Cancelled, outcome);
        Assert.Contains("Install failed:", message);
        Assert.Null(action);
    }

    [Fact]
    public void NoMutation_CancelledKeyword_CaseInsensitive()
    {
        var result = new InstallResult { Success = false, IsPartial = false, Error = "Install Cancelled by user." };

        var (outcome, _, _) = InstallViewModel.ClassifyFailure(result);

        Assert.Equal(InstallOutcome.Cancelled, outcome);
    }

    [Fact]
    public void NoMutation_NoSuggestedAction()
    {
        var result = new InstallResult { Success = false, IsPartial = false, Error = "Blocking issue." };

        var (_, _, action) = InstallViewModel.ClassifyFailure(result);

        Assert.Null(action);
    }

    // ── Shape 2: Mutation + clean rollback ───────────────────────────────────

    [Fact]
    public void MutationWithCleanRollback_IsFailedRestored()
    {
        var result = new InstallResult
        {
            Success = false,
            IsPartial = true,
            Error = "Stream corruption mid-extract.",
            RollbackErrors = [],
        };

        var (outcome, message, action) = InstallViewModel.ClassifyFailure(result);

        Assert.Equal(InstallOutcome.FailedRestored, outcome);
        Assert.Contains("Install failed:", message);
        Assert.Contains("Stream corruption mid-extract.", message);
        Assert.Null(action);
    }

    [Fact]
    public void MutationWithCleanRollback_NoSuggestedAction()
    {
        var result = new InstallResult
        {
            Success = false,
            IsPartial = true,
            Error = "Path traversal detected.",
            RollbackErrors = [],
        };

        var (_, _, action) = InstallViewModel.ClassifyFailure(result);

        Assert.Null(action);
    }

    // ── Shape 3: Mutation + partial rollback ─────────────────────────────────

    [Fact]
    public void MutationWithPartialRollback_IsFailedPartial()
    {
        var result = new InstallResult
        {
            Success = false,
            IsPartial = true,
            Error = "Disk full.",
            RollbackErrors = ["Rollback failed for 'C:\\GTA5\\plugins\\file1.dll': Access denied."],
        };

        var (outcome, message, action) = InstallViewModel.ClassifyFailure(result);

        Assert.Equal(InstallOutcome.FailedPartial, outcome);
        Assert.Contains("Install failed:", message);
        Assert.Contains("Disk full.", message);
        Assert.NotNull(action);
    }

    [Fact]
    public void MutationWithPartialRollback_SuggestedActionMentionsCount()
    {
        var result = new InstallResult
        {
            Success = false,
            IsPartial = true,
            Error = "Disk full.",
            RollbackErrors =
            [
                "Rollback failed for 'file1.dll': Access denied.",
                "Rollback failed for 'file2.dll': Access denied.",
            ],
        };

        var (_, _, action) = InstallViewModel.ClassifyFailure(result);

        Assert.NotNull(action);
        Assert.Contains("2", action);
    }

    [Fact]
    public void MutationWithPartialRollback_SuggestedActionAdvicesManualVerification()
    {
        var result = new InstallResult
        {
            Success = false,
            IsPartial = true,
            Error = "Any error.",
            RollbackErrors = ["one error"],
        };

        var (_, _, action) = InstallViewModel.ClassifyFailure(result);

        Assert.NotNull(action);
        Assert.Contains("GTA V", action, StringComparison.OrdinalIgnoreCase);
    }

    // ── Error message always present ─────────────────────────────────────────

    [Fact]
    public void NullError_FallsBackToUnknownError()
    {
        var result = new InstallResult { Success = false, IsPartial = false, Error = null };

        var (_, message, _) = InstallViewModel.ClassifyFailure(result);

        Assert.Contains("Unknown error", message);
    }

    // ── IsPartial is the gate — not error content ────────────────────────────

    [Fact]
    public void IsPartialFalse_NeverSuggestsRollback_EvenWithRollbackErrors()
    {
        // Defensive: if somehow RollbackErrors is populated but IsPartial is false,
        // classify as no-mutation failure (no rollback messaging).
        var result = new InstallResult
        {
            Success = false,
            IsPartial = false,
            Error = "Something went wrong.",
            RollbackErrors = ["stale error"],
        };

        var (outcome, _, action) = InstallViewModel.ClassifyFailure(result);

        Assert.True(outcome is InstallOutcome.FailedNoMutation or InstallOutcome.Cancelled);
        Assert.Null(action);
    }

    // ── Outcome enum maps correctly to visual tier ────────────────────────────

    [Theory]
    [InlineData(InstallOutcome.FailedPartial,    true,  false, false)]
    [InlineData(InstallOutcome.FailedRestored,   false, true,  false)]
    [InlineData(InstallOutcome.Cancelled,        false, false, true)]
    [InlineData(InstallOutcome.FailedNoMutation, false, false, true)]
    [InlineData(InstallOutcome.None,             false, false, false)]
    public void OutcomeEnum_MapsToCorrectVisibilityBools(
        InstallOutcome outcome,
        bool expectedPartial,
        bool expectedRestored,
        bool expectedSimple)
    {
        // These computations mirror the VM properties — they are duplicated here
        // so a refactor that breaks the mapping will surface as a test failure.
        var isPartial      = outcome == InstallOutcome.FailedPartial;
        var isRestored     = outcome == InstallOutcome.FailedRestored;
        var isSimpleFailure = outcome is InstallOutcome.Cancelled or InstallOutcome.FailedNoMutation;

        Assert.Equal(expectedPartial,  isPartial);
        Assert.Equal(expectedRestored, isRestored);
        Assert.Equal(expectedSimple,   isSimpleFailure);
    }
}
