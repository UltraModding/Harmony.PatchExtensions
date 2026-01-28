using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

// ReSharper disable once CheckNamespace
namespace PatchExtensions;

public static class MixinLoader
{
    static MixinLoader()
    {
        var assemblyName = new AssemblyName("DolfeMixinDynamicAssembly");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _moduleBuilder = assemblyBuilder.DefineDynamicModule("MixinWrappers");
    }
    private class TranspilerConfig
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
    private class QueuedPatch // checking for conflicts
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
    public enum ConflictResolver
    {
        Warn,
        Error,
        SkipConflicts,
    }

    private static ModuleBuilder _moduleBuilder;
    
    public static ConflictResolver ConflictResolutionMethod = ConflictResolver.Warn;
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
        _queuedTranspilers.Clear();

        foreach (var type in assembly.GetTypes())
        {
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
        DetectPatchConflicts(_queuedPatches, patchesToRemove);
        foreach (var key in patchesToRemove)
            _queuedPatches.Remove(key);

        // Process transpiler conflicts
        var transpilersToRemove = new HashSet<MethodBase>();
        DetectTranspilerConflicts(_queuedTranspilers, transpilersToRemove);
        foreach (var key in transpilersToRemove)
            _queuedTranspilers.Remove(key);
        
        foreach (KeyValuePair<MethodInfo, List<QueuedPatch>> patch in _queuedPatches)
        {
            foreach (QueuedPatch queuedPatch in patch.Value)
            {
                switch (queuedPatch.Type)
                {
                    case AT.HEAD:
                        if (queuedPatch.Overwriting && queuedPatch.PatchMethod.ReturnType != typeof(bool) && queuedPatch.PatchMethod.ReturnType != typeof(void))
                        {
                            if (queuedPatch.PatchMethod.ReturnType != patch.Key.ReturnType)
                            {
                                Logger.LogError($"Patch {queuedPatch.PatchMethod.Name} returns {queuedPatch.PatchMethod.Name}, but target returns {patch.Key.ReturnType.Name}. They must match.");
                                return;
                            }
                            
                            Logger.Log($"Using wrapper as the method returns: {queuedPatch.PatchMethod.ReturnType}");
                            var wrapper = BoolLessPrefix(targetMethod: patch.Key, userPatchMethod: queuedPatch.PatchMethod);
                            if (wrapper == null)
                            {
                                Logger.LogError($"Failed to create wrapper for {queuedPatch.PatchMethod.Name}");
                                break;
                            }
                            Logger.Log(
                                $"Applied HEAD (prefix) with wrapper on {patch.Key.Name} using {queuedPatch.HarmonyMethod.methodName}");
                            harmony.Patch(patch.Key, prefix: new HarmonyMethod(wrapper));
                        }
                        else
                        {
                            harmony.Patch(patch.Key, prefix: queuedPatch.HarmonyMethod);
                            Logger.Log(
                                $"Applied HEAD (prefix) on {patch.Key.Name} using {queuedPatch.HarmonyMethod.methodName}");
                        }
                        break;
                    case AT.RETURN:
                        harmony.Patch(patch.Key, postfix: queuedPatch.HarmonyMethod);
                        Logger.Log(
                            $"Applied RETURN (postfix) on {patch.Key.Name} using {queuedPatch.HarmonyMethod.methodName}");
                        break;
                    default:
                        throw new NotImplementedException($"Have not implemented: {queuedPatch.Type}");
                }
            }
        } 
        
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
                Logger.LogError($"Shits fucked frfr no cap {targetMethod.Name}: {ex.Message}");
            }
        }
    }

    private static void DetectPatchConflicts(
        Dictionary<MethodInfo, List<QueuedPatch>> patches,
        HashSet<MethodInfo> toRemove)
    {
        foreach (var group in patches.Where(g => g.Value.Count > 1))
        {
            LogConflict(group.Key, group.Value.Select(p => p.HarmonyMethod.method));
        
            switch (ConflictResolutionMethod)
            {
                case ConflictResolver.SkipConflicts:
                    toRemove.Add(group.Key);
                    break;
                case ConflictResolver.Error:
                    throw new InvalidOperationException(
                        $"Conflict detected: {group.Value.Count} patches target {group.Key.Name}");
            }
        }
    }
    private static void DetectTranspilerConflicts(
        Dictionary<MethodBase, List<TranspilerConfig>> transpilers,
        HashSet<MethodBase> toRemove)
    {
        foreach (var group in transpilers.Where(g => g.Value.Count > 1))
        {
            LogConflict(group.Key, group.Value.Select(t => t.PatchMethod));
        
            switch (ConflictResolutionMethod)
            {
                case ConflictResolver.SkipConflicts:
                    toRemove.Add(group.Key);
                    break;
                case ConflictResolver.Error:
                    throw new InvalidOperationException(
                        $"Conflict detected: {group.Value.Count} transpilers target {group.Key.Name}");
            }
        }
    }
    private static void LogConflict(MethodBase targetMethod, IEnumerable<MethodInfo> patchMethods)
    {
        Logger.LogWarning($"Multiple patches queued for {targetMethod.DeclaringType?.FullName}.{targetMethod.Name}");
    
        foreach (var method in patchMethods)
        {
            var parameters = string.Join(", ", method.GetParameters()
                .Select(p => $"{p.ParameterType.Name} {p.Name}" + 
                             (p.HasDefaultValue ? $" = {p.DefaultValue}" : "")));
            Logger.LogWarning($"  - {method.DeclaringType?.FullName}.{method.Name}({parameters})");
        }
    }
    
    public static IEnumerable<CodeInstruction> TranspilerPiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
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

    private static MethodInfo? BoolLessPrefix(MethodInfo targetMethod, MethodInfo userPatchMethod)
    {
        var typeName = $"MixinWrapper_{userPatchMethod.Name}_{Guid.NewGuid():N}";
        var typeBuilder = _moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);

        // Adds Params: User Args + ref __result
        var userParams = userPatchMethod.GetParameters();
        var originalReturnType = targetMethod.ReturnType;

        var wrapperParamTypes = userParams.Select(p => p.ParameterType).ToList();
        wrapperParamTypes.Add(originalReturnType.MakeByRefType()); // add ref Type __result

        var methodBuilder = typeBuilder.DefineMethod(
            $"Wrapper_{userPatchMethod.Name}",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(bool), // return bool for harmony
            wrapperParamTypes.ToArray()
        );

        for (int i = 0; i < userParams.Length; i++)
        {
            methodBuilder.DefineParameter(i + 1, ParameterAttributes.None, userParams[i].Name);
        }
        methodBuilder.DefineParameter(userParams.Length + 1, ParameterAttributes.Out, "__result"); // add __result at the end

        ILGenerator il = methodBuilder.GetILGenerator();

        LoadArg(il, userParams.Length); // load __result
        for (int i = 0; i < userParams.Length; i++) // load userAgs
        {
            LoadArg(il, i);
        }

        il.Emit(OpCodes.Call, userPatchMethod); // call user patch

        il.Emit(OpCodes.Stobj, originalReturnType); // store result into __result

        il.Emit(OpCodes.Ldc_I4_0); // set false (0)
        il.Emit(OpCodes.Ret); // return
        
        var builtType = CreateType(typeBuilder); // make type
        return builtType.GetMethod($"Wrapper_{userPatchMethod.Name}");
    }

    private static Type CreateType(TypeBuilder typeBuilder)
    {
#if NETSTANDARD2_0 || NETSTANDARD2_1
        return typeBuilder.CreateTypeInfo()!.AsType();
#else
        return typeBuilder.CreateType()!;
#endif
    }

    private static void LoadArg(ILGenerator il, int index)
    {
        switch (index)
        {
            case 0: il.Emit(OpCodes.Ldarg_0); break;
            case 1: il.Emit(OpCodes.Ldarg_1); break;
            case 2: il.Emit(OpCodes.Ldarg_2); break;
            case 3: il.Emit(OpCodes.Ldarg_3); break;
            default: il.Emit(OpCodes.Ldarg, index); break;
        }
    }
}
