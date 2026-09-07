using ForceAutoHDR.Core.Registry;

namespace ForceAutoHDR.Core.Tests;

public class D3DBehaviorsStoreTests
{
    private const string Forced = "BufferUpgradeOverride=1;BufferUpgradeEnable10Bit=1";

    private readonly InMemoryRegistryStore _registry = new();
    private readonly D3DBehaviorsStore _store;

    public D3DBehaviorsStoreTests() => _store = new D3DBehaviorsStore(_registry);

    [Fact]
    public void GetAll_ignores_the_unrelated_Direct3D_state_that_shares_the_key()
    {
        Seed("Endfield", "Endfield.exe", Forced);
        _registry.SetString(D3DBehaviorsStore.KeyPath + @"\MostRecentApplication", "Name", "explorer.exe");
        _registry.CreateKey(D3DBehaviorsStore.KeyPath + @"\Empty");

        var entry = Assert.Single(_store.GetAll());

        Assert.Equal("Endfield", entry.SubKeyName);
        Assert.Equal("Endfield.exe", entry.ExecutableName);
        Assert.True(entry.IsAutoHdrForced);
    }

    [Fact]
    public void An_override_with_only_one_of_the_two_flags_is_not_forcing()
    {
        Seed("Half", "half.exe", "BufferUpgradeOverride=1");

        Assert.False(Assert.Single(_store.GetAll()).IsAutoHdrForced);
    }

    [Fact]
    public void Forcing_a_new_app_writes_what_the_original_tool_writes()
    {
        _store.SetForced(@"D:\Games\Endfield\Endfield.exe", forced: true);

        Assert.Equal("Endfield.exe", ReadValue("Endfield", D3DBehaviorsStore.NameValueName));
        Assert.Equal(Forced, ReadValue("Endfield", D3DBehaviorsStore.BehaviorsValueName));
    }

    [Fact]
    public void Forcing_twice_does_not_grow_a_second_subkey()
    {
        _store.SetForced("Endfield.exe", forced: true);
        _store.SetForced("Endfield.exe", forced: true);

        Assert.Single(_registry.GetSubKeyNames(D3DBehaviorsStore.KeyPath));
    }

    [Fact]
    public void A_subkey_name_taken_by_another_app_gets_a_suffix()
    {
        Seed("Endfield", "SomethingElse.exe", Forced);

        _store.SetForced("Endfield.exe", forced: true);

        Assert.Equal("Endfield.exe", ReadValue("Endfield_2", D3DBehaviorsStore.NameValueName));
    }

    [Fact]
    public void Unforcing_deletes_the_subkey_when_nothing_is_left()
    {
        _store.SetForced("Endfield.exe", forced: true);

        _store.SetForced("Endfield.exe", forced: false);

        Assert.Empty(_registry.GetSubKeyNames(D3DBehaviorsStore.KeyPath));
    }

    [Fact]
    public void Unforcing_keeps_flags_this_app_did_not_put_there()
    {
        Seed("Endfield", "Endfield.exe", "SomeOtherBehavior=1;BufferUpgradeOverride=1;BufferUpgradeEnable10Bit=1");

        _store.SetForced("Endfield.exe", forced: false);

        Assert.Equal("SomeOtherBehavior=1", ReadValue("Endfield", D3DBehaviorsStore.BehaviorsValueName));
    }

    [Fact]
    public void Unforcing_an_app_that_was_never_forced_writes_nothing()
    {
        _store.SetForced("Endfield.exe", forced: false);

        Assert.Empty(_registry.GetSubKeyNames(D3DBehaviorsStore.KeyPath));
    }

    [Fact]
    public void Duplicate_subkeys_for_one_executable_are_all_cleared()
    {
        Seed("Endfield", "Endfield.exe", Forced);
        Seed("Endfield_copy", "ENDFIELD.EXE", Forced);

        _store.SetForced("Endfield.exe", forced: false);

        Assert.Empty(_registry.GetSubKeyNames(D3DBehaviorsStore.KeyPath));
    }

    [Fact]
    public void Find_matches_on_the_file_name_whatever_path_it_is_given()
    {
        Seed("Endfield", "Endfield.exe", Forced);

        Assert.Single(_store.Find(@"C:\Anywhere\Else\endfield.exe"));
        Assert.Empty(_store.Find("other.exe"));
    }

    [Fact]
    public void RemoveSubKey_drops_the_override_outright()
    {
        Seed("Endfield", "Endfield.exe", "SomeOtherBehavior=1;" + Forced);

        _store.RemoveSubKey("Endfield");

        Assert.Empty(_store.GetAll());
    }

    private void Seed(string subKeyName, string executableName, string behaviors)
    {
        var path = D3DBehaviorsStore.KeyPath + @"\" + subKeyName;
        _registry.SetString(path, D3DBehaviorsStore.NameValueName, executableName);
        _registry.SetString(path, D3DBehaviorsStore.BehaviorsValueName, behaviors);
    }

    private string? ReadValue(string subKeyName, string valueName) =>
        _registry.GetString(D3DBehaviorsStore.KeyPath + @"\" + subKeyName, valueName);
}
