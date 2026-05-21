using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class ProfileManagerTests : CommandCenterTestBase
{
    [Fact]
    public void Create_AddsProfile()
    {
        Directory.CreateDirectory(AppDataPaths.ProfilesDirectory);
        var mgr = new ProfileManager();
        mgr.Load();
        var before = mgr.Profiles.Count;

        mgr.Create("My Profile");

        Assert.Equal(before + 1, mgr.Profiles.Count);
        Assert.Contains(mgr.Profiles, p => p.Name == "My Profile");
    }

    [Fact]
    public void Duplicate_CopiesEntries()
    {
        Directory.CreateDirectory(AppDataPaths.ProfilesDirectory);
        var mgr = new ProfileManager();
        mgr.Load();

        var original = mgr.Create("Original");
        original.Entries.Add(new ProfileEntry { RelativePath = "test.dll", Enabled = true });
        var copy = mgr.Duplicate(original);

        Assert.Equal(original.Entries.Count, copy.Entries.Count);
        Assert.NotEqual(original.Id, copy.Id);
    }

    [Fact]
    public void Delete_RemovesProfile()
    {
        Directory.CreateDirectory(AppDataPaths.ProfilesDirectory);
        var mgr = new ProfileManager();
        mgr.Load();

        var profile = mgr.Create("ToDelete");
        var before = mgr.Profiles.Count;
        mgr.Delete(profile);

        Assert.Equal(before - 1, mgr.Profiles.Count);
        Assert.DoesNotContain(mgr.Profiles, p => p.Id == profile.Id);
    }

    [Fact]
    public void SeedsDefaults_WhenEmpty()
    {
        Directory.CreateDirectory(AppDataPaths.ProfilesDirectory);
        var mgr = new ProfileManager();
        mgr.Load();

        Assert.NotEmpty(mgr.Profiles);
        Assert.Contains(mgr.Profiles, p => p.Name == "Vanilla GTA V");
    }
}
