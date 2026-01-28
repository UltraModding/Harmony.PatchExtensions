namespace HarmonyLib.PatchExtensions.Tests;

public class INVOKETests : IDisposable
{
    private readonly Harmony _harmony;

    public INVOKETests()
    {
        _harmony = new Harmony("tests.patchextensions.invoke");
        MixinLoader.ApplyPatches(_harmony, typeof(INVOKETests).Assembly, typeof(InvokePatches));
    }

    /// <summary>
    /// injects before the 2nd call,
    /// so the counter increments when the second Helper.Noop is reached
    /// </summary>
    [Fact]
    public void Invoke_InsertBeforeSecondCall()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.CallHelpersTwice();

        Assert.Equal(2, PatchingTargets.PatchingHelper.NothinCalls);
        Assert.Equal(1, PatchingTargets.CallCounter.NothinCalls);
    }

    private static void ResetCounters()
    {
        PatchingTargets.CallCounter.Reset();
        PatchingTargets.PatchingHelper.Reset();
    }
    
    public void Dispose()
    {
        _harmony.UnpatchSelf();
    }
}

public static class InvokePatches
{
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.CallHelpersTwice), AT.INVOKE, target: "PatchingHelper.Nothin", occurrence: 2)]
    public static void BeforeSecondNothin()
    {
        PatchingTargets.CallCounter.NothinCalls++;
    }
}
