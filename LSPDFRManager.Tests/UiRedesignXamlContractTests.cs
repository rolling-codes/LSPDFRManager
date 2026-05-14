using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Contract tests for UI redesign markers in XAML resources.
/// These tests validate text-level contracts only (no WPF runtime).
/// </summary>
public class UiRedesignXamlContractTests
{
    [Fact]
    public void ColorsXaml_ContainsBrighterBlueAccentAndRequiredGradients()
    {
        var text = ReadRepoFile("Resources", "Colors.xaml");

        Assert.Contains("x:Key=\"AccentPrimary\"", text);
        Assert.Contains("Color=\"#3A98FF\"", text);
        Assert.Contains("x:Key=\"AccentGradient\"", text);
        Assert.Contains("x:Key=\"AccentGlow\"", text);
        Assert.Contains("x:Key=\"ProgressTrackGradient\"", text);
    }

    [Fact]
    public void StylesXaml_UsesDropShadowEffectForPrimaryButtonCardPanelAndStatusChip()
    {
        var text = ReadRepoFile("Resources", "Styles.xaml");

        Assert.Contains("x:Key=\"PrimaryButton\"", text);
        Assert.Contains("x:Key=\"CardPanel\"", text);
        Assert.Contains("x:Key=\"StatusChip\"", text);
        Assert.Contains("<effects:DropShadowEffect", text);

        Assert.Contains("x:Key=\"PrimaryButton\" TargetType=\"Button\">", text);
        Assert.Contains("x:Key=\"CardPanel\" TargetType=\"Border\">", text);
        Assert.Contains("x:Key=\"StatusChip\" TargetType=\"Border\">", text);
    }

    [Fact]
    public void MainWindowXaml_ContainsShellGlowAccentAndBoldActiveNavigationSetters()
    {
        var text = ReadRepoFile("MainWindow.xaml");

        Assert.Contains("x:Key=\"ShellGlowAccent\"", text);
        Assert.Contains("Background=\"{StaticResource ShellGlowAccent}\"", text);
        Assert.Contains("BasedOn=\"{StaticResource NavButton}\"", text);
        Assert.Contains("DataTrigger Binding=\"{Binding IsHomeActive}\" Value=\"True\">", text);
        Assert.Contains("Setter Property=\"FontWeight\" Value=\"Bold\"", text);
    }

    private static string ReadRepoFile(params string[] parts)
    {
        var path = Path.GetFullPath(
            Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", .. parts]));

        return File.ReadAllText(path);
    }
}
