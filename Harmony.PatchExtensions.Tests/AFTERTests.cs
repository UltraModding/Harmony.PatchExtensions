namespace HarmonyLib.PatchExtensions.Tests;

public class AFTERTests : IDisposable
{
    private readonly Harmony _harmony;

    public AFTERTests()
    {
        _harmony = new Harmony("tests.patchextensions.after");
        MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Error;
        MixinLoader.ApplyPatches(_harmony, typeof(AFTERTests).Assembly, typeof(AfterPatches));
    }

    /// <summary>
    /// injects after the 2nd call
    /// so the counter increments after the second Helper.Nothin completes
    /// </summary>
    [Fact]
    public void After_InsertAfterSecondCall()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.CallHelpersTwice5();

        Assert.Equal(2, PatchingTargets.PatchingHelper.NothinCalls);
        Assert.Equal(1, PatchingTargets.CallCounter.NothinCalls);
    }

    /// <summary>
    /// injects after the first call (occurrence: 1)
    /// so the counter increments after the first Helper.Nothin completes
    /// </summary>
    [Fact]
    public void After_InsertAfterFirstCall()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.CallHelpersTwice6();

        Assert.Equal(2, PatchingTargets.PatchingHelper.NothinCalls);
        Assert.Equal(1, PatchingTargets.CallCounter.AddCalls);
    }

    /// <summary>
    /// injects after all calls (occurrence: 0)
    /// so the counter increments twice
    /// </summary>
    [Fact]
    public void After_InsertAfterAllCalls()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.CallHelpersTwice7();

        Assert.Equal(2, PatchingTargets.PatchingHelper.NothinCalls);
        Assert.Equal(2, PatchingTargets.CallCounter.AddCalls2);
    }

    /// <summary>
    /// injects after the first call after startIndex: 1 (occurrence: 1, startIndex: 1)
    /// so the counter increments after the second Helper.Nothin completes
    /// </summary>
    [Fact]
    public void After_WithStartIndex()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.CallHelpersTwice();

        Assert.Equal(2, PatchingTargets.PatchingHelper.NothinCalls);
        Assert.Equal(1, PatchingTargets.CallCounter.AddCalls3);
    }

    /// <summary>
    /// injects after Bar when its return value is stored and then passed to Foo
    /// so foo and the downstream call remain correct
    /// </summary>
    [Fact]
    public void After_StackIntact_WithLocalFlow()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.CallBarThenFoo(4f);

        const float expectedFoo = 6.5f;
        const float expectedResult = 9.0f;
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedFoo, PatchingTargets.PatchingHelper.LastFooValue);
        Assert.Equal(1, PatchingTargets.PatchingHelper.BarCalls);
        Assert.Equal(1, PatchingTargets.PatchingHelper.FooCalls);
        Assert.Equal(1, PatchingTargets.CallCounter.AfterStackCalls);
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

public static class AfterPatches
{
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.CallHelpersTwice5), AT.AFTER, target: "PatchingHelper.Nothin", occurrence: 2)]
    public static void AfterSecondNothin()
    {
        PatchingTargets.CallCounter.NothinCalls++;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.CallHelpersTwice6), AT.AFTER, target: "PatchingHelper.Nothin", occurrence: 1)]
    public static void AfterFirstNothin()
    {
        PatchingTargets.CallCounter.AddCalls++;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.CallHelpersTwice7), AT.AFTER, target: "PatchingHelper.Nothin", occurrence: 0)]
    public static void AfterAllNothin()
    {
        PatchingTargets.CallCounter.AddCalls2++;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.CallHelpersTwice), AT.AFTER, target: "PatchingHelper.Nothin", occurrence: 1, startIndex: 1)]
    public static void AfterFirstNothinAfterStartIndex()
    {
        PatchingTargets.CallCounter.AddCalls3++;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.CallBarThenFoo), AT.AFTER, target: "PatchingHelper.Bar")]
    public static void AfterBarForStack()
    {
        PatchingTargets.CallCounter.AfterStackCalls++;
    }
}
