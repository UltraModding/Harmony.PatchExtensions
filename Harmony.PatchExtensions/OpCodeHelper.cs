using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib.PatchExtensions;

public class OpCodeHelper
{
    private static readonly HashSet<OpCode> LocalOpcodes = new()
    {
        OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_2, OpCodes.Ldloc_3,
        OpCodes.Ldloc_S, OpCodes.Ldloc,
        OpCodes.Ldloca_S, OpCodes.Ldloca,
        OpCodes.Stloc_0, OpCodes.Stloc_1, OpCodes.Stloc_2, OpCodes.Stloc_3,
        OpCodes.Stloc_S, OpCodes.Stloc,
    };
    public static bool IsLocalOpcode(OpCode opcode) => LocalOpcodes.Contains(opcode);
    
    private static readonly HashSet<OpCode> ArgLocodes = new()
    {
        OpCodes.Ldarg_0, OpCodes.Ldarg_1, OpCodes.Ldarg_2, OpCodes.Ldarg_3,
        OpCodes.Ldarg, OpCodes.Ldarg_S, 
        OpCodes.Ldarga, OpCodes.Ldarga_S,
        OpCodes.Starg, OpCodes.Starg_S
    };
    public static bool IsArgOpcode(OpCode opcode) => ArgLocodes.Contains(opcode);
    
    private static readonly HashSet<OpCode> MethodCallOpcodes = new()
    {
        OpCodes.Call,
        OpCodes.Callvirt,
        OpCodes.Newobj,
    };
    public static bool IsMethod(OpCode opcode) => MethodCallOpcodes.Contains(opcode);
    
    private static readonly HashSet<OpCode> FieldOpcodes = new()
    {
        OpCodes.Stfld,
        OpCodes.Ldfld,
        OpCodes.Ldsfld,
        OpCodes.Stsfld,
        OpCodes.Ldflda,
        OpCodes.Ldsflda,
    };
    public static bool IsField(OpCode opcode) => FieldOpcodes.Contains(opcode);
    
    private static readonly HashSet<OpCode> BranchOpcodes = new()
    {
        OpCodes.Brtrue,
        OpCodes.Brtrue_S,
        OpCodes.Brfalse,
        OpCodes.Brfalse_S
    };
    public static bool IsBranch(OpCode opcode) => BranchOpcodes.Contains(opcode);
    
    private static readonly HashSet<OpCode> LoopOpcodes = new()
    {
        OpCodes.Br,
        OpCodes.Br_S
    };
    public static bool IsLoop(OpCode opcode) => LoopOpcodes.Contains(opcode);
    
    private static readonly HashSet<OpCode> ConditionalOpcodes = new()
    {
        OpCodes.Blt,
        OpCodes.Blt_S,
        OpCodes.Ble,
        OpCodes.Ble_S,
        OpCodes.Bgt,
        OpCodes.Bgt_S,
        OpCodes.Bge,
        OpCodes.Bge_S,
        OpCodes.Brtrue,
        OpCodes.Brtrue_S,
        OpCodes.Brfalse,
        OpCodes.Brfalse_S,
    };
    public static bool IsConditional(OpCode opcode) => ConditionalOpcodes.Contains(opcode);
    
    public static bool TryGetLocalIndex(CodeInstruction instruction, out int index, out bool isWrite, out bool isAddress)
    {
        index = -1;
        isWrite = false;
        isAddress = false;
        var op = instruction.opcode;
        
        // read
        if (op == OpCodes.Ldloc_0) { index = 0; return true; }
        if (op == OpCodes.Ldloc_1) { index = 1; return true; }
        if (op == OpCodes.Ldloc_2) { index = 2; return true; }
        if (op == OpCodes.Ldloc_3) { index = 3; return true; }
        if (op == OpCodes.Ldloc_S) { index = ((LocalBuilder)instruction.operand).LocalIndex; return true; }
        if (op == OpCodes.Ldloc)   { index = ((LocalBuilder)instruction.operand).LocalIndex; return true; }
        
        // address TODO
        if (op == OpCodes.Ldloca_S) { index = ((LocalBuilder)instruction.operand).LocalIndex; isAddress = true; return true; }
        if (op == OpCodes.Ldloca)   { index = ((LocalBuilder)instruction.operand).LocalIndex; isAddress = true; return true; }
        
        // write
        if (op == OpCodes.Stloc_0) { index = 0; isWrite = true; return true; }
        if (op == OpCodes.Stloc_1) { index = 1; isWrite = true; return true; }
        if (op == OpCodes.Stloc_2) { index = 2; isWrite = true; return true; }
        if (op == OpCodes.Stloc_3) { index = 3; isWrite = true; return true; }
        if (op == OpCodes.Stloc_S) { index = ((LocalBuilder)instruction.operand).LocalIndex; isWrite = true; return true; }
        if (op == OpCodes.Stloc)   { index = ((LocalBuilder)instruction.operand).LocalIndex; isWrite = true; return true; }
        
        return false;
    }
    
