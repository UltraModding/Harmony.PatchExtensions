namespace HarmonyLib.PatchExtensions.Tests;

public class INVOKETests : IDisposable
{
    private readonly Harmony _harmony;

    public INVOKETests()
    {
        _harmony = new Harmony("tests.patchextensions.invoke");
        MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Error;
        MixinLoader.ApplyPatches(_harmony, typeof(INVOKETests).Assembly, typeof(InvokePatches));
    }

    /// <summary>
    /// injects before the 2nd call
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

    /// <summary>
    /// injects before the first call (occurrence: 1)
    /// so the counter increments when the first Helper.Nothin is reached
    /// </summary>
    [Fact]
    public void Invoke_InsertBeforeFirstCall()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.CallHelpersTwice2();

        Assert.Equal(2, PatchingTargets.PatchingHelper.NothinCalls);
        Assert.Equal(1, PatchingTargets.CallCounter.AddCalls);
    }

    /// <summary>
    /// injects before all calls (occurrence: 0)
    /// so the counter increments twice
    /// </summary>
    [Fact]
    public void Invoke_InsertBeforeAllCalls()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.CallHelpersTwice3();

        Assert.Equal(2, PatchingTargets.PatchingHelper.NothinCalls);
        Assert.Equal(2, PatchingTargets.CallCounter.AddCalls2);
    }

    /// <summary>
    /// injects before the first call after startIndex: 1 (occurrence: 1, startIndex: 1)
    /// so the counter increments when the second Helper.Nothin is reached
    /// </summary>
    [Fact]
    public void Invoke_WithStartIndex()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.CallHelpersTwice4();

        Assert.Equal(2, PatchingTargets.PatchingHelper.NothinCalls);
        Assert.Equal(1, PatchingTargets.CallCounter.AddCalls3);
    }

    /// <summary>
    /// injects before the second field access (occurrence: 2)
    /// so the counter increments when the second SomeField assignment is reached
    /// </summary>
    [Fact]
    public void Invoke_InsertBeforeFieldAccess()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.AccessFieldTwice();

        Assert.Equal(1, PatchingTargets.CallCounter.FieldAccessCalls);
    }

    /// <summary>
    /// injects before Bar while its argument is already on the stack,
    /// so the stored value and downstream Foo call must remain correct
    /// </summary>
    [Fact]
    public void Invoke_StackIntact_WithLocalFlow()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.CallBarThenFooAdjusted(4f, 2f);

        const float expected = 10.625f;
        Assert.Equal(expected, result);
        Assert.Equal(expected, PatchingTargets.PatchingHelper.LastFooValue);
        Assert.Equal(1, PatchingTargets.PatchingHelper.BarCalls);
        Assert.Equal(1, PatchingTargets.PatchingHelper.FooCalls);
        Assert.Equal(1, PatchingTargets.CallCounter.InvokeStackCalls);
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

public static class InvokePatches
{
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.CallHelpersTwice), AT.INVOKE, target: "PatchingHelper.Nothin", occurrence: 2)]
    public static void BeforeSecondNothin()
    {
        PatchingTargets.CallCounter.NothinCalls++;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.CallHelpersTwice2), AT.INVOKE, target: "PatchingHelper.Nothin", occurrence: 1)]
    public static void BeforeFirstNothin()
    {
        PatchingTargets.CallCounter.AddCalls++;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.CallHelpersTwice3), AT.INVOKE, target: "PatchingHelper.Nothin", occurrence: 0)]
    public static void BeforeAllNothin()
    {
        PatchingTargets.CallCounter.AddCalls2++;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.CallHelpersTwice4), AT.INVOKE, target: "PatchingHelper.Nothin", occurrence: 1, startIndex: 1)]
    public static void BeforeFirstNothinAfterStartIndex()
    {
        PatchingTargets.CallCounter.AddCalls3++;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.AccessFieldTwice), AT.INVOKE, target: "SomeField", occurrence: 2)]
    public static void BeforeSecondFieldAccess()
    {
        PatchingTargets.CallCounter.FieldAccessCalls++;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.CallBarThenFooAdjusted), AT.INVOKE, target: "PatchingHelper.Bar")]
    public static void BeforeBarForStack()
    {
        PatchingTargets.CallCounter.InvokeStackCalls++;
    }
}
