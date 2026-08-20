namespace HarmonyLib.PatchExtensions.Tests;

public class RETURNTests : IDisposable
{
    private readonly Harmony _harmony;

    public RETURNTests()
    {
        _harmony = new Harmony("tests.patchextensions.returnat");
        MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Error;
        MixinLoader.ApplyPatches(_harmony, typeof(RETURNTests).Assembly, typeof(ReturnAtPatches));
    }

    /// <summary>
    /// the compiler funnels both return statements through a single 'ret' in Debug IL,
    /// so this and the trailing-return test hit the same injected call site -
    /// only the runtime value differs. Counter increments once, result is untouched
    /// </summary>
    [Fact]
    public void Return_RunsOnEarlyReturnPoint()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.ReturnEarly(-5);

        Assert.Equal(-1, result);
        Assert.Equal(1, PatchingTargets.CallCounter.ReturnCalls);
    }

    /// <summary>
    /// same injected call site as the early-return test (see above), taken via the
    /// trailing-return branch instead. Counter increments once, result is untouched
    /// </summary>
    [Fact]
    public void Return_RunsOnTrailingReturnPoint()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.ReturnEarly(5);

        Assert.Equal(5, result);
        Assert.Equal(1, PatchingTargets.CallCounter.ReturnCalls);
    }

    private static void ResetCounters() => PatchingTargets.CallCounter.Reset();

    /// <inheritdoc />
    public void Dispose() => _harmony.UnpatchSelf();
}

public static class ReturnAtPatches
{
    // targetMember is required by MixinLoader for AT.RETURN but unused by the transpiler, which matches on every 'ret'.
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.ReturnEarly), AT.RETURN, targetMember: "unused", occurrence: 0)]
    public static void OnReturn()
    {
        PatchingTargets.CallCounter.ReturnCalls++;
    }
}
