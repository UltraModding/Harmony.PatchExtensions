using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib.PatchExtensions;

public static class TranspilerApplier
{
    public static IEnumerable<CodeInstruction> TranspilerPiler(IEnumerable<CodeInstruction> instructions, MethodBase original, ILGenerator generator)
    {
        if (!MixinLoader.QueuedTranspilers.TryGetValue(original, out var transpilerConfigs))
            return instructions;
        
        var matcher = new CodeMatcher(instructions, generator);
        
        foreach (var config in transpilerConfigs)
        {
            matcher.Start();
            
            int currentOccurrence = 0; // for the occurrence
            int relativeOccurrence = 0;
            
            (string requiredClass, string requiredMethod) = GetRequired(config);
            
            while (true)
            {
                matcher.MatchForward(false, 
                    new CodeMatch(instruction => Matcher(instruction, config, requiredMethod, requiredClass))
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
                            if (ApplyRedirect(matcher, config))
                                continue;
                        }
                        else if (config.Type == AT.AFTER)
                        {
                            ApplyAfter(matcher, config, generator);
                        }
                        else if (config.Type == AT.RETURN)
                        {
                            ApplyReturn(original, generator, matcher, config);
                        }
                        else if (config.Type == AT.ARG)
                        {
                            ApplyArg(matcher, config, generator);
                        }
                        
                        if (config.Occurrence != 0)
                            break;
                    }
                }
                
                matcher.Advance(1);
            
            }
            
            // foreach (var instur in matcher.Instructions())
            // {
            //     FileLog.Log(instur.ToString());
            // }
        }
        
        return matcher.InstructionEnumeration();
    }
    
    private static void ApplyArg(CodeMatcher matcher, TranspilerConfig config, ILGenerator generator)
    {
        var targetInstruction = matcher.Instruction;
        
        if (targetInstruction.operand is MethodInfo info)
        {
            int maxArgs = info.GetParameters().Length;
            if (config.ArgIndex > maxArgs)
            {
                Logger.LogWarning($"ARG ArgIndex '{config.ArgIndex}' is bigger than the args on '{info.Name}' ({maxArgs}). Skipped.");
                return;
            }
            int n = maxArgs - (int)config.ArgIndex;
            matcher.Advance(-n);
            matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, config.PatchMethod));
            matcher.Advance(n);
        }
    }
    
    private static void ApplyReturn(MethodBase original, ILGenerator generator, CodeMatcher matcher, TranspilerConfig config)
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
    
    private static void ApplyAfter(CodeMatcher matcher, TranspilerConfig config, ILGenerator generator)
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
    
    private static bool ApplyRedirect(CodeMatcher matcher, TranspilerConfig config)
    {
        var targetInstruction = matcher.Instruction;
        
        if (targetInstruction.operand is not MethodBase originalMethod)
        {
            Logger.LogWarning($"REDIRECT target '{config.TargetMember}' is a field, not a method. Skipped.");
            matcher.Advance(1);
            return true;
        }
        
        if (!DontScrewUpStack(originalMethod, targetInstruction.opcode, config.PatchMethod))
        {
            Logger.LogWarning($"REDIRECT patch '{config.PatchMethod.Name}' doesn't match with '{config.TargetMember}'. Skipped.");
            matcher.Advance(1);
            return true;
        }
        
        matcher.SetInstruction(new CodeInstruction(OpCodes.Call, config.PatchMethod));
        
        return false;
    }
    
    private static (string, string) GetRequired(TranspilerConfig config)
    {
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
        
        return (requiredClass, requiredMethod);
    }
    
    private static bool Matcher(CodeInstruction instruction, TranspilerConfig config, string requiredMethod, string requiredClass)
    {
        // FileLog.Log(instruction.ToString()); 
        
        if (config.Type == AT.RETURN && instruction.opcode == OpCodes.Ret)
            return true;
        if (config.Type == AT.RETURN)
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
        
        if (!isMethod && !isField)
            return false;
        
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