    public static bool TryGetArgIndex(CodeInstruction instruction, out int index, out bool isWrite, out bool isAddress)
    {
        index = -1;
        isWrite = false;
        isAddress = false;
        var op = instruction.opcode;
        
        // read
        if (op == OpCodes.Ldarg_0) { index = 0; return true; }
        if (op == OpCodes.Ldarg_1) { index = 1; return true; }
        if (op == OpCodes.Ldarg_2) { index = 2; return true; }
        if (op == OpCodes.Ldarg_3) { index = 3; return true; }
        if (op == OpCodes.Ldarg_S) { index = (byte)instruction.operand; return true; }
        if (op == OpCodes.Ldarg) { index = (short)instruction.operand; return true; }
        
        // address
        if (op == OpCodes.Ldarga_S) { index = (byte)instruction.operand; isAddress = true; return true; }
        if (op == OpCodes.Ldarga) { index = (short)instruction.operand; isAddress = true; return true; }
        
        // write
        if (op == OpCodes.Starg_S) { index = (byte)instruction.operand; isWrite = true; return true; }
        if (op == OpCodes.Starg) { index = (short)instruction.operand; isWrite = true; return true; }
        
        return false;
    }
    
    public static bool TryGetFieldInfo(CodeInstruction instruction, out FieldInfo field, out bool isWrite, out bool isAddress, out bool isStatic)
    {
        field = null;
        isWrite = false;
        isAddress = false;
        isStatic = false;
        var op = instruction.opcode;
        
        // read
        if (op == OpCodes.Ldfld)  { field = (FieldInfo)instruction.operand; return true; }
        if (op == OpCodes.Ldsfld) { field = (FieldInfo)instruction.operand; isStatic = true; return true; }
        
        // address
        if (op == OpCodes.Ldflda)  { field = (FieldInfo)instruction.operand; isAddress = true; return true; }
        if (op == OpCodes.Ldsflda) { field = (FieldInfo)instruction.operand; isAddress = true; isStatic = true; return true; }
        
        // write
        if (op == OpCodes.Stfld)  { field = (FieldInfo)instruction.operand; isWrite = true; return true; }
        if (op == OpCodes.Stsfld) { field = (FieldInfo)instruction.operand; isWrite = true; isStatic = true; return true; }
        
        return false;
    }
    
    public static bool IsLoopEntryBranch(CodeInstruction instruction, List<CodeInstruction> instructions)
    {
        if (!IsLoop(instruction.opcode) || instruction.operand is not Label conditionLabel)
            return false;
        
        int branchIndex = instructions.FindIndex(ci => ReferenceEquals(ci, instruction));
        int conditionIndex = instructions.FindIndex(ci => ci.labels.Contains(conditionLabel));
        if (branchIndex == -1 || conditionIndex == -1)
            return false;
        
        for (int i = conditionIndex; i < instructions.Count; i++)
        {
            var current = instructions[i];
            
            if (IsConditional(current.opcode))
            {
                if (current.operand is not Label bodyLabel)
                    return false;
                
                int bodyIdx = instructions.FindIndex(ci => ci.labels.Contains(bodyLabel));
                return bodyIdx != -1 && bodyIdx > branchIndex;
            }
            
            if (current.opcode == OpCodes.Ret || IsLoop(current.opcode))
                return false;
        }
        
        return false;
    }
    
    public static IEnumerable<MethodBase> FindMethodsUsingField(Type scanType, FieldInfo target)
    {
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (var method in scanType.GetMethods(flags).Cast<MethodBase>()
                     .Concat(scanType.GetConstructors(flags)))
        {
            if (method.IsAbstract || method.ContainsGenericParameters) continue;
            var body = method.GetMethodBody();
            if (body == null) continue;
            
            foreach (var instr in PatchProcessor.GetOriginalInstructions(method))
            {
                if (TryGetFieldInfo(instr, out var field, out _, out _, out _) && field == target)
                {
                    yield return method;
                    break;
                }
            }
        }
    }
}