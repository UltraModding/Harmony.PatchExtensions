using System.Reflection;

namespace HarmonyLib.PatchExtensions;

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