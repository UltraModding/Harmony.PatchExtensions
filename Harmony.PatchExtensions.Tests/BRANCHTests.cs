using HarmonyLib.Tools;

namespace HarmonyLib.PatchExtensions.Tests;

public class BRANCHTests : IDisposable
{
    private readonly Harmony _harmony;

    public BRANCHTests()
    {
        _harmony = new Harmony("tests.patchextensions.branch");
        MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Error;
        MixinLoader.ApplyPatches(_harmony, typeof(BRANCHTests).Assembly, typeof(BranchPatches));
    }

    [Fact]
    public void BranchTrue_RunsOnlyWhenConditionTrue()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.Branch(true);

        Assert.Equal(1, PatchingTargets.CallCounter.BranchTrueCalls);
        Assert.Equal(0, PatchingTargets.CallCounter.BranchFalseCalls);
        Assert.Equal(2, result); // Double(1)
    }

    [Fact]
    public void BranchFalse_RunsOnlyWhenConditionFalse()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.Branch(false);

        Assert.Equal(0, PatchingTargets.CallCounter.BranchTrueCalls);
        Assert.Equal(1, PatchingTargets.CallCounter.BranchFalseCalls);
        Assert.Equal(4, result); // Double(2)
    }

    [Fact]
    public void Branch2_TrueAndFalse_IndependentAcrossCalls()
    {
        ResetCounters();

        var target = new PatchingTargets();
        target.Branch2(true);
        target.Branch2(false);
        target.Branch2(true);

        Assert.Equal(2, PatchingTargets.CallCounter.BranchTrueCalls);
        Assert.Equal(1, PatchingTargets.CallCounter.BranchFalseCalls);
    }

    private static void ResetCounters() => PatchingTargets.CallCounter.Reset();

    public void Dispose() => _harmony.UnpatchSelf();
}

public static class BranchPatches
{
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.Branch), AT.BRANCH_TRUE, occurrence: 0)]
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.Branch2), AT.BRANCH_TRUE, occurrence: 0)]
    public static void BranchTrue()
    {
        PatchingTargets.CallCounter.BranchTrueCalls++;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.Branch), AT.BRANCH_FALSE, occurrence: 0)]
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.Branch2), AT.BRANCH_FALSE, occurrence: 0)]
    public static void BranchFalse()
    {
        PatchingTargets.CallCounter.BranchFalseCalls++;
    }
}