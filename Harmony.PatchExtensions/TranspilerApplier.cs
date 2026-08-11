using System.Reflection;
using System.Reflection.Emit;

using Mono.Cecil;

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
                    new CodeMatch(instruction => Matcher(instruction, config, requiredMethod, requiredClass, matcher))
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
                        switch (config.Type)
                        {
                            case AT.INVOKE: 
                                matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, config.PatchMethod));
                                break;
                            case AT.REDIRECT 
                                when ApplyRedirect(matcher, config): 
                                    continue;
                            case AT.AFTER:
                                ApplyAfter(matcher, config, generator);
                                break;
                            case AT.RETURN:
                                ApplyReturn(original, generator, matcher, config);
                                break;
                            case AT.ARG:
                                ApplyArg(matcher, config, generator);
                                break;
                            case AT.BRANCH_TRUE:
                                ApplyBranch(matcher, config, generator, true);
                                break;
                            case AT.BRANCH_FALSE:
                                ApplyBranch(matcher, config, generator, false);
                                break;
                            case AT.LOOP_BEFORE:
                                matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, config.PatchMethod)); break;
                            case AT.LOOP_TOP:
                                ApplyLoopTop(matcher, config, generator);
                                break;
                            case AT.LOOP_BOTTOM:
                                ApplyLoopBottom(matcher, config, generator);
                                break;
                            case AT.LOOP_AFTER:
                                ApplyLoopAfter(matcher, config, generator);
                                break;
                            case AT.LOCAL_READ:
                                LocalRead(original, matcher, config, generator);
                                break;
                            case AT.LOCAL_WRITE:
                                LocalWrite(original, matcher, config, generator);
                                break;
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
    
    #region ARG_R/W
    
    
    
    #endregion
    
    #region FIELD_R/W
    
    
    
    #endregion
    
    #region LOCAL_R/W
    
    private static void LocalWrite(
        MethodBase original,
        CodeMatcher matcher,
        TranspilerConfig config,
        ILGenerator generator
    )
    {
        string? dllPath = original.DeclaringType?.Assembly.Location;
        if (dllPath == null)
        {
            throw new Exception("Unable to get target Assembly path.");
        }
        
        var module = ModuleDefinition.ReadModule(dllPath, new ReaderParameters { ReadSymbols = true });
        var method = (MethodDefinition)module.LookupToken(original.MetadataToken);
        
        Dictionary<int, string> localsIndex = new();
        foreach (var v in method.DebugInformation.Scope.Variables)
        {
            localsIndex.Add(v.Index, v.Name);
        }
        
        var instruction = matcher.Instruction;
        
        if (OpCodeHelper.TryGetLocalIndex(instruction, out int index, out bool isWrite, out bool isAddress))
        {
            if (!isWrite || isAddress)
                return;
            
            
            if (localsIndex.TryGetValue(index, out string? name) && name == config.TargetMember)
            {
                // Logger.Log($"Writing {localsIndex[index]}");
                matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Dup));
                matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, config.PatchMethod));
            }
            // Logger.Log($"Instruction: {instruction.opcode}, index: {index}, isWrite: {isWrite}, isAddress: {isAddress}");
        }
    }
    
    private static void LocalRead(
        MethodBase original,
        CodeMatcher matcher,
        TranspilerConfig config,
        ILGenerator generator
    )
    {
        string? dllPath = original.DeclaringType?.Assembly.Location;
        if (dllPath == null)
        {
            throw new Exception("Unable to get target Assembly path.");
        }
        
        var module = ModuleDefinition.ReadModule(dllPath, new ReaderParameters { ReadSymbols = true });
        var method = (MethodDefinition)module.LookupToken(original.MetadataToken);
        
        Dictionary<int, string> localsIndex = new();
        foreach (var v in method.DebugInformation.Scope.Variables)
        {
            localsIndex.Add(v.Index, v.Name);
            // Logger.Log($"{v.Index}: {v.Name}");
        }
        
        var instruction = matcher.Instruction;
        
        if (OpCodeHelper.TryGetLocalIndex(instruction, out int index, out bool isWrite, out bool isAddress))
        {
            if (isWrite || isAddress)
                return;
            
            if (localsIndex.TryGetValue(index, out string? name) && name == config.TargetMember)
            {
                // Logger.Log($"Reading {localsIndex[index]}");
                matcher.Advance(1);
                matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Dup));
                matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, config.PatchMethod));
            }
        }
    }
    
    #endregion
    
    #region LOOP_T/B/A/B
    
    private static void ApplyLoopTop(CodeMatcher matcher, TranspilerConfig config, ILGenerator generator)
    {
        var brInstruction = matcher.Instruction;
        Label conditionLabel = (Label)brInstruction.operand;
        int branchIdx = matcher.Pos;
        
        int conditionIndex = matcher.Instructions().FindIndex(ci => ci.labels.Contains(conditionLabel));
        if (conditionIndex == -1)
            throw new Exception("Could not resolve branch target label.");
        
        matcher.Advance(conditionIndex - branchIdx);
        
        ContinueToConditional(matcher);
        
        var condBranch = matcher.Instruction;
        Label bodyLabel = (Label)condBranch.operand;
        int condBranchIndex = matcher.Pos;
        
        int bodyIndex = matcher.Instructions().FindIndex(ci => ci.labels.Contains(bodyLabel));
        if (bodyIndex == -1)
            throw new Exception("Could not resolve loop body label.");
        
        matcher.Advance(bodyIndex - condBranchIndex);
        matcher.Advance(1);
        matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, config.PatchMethod));
    }
    
    private static void ApplyLoopBottom(CodeMatcher matcher, TranspilerConfig config, ILGenerator generator)
    {
        var brInstruction = matcher.Instruction; // IL_0003: br.s  IL_002a
        Label conditionLabel = (Label)brInstruction.operand; // IL_002a
        int branchIdx = matcher.Pos;
        
        var instructions = matcher.Instructions();
        int conditionIdx = instructions.FindIndex(codeInstruction => codeInstruction.labels.Contains(conditionLabel));
        if (conditionIdx == -1)
            throw new Exception("Could not resolve branch target label.");
        
        int insertIdx = conditionIdx;
        for (int i = branchIdx + 1; i < conditionIdx; i++)
        {
            if (!OpCodeHelper.IsLoop(instructions[i].opcode) || instructions[i].operand is not Label continueLabel)
                continue;
            
            int continueTarget = instructions.FindIndex(x => x.labels.Contains(continueLabel));
            if (continueTarget > i && continueTarget < conditionIdx)
            {
                insertIdx = continueTarget;
                break;
            }
        }
        
        matcher.Advance(insertIdx - branchIdx);
        
        matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, config.PatchMethod));
    }
    
    private static void ApplyLoopAfter(CodeMatcher matcher, TranspilerConfig config, ILGenerator generator)
    {
        var brInstruction = matcher.Instruction;
        Label conditionLabel = (Label)brInstruction.operand;
        int branchIndex = matcher.Pos;
        
        int conditionIndex = matcher.Instructions().FindIndex(ci => ci.labels.Contains(conditionLabel));
        if (conditionIndex == -1)
            throw new Exception("Could not resolve branch target label.");
        
        matcher.Advance(conditionIndex - branchIndex);
        
        ContinueToConditional(matcher);
        
        matcher.Advance(1);
        
        var afterLoop = matcher.Instruction;
        var call = new CodeInstruction(OpCodes.Call, config.PatchMethod) { labels = afterLoop.labels };
        afterLoop.labels = new List<Label>();
        matcher.InsertAndAdvance(call);
    }

    private static void ContinueToConditional(CodeMatcher matcher)
    {
        int steps = 0;
        int max = matcher.Instructions().Count;

        while (!OpCodeHelper.IsConditional(matcher.Instruction.opcode))
        {
            matcher.Advance(1);
            if (++steps > max)
                throw new Exception("Failed to resolve loop condition branch.");
        }
    }

    #endregion

    #region BRANCH_T/F
    
    // Edge cases:
    // Multiple vars, else existing or not, should be solved, and Brfalse is not _S
    private static void ApplyBranch(CodeMatcher matcher, TranspilerConfig config, ILGenerator generator, bool wantTrue)
    {
        // At brfalse, dup then add brtrue that goes to brfalse
        // between brtrue and brfalse add calls
        // same case but inverted
        OpCode guardOp = wantTrue ? OpCodes.Brfalse_S : OpCodes.Brtrue_S;
        
        Label skip = generator.DefineLabel();
        matcher.Instruction.labels.Add(skip);
        
        matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Dup));
        matcher.InsertAndAdvance(new CodeInstruction(guardOp, skip));
        matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, config.PatchMethod));
    }
    
    #endregion
    
    #region ARG
    
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
    
    #endregion
    
    #region RETURN
    
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
    
    #endregion
    
    #region AFTER
    
    private static void ApplyAfter(CodeMatcher matcher, TranspilerConfig config, ILGenerator generator)
    {
        // save and restore the stack because plates break
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
    
    #endregion
    
    #region REDIRECT
    
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
    
    #endregion
    
    
    private static (string, string) GetRequired(TranspilerConfig config)
    {
        if (string.IsNullOrEmpty(config.TargetMember))
            return ("", "");
        
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
    
    private static bool Matcher(CodeInstruction instruction, TranspilerConfig config, string requiredMethod, string requiredClass, CodeMatcher matcher)
    {
        // FileLog.Log(instruction.ToString());
        
        bool isRet = instruction.opcode == OpCodes.Ret;
        if (config.Type == AT.RETURN)
            return isRet;
        
        bool isBranch = OpCodeHelper.IsBranch(instruction.opcode);
        if (config.Type == AT.BRANCH_TRUE || config.Type == AT.BRANCH_FALSE)
            return isBranch;

        if (config.Type is AT.LOOP_BEFORE or AT.LOOP_TOP or AT.LOOP_BOTTOM or AT.LOOP_AFTER)
            return OpCodeHelper.IsLoopEntryBranch(instruction, matcher.Instructions());
        
        bool isLoc = OpCodeHelper.IsLocalOpcode(instruction.opcode);
        if (config.Type is AT.LOCAL_READ or AT.LOCAL_WRITE)
            return isLoc;
        
        bool isMethod = OpCodeHelper.IsMethod(instruction.opcode);
        bool isField = OpCodeHelper.IsField(instruction.opcode);
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