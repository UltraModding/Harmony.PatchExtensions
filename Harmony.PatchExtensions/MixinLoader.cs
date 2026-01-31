using System.Reflection;
using System.Reflection.Emit;

// ReSharper disable once CheckNamespace
namespace HarmonyLib.PatchExtensions;

/// <summary>
/// Provides functionality for discovering and applying Harmony patches in a Mixin like fashion.
/// </summary>
public static class MixinLoader
{
    static MixinLoader()
    {
        var assemblyName = new AssemblyName("DolfeMixinDynamicAssembly");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _moduleBuilder = assemblyBuilder.DefineDynamicModule("MixinWrappers");
    }
    
    internal class TranspilerConfig
    {
        public TranspilerConfig(AT type, string targetMember, MethodInfo patchMethod, uint occurrence, uint startIndex)
        {
            Type = type;
            TargetMember = targetMember;
            PatchMethod = patchMethod;
            Occurrence = occurrence;
            StartIndex = startIndex;
        }
        
        public AT Type;
        public string TargetMember;
        public MethodInfo PatchMethod;
        public uint Occurrence;
        public uint StartIndex;
    }
    internal class QueuedPatch // for checking for conflicts
    {
        public QueuedPatch(HarmonyMethod harmonyMethod, AT type, bool overwriting, MethodInfo patchMethod)
        {
            HarmonyMethod = harmonyMethod;
            Type = type;
            Overwriting = overwriting;
            PatchMethod = patchMethod;
        }
        
        public HarmonyMethod HarmonyMethod;
        public AT Type;
        public bool Overwriting;
        public MethodInfo PatchMethod;
    }
    /// <summary>
    /// Defines how conflicts between patches should be resolved when multiple patches target the same method.
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
    private static Dictionary<MethodBase, List<TranspilerConfig>> _queuedTranspilers = new();
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
        HashSet<Type>? allowedTypes = patchTypes.Length == 0 ? null : new HashSet<Type>(patchTypes);
        ApplyPatches(harmony, assembly, allowedTypes);
    }

    private static void ApplyPatches(Harmony harmony, Assembly assembly, HashSet<Type>? allowedTypes)
    {
        _queuedTranspilers.Clear();
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
                    
                    if (attr.At == AT.HEAD) // prefix
                    {
                        if (!_queuedPatches.ContainsKey(attr.TargetMethod))
                            _queuedPatches[attr.TargetMethod] = new List<QueuedPatch>();
                        
                        _queuedPatches[attr.TargetMethod].Add(patch);
                        Logger.Log($"Queueing HEAD on {attr.TargetMethod.Name}");
                    }
                    else if (attr.At == AT.RETURN) // postfix
                    {
                        if (!_queuedPatches.ContainsKey(attr.TargetMethod))
                            _queuedPatches[attr.TargetMethod] = new List<QueuedPatch>();
                        
                        _queuedPatches[attr.TargetMethod].Add(patch);
                        Logger.Log($"Queueing RETURN on {attr.TargetMethod.Name}");
                    }
                    else if (attr.At == AT.INVOKE || attr.At == AT.REDIRECT) // before target / replace target
                    {
                        if (!_queuedTranspilers.ContainsKey(attr.TargetMethod))
                            _queuedTranspilers[attr.TargetMethod] = new List<TranspilerConfig>();
                        
                        _queuedTranspilers[attr.TargetMethod].Add(new TranspilerConfig(
                            type: attr.At,
                            targetMember: attr.TargetMember,
                            patchMethod: patchMethod,
                            occurrence: attr.Occurrence,
                            startIndex: attr.StartIndex)
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
        PatchExtensions.ConflictResolver.DetectTranspilerConflicts(_queuedTranspilers, transpilersToRemove);
        foreach (var key in transpilersToRemove)
            _queuedTranspilers.Remove(key);
        
                
        MixinApplier.ApplyPatches(_queuedPatches, harmony, _moduleBuilder);
        
        var transpiler = new HarmonyMethod(typeof(MixinLoader), nameof(TranspilerPiler));
        foreach (var targetMethod in _queuedTranspilers.Keys)
        {
            try
            {
                harmony.Patch(targetMethod, transpiler: transpiler);
                Logger.Log($"Processed patch for {targetMethod.Name}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception {targetMethod.Name}: {ex.Message}");
            }
        }
    }
    
    private static IEnumerable<CodeInstruction> TranspilerPiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        if (!_queuedTranspilers.TryGetValue(original, out var transpilerConfigs))
            return instructions;

        var matcher = new CodeMatcher(instructions);
        foreach (var config in transpilerConfigs)
        {
            matcher.Start();
            
            string requiredClass = "";
            string requiredMethod = config.TargetMember;
            // for Class.Method
            if (requiredMethod.Contains(".")) // C#
            {
                var parts = requiredMethod.Split('.');
                requiredClass = parts[0];
                requiredMethod = parts[1];
            }
            else if (requiredMethod.Contains("::")) // IL
            {
                var parts = requiredMethod.Split(new[] { "::" }, StringSplitOptions.None); // why C#
                requiredClass = parts[0];
                requiredMethod = parts[1];
            }
            
            int currentOccurrence = 0; // for the occurrence
            int relativeOccurrence = 0; //
            while (true)
            {
                matcher.MatchForward(false, 
                    new CodeMatch(instruction => 
                    {
                        if (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) return false;
                        if (!(instruction.operand is MethodInfo methodInfo)) return false;

                        // Method Name
                        if (methodInfo.Name != requiredMethod) return false;

                        // Class Name
                        if (!string.IsNullOrEmpty(requiredClass))
                        {
                            if (methodInfo.DeclaringType == null || methodInfo.DeclaringType.Name != requiredClass)
                                return false;
                        }

                        return true;
                    })
                );

                if (matcher.IsInvalid) break;

                currentOccurrence++;
                if (config.StartIndex == 0 || currentOccurrence >= config.StartIndex)
                {
                    relativeOccurrence++;
    
                    bool correctOccurrence = config.Occurrence == 0 || relativeOccurrence == config.Occurrence;
                    
                    if (correctOccurrence)
                    {
                        if (config.Type == AT.INVOKE)
                            matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, config.PatchMethod));
                        else if (config.Type == AT.REDIRECT)
                            matcher.SetInstruction(new CodeInstruction(OpCodes.Call, config.PatchMethod));

                        if (config.Occurrence != 0) break;
                    }
                }

                matcher.Advance(1);
            }
        }
        
        return matcher.InstructionEnumeration();
    }
}
