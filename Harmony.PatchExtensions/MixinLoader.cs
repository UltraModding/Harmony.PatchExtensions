using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

// ReSharper disable once CheckNamespace
namespace HarmonyLib.PatchExtensions;

/// <summary>
/// Provides functionality for discovering and applying Harmony patches in a Mixin like fashion.
/// </summary>
public static class MixinLoader
{
    /// <summary>
    /// The latest version of Harmony.PatchExtensions that causes inconsistencies between new and old versions
    /// </summary>
    private static Version LatestBreakingVersion { get; } = new Version(1, 2, 0);
    
    static MixinLoader()
    {
        var assemblyName = new AssemblyName("DolfeMixinDynamicAssembly");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _moduleBuilder = assemblyBuilder.DefineDynamicModule("MixinWrappers");
    }
    
    /// <summary>
    /// Defines how conflicts between patches should be resolved when multiple patches targetMember the same method.
    /// </summary>
    public enum ConflictResolver
    {
        /// <summary>
        /// Logs a warning message when conflicts are detected but continues to apply all patches.
        /// </summary>
        Warn,

        /// <summary>
        /// Throws an <see cref="InvalidOperationException"/> when conflicts are detected.
        /// </summary>
        Error,

        /// <summary>
        /// Automatically skips conflicting patches and only applies not conflicting patches.
        /// </summary>
        SkipConflicts,
    }

    /// <summary>
    /// What way conflicts should be resolved
    /// <see cref="MixinLoader.ConflictResolver"/>
    /// </summary>
    public static ConflictResolver ConflictResolutionMethod = ConflictResolver.Warn;
    
    private static ModuleBuilder _moduleBuilder;
    
    internal static Dictionary<MethodBase, List<TranspilerConfig>> QueuedTranspilers = new();
    internal static Dictionary<Assembly, List<TranspilerConfig>> AssemblyTranspilers = new();
    private static Dictionary<MethodInfo, List<QueuedPatch>> _queuedPatches = new();
    
    /// <summary>
    /// Scans <paramref name="assembly"/> for methods decorated with <see cref="PatchAttribute"/>
    /// and applies their Harmony patches to the configured targets.
    /// </summary>
    /// <param name="harmony">The Harmony instance used to apply patches.</param>
    /// <param name="assembly">The assembly that contains patch methods.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="ConflictResolutionMethod"/> is set to <see cref="ConflictResolver.Error"/>
    /// and conflicting patches or transpilers are detected.
    /// </exception>
    public static void ApplyPatches(Harmony harmony, Assembly assembly)
    {
        WarnOutOfDate();
        ApplyPatches(harmony, assembly, Array.Empty<Type>());
    }
    
    /// <summary>
    /// Applying patches for a single type, used in testing
    /// Can also be used like MixinLoader.ApplyPatches(harmony, assembly, typeof(PatchClass)) to only patch using a single class
    /// </summary>
    /// <param name="harmony">The Harmony instance used to apply patches.</param>
    /// <param name="assembly">The assembly that contains patch methods.</param>
    /// <param name="patchTypes">Types containing patch methods to apply. If empty, all types in the assembly are done.</param>
    public static void ApplyPatches(Harmony harmony, Assembly assembly, params Type[] patchTypes)
    {
        WarnOutOfDate();
        HashSet<Type>? allowedTypes = patchTypes.Length == 0 ? null : new HashSet<Type>(patchTypes);
        ApplyPatches(harmony, assembly, allowedTypes);
    }
    
    private static void WarnOutOfDate()
    {
        var stackFrame = new StackTrace().GetFrame(2); // 0 this, 1 ApplyPatches, 2 caller
        var callerAssembly = stackFrame?.GetMethod()?.Module.Assembly ?? null;
        
        if (callerAssembly == null)
            return;
        
        var refUtil = callerAssembly.GetReferencedAssemblies()
            .FirstOrDefault(a => a.Name == Assembly.GetExecutingAssembly().GetName().Name);
        
        if (refUtil != null && refUtil.Version < LatestBreakingVersion)
        {
            Logger.LogError($"{callerAssembly.FullName} is using an outdated version of Harmony.PatchExtensions ({refUtil.Version}), a breaking update has been introduced since then ({LatestBreakingVersion})");
        }
    }
    
