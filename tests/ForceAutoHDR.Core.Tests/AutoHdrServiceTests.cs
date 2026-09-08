using ForceAutoHDR.Core.Registry;

namespace ForceAutoHDR.Core.Tests;

public class AutoHdrServiceTests
{
    private const string X4 = @"C:\Games\Steam\steamapps\common\X4 Foundations\X4.exe";
    private const string Wow = @"C:\Games\World of Warcraft\_retail_\Wow.exe";
    private const string Forced = "BufferUpgradeOverride=1;BufferUpgradeEnable10Bit=1";

    private readonly InMemoryRegistryStore _registry = new();
    private readonly AutoHdrService _service;

    public AutoHdrServiceTests() => _service = new AutoHdrService(_registry);

    [Fact]
    public void A_row_is_named_after_the_game_not_the_shipping_binary()
    {
        // The same name the add dialog showed when this game was discovered. Naming it
        // "Client-Win64-Shipping" here and "Wuthering Waves" there would read as two games.
        SeedPreference(
            @"C:\Games\Steam\steamapps\common\Wuthering Waves\Client\Binaries\Win64\Client-Win64-Shipping.exe",
            "AutoHDREnable=2097;");

        Assert.Equal("Wuthering Waves", Assert.Single(_service.GetProfiles()).DisplayName);
    }

    [Fact]
    public void An_override_only_row_falls_back_to_the_file_name()
    {
        // A D3DBehaviors override stores a bare file name, so there is no path to derive from.
        SeedOverride("Riftbreaker", "riftbreaker_win_release.exe", Forced);

        var profile = Assert.Single(_service.GetProfiles());

        Assert.Null(profile.ExecutablePath);
        Assert.Equal("riftbreaker_win_release", profile.DisplayName);
    }

    [Fact]
    public void The_two_mechanisms_merge_into_one_row_per_app()
    {
        SeedPreference(X4, "AutoHDREnable=2097;");
        SeedOverride("X4", "X4.exe", Forced);

        var profile = Assert.Single(_service.GetProfiles());

        Assert.Equal("X4", profile.DisplayName);
        Assert.Equal(X4, profile.ExecutablePath);
        Assert.Equal(AutoHdrState.Enabled, profile.AutoHdr);
        Assert.True(profile.IsForced);
        Assert.Equal("X4", profile.D3DSubKeyName);
    }

    [Fact]
    public void An_override_with_no_matching_preference_still_gets_a_row_but_no_path()
    {
        SeedOverride("Endfield", "Endfield.exe", Forced);

        var profile = Assert.Single(_service.GetProfiles());

        Assert.Null(profile.ExecutablePath);
        Assert.Equal("Endfield.exe", profile.ExecutableName);
        Assert.Equal(AutoHdrState.NotConfigured, profile.AutoHdr);
        Assert.True(profile.IsForced);
    }

    [Fact]
    public void One_override_lights_up_every_path_with_that_file_name()
    {
        // D3DBehaviors matches on the bare file name, so two installs of the same game share it.
        SeedPreference(@"C:\Games\A\game.exe", "AutoHDREnable=2097;");
        SeedPreference(@"D:\Games\B\game.exe", "AutoHDREnable=2096;");
        SeedOverride("game", "game.exe", Forced);

        var profiles = _service.GetProfiles();

        Assert.Equal(2, profiles.Count);
        Assert.All(profiles, p => Assert.True(p.IsForced));
        Assert.All(profiles, p => Assert.Equal("game", p.D3DSubKeyName));
    }

    [Fact]
    public void Profiles_are_sorted_by_display_name_then_path()
    {
        SeedPreference(Wow, "AutoHDREnable=2097;");
        SeedPreference(X4, "AutoHDREnable=2097;");
        SeedOverride("Endfield", "Endfield.exe", Forced);

        Assert.Equal(["Endfield", "Wow", "X4"], _service.GetProfiles().Select(p => p.DisplayName));
    }

    [Fact]
    public void Setting_both_switches_writes_both_mechanisms()
    {
        _service.SetAutoHdr(X4, AutoHdrState.Enabled);
        _service.SetForced(X4, forced: true);

        var profile = Assert.Single(_service.GetProfiles());

        Assert.Equal(AutoHdrState.Enabled, profile.AutoHdr);
        Assert.True(profile.IsForced);
    }

    [Fact]
    public void Remove_wipes_the_app_from_both_mechanisms()
    {
        _service.SetAutoHdr(X4, AutoHdrState.Enabled);
        _service.SetForced(X4, forced: true);

        _service.Remove(X4);

        Assert.Empty(_service.GetProfiles());
        Assert.Empty(_registry.GetValueNames(GpuPreferencesStore.KeyPath));
        Assert.Empty(_registry.GetSubKeyNames(D3DBehaviorsStore.KeyPath));
    }

    private void SeedPreference(string executablePath, string flags) =>
        _registry.SetString(GpuPreferencesStore.KeyPath, executablePath, flags);

    private void SeedOverride(string subKeyName, string executableName, string behaviors)
    {
        var path = D3DBehaviorsStore.KeyPath + @"\" + subKeyName;
        _registry.SetString(path, D3DBehaviorsStore.NameValueName, executableName);
        _registry.SetString(path, D3DBehaviorsStore.BehaviorsValueName, behaviors);
    }
}
