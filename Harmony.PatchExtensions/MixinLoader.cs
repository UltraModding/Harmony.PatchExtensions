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
                    
                    if (attr.At == AT.HEAD) // prefix
                    {
                        if (!_queuedPatches.ContainsKey(attr.TargetMethod))
                            _queuedPatches[attr.TargetMethod] = new List<QueuedPatch>();
                        
                        _queuedPatches[attr.TargetMethod].Add(patch);
                        Logger.Log($"Queueing HEAD on {attr.TargetMethod.Name}");
                    }
                    else if (attr.At == AT.POSTFIX) // postfix
                    {
                        if (!_queuedPatches.ContainsKey(attr.TargetMethod))
                            _queuedPatches[attr.TargetMethod] = new List<QueuedPatch>();
                        
                        _queuedPatches[attr.TargetMethod].Add(patch);
                        Logger.Log($"Queueing POSTFIX on {attr.TargetMethod.Name}");
                    }
                    else if (attr.At is not AT.HEAD or AT.POSTFIX) // everything else
                    {
                        if (string.IsNullOrEmpty(attr.TargetMember))
                        {
                            Logger.LogWarning($"You must set 'target' in {patchMethod.Name} when using AT.{attr.At}");
                            continue;
                        }
                        
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
    
    private static IEnumerable<CodeInstruction> TranspilerPiler(IEnumerable<CodeInstruction> instructions, MethodBase original, ILGenerator generator)
    {
        if (!_queuedTranspilers.TryGetValue(original, out var transpilerConfigs))
            return instructions;
        
        var matcher = new CodeMatcher(instructions, generator);
        
        foreach (var config in transpilerConfigs)
        {
            matcher.Start();
            
            int currentOccurrence = 0; // for the occurrence
            int relativeOccurrence = 0; //
            
            string requiredClass = "";
            string requiredMethod = config.TargetMember;
            // for Class.Method
            if (requiredMethod.Contains('.')) // C#
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
            
            while (true)
            {
                matcher.MatchForward(false, 
                    new CodeMatch(instruction =>
                    {
                        if (config.Type == AT.RETURN && instruction.opcode == OpCodes.Ret)
                            return true;
                        else if (config.Type == AT.RETURN)
                            return false;
                        
                        bool isMethod = instruction.opcode == OpCodes.Call 
                                        || instruction.opcode == OpCodes.Callvirt 
                                        || instruction.opcode == OpCodes.Newobj;
                        bool isField  = instruction.opcode == OpCodes.Stfld 
                                        || instruction.opcode == OpCodes.Ldfld 
                                        || instruction.opcode == OpCodes.Ldsfld 
                                        || instruction.opcode == OpCodes.Stsfld 
                                        || instruction.opcode == OpCodes.Ldflda 
                                        || instruction.opcode == OpCodes.Ldsflda;
                        
                        if (!isMethod && !isField) return false;
                        
                        string member; // in 'callvirt Class::Method' it would be 'Method'
                        string? declaring; // in 'callvirt Class::Method' it would be 'Class'
                        
                        if (isMethod && instruction.operand is MethodInfo m)
                        {
                            member = m.Name;
                            declaring = m.DeclaringType?.Name;
                        }
                        else if (isField && instruction.operand is FieldInfo f)
                        {
                            member = f.Name;
                            declaring = f.DeclaringType?.Name;
                        }
                        else
                            return false;
                        
                        if (member != requiredMethod)
                            return false;
                        
                        if (!string.IsNullOrEmpty(requiredClass) && declaring != requiredClass)
                            return false;
                        
                        return true;
                    })
                );

                if (matcher.IsInvalid)
                    break;
                
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
                        {
                            var targetInstruction = matcher.Instruction;
                            
                            if (targetInstruction.operand is not MethodBase originalMethod)
                            {
                                Logger.LogWarning($"REDIRECT target '{config.TargetMember}' is a field, not a method. Skipped.");
                                matcher.Advance(1);
                                continue;
                            }
                            
                            if (!DontScrewUpStack(originalMethod, targetInstruction.opcode, config.PatchMethod))
                            {
                                Logger.LogWarning($"REDIRECT patch '{config.PatchMethod.Name}' doesn't match with '{config.TargetMember}'. Skipped.");
                                matcher.Advance(1);
                                continue;
                            }
                            
                            matcher.SetInstruction(new CodeInstruction(OpCodes.Call, config.PatchMethod));
                        }
                        else if (config.Type == AT.AFTER)
                        {
                            // because calling another method (the user's patch) will lose the return value the stack has to be saved and restored after
                            var targetIns = matcher.Instruction;
                            bool hasReturnValue = false;
                            Type? returnType = null;

                            if (targetIns.operand is MethodInfo targetMethod)
                            {
                                hasReturnValue = targetMethod.ReturnType != typeof(void);
                                returnType = targetMethod.ReturnType;
                            }

                            matcher.Advance(1);

                            if (hasReturnValue && returnType != null)
                            {
                                var tempLocal = generator.DeclareLocal(returnType);

                                matcher.Insert(
                                    new CodeInstruction(OpCodes.Stloc, tempLocal),  // store return val
                                    new CodeInstruction(OpCodes.Call, config.PatchMethod),  // call patch
                                    new CodeInstruction(OpCodes.Ldloc, tempLocal)   // restore return val
                                );
                                matcher.Advance(3);
                            }
                            else // no need for all that stuff if its void or returns null
                            {
                                matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, config.PatchMethod));
                            }
                        }
                        else if (config.Type == AT.RETURN)
                        {
                            bool returns = original is MethodInfo mi && mi.ReturnType != typeof(void);
                            
                            if (returns)
                            {
                                var tempLocal = generator.DeclareLocal(((MethodInfo)original).ReturnType);
                                matcher.Insert(
                                    new CodeInstruction(OpCodes.Stloc, tempLocal),
                                    new CodeInstruction(OpCodes.Call, config.PatchMethod),
                                    new CodeInstruction(OpCodes.Ldloc, tempLocal)
                                );
                                matcher.Advance(3);
                            }
                        }
                        
                        if (config.Occurrence != 0) break;
                    }
                }

                matcher.Advance(1);
            
            }
        }
        
        return matcher.InstructionEnumeration();
    }
    
    private static bool DontScrewUpStack(MethodBase originalMethod, OpCode opCode, MethodInfo patchMethod)
    {
        var expectedParams = originalMethod.GetParameters().Select(p => p.ParameterType).ToList();
        
        // if it is calling an instance
        if (opCode != OpCodes.Newobj && !originalMethod.IsStatic)
            expectedParams.Insert(0, originalMethod.DeclaringType!);
        
        var patchParams = patchMethod.GetParameters().Select(p => p.ParameterType).ToList();
        
        if (expectedParams.Count != patchParams.Count)
            return false;
        
        for (int i = 0; i < expectedParams.Count; i++)
        {
            if (!expectedParams[i].IsAssignableFrom(patchParams[i]))
                return false;
        }
        
        Type expectedReturn = originalMethod is MethodInfo mi ? mi.ReturnType : originalMethod.DeclaringType!;
        return patchMethod.ReturnType == expectedReturn;
    }
}
