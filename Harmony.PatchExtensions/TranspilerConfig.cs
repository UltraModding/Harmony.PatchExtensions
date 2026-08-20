using System.Reflection;

namespace HarmonyLib.PatchExtensions;

internal class TranspilerConfig
{
    /// <param name="type">The class type containing the method you want to patch.</param>
    /// <param name="methodName">The name of the method you want to patch.</param>
    /// <param name="type">The injection point (HEAD, RETURN, POSTFIX, INVOKE, REDIRECT, AFTER).</param>
    /// <param name="targetMember">
    /// (Optional) For <see cref="AT.INVOKE"/> or <see cref="AT.REDIRECT"/> or <see cref="AT.AFTER"/>, this is the name of the method being called inside the targetMember.
    /// </param>
    /// <param name="occurrence">
    /// (Optional) For <see cref="AT.INVOKE"/> and <see cref="AT.REDIRECT"/> and <see cref="AT.AFTER"/>
    /// Specifies which occurrence to patch, counted relative to <see cref="StartIndex"/>.
    /// Use 0 to patch all matching calls after <see cref="StartIndex"/>.
    /// </param>
    /// <param name="startIndex">
    /// (Optional) For <see cref="AT.INVOKE"/> and <see cref="AT.REDIRECT"/> and <see cref="AT.AFTER"/> and <see cref="AT.ARG"/>
    /// Matches before this index are ignored.
    /// Use 0 to start from the first match.
    /// </param>
    /// <param name="argIndex">
    /// For <see cref="AT.ARG"/>
    /// What argument index to replace
    /// Use 1 to start from the first match (0 gives error)
    /// </param>
    /// <para name="targetType">
    /// For <see cref="AT.FIELD_READ/FIELD_WRITE"/>
    /// If the targetMember is inside a different Type than the TargetMember Method
    /// </para>
    public TranspilerConfig(AT type, string targetMember, MethodInfo patchMethod, uint occurrence, uint startIndex, uint argIndex, Type targetType)
    {
        Type = type;
        TargetMember = targetMember;
        PatchMethod = patchMethod;
        Occurrence = occurrence;
        StartIndex = startIndex;
        ArgIndex = argIndex;
        TargetType = targetType;
    }
    
    /// <summary>
    /// The location or type of patch HEAD, RETURN, POSTFIX, INVOKE, REDIRECT, AFTER
    /// </summary>
    public AT Type;
    /// <summary>
    /// The name of the method call inside the <see cref="TargetMethod"/> that you want to target.
    /// </summary>
    public string? TargetMember;
    /// <summary>
    /// For <see cref="AT.FIELD_READ/FIELD_WRITE"/>
    /// If the target is inside a different Type than the TargetMember Method
    /// </summary>
    public Type? TargetType;
    /// <summary>
    /// The method with the attribute patching
    /// </summary>
    public MethodInfo PatchMethod;
    public uint Occurrence;
    public uint StartIndex;
    public uint ArgIndex;
}