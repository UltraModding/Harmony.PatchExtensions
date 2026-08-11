using HarmonyLib.Tools;

namespace HarmonyLib.PatchExtensions.Tests;

public class LOOPTests : IDisposable
{
    private readonly Harmony _harmony;

    public LOOPTests()
    {
        _harmony = new Harmony("tests.patchextensions.loop");
        MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Error;
        MixinLoader.ApplyPatches(_harmony, typeof(LOOPTests).Assembly, typeof(LoopPatches));
    }

    [Fact]
    public void LoopBefore_RunsOnceRegardlessOfIterationCount()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.SumLoop(5);

        Assert.Equal(1, PatchingTargets.CallCounter.BeforeLoopCalls);
    }

    [Fact]
    public void LoopTop_RunsOncePerIteration()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.SumLoop(5);

        Assert.Equal(5, PatchingTargets.CallCounter.LoopTopCalls);
        Assert.Equal(10, result); // 0+1+2+3+4
    }

    [Fact]
    public void LoopBottom_RunsOncePerCompletedIteration_NoBreak()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.SumLoop(5);

        Assert.Equal(5, PatchingTargets.CallCounter.LoopBottomCalls);
    }

    [Fact]
    public void LoopBottom_SkippedOnIterationThatBreaks()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.SumLoopWithBreak(5, breakAt: 3);

        // iterations 0,1,2 reach bottom; iteration 3 breaks before bottom
        Assert.Equal(3, PatchingTargets.CallCounter.LoopBottomCalls);
    }

    [Fact]
    public void LoopBottom_SkippedOnIterationThatContinues()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.SumLoopWithContinue(5, skip: 2);

        // iteration 2 continues before bottom
        // 0,1,3,4 reach it
        Assert.Equal(4, PatchingTargets.CallCounter.LoopBottomCalls);
    }

    [Fact]
    public void LoopTop_StillRunsForContinuedIteration()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.SumLoopWithContinue(5, skip: 2);

        Assert.Equal(5, PatchingTargets.CallCounter.LoopTopCalls);
    }

    [Fact]
    public void LoopAfter_RunsOnceOnNormalCompletion()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.SumLoop(5);

        Assert.Equal(1, PatchingTargets.CallCounter.LoopExitCalls);
    }

    [Fact]
    public void LoopAfter_RunsOnceAfterBreak()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.SumLoopWithBreak(5, breakAt: 2);

        Assert.Equal(1, PatchingTargets.CallCounter.LoopExitCalls);
    }

    [Fact]
    public void LoopAfter_RunsOnceAfterContinue()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.SumLoopWithContinue(5, skip: 2);

        Assert.Equal(1, PatchingTargets.CallCounter.LoopExitCalls);
    }

    [Fact]
    public void Loop_AllFourHooksFireTogetherWithoutConflict()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.SumLoop(4);

        Assert.Equal(1, PatchingTargets.CallCounter.BeforeLoopCalls);
        Assert.Equal(4, PatchingTargets.CallCounter.LoopTopCalls);
        Assert.Equal(4, PatchingTargets.CallCounter.LoopBottomCalls);
        Assert.Equal(1, PatchingTargets.CallCounter.LoopExitCalls);
        Assert.Equal(6, result); // 0+1+2+3
    }

    private static void ResetCounters() => PatchingTargets.CallCounter.Reset();

    public void Dispose() => _harmony.UnpatchSelf();
}

public static class LoopPatches
{
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.SumLoop), AT.LOOP_BEFORE, occurrence: 0)]
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.SumLoopWithBreak), AT.LOOP_BEFORE, occurrence: 0)]
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.SumLoopWithContinue), AT.LOOP_BEFORE, occurrence: 0)]
    public static void LoopBefore()
    {
        PatchingTargets.CallCounter.BeforeLoopCalls++;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.SumLoop), AT.LOOP_TOP, occurrence: 0)]
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.SumLoopWithBreak), AT.LOOP_TOP, occurrence: 0)]
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.SumLoopWithContinue), AT.LOOP_TOP, occurrence: 0)]
    public static void LoopTop()
    {
        PatchingTargets.CallCounter.LoopTopCalls++;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.SumLoop), AT.LOOP_BOTTOM, occurrence: 0)]
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.SumLoopWithBreak), AT.LOOP_BOTTOM, occurrence: 0)]
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.SumLoopWithContinue), AT.LOOP_BOTTOM, occurrence: 0)]
    public static void LoopBottom()
    {
        PatchingTargets.CallCounter.LoopBottomCalls++;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.SumLoop), AT.LOOP_AFTER, occurrence: 0)]
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.SumLoopWithBreak), AT.LOOP_AFTER, occurrence: 0)]
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.SumLoopWithContinue), AT.LOOP_AFTER, occurrence: 0)]
    public static void LoopAfter()
    {
        PatchingTargets.CallCounter.LoopExitCalls++;
    }
}