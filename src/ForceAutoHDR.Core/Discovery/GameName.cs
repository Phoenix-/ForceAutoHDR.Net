namespace ForceAutoHDR.Core.Discovery;

/// <summary>
/// Turns an executable path into something worth putting in a list.
/// </summary>
/// <remarks>
/// <para>
/// Game Bar stores <c>ExeParentDirectory</c> and <c>WorkingDirectory</c> next to the path and they
/// look like exactly this -- Wuthering Waves gets "Wuthering Waves" -- but VOIN gets "Win64" and
/// Subnautica gets nothing. They are not path fragments at all: on a list-matched entry they are
/// the criteria Game Bar matched the title by, authored per title by Microsoft, so a good-looking
/// one is a coincidence. See <c>notes/gamebar-matches-games-against-a-microsoft-list.md</c>.
/// Deriving the name from the path instead is not always prettier, but it fails the same way
/// every time.
/// </para>
/// <para>
/// This is a placeholder for real store metadata: once a Steam/Epic library is matched against the
/// install directory, the title from there wins. Until then, this is what the list shows.
/// </para>
/// </remarks>
internal static class GameName
{
    // Build-appended suffixes on the binary. Without stripping these, every Unreal game is named
    // "<something>-Win64-Shipping" and the actual title hides in the path. The architecture tags
    // come from the file's own extension chain -- "Warframe.x64.exe" leaves "Warframe.x64" behind.
    private static readonly string[] BuildSuffixes =
    [
        "-Win64-Shipping",
        "-WinGDK-Shipping",
        "-Win32-Shipping",
        "-Win64-Test",
        ".x64",
        ".x86",
    ];

    // Names that describe the build layout or the launcher rather than the game. Directory and
    // file names share one set: "Client" and "Game" occur as both, and no entry here is ever a
    // title on its own.
    private static readonly HashSet<string> Structural = new(StringComparer.OrdinalIgnoreCase)
    {
        "binaries", "bin", "bin64", "bin32", "x64", "x86", "win64", "win32", "win64r",
        "lib", "libs", "windows-i686", "windows-x86_64", "engine", "content", "data",
        "client", "game", "app", "application", "build", "release", "retail", "_retail_",
        "exefile", "launcher", "start", "startup", "main", "run", "play", "shipping",
    };

    // Names that mean "a shelf of games lives here". Climbing past one of these lands on a drive
    // folder, never on a title, so the climb stops when it sees one.
    private static readonly HashSet<string> Libraries = new(StringComparer.OrdinalIgnoreCase)
    {
        "common", "steamapps", "steamlibrary", "steam", "games", "hoyoplay", "epic games",
        "program files", "program files (x86)", "users", "downloads", "desktop", "temp",
    };

    /// <summary>
    /// The file's own name when it carries one, otherwise the nearest enclosing directory that
    /// looks like a title. Falls back to the file name, so the result is never empty.
    /// </summary>
    public static string FromPath(string executablePath)
    {
        var stem = StripBuildSuffix(Path.GetFileNameWithoutExtension(executablePath));
        if (stem.Length > 0 && !Structural.Contains(stem))
        {
            return stem;
        }

        // "...\Wuthering Waves\Client\Binaries\Win64\Client-Win64-Shipping.exe" -> "Wuthering Waves"
        var directory = Path.GetDirectoryName(executablePath);
        while (directory is not null)
        {
            var name = Path.GetFileName(directory);
            if (name.Length == 0 || Libraries.Contains(name))
            {
                break;
            }

            if (!Structural.Contains(name))
            {
                return name;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return stem.Length > 0 ? stem : Path.GetFileName(executablePath);
    }

    private static string StripBuildSuffix(string fileName)
    {
        foreach (var suffix in BuildSuffixes)
        {
            if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^suffix.Length];
            }
        }

        return fileName;
    }
}
