namespace HarmonyLib.PatchExtensions.Tests;

public class LOCALTests : IDisposable
{
    private readonly Harmony _harmony;

    public LOCALTests()
    {
        _harmony = new Harmony("tests.patchextensions.local");
        MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Error;
        MixinLoader.ApplyPatches(_harmony, typeof(LOCALTests).Assembly, typeof(LocalPatches));
    }

    /// <summary>
    /// occurrence counts every local read/write in the method in IL order (total is the only
    /// local here: write, read, write). occurrence: 1 is the first of those, the initial
    /// assignment 'total = a', so the counter increments once and the patch observes a's value
    /// </summary>
    [Fact]
    public void LocalWrite_OccurrenceTargetsFirstAssignment()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.LocalWriteTwice(2, 3);

        Assert.Equal(5, result);
        Assert.Equal(1, PatchingTargets.CallCounter.LocalWriteCalls);
        Assert.Equal(2, PatchingTargets.CallCounter.LastLocalWriteValue);
    }

    /// <summary>
    /// occurrence: 3 is the third local read/write in IL order (write, read, write) -
    /// the compound assignment's store 'total += b', so the patch observes the summed value
    /// </summary>
    [Fact]
    public void LocalWrite_OccurrenceTargetsSecondAssignment()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.LocalWriteTwice2(2, 3);

        Assert.Equal(5, result);
        Assert.Equal(1, PatchingTargets.CallCounter.LocalWriteCalls2);
        Assert.Equal(5, PatchingTargets.CallCounter.LastLocalWriteValue2);
    }

    /// <summary>
    /// occurrence: 2 is the second local read/write in IL order (write, read, write) -
    /// the read of 'total' inside 'total += b', so the patch observes the pre-addition value
    /// </summary>
    [Fact]
    public void LocalRead_OccurrenceTargetsReadInsideCompoundAssignment()
    {
        ResetCounters();

        var target = new PatchingTargets();
        var result = target.LocalWriteTwice3(2, 3);

        Assert.Equal(5, result);
        Assert.Equal(1, PatchingTargets.CallCounter.LocalReadCalls);
        Assert.Equal(2, PatchingTargets.CallCounter.LastLocalReadValue);
    }

    private static void ResetCounters() => PatchingTargets.CallCounter.Reset();

    /// <inheritdoc />
    public void Dispose() => _harmony.UnpatchSelf();
}

public static class LocalPatches
{
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.LocalWriteTwice), AT.LOCAL_WRITE, targetMember: "total", occurrence: 1)]
    public static void FirstWrite(int value)
    {
        PatchingTargets.CallCounter.LocalWriteCalls++;
        PatchingTargets.CallCounter.LastLocalWriteValue = value;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.LocalWriteTwice2), AT.LOCAL_WRITE, targetMember: "total", occurrence: 3)]
    public static void SecondWrite(int value)
    {
        PatchingTargets.CallCounter.LocalWriteCalls2++;
        PatchingTargets.CallCounter.LastLocalWriteValue2 = value;
    }

    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.LocalWriteTwice3), AT.LOCAL_READ, targetMember: "total", occurrence: 2)]
    public static void ReadTotal(int value)
    {
        PatchingTargets.CallCounter.LocalReadCalls++;
        PatchingTargets.CallCounter.LastLocalReadValue = value;
    }
}
