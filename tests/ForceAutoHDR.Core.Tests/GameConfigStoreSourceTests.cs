using ForceAutoHDR.Core.Discovery;
using ForceAutoHDR.Core.Registry;

namespace ForceAutoHDR.Core.Tests;

public class GameConfigStoreSourceTests
{
    private const string Wuthering =
        @"C:\Games\Steam\steamapps\common\Wuthering Waves\Client\Binaries\Win64\Client-Win64-Shipping.exe";
    private const string Warframe = @"C:\Games\Steam\steamapps\common\Warframe\Warframe.x64.exe";

    private readonly InMemoryRegistryStore _registry = new();

    [Fact]
    public void Children_without_a_matched_executable_are_not_games()
    {
        // Most children on a real machine look like this: a title id for a game that was never
        // installed here, apparently arriving with the Microsoft account.
        var pathless = Child("024e465e-a7c2-4a4e-81ba-454475ce049c");
        _registry.SetString(pathless, GameConfigStoreSource.TitleIdValueName, "1885819086");
        _registry.SetString(pathless, "GameDVR_GameGUID", "c3f4f46d-343f-4ea8-86dc-d098f7d37e8e");
        SeedGame("real", Wuthering);

        var candidates = Source().GetCandidates();

        Assert.Equal(Wuthering, Assert.Single(candidates).ExecutablePath);
    }

    [Fact]
    public void Flags_does_not_decide_what_counts_as_a_game()
    {
        // 43 of the 44 real entries on the test machine carry Flags=17 and the account-synced ones
        // carry 51 -- but Warframe has 51 with a perfectly valid path. Filtering on it drops a game.
        SeedGame("wuthering", Wuthering, flags: 17);
        SeedGame("warframe", Warframe, flags: 51);

        var candidates = Source().GetCandidates();

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.ExecutablePath == Warframe);
    }

    [Fact]
    public void LastAccessed_is_read_as_a_utc_filetime()
    {
        var lastPlayed = new DateTime(2026, 8, 28, 14, 8, 0, DateTimeKind.Utc);
        SeedGame("wuthering", Wuthering, lastAccessed: lastPlayed.ToFileTimeUtc());

        Assert.Equal(lastPlayed, Assert.Single(Source().GetCandidates()).LastPlayedUtc);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    public void A_garbled_timestamp_reads_as_unknown_rather_than_throwing(long fileTime)
    {
        SeedGame("wuthering", Wuthering, lastAccessed: fileTime);

        Assert.Null(Assert.Single(Source().GetCandidates()).LastPlayedUtc);
    }

    [Fact]
    public void Uninstalled_games_are_reported_but_flagged()
    {
        // Game Bar never prunes: 20 of 44 entries on the test machine pointed at games that are gone.
        SeedGame("gone", Wuthering);
        SeedGame("here", Warframe);

        var candidates = Source(exists: path => path == Warframe).GetCandidates();

        Assert.False(candidates.Single(c => c.ExecutablePath == Wuthering).ExecutableExists);
        Assert.True(candidates.Single(c => c.ExecutablePath == Warframe).ExecutableExists);
    }

    [Fact]
    public void The_same_executable_in_two_children_keeps_the_most_recent_launch()
    {
        var older = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc);
        SeedGame("first", Wuthering, lastAccessed: older.ToFileTimeUtc());
        SeedGame("second", Wuthering, lastAccessed: newer.ToFileTimeUtc());

        Assert.Equal(newer, Assert.Single(Source().GetCandidates()).LastPlayedUtc);
    }

    [Fact]
    public void Candidates_are_newest_first_with_undated_ones_last()
    {
        SeedGame("old", @"C:\Games\Old\old.exe", lastAccessed: new DateTime(2024, 1, 1).ToFileTimeUtc());
        SeedGame("undated", @"C:\Games\Undated\undated.exe");
        SeedGame("new", @"C:\Games\New\new.exe", lastAccessed: new DateTime(2026, 9, 1).ToFileTimeUtc());

        var names = Source().GetCandidates().Select(c => c.ExecutableName).ToList();

        Assert.Equal(["new.exe", "old.exe", "undated.exe"], names);
    }

    [Fact]
    public void A_value_that_is_not_a_full_path_is_ignored()
    {
        SeedGame("bare", "game.exe");

        Assert.Empty(Source().GetCandidates());
    }

    [Fact]
    public void A_missing_key_means_no_data_not_an_exception()
    {
        Assert.Empty(Source().GetCandidates());
    }

    [Fact]
    public void The_title_id_is_carried_through()
    {
        SeedGame("wuthering", Wuthering, titleId: "1980190385");

        Assert.Equal("1980190385", Assert.Single(Source().GetCandidates()).TitleId);
    }

    private GameConfigStoreSource Source(Func<string, bool>? exists = null) =>
        new(_registry, exists ?? (_ => true));

    private static string Child(string name) => $@"{GameConfigStoreSource.KeyPath}\{name}";

    private void SeedGame(
        string subKeyName,
        string executablePath,
        long? lastAccessed = null,
        string? titleId = null,
        int flags = 17)
    {
        var child = Child(subKeyName);
        _registry.SetString(child, GameConfigStoreSource.MatchedExecutableValueName, executablePath);
        _registry.SetString(child, "Flags", flags.ToString());
        if (lastAccessed is { } fileTime)
        {
            _registry.SetInt64(child, GameConfigStoreSource.LastAccessedValueName, fileTime);
        }

        if (titleId is not null)
        {
            _registry.SetString(child, GameConfigStoreSource.TitleIdValueName, titleId);
        }
    }
}
