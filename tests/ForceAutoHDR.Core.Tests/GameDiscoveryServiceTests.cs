using ForceAutoHDR.Core.Discovery;

namespace ForceAutoHDR.Core.Tests;

public class GameDiscoveryServiceTests
{
    private const string Wuthering =
        @"C:\Games\Steam\steamapps\common\Wuthering Waves\Client\Binaries\Win64\Client-Win64-Shipping.exe";
    private const string Riftbreaker = @"C:\Games\Steam\steamapps\common\Riftbreaker\bin\riftbreaker_win_release.exe";

    [Fact]
    public void One_executable_seen_by_two_sources_becomes_one_row()
    {
        var service = new GameDiscoveryService(
            Source(GameCandidateOrigins.GameConfigStore, Candidate(Wuthering, lastPlayed: new DateTime(2026, 8, 28))),
            Source(GameCandidateOrigins.GpuPreferences, Candidate(Wuthering)));

        var candidate = Assert.Single(service.GetCandidates());

        Assert.Equal(
            GameCandidateOrigins.GameConfigStore | GameCandidateOrigins.GpuPreferences,
            candidate.Origins);
        // The source that knows a timestamp wins over the one that has none to offer.
        Assert.Equal(new DateTime(2026, 8, 28), candidate.LastPlayedUtc);
    }

    [Fact]
    public void Paths_differing_only_in_casing_are_the_same_game()
    {
        var service = new GameDiscoveryService(
            Source(GameCandidateOrigins.GameConfigStore, Candidate(Wuthering)),
            Source(GameCandidateOrigins.GpuPreferences, Candidate(Wuthering.ToUpperInvariant())));

        Assert.Single(service.GetCandidates());
    }

    [Fact]
    public void Uninstalled_games_are_left_out_unless_asked_for()
    {
        var service = new GameDiscoveryService(Source(
            GameCandidateOrigins.GameConfigStore,
            Candidate(Wuthering, exists: false),
            Candidate(Riftbreaker)));

        Assert.Equal(Riftbreaker, Assert.Single(service.GetCandidates()).ExecutablePath);
        Assert.Equal(2, service.GetCandidates(includeMissingExecutables: true).Count);
    }

    [Fact]
    public void A_running_game_outranks_whatever_was_played_most_recently()
    {
        var service = new GameDiscoveryService(
            Source(GameCandidateOrigins.GameConfigStore, Candidate(Wuthering, lastPlayed: DateTime.UtcNow)),
            Source(GameCandidateOrigins.Running, new GameCandidate
            {
                ExecutablePath = Riftbreaker,
                Origins = GameCandidateOrigins.Running,
                // Deliberately stale: being live is what should promote it, not the timestamp.
                LastPlayedUtc = new DateTime(2020, 1, 1),
                ExecutableExists = true,
            }));

        Assert.Equal(Riftbreaker, service.GetCandidates()[0].ExecutablePath);
    }

    [Fact]
    public void Stale_preferences_are_the_configured_entries_whose_game_is_gone()
    {
        var service = new GameDiscoveryService(
            Source(GameCandidateOrigins.GpuPreferences, Candidate(Wuthering, exists: false)),
            // Gone too, but only Game Bar ever knew about it -- not ours to clean up.
            Source(GameCandidateOrigins.GameConfigStore, Candidate(Riftbreaker, exists: false)));

        Assert.Equal(Wuthering, Assert.Single(service.GetStalePreferences()).ExecutablePath);
    }

    [Fact]
    public void A_source_with_nothing_to_say_is_harmless()
    {
        var service = new GameDiscoveryService(
            Source(GameCandidateOrigins.Running),
            Source(GameCandidateOrigins.GameConfigStore, Candidate(Wuthering)));

        Assert.Single(service.GetCandidates());
    }

    private static GameCandidate Candidate(string path, DateTime? lastPlayed = null, bool exists = true) =>
        new()
        {
            ExecutablePath = path,
            Origins = GameCandidateOrigins.None,
            LastPlayedUtc = lastPlayed,
            ExecutableExists = exists,
        };

    private static IGameCandidateSource Source(GameCandidateOrigins origin, params GameCandidate[] candidates) =>
        new StubSource(origin, [.. candidates.Select(c => c with { Origins = origin })]);

    private sealed class StubSource(GameCandidateOrigins origin, IReadOnlyList<GameCandidate> candidates)
        : IGameCandidateSource
    {
        public GameCandidateOrigins Origin { get; } = origin;

        public IReadOnlyList<GameCandidate> GetCandidates() => candidates;
    }
}
