using HarmonyLib.Tools;

namespace HarmonyLib.PatchExtensions.Tests;

public class FinallyTests : IDisposable
{
    private readonly Harmony _harmony;
    
    public FinallyTests()
    {
        _harmony = new Harmony("tests.patchextensions.finally");
        MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Error;
        MixinLoader.ApplyPatches(_harmony, typeof(FinallyTests).Assembly, typeof(FinallyPatches));
    }
    
    [Fact]
    public void Finally_RunsOnNormalReturn_NoException()
    {
        ResetCounters();
        
        var target = new PatchingTargets();
        var result = target.DivideOrThrow(10, 2);
        
        Assert.Equal(5, result);
        Assert.Equal(2, PatchingTargets.CallCounter.FinallyCalls); // DivideOrThrow's own finally block + the AT.FINALLY patch
        Assert.Null(PatchingTargets.CallCounter.LastFinallyException);
    }
    
    [Fact]
    public void Finally_RunsOnException_ExceptionStillPropagates()
    {
        ResetCounters();
        
        var target = new PatchingTargets();
        
        Assert.Throws<DivideByZeroException>(() => target.DivideOrThrow2(10, 0));
        
        Assert.Equal(1, PatchingTargets.CallCounter.FinallyCalls);
        Assert.IsType<DivideByZeroException>(PatchingTargets.CallCounter.LastFinallyException);
    }
    
    [Fact]
    public void Finally_SwallowsException_NoThrow_DefaultResult()
    {
        ResetCounters();
        
        var target = new PatchingTargets();
        var result = target.DivideOrThrow3(10, 0);
        
        Assert.Equal(0, result);
        Assert.Equal(1, PatchingTargets.CallCounter.FinallySwallowCalls);
    }
    
    private static void ResetCounters() => PatchingTargets.CallCounter.Reset();
    
    public void Dispose() => _harmony.UnpatchSelf();
}

public static class FinallyPatches
{
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.DivideOrThrow), AT.FINALLY)]
    public static void RecordFinally(Exception __exception)
    {
        PatchingTargets.CallCounter.FinallyCalls++;
        PatchingTargets.CallCounter.LastFinallyException = __exception;
    }
    
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.DivideOrThrow2), AT.FINALLY)]
    public static void RecordFinallyOnException(Exception __exception)
    {
        PatchingTargets.CallCounter.FinallyCalls++;
        PatchingTargets.CallCounter.LastFinallyException = __exception;
    }
    
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.DivideOrThrow3), AT.FINALLY)]
    public static Exception SwallowFinally(Exception __exception)
    {
        PatchingTargets.CallCounter.FinallySwallowCalls++;
        return null; // suppress
    }
}