namespace HarmonyLib.PatchExtensions.Tests;

public class HEADTests : IDisposable
{
    private readonly Harmony _harmony;

    public HEADTests()
    {
        _harmony = new Harmony("tests.patchextensions.head");
        MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Error;
        MixinLoader.ApplyPatches(_harmony, typeof(HEADTests).Assembly, typeof(HeadPatches));
    }

    /// <summary>
    /// The patch just adds another call,
    /// so it should result in 5 and have 1 call
    /// </summary>
    [Fact]
    public void HeadPrefix()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.Add(2, 3);

        Assert.Equal(5, result);
        Assert.Equal(1, PatchingTargets.CallCounter.AddCalls);
    }

    /// <summary>
    /// overwriting prefix sets __result and returns false,
    /// so the original method is skipped and its counter does not increment.
    /// </summary>
    [Fact]
    public void HeadOverwrite()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.AddWithCounter(4, 5);

        Assert.Equal(68, result);
        Assert.Equal(0, PatchingTargets.CallCounter.AddCalls);
    }

    /// <summary>
    /// HeadOverwrite but the class returns a non bool type and has no reference to __result
    /// and also increments AddCalls
    /// </summary>
    [Fact]
    public void AddOverwriteReturnInt()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.Add2(6, 7);

        Assert.Equal(68, result);
        Assert.Equal(1, PatchingTargets.CallCounter.AddCalls);
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

public static class HeadPatches
{
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.Add), AT.HEAD)]
    public static void AddPrefix()
    {
        PatchingTargets.CallCounter.AddCalls++;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.AddWithCounter), AT.HEAD, overwriting: true)]
    public static bool AddOverwrite(int a, int b, ref int __result)
    {
        __result = 68;
        return false;
    }
    
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.Add2), AT.HEAD, overwriting: true)]
    public static int AddOverwriteReturnInt(int a, int b)
    {
        PatchingTargets.CallCounter.AddCalls++;
        return 68;
    }
}
