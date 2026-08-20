using System.Reflection;
using static HarmonyLib.PatchExtensions.MixinLoader;

namespace HarmonyLib.PatchExtensions;

internal static class ConflictResolver
{
    /// <summary>
    /// Detects conflicts between patches targeting the same method.
    /// </summary>
    /// <param name="patches">Dictionary of methods and their queued patches.</param>
    /// <param name="toRemove">HashSet of methods that have conflicts and should be removed.</param>
    /// <exception cref="InvalidOperationException">Thrown when a conflict is detected and ConflictResolver is set to Error.</exception>
    public static void DetectPatchConflicts(
        Dictionary<MethodInfo, List<QueuedPatch>> patches,
        HashSet<MethodInfo> toRemove)
    {
        foreach (KeyValuePair<MethodInfo, List<QueuedPatch>> group in patches.Where(group => group.Value.Count > 1))
        {
            LogConflict(group.Key, group.Value.Select(p => p.HarmonyMethod.method));
            
            switch (MixinLoader.ConflictResolutionMethod)
            {
                case MixinLoader.ConflictResolver.SkipConflicts:
                    toRemove.Add(group.Key);
                    break;
                case MixinLoader.ConflictResolver.Error:
                    toRemove.Add(group.Key);
                    throw new InvalidOperationException(
                        $"Conflict detected: {group.Value.Count} patches target {group.Key.Name}");
            }
        }
    }
    /// <summary>
    /// Detects conflicts between transpiler patches targeting the same method.
    /// </summary>
    /// <param name="transpilers">Dictionary of methods and their transpiler configs.</param>
    /// <param name="toRemove">HashSet of methods that have conflicts and should be removed.</param>
    /// <exception cref="InvalidOperationException">Thrown when a conflict is detected and ConflictResolver is set to Error.</exception>
    public static void DetectTranspilerConflicts(
        Dictionary<MethodBase, List<TranspilerConfig>> transpilers,
        HashSet<MethodBase> toRemove)
    {
        foreach (var trans in transpilers)
        {
            MethodBase method = trans.Key;
            List<TranspilerConfig> configs = trans.Value;
            
            var groupBy = configs.GroupBy(t => (TargetMember: t.TargetMember, t.StartIndex, t.Occurrence));
            
            foreach (var config in groupBy.Where(grouping => grouping.Count() > 1))
            {
                if (config.Count(transpilerConfig => transpilerConfig.Type is AT.REDIRECT or AT.RETURN) <= 1)
                {
                    continue;
                }
                
                LogConflict(method, config.Select(transpilerConfig => transpilerConfig.PatchMethod));
                
                switch (MixinLoader.ConflictResolutionMethod)
                {
                    case MixinLoader.ConflictResolver.SkipConflicts:
                        toRemove.Add(method);
                        break;
                    case MixinLoader.ConflictResolver.Error:
                        toRemove.Add(method);
                        throw new InvalidOperationException(
                            $"Conflict detected: {config.Count()} transpiler patches target {method.Name} at {config.Key}");
                }
            }
        }
    }
    
    /// <summary>
    /// Logs the conflicting methods in a nice fashion
    /// </summary>
    /// <param name="targetMethod">The method that has conflicting Mixins.</param>
    /// <param name="patchMethods">The methods that are conflicting with the target method.</param>
    /// <remarks>
    /// Example output: <br/>
    /// [HarmonyLib.PatchExtensions] Multiple transpiler on Class.Method <br/>
    ///   - Namespace.Class.Method(var name, ...) <br/>
    ///   - Namespace.Class.Method2(var name = False, ...) <br/>
    /// </remarks>
    private static void LogConflict(MethodBase targetMethod, IEnumerable<MethodInfo> patchMethods)
    {
        Logger.LogWarning($"Multiple Mixins queued for {targetMethod.DeclaringType?.FullName}.{targetMethod.Name}");
    
        foreach (var method in patchMethods)
        {
            var parameters = string.Join(", ", method.GetParameters() // im sorry
                .Select(p => $"{p.ParameterType.Name} {p.Name}" + 
                             (p.HasDefaultValue ? $" = {p.DefaultValue}" : "")));
            Logger.LogWarning($"  - {method.DeclaringType?.FullName}.{method.Name}({parameters})");
        }
    }
}