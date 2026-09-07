namespace ForceAutoHDR.Core.Tests;

public class FlagStringTests
{
    [Theory]
    // The two shapes Windows actually writes, verified in HKCU on Windows 11 26200.
    [InlineData("AutoHDREnable=2097;")]
    [InlineData("BufferUpgradeOverride=1;BufferUpgradeEnable10Bit=1")]
    [InlineData("AutoHDREnable=1;VRROptimizeEnable=1;SwapEffectUpgradeEnable=1;")]
    public void Parse_round_trips_verbatim(string raw) =>
        Assert.Equal(raw, FlagString.Parse(raw).ToString());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Parse_of_nothing_yields_an_empty_string(string? raw)
    {
        var flags = FlagString.Parse(raw);

        Assert.True(flags.IsEmpty);
        Assert.Equal(string.Empty, flags.ToString());
    }

    [Fact]
    public void Set_keeps_the_position_and_casing_of_an_existing_flag()
    {
        var flags = FlagString.Parse("GpuPreference=2;AUTOHDRENABLE=2096;DXGIEffects=1;");

        flags.Set("AutoHDREnable", "2097");

        Assert.Equal("GpuPreference=2;AUTOHDRENABLE=2097;DXGIEffects=1;", flags.ToString());
    }

    [Fact]
    public void Set_appends_a_flag_that_is_not_there_yet()
    {
        var flags = FlagString.Parse("GpuPreference=2;");

        flags.Set("AutoHDREnable", "2097");

        Assert.Equal("GpuPreference=2;AutoHDREnable=2097;", flags.ToString());
    }

    [Fact]
    public void Remove_leaves_the_neighbours_alone()
    {
        var flags = FlagString.Parse("SwapEffectUpgradeEnable=1;AutoHDREnable=2097;AppStatus=1;");

        Assert.True(flags.Remove("autohdrenable"));
        Assert.False(flags.Remove("autohdrenable"));
        Assert.Equal("SwapEffectUpgradeEnable=1;AppStatus=1;", flags.ToString());
    }

    [Fact]
    public void A_segment_without_a_value_survives_untouched()
    {
        var flags = FlagString.Parse("AppStatus;AutoHDREnable=2097;");

        Assert.False(flags.TryGetValue("AppStatus", out _));
        Assert.Equal("AppStatus;AutoHDREnable=2097;", flags.ToString());
    }

    [Fact]
    public void Empty_segments_are_dropped_but_the_trailing_separator_is_kept()
    {
        Assert.Equal("A=1;B=2;", FlagString.Parse(";A=1;;B=2;").ToString());
        Assert.Equal("A=1", FlagString.Parse("A=1").ToString());
    }

    [Fact]
    public void TryGetInt32_reads_numbers_and_refuses_the_rest()
    {
        var flags = FlagString.Parse("AutoHDREnable=2097;Junk=yes;");

        Assert.True(flags.TryGetInt32("AutoHDREnable", out var value));
        Assert.Equal(2097, value);
        Assert.False(flags.TryGetInt32("Junk", out _));
        Assert.False(flags.TryGetInt32("Missing", out _));
    }

    [Fact]
    public void A_new_flag_string_renders_the_way_its_mechanism_wants()
    {
        var withTrailing = new FlagString();
        withTrailing.Set("AutoHDREnable", "2097");
        Assert.Equal("AutoHDREnable=2097;", withTrailing.ToString());

        var without = new FlagString(trailingSeparator: false);
        without.Set("BufferUpgradeOverride", "1");
        Assert.Equal("BufferUpgradeOverride=1", without.ToString());
    }
}
