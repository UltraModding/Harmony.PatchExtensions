namespace HarmonyLib.PatchExtensions.Tests;

public class REDIRECTTests : IDisposable
{
    private readonly Harmony _harmony;

    public REDIRECTTests()
    {
        _harmony = new Harmony("tests.patchextensions.redirect");
        MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Error;
        MixinLoader.ApplyPatches(_harmony, typeof(REDIRECTTests).Assembly, typeof(RedirectPatches));
    }

    /// <summary>
    /// replaces the targeted call
    /// so the replacement result is returned and the original helper is not executed.
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

    /// <summary>
    /// replaces Bar in a method that stores and reuses the result
    /// so the downstream Foo call must see the redirected value
    /// </summary>
    [Fact]
    public void Redirect_StackIntact_WithLocalFlow()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.CallBarThenFooAdjusted(4f, 2f);

        const float expected = 12.5f;
        Assert.Equal(expected, result);
        Assert.Equal(expected, PatchingTargets.PatchingHelper.LastFooValue);
        Assert.Equal(0, PatchingTargets.PatchingHelper.BarCalls);
        Assert.Equal(1, PatchingTargets.PatchingHelper.FooCalls);
        Assert.Equal(1, PatchingTargets.CallCounter.RedirectStackCalls);
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

public static class RedirectPatches
{
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.Double), AT.REDIRECT, target: "PatchingHelper.Double")]
    public static int ReplaceDouble(int value)
    {
        return 68;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.CallBarThenFooAdjusted), AT.REDIRECT, target: "PatchingHelper.Bar")]
    public static float ReplaceBarForStack(float value)
    {
        PatchingTargets.CallCounter.RedirectStackCalls++;
        return value * 2f;
    }
}
