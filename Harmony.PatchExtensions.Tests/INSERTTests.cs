namespace HarmonyLib.PatchExtensions.Tests;

public class INSERTTests : IDisposable
{
    private readonly Harmony _harmony;

    public INSERTTests()
    {
        _harmony = new Harmony("tests.patchextensions.redirect");
        MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Error;
        MixinLoader.ApplyPatches(_harmony, typeof(INSERTTests).Assembly, typeof(REDIRECTPatches));
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Redirect()
    {
        ResetCounters();
        
        var target = new PatchingTargets();
        var result = target.Double(10);
        
        Assert.Equal(68, result);
        Assert.Equal(0, PatchingTargets.PatchingHelper.DoubleCalls);
    }

    private static void ResetCounters()
    {
        PatchingTargets.CallCounter.Reset();
        PatchingTargets.PatchingHelper.Reset();
    }

    /// <inheritdoc />
    public void Dispose() => _harmony.UnpatchSelf();
}

public static class REDIRECTPatches
{
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.Double), AT.REDIRECT, targetMember: "PatchingHelper.Double")]
    public static int ReplaceDouble(int value)
    {
        return 68;
    }
}