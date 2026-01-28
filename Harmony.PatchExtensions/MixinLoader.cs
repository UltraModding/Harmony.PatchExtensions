using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace DolfeMixin;

public static class MixinLoader
{
    static MixinLoader()
    {
        var assemblyName = new AssemblyName("DolfeMixinDynamicAssembly");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder = assemblyBuilder.DefineDynamicModule("MixinWrappers");
    }
    private class TranspilerConfig
    {
        public AT Type;
        public string TargetMember;
        public MethodInfo PatchMethod;
        public uint Occurrence;
        public uint StartIndex;
    }
    private class QueuedPatch // checking for conflicts
    {
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

    private static ModuleBuilder ModuleBuilder;
    
    public static ConflictResolver ConflictResolutionMethod = ConflictResolver.Warn;
    private static Dictionary<MethodBase, List<TranspilerConfig>> queuedTranspilers = new();
    private static Dictionary<MethodInfo, List<QueuedPatch>> queuedPatches = new();
    
    public static void ApplyPatches(Harmony harmony, Assembly assembly)
    {
        queuedTranspilers.Clear();

        foreach (var type in assembly.GetTypes())
        {
            foreach (var patchMethod in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var attrs = patchMethod.GetCustomAttributes<PatchAttribute>();
                foreach (var attr in attrs)
                {
                    if (attr.TargetMethod == null)
                    {
                        Logger.LogWarning($"You must set TargetMethod in {patchMethod.Name} for the Patch to work");
                        continue;
                    }

                    var harmonyMethod = new HarmonyMethod(patchMethod);
                    QueuedPatch patch = new QueuedPatch
                    {
                        HarmonyMethod = harmonyMethod,
                        Type = attr.At,
                        Overwriting = attr.Overwriting,
                        PatchMethod = patchMethod
                    };
                    
                    if (attr.At == AT.HEAD) // prefix
                    {
                        if (!queuedPatches.ContainsKey(attr.TargetMethod))
                            queuedPatches[attr.TargetMethod] = new List<QueuedPatch>();
                        
                        queuedPatches[attr.TargetMethod].Add(patch);
                        Logger.Log($"Applied head on {attr.TargetMethod.Name}");
                    }
                    else if (attr.At == AT.RETURN) // postfix
                    {
                        if (!queuedPatches.ContainsKey(attr.TargetMethod))
                            queuedPatches[attr.TargetMethod] = new List<QueuedPatch>();
                        
                        queuedPatches[attr.TargetMethod].Add(patch);
                        Logger.Log($"Applied return on {attr.TargetMethod.Name}");
                    }
                    else if (attr.At == AT.INVOKE || attr.At == AT.REDIRECT) // before target / replace target
                    {
                        if (!queuedTranspilers.ContainsKey(attr.TargetMethod))
                            queuedTranspilers[attr.TargetMethod] = new List<TranspilerConfig>();
                        
                        queuedTranspilers[attr.TargetMethod].Add(new TranspilerConfig
                        {
                            Type = attr.At,
                            TargetMember = attr.TargetMember,
                            PatchMethod = patchMethod,
                            Occurrence = attr.Occurrence,
                            StartIndex = attr.StartIndex
                        });
                    }
                }
            }
        }

        // Process patch conflicts
        var patchesToRemove = new HashSet<MethodInfo>();
        DetectPatchConflicts(queuedPatches, patchesToRemove);
        foreach (var key in patchesToRemove)
            queuedPatches.Remove(key);

        // Process transpiler conflicts
        var transpilersToRemove = new HashSet<MethodBase>();
        DetectTranspilerConflicts(queuedTranspilers, transpilersToRemove);
        foreach (var key in transpilersToRemove)
            queuedTranspilers.Remove(key);
        
        foreach (KeyValuePair<MethodInfo, List<QueuedPatch>> patch in queuedPatches)
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
                            var wrapper = BoolLessPrefix(patch.Key, queuedPatch.PatchMethod);
                            harmony.Patch(patch.Key, prefix: new HarmonyMethod(wrapper));
                            Logger.Log($"Applied Auto-Return replacement on {patch.Key.Name}");
                        }
                        else
                        {
                            Logger.Log($"Using normal harmony patch as it returns bool");
                            harmony.Patch(patch.Key, prefix: queuedPatch.HarmonyMethod);
                        }
                        break;
                    case AT.RETURN:
                        harmony.Patch(patch.Key, postfix: queuedPatch.HarmonyMethod);
                        break;
                    default:
                        throw new NotImplementedException($"Have not implemented: {queuedPatch.Type}");
                }
            }
        } 
        
        var transpiler = new HarmonyMethod(typeof(MixinLoader), nameof(TranspilerPiler));
        foreach (var targetMethod in queuedTranspilers.Keys)
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
        Logger.LogWarning($"Multiple patches queued for {targetMethod.DeclaringType.FullName}.{targetMethod.Name}");
    
        foreach (var method in patchMethods)
        {
            var parameters = string.Join(", ", method.GetParameters()
                .Select(p => $"{p.ParameterType.Name} {p.Name}" + 
                             (p.HasDefaultValue ? $" = {p.DefaultValue}" : "")));
            Logger.LogWarning($"  - {method.DeclaringType.FullName}.{method.Name}({parameters})");
        }
    }
    
    public static IEnumerable<CodeInstruction> TranspilerPiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        if (!queuedTranspilers.TryGetValue(original, out var transpilerConfigs))
            return instructions;

        var matcher = new CodeMatcher(instructions);
        foreach (var config in transpilerConfigs)
        {
            matcher.Start();
            
            string requiredClass = null;
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

    private static MethodInfo BoolLessPrefix(MethodInfo original, MethodInfo userPatch)
    {
        var typeName = $"MixinWrapper_{userPatch.Name}_{Guid.NewGuid():N}";
        var typeBuilder = ModuleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);

        // Adds Params: User Args + ref __result
        var userParams = userPatch.GetParameters();
        var originalReturnType = original.ReturnType;

        var wrapperParamTypes = userParams.Select(p => p.ParameterType).ToList();
        wrapperParamTypes.Add(originalReturnType.MakeByRefType()); // add ref Type __result

        var methodBuilder = typeBuilder.DefineMethod(
            $"Wrapper_{userPatch.Name}",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(bool), // return bool for harmony
            wrapperParamTypes.ToArray()
        );

        for (int i = 0; i < userParams.Length; i++)
        {
            methodBuilder.DefineParameter(i + 1, ParameterAttributes.None, userParams[i].Name);
        }
        methodBuilder.DefineParameter(userParams.Length + 1, ParameterAttributes.Out, "__result"); // add __result at end

        ILGenerator il = methodBuilder.GetILGenerator();

        LoadArg(il, userParams.Length); // load __result
        for (int i = 0; i < userParams.Length; i++) // load userags
        {
            LoadArg(il, i);
        }

        il.Emit(OpCodes.Call, userPatch); // call user patch

        il.Emit(OpCodes.Stobj, originalReturnType); // store result into __result

        // E. Return false (skip original)
        il.Emit(OpCodes.Ldc_I4_0); // set false (0)
        il.Emit(OpCodes.Ret); // return

        var builtType = typeBuilder.CreateType(); // make type
        return builtType.GetMethod($"Wrapper_{userPatch.Name}");
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
