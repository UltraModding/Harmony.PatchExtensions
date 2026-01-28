namespace HarmonyLib.PatchExtensions.Tests;

public class REDIRECTTests : IDisposable
{
    private readonly Harmony _harmony;

    public REDIRECTTests()
    {
        _harmony = new Harmony("tests.patchextensions.redirect");
        MixinLoader.ApplyPatches(_harmony, typeof(REDIRECTTests).Assembly, typeof(RedirectPatches));
    }

    /// <summary>
    /// replaces the targeted call,
    /// so the replacement result is returned and the original helper is not executed.
    /// </summary>
    [Fact]
    public void Redirect()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.CallHelper(10);

        Assert.Equal(68, result);
        Assert.Equal(0, PatchingTargets.PatchingHelper.DoubleCalls);
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

public static class RedirectPatches
{
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.CallHelper), AT.REDIRECT, target: "PatchingHelper.Double")]
    public static int ReplaceDouble(int value)
    {
        return 68;
    }
}
