namespace HarmonyLib.PatchExtensions.Tests;

public class POSTFIXTests : IDisposable
{
    private readonly Harmony _harmony;

    public POSTFIXTests()
    {
        _harmony = new Harmony("tests.patchextensions.return");
        MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Error;
        MixinLoader.ApplyPatches(_harmony, typeof(POSTFIXTests).Assembly, typeof(ReturnPatches));
    }

    /// <summary>
    /// postfix runs after the original method,
    /// so 1 is added to the result
    /// </summary>
    [Fact]
    public void ReturnPostfix()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.Add(2, 3);

        Assert.Equal(6, result);
    }

    private static void ResetCounters()
    {
        PatchingTargets.CallCounter.Reset();
        PatchingTargets.PatchingHelper.Reset();
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
        _harmony.UnpatchSelf();
    }
}

public static class ReturnPatches
{
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.Add), AT.POSTFIX)]
    public static void AddPostfix(ref int __result)
    {
        __result += 1;
    }
}
