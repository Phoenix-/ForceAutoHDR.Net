using ForceAutoHDR.Core.Discovery;

namespace ForceAutoHDR.Core.Tests;

public class GameNameTests
{
    [Theory]
    // The file name carries the title: nothing to climb for.
    [InlineData(@"C:\Games\Steam\steamapps\common\Subnautica\Subnautica.exe", "Subnautica")]
    [InlineData(@"C:\Games\World of Warcraft\_retail_\Wow.exe", "Wow")]
    [InlineData(@"D:\Games\HoYoPlay\games\Genshin Impact game\GenshinImpact.exe", "GenshinImpact")]
    // Unreal's suffix hides the title in plain sight.
    [InlineData(@"C:\Games\Steam\steamapps\common\VOIN\VOIN\Binaries\Win64\VOIN-Win64-Shipping.exe", "VOIN")]
    // The case that motivated all of this: the executable says nothing, the path says everything.
    [InlineData(
        @"C:\Games\Steam\steamapps\common\Wuthering Waves\Client\Binaries\Win64\Client-Win64-Shipping.exe",
        "Wuthering Waves")]
    [InlineData(@"C:\Games\EVE\tq\bin64\exefile.exe", "tq")]
    // An architecture tag in the file's extension chain is not part of the title.
    [InlineData(@"C:\Games\Steam\steamapps\common\Warframe\Warframe.x64.exe", "Warframe")]
    // Ren'Py buries the executable two structural directories down.
    [InlineData(@"C:\GCP\A_Struggle_With_Sin\lib\windows-i686\Game.exe", "A_Struggle_With_Sin")]
    // Both Control executables stay distinct: they are separate rows with separate toggles, and
    // collapsing them to "Control" would make the list lie about which one is configured.
    [InlineData(@"C:\Games\Steam\steamapps\common\Control\Control_DX12.exe", "Control_DX12")]
    public void Names_come_from_the_executable_or_the_nearest_meaningful_directory(string path, string expected) =>
        Assert.Equal(expected, GameName.FromPath(path));

    [Fact]
    public void The_climb_stops_before_a_library_directory()
    {
        // Every segment of this path is structure. Without the stop the climb would walk out
        // through "common" and "steamapps" and confidently name the Steam install folder.
        Assert.Equal("game", GameName.FromPath(@"C:\Games\Steam\steamapps\common\bin\game.exe"));
    }

    [Fact]
    public void A_path_that_is_all_structure_falls_back_to_the_file_name()
    {
        Assert.Equal("game", GameName.FromPath(@"C:\bin\game.exe"));
    }
}
