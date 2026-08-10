namespace HarmonyLib.PatchExtensions.Tests;

public class ARGTests : IDisposable
{
    private readonly Harmony _harmony;
    
    public ARGTests()
    {
        _harmony = new Harmony("tests.patchextensions.arg");
        MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Error;
        MixinLoader.ApplyPatches(_harmony, typeof(ARGTests).Assembly, typeof(ArgPatches));
    }
    
    /// <summary>
    /// Offset gets multiplied by 2 so the result should be
    /// (value * 1.5) + offset * 2
    /// (5 * 1.5) + 2 * 2
    /// 7.5 + 4
    /// = 11.5
    /// </summary>
    [Fact]
    public void Arg_OffsetArgResult()
    {
        ResetCounters();
        
        var target = new PatchingTargets();
        var result = target.BarWithTwoArgs(5, 2);
        
        Assert.Equal(1, PatchingTargets.PatchingHelper.BarTwoArgsCalls);
        Assert.Equal(11.5f, result);
    }
    
    /// <summary>
    /// Check arg values are as expected
    /// </summary>
    [Fact]
    public void Arg_ValueArgUntouched()
    {
        ResetCounters();
        
        var target = new PatchingTargets();
        target.BarWithTwoArgs(5, 2);
        
        Assert.Equal(5, PatchingTargets.PatchingHelper.BarTwoArgsValueValue);
        Assert.Equal(4, PatchingTargets.PatchingHelper.BarTwoArgsOffsetValue);
    }
    
    /// <summary>
    /// Check BarTwoArgsCalls only runs once
    /// </summary>
    [Fact]
    public void Arg_DoesNotDuplicateOrSkipCall()
    {
        ResetCounters();
        
        var target = new PatchingTargets();
        target.BarWithTwoArgs(5, 2);
        
        Assert.Equal(1, PatchingTargets.PatchingHelper.BarTwoArgsCalls);
    }
    
    /// <summary>
    /// Check BarTwoArgsTwice
    /// </summary>
    [Fact]
    public void Arg_OccurrenceTargetsCorrectCallSite()
    {
        ResetCounters();
        
        var target = new PatchingTargets();
        (float res1, float res2) = target.BarWithTwoArgsTwice(5, 2);
        
        Assert.Equal(2, PatchingTargets.PatchingHelper.BarTwoArgsCalls);
        Assert.Equal(9.5f, res1);
        Assert.Equal(11.5f, res2);
    }
    
    
    /// <summary>
    /// Both offset vars are set to og * 2
    /// </summary>
    [Fact]
    public void Arg_Arg_OccurrenceZeroMatchesAll()
    {
        ResetCounters();
        
        var target = new PatchingTargets();
        (float res1, float res2) = target.BarWithTwoArgsTwice2(5, 2);
        
        Assert.Equal(2, PatchingTargets.PatchingHelper.BarTwoArgsCalls);
        Assert.Equal(11.5f, res1);
        Assert.Equal(11.5f, res2);
    }
    
    
    /// <summary>
    /// BarWithTwoArgsTwice3 has offset return itself so it should be 9.5 for both
    /// </summary>
    [Fact]
    public void Arg_OriginalValuePassedThroughWhenUnmodified()
    {
        ResetCounters();
        
        var target = new PatchingTargets();
        (float res1, float res2) = target.BarWithTwoArgsTwice3(5, 2);
        
        Assert.Equal(2, PatchingTargets.PatchingHelper.BarTwoArgsCalls);
        Assert.Equal(9.5f, res1);
        Assert.Equal(9.5f, res2);
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

public static class ArgPatches
{
    // startIndex is the value
    // occurrence is the call
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.BarWithTwoArgs), AT.ARG, startIndex: 1, occurrence: 0)]
    public static float ReplaceOffset(float original)
    {
        return original * 2;
    }
    
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.BarWithTwoArgsTwice), AT.ARG, startIndex: 1, occurrence: 2)]
    public static float ReplaceOffsetBarWithTwoArgsTwice(float original)
    {
        return original * 2;
    }
    
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.BarWithTwoArgsTwice2), AT.ARG, startIndex: 1, occurrence: 0)]
    public static float ReplaceOffsetBarWithTwoArgsTwice2(float original)
    {
        return original * 2;
    }
    
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.BarWithTwoArgsTwice3), AT.ARG, startIndex: 1, occurrence: 0)]
    public static float ReplaceOffsetBarWithTwoArgsTwice3(float original)
    {
        return original;
    }

    
}