    private static void ApplyPatches(Harmony harmony, Assembly assembly, HashSet<Type>? allowedTypes)
    {
        QueuedTranspilers.Clear();
        _queuedPatches.Clear();

        foreach (var type in assembly.GetTypes())
        {
            if (allowedTypes != null && !allowedTypes.Contains(type))
                continue;

            foreach (var patchMethod in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var attrs = patchMethod.GetCustomAttributes<PatchAttribute>();
                foreach (var attr in attrs)
                {
                    if (attr.DoNotPatch)
                    {
                        Logger.LogWarning($"{patchMethod.Name} has attribute errors so it has been skipped");
                        continue;
                    }
                    
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                    if (attr.TargetMethod == null)
                    {
                        Logger.LogWarning($"You must set TargetMethod in {patchMethod.Name} for the Patch to work");
                        continue;
                    }
                    
                    var harmonyMethod = new HarmonyMethod(patchMethod);
                    QueuedPatch patch = new QueuedPatch(
                        harmonyMethod: harmonyMethod,
                        type: attr.At,
                        overwriting: attr.Overwriting,
                        patchMethod: patchMethod
                    );
                    
                    switch (attr.At)
                    {
                        case AT.HEAD:
                        case AT.POSTFIX:
                        case AT.FINALLY:
                            if (!_queuedPatches.ContainsKey(attr.TargetMethod!))
                                _queuedPatches[attr.TargetMethod] = new List<QueuedPatch>();
                            
                            _queuedPatches[attr.TargetMethod].Add(patch);
                            Logger.Log($"Queueing {attr.At} on {attr.TargetMethod.Name}");
                            break;
                        case AT.BRANCH_TRUE:
                        case AT.BRANCH_FALSE:
                        case AT.LOOP_BEFORE:
                        case AT.LOOP_TOP:
                        case AT.LOOP_BOTTOM:
                        case AT.LOOP_AFTER:
                            AddTranspiler(QueuedTranspilers, attr.TargetMethod!,
                                new TranspilerConfig(
                                    type: attr.At,
                                    targetMember: attr.TargetMember,
                                    patchMethod: patchMethod,
                                    occurrence: attr.Occurrence,
                                    startIndex: attr.StartIndex,
                                    argIndex: attr.ArgIndex,
                                    targetType: attr.TargetType!
                                )
                            );
                            break;
                        default:
                            if (string.IsNullOrEmpty(attr.TargetMember))
                            {
                                Logger.LogWarning($"You must set 'targetMember' in {patchMethod.Name} when using AT.{attr.At}");
                                continue;
                            }
                            
                            AddTranspiler(QueuedTranspilers, attr.TargetMethod!,
                                new TranspilerConfig(
                                    type: attr.At,
                                    targetMember: attr.TargetMember,
                                    patchMethod: patchMethod,
                                    occurrence: attr.Occurrence,
                                    startIndex: attr.StartIndex,
                                    argIndex: attr.ArgIndex,
                                    targetType: attr.TargetType!
                                )
                            );
                            break;
                    }
                }
                
                var asmAttrs = patchMethod.GetCustomAttributes<AssemblyPatchAttribute>();
                foreach (var attr in asmAttrs)
                {
                    if (attr.DoNotPatch)
                    {
                        Logger.LogWarning($"{patchMethod.Name} has attribute errors so it has been skipped");
                        continue;
                    }
                    
                    Assembly targAsm = attr.TargetType.Assembly;
                    if (!AssemblyTranspilers.ContainsKey(targAsm))
                        AssemblyTranspilers[targAsm] = new List<TranspilerConfig>();
                    
                    AssemblyTranspilers[targAsm].Add(new TranspilerConfig(
                        type: attr.At,
                        targetMember: attr.TargetMember,
                        patchMethod: patchMethod,
                        occurrence: attr.Occurrence,
                        startIndex: 0,
                        argIndex: 0,
                        targetType: attr.TargetType
                    ));
                }
            }
        }
        
        // for assembly transpilers read the assembly and add queued transpilersS
        foreach (KeyValuePair<Assembly, List<TranspilerConfig>> thing in AssemblyTranspilers)
        {
            Assembly targetAssembly = thing.Key;
            List<TranspilerConfig> configs = thing.Value;
            
            foreach (var config in configs)
            {
                if (string.IsNullOrEmpty(config.TargetMember))
                {
                    Logger.LogError($"TargetMember is null or empty in {config.PatchMethod.Name}");
                    continue;
                }
                
                FieldInfo? fieldInfo = config.TargetType?.GetField(config.TargetMember)
                    ?? throw new InvalidOperationException($"TargetType required to resolve field '{config.TargetMember}'");
                
                IEnumerable<Type> typesToScan = config.TargetType != null
                                                                  ? new[] { config.TargetType }
                                                                  : targetAssembly.GetTypes();
                foreach (var type in typesToScan)
                {
                    foreach (MethodBase methodBase in OpCodeHelper.FindMethodsUsingField(type, fieldInfo))
                    {
                        if (!QueuedTranspilers.ContainsKey(methodBase))
                            QueuedTranspilers[methodBase] = new List<TranspilerConfig>();
                        
                        QueuedTranspilers[methodBase].Add(new TranspilerConfig(
                            type: config.Type,
                            targetMember: null,
                            patchMethod: config.PatchMethod,
                            occurrence: config.Occurrence,
                            startIndex: 0,
                            argIndex: 0,
                            targetType: config.TargetType!)
                        );
                    }
                }
            }
        }
        
        // Process patch conflicts
        var patchesToRemove = new HashSet<MethodInfo>();
        PatchExtensions.ConflictResolver.DetectPatchConflicts(_queuedPatches, patchesToRemove);
        foreach (var key in patchesToRemove)
            _queuedPatches.Remove(key);
        
        // Process transpiler conflicts
        var transpilersToRemove = new HashSet<MethodBase>();
        PatchExtensions.ConflictResolver.DetectTranspilerConflicts(QueuedTranspilers, transpilersToRemove);
        foreach (var key in transpilersToRemove)
            QueuedTranspilers.Remove(key);
        
        MixinApplier.ApplyPatches(_queuedPatches, harmony, _moduleBuilder);
        
        var transpiler = new HarmonyMethod(typeof(TranspilerApplier), nameof(TranspilerApplier.TranspilerPiler));
        foreach (var targetMethod in QueuedTranspilers.Keys)
        {
            try
            {
                Logger.Log($"Processing patch for {targetMethod.Name}");
                harmony.Patch(targetMethod, transpiler: transpiler);
                Logger.Log($"Processed patch for {targetMethod.Name}");
            }
            catch (Exception ex)
            {
                StackFrame[] frames = new StackTrace().GetFrames();
                string log = string.Join(", ", frames.Select(_ => $"{_.GetFileName()}:{_.GetFileLineNumber()}"));
                Logger.LogError($"Exception {targetMethod.Name}: {ex.Message}, type: {ex.GetType()}, {log}");
                Exception? cur = ex;
                int depth = 0;
                while (cur != null)
                {
                    Logger.LogError($"[{depth}] {cur.GetType()}: {cur.Message}");
                    cur = cur.InnerException;
                    depth++;
                }
                if (ex is HarmonyException hEx)
                {
                    foreach (var kv in hEx.GetInstructionsWithOffsets())
                        Logger.LogError($"IL[{kv.Key:X4}] {kv.Value}");
                    Logger.LogError($"ErrorOffset: {hEx.GetErrorOffset()}, ErrorIndex: {hEx.GetErrorIndex()}");
                }
            }
        }
    }
    
    private static void AddTranspiler<TKey>(
        Dictionary<TKey, List<TranspilerConfig>> dict,
        TKey key,
        TranspilerConfig config
    )
    {
        if (!dict.TryGetValue(key, out var list))
            dict[key] = list = new List<TranspilerConfig>();
        list.Add(config);
    }
}
