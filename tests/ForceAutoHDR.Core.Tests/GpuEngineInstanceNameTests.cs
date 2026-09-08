using ForceAutoHDR.Core.Discovery;

namespace ForceAutoHDR.Core.Tests;

public class GpuEngineInstanceNameTests
{
    [Fact]
    public void A_3d_engine_instance_yields_its_process_id()
    {
        Assert.True(GpuEngineInstanceName.TryParseRender3D(
            "pid_33100_luid_0x00000000_0x0000E885_phys_0_eng_0_engtype_3D",
            out var processId));
        Assert.Equal(33100u, processId);
    }

    [Fact]
    public void Casing_is_not_load_bearing()
    {
        // Observed as "3D" on the test machine and "3d" through other readers; neither is a
        // contract worth betting on.
        Assert.True(GpuEngineInstanceName.TryParseRender3D(
            "pid_2596_luid_0x00000000_0x0000e885_phys_0_eng_0_engtype_3d",
            out var processId));
        Assert.Equal(2596u, processId);
    }

    [Theory]
    // Copy, decode and encode engines are busy for reasons that have nothing to do with rendering:
    // a video call or a download saturates them while nothing is drawing.
    [InlineData("pid_2596_luid_0x00000000_0x0000E885_phys_0_eng_4_engtype_copy")]
    [InlineData("pid_4_luid_0x00000000_0x0000E885_phys_0_eng_11_engtype_VideoDecode")]
    [InlineData("engtype_3D")]
    [InlineData("pid__luid_0x0_phys_0_eng_0_engtype_3D")]
    [InlineData("pid_notanumber_luid_0x0_phys_0_eng_0_engtype_3D")]
    [InlineData("")]
    public void Anything_that_is_not_a_3d_instance_is_rejected(string instanceName) =>
        Assert.False(GpuEngineInstanceName.TryParseRender3D(instanceName, out _));
}
