using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib.PatchExtensions;

internal static class MixinApplier
{
    /// <summary>
    /// Applies queued mixin patches to target methods using Harmony.
    /// </summary>
    /// <param name="_queuedPatches">Dictionary of target methods and their associated queued patches.</param>
    /// <param name="harmony">Harmony instance for applying patches.</param>
    /// <param name="_moduleBuilder">Module builder for creating dynamic types.</param>
    /// <exception cref="NotImplementedException">Thrown if a patch type is not supported.</exception>
    public static void ApplyPatches(Dictionary<MethodInfo, List<MixinLoader.QueuedPatch>> _queuedPatches, Harmony harmony, ModuleBuilder _moduleBuilder)
    {
        foreach (KeyValuePair<MethodInfo, List<MixinLoader.QueuedPatch>> patch in _queuedPatches)
        {
            MethodInfo methodInfo = patch.Key;
            foreach (MixinLoader.QueuedPatch queuedPatch in patch.Value)
            {
                switch (queuedPatch.Type)
                {
                    case AT.HEAD:
                        if (queuedPatch.Overwriting && queuedPatch.PatchMethod.ReturnType != typeof(bool) && queuedPatch.PatchMethod.ReturnType != typeof(void))
                        {
                            if (queuedPatch.PatchMethod.ReturnType != methodInfo.ReturnType)
                            {
                                Logger.LogError($"Patch {queuedPatch.PatchMethod.Name} returns {queuedPatch.PatchMethod.Name}, but target returns {methodInfo.ReturnType.Name}. They must match.");
                                return;
                            }
                            
                            Logger.Log($"Using wrapper as the method returns: {queuedPatch.PatchMethod.ReturnType}");
                            MethodInfo? wrapper = BoolLessPrefix(targetMethod: methodInfo, userPatchMethod: queuedPatch.PatchMethod, _moduleBuilder);
                            if (wrapper == null)
                            {
                                Logger.LogError($"Failed to create wrapper for {queuedPatch.PatchMethod.Name}");
                                break;
                            }
                            Logger.Log(
                                $"Applied HEAD (prefix) with wrapper on {methodInfo.Name} using {queuedPatch.HarmonyMethod.methodName}");
                            harmony.Patch(methodInfo, prefix: new HarmonyMethod(wrapper));
                        }
                        else
                        {
                            harmony.Patch(methodInfo, prefix: queuedPatch.HarmonyMethod);
                            Logger.Log(
                                $"Applied HEAD (prefix) on {methodInfo.Name} using {queuedPatch.HarmonyMethod.methodName}");
                        }
                        break;
                    case AT.RETURN:
                        harmony.Patch(methodInfo, postfix: queuedPatch.HarmonyMethod);
                        Logger.Log(
                            $"Applied RETURN (postfix) on {methodInfo.Name} using {queuedPatch.HarmonyMethod.methodName}");
                        break;
                    default:
                        throw new NotImplementedException($"Have not implemented: {queuedPatch.Type}");
                }
            }
        } 
    }

    /// <summary>
    /// Creates a wrapper method for the user's patch so it acts the same as the equivalent harmony method would
    /// </summary>
    /// <param name="targetMethod">The target method to be patched.</param>
    /// <param name="userPatchMethod">The user patch method that modifies the behavior of the target method.</param>
    /// <param name="_moduleBuilder">The module builder used to create the wrapper method.</param>
    /// <returns>
    /// A wrapper method as <see cref="MethodInfo"/>, or null if it fails.
    /// </returns>
    private static MethodInfo? BoolLessPrefix(MethodInfo targetMethod, MethodInfo userPatchMethod, ModuleBuilder _moduleBuilder)
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