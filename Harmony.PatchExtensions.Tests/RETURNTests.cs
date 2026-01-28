namespace HarmonyLib.PatchExtensions.Tests;

public class RETURNTests : IDisposable
{
    private readonly Harmony _harmony;

    public RETURNTests()
    {
        _harmony = new Harmony("tests.patchextensions.return");
        MixinLoader.ApplyPatches(_harmony, typeof(RETURNTests).Assembly, typeof(ReturnPatches));
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
    
    public void Dispose()
    {
        _harmony.UnpatchSelf();
    }
}

public static class ReturnPatches
{
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.Add), AT.RETURN)]
    public static void AddPostfix(ref int __result)
    {
        __result += 1;
    }
}
