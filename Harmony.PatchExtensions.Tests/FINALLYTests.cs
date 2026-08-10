namespace HarmonyLib.PatchExtensions.Tests;

public class FINALLYTests : IDisposable
{
    private readonly Harmony _harmony;
    private readonly PatchingTargets _target = new();
    
    public FINALLYTests()
    {
        _harmony = new Harmony("tests.patchextensions.finally");
        MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Error;
        MixinLoader.ApplyPatches(_harmony, typeof(FINALLYTests).Assembly, typeof(FinallyPatches));
        ResetCounters();
    }
    
    private static void ResetCounters()
    {
        PatchingTargets.CallCounter.Reset();
        PatchingTargets.PatchingHelper.Reset();
    }
    
    [Fact]
    public void Finally_RunsOnNormalReturn_NoException()
    {
        int result = _target.DivideOrThrow2(10, 2);
        
        Assert.Equal(5, result);
        Assert.Equal(1, PatchingTargets.CallCounter.FinallyCalls);
        Assert.Null(PatchingTargets.CallCounter.LastFinallyException);
    }
    
    [Fact]
    public void Finally_RunsOnException_AndExceptionPropagates()
    {
        Assert.Throws<DivideByZeroException>(() => _target.DivideOrThrow2(10, 0));
        
        Assert.Equal(1, PatchingTargets.CallCounter.FinallyCalls);
        Assert.NotNull(PatchingTargets.CallCounter.LastFinallyException);
        Assert.IsType<DivideByZeroException>(PatchingTargets.CallCounter.LastFinallyException);
    }
    
    [Fact]
    public void Finally_CanSwallowException()
    {
        // No exception should propagate — swallowed by the finalizer
        var ex = Record.Exception(() => _target.DivideOrThrow3(10, 0));
        
        Assert.Null(ex);
        Assert.Equal(1, PatchingTargets.CallCounter.FinallySwallowCalls);
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
        _harmony.UnpatchSelf();
    }
}

public static class FinallyPatches
{
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.DivideOrThrow2), AT.FINALLY)]
    public static void OnDivideFinally(Exception __exception)
    {
        PatchingTargets.CallCounter.FinallyCalls++;
        PatchingTargets.CallCounter.LastFinallyException = __exception;
    }
    
    [Patch(typeof(PatchingTargets), nameof(PatchingTargets.DivideOrThrow3), AT.FINALLY)]
    public static Exception? SwallowDivideException(Exception __exception)
    {
        PatchingTargets.CallCounter.FinallySwallowCalls++;
        return null;
    }
}