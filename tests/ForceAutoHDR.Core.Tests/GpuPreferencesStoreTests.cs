using ForceAutoHDR.Core.Registry;

namespace ForceAutoHDR.Core.Tests;

public class GpuPreferencesStoreTests
{
    private const string X4 = @"C:\Games\Steam\steamapps\common\X4 Foundations\X4.exe";

    private readonly InMemoryRegistryStore _registry = new();
    private readonly GpuPreferencesStore _store;

    public GpuPreferencesStoreTests() => _store = new GpuPreferencesStore(_registry);

    [Fact]
    public void GetAll_reads_the_apps_and_skips_the_housekeeping_values()
    {
        // Both of these sit in the same key on a real Windows 11 install and are not applications.
        Seed(GpuPreferencesStore.GlobalSettingsValueName, "AutoHDREnable=1;VRROptimizeEnable=1;");
        Seed("GraphicsFeaturesNotificationConfig", "1");
        Seed(X4, "AutoHDREnable=2097;");
        Seed(@"C:\Games\Old\legacy.exe", "AutoHDREnable=2096;");
        Seed(@"C:\Games\Other\plain.exe", "GpuPreference=2;");

        var entries = _store.GetAll();

        Assert.Equal(3, entries.Count);
        Assert.Equal(AutoHdrState.Enabled, entries.Single(e => e.ExecutablePath == X4).AutoHdr);
        Assert.Equal("X4.exe", entries.Single(e => e.ExecutablePath == X4).ExecutableName);
        Assert.Equal(AutoHdrState.Disabled, entries.Single(e => e.ExecutableName == "legacy.exe").AutoHdr);
        Assert.Equal(AutoHdrState.NotConfigured, entries.Single(e => e.ExecutableName == "plain.exe").AutoHdr);
    }

    [Fact]
    public void Enabling_an_unknown_app_writes_the_value_Windows_writes()
    {
        _store.SetAutoHdr(X4, AutoHdrState.Enabled);

        Assert.Equal("AutoHDREnable=2097;", Read(X4));
    }

    [Fact]
    public void Disabling_writes_2096()
    {
        _store.SetAutoHdr(X4, AutoHdrState.Enabled);
        _store.SetAutoHdr(X4, AutoHdrState.Disabled);

        Assert.Equal("AutoHDREnable=2096;", Read(X4));
    }

    [Fact]
    public void Flags_written_by_Windows_are_preserved_in_place()
    {
        Seed(X4, "GpuPreference=2;AutoHDREnable=2096;SwapEffectUpgradeEnable=1;AppStatus=1;");

        _store.SetAutoHdr(X4, AutoHdrState.Enabled);

        Assert.Equal("GpuPreference=2;AutoHDREnable=2097;SwapEffectUpgradeEnable=1;AppStatus=1;", Read(X4));
    }

    [Fact]
    public void Clearing_the_toggle_keeps_the_other_flags()
    {
        Seed(X4, "GpuPreference=2;AutoHDREnable=2097;");

        _store.SetAutoHdr(X4, AutoHdrState.NotConfigured);

        Assert.Equal("GpuPreference=2;", Read(X4));
    }

    [Fact]
    public void Clearing_the_only_flag_deletes_the_entry()
    {
        Seed(X4, "AutoHDREnable=2097;");

        _store.SetAutoHdr(X4, AutoHdrState.NotConfigured);

        Assert.Null(Read(X4));
        Assert.Empty(_store.GetAll());
    }

    [Fact]
    public void Clearing_an_app_that_was_never_configured_writes_nothing()
    {
        _store.SetAutoHdr(X4, AutoHdrState.NotConfigured);

        Assert.Empty(_registry.GetValueNames(GpuPreferencesStore.KeyPath));
    }

    [Fact]
    public void A_path_in_different_casing_updates_the_existing_entry()
    {
        Seed(X4, "AutoHDREnable=2096;");

        _store.SetAutoHdr(X4.ToUpperInvariant(), AutoHdrState.Enabled);

        Assert.Equal(X4, Assert.Single(_store.GetAll()).ExecutablePath);
        Assert.Equal("AutoHDREnable=2097;", Read(X4));
    }

    [Fact]
    public void Find_matches_case_insensitively_and_returns_null_otherwise()
    {
        Seed(X4, "AutoHDREnable=2097;");

        Assert.NotNull(_store.Find(X4.ToLowerInvariant()));
        Assert.Null(_store.Find(@"C:\Games\nope.exe"));
    }

    [Fact]
    public void Remove_drops_the_whole_entry()
    {
        Seed(X4, "GpuPreference=2;AutoHDREnable=2097;");

        _store.Remove(X4);

        Assert.Null(Read(X4));
    }

    [Fact]
    public void The_global_defaults_entry_is_readable_but_not_writable_as_an_app()
    {
        Seed(GpuPreferencesStore.GlobalSettingsValueName, "AutoHDREnable=1;VRROptimizeEnable=1;");

        Assert.Equal("1", _store.GetGlobalSettings()["AutoHDREnable"]);
        Assert.Throws<ArgumentException>(() =>
            _store.SetAutoHdr(GpuPreferencesStore.GlobalSettingsValueName, AutoHdrState.Disabled));
    }

    [Theory]
    [InlineData("X4.exe")]
    [InlineData(@"..\X4.exe")]
    [InlineData(GpuPreferencesStore.GlobalSettingsValueName)]
    public void An_executable_without_a_full_path_is_rejected(string executable)
    {
        Assert.Throws<ArgumentException>(() => _store.SetAutoHdr(executable, AutoHdrState.Enabled));
        Assert.Throws<ArgumentException>(() => _store.Find(executable));
        Assert.Throws<ArgumentException>(() => _store.Remove(executable));
    }

    private void Seed(string valueName, string value) =>
        _registry.SetString(GpuPreferencesStore.KeyPath, valueName, value);

    private string? Read(string valueName) =>
        _registry.GetString(GpuPreferencesStore.KeyPath, valueName);
}
