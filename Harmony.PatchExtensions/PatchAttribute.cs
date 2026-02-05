using System;
using System.Reflection;

namespace HarmonyLib.PatchExtensions 
{
    /// <summary>
    /// The injection point or strategy for applying a Mixin patch.
    /// </summary>
    public enum AT
    {
        /// <summary>
        /// <b>Harmony Prefix</b>. <br/>
        /// Use <see cref="PatchAttribute.Overwriting"/> to skip original method.
        /// </summary>
        HEAD,

        /// <summary>
        /// <b>Harmony Postfix</b>.<br/>
        /// </summary>
        RETURN,

        /// <summary>
        /// <b>Injects</b> code before the method call in the target method. <br/>
        /// You must specify <see cref="PatchAttribute.TargetMember"/> to choose which call to target.
        /// You can also specify <see cref="PatchAttribute.Occurrence"/> to choose a specific occurrence
        /// </summary>
        INVOKE,

        /// <summary>
        /// <b>Replaces</b> a specific method call in the target method with your patch method. <br/>
        /// You must specify <see cref="PatchAttribute.TargetMember"/> to choose which call to replace.
        /// You can also specify <see cref="PatchAttribute.Occurrence"/> to choose a specific occurrence
        /// </summary>
        REDIRECT,
        
        /// <summary>
        /// <b>Injects</b> code after the method call in the target method. <br/>
        /// You must specify <see cref="PatchAttribute.TargetMember"/> to choose which call to target.
        /// You can also specify <see cref="PatchAttribute.Occurrence"/> to choose a specific occurrence
        /// </summary>
        AFTER,
        
        /// <summary>
        /// Inserts code at the specified location in the target method.
        /// You need to <see cref=""/>
        /// </summary>
        [Obsolete("Not yet implemented")]
        INSERT,
    }

    /// <summary>
    /// Declares a Harmony patch to be discovered and applied by <see cref="MixinLoader.ApplyPatches"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class PatchAttribute : Attribute
    {
        /// <summary>
        /// The original method in the that will be modified.
        /// </summary>
        public MethodInfo TargetMethod { get; }

        /// <summary>
        /// The location or type of patch HEAD, RETURN, INVOKE, REDIRECT
        /// </summary>
        public AT At { get; }

        /// <summary>
        /// Required only for <see cref="AT.INVOKE"/> and <see cref="AT.REDIRECT"/>. <br/>
        /// The name of the method call inside the <see cref="TargetMethod"/> that you want to target.
        /// </summary>
        public string TargetMember { get; }

        /// <summary>
        /// Only used with <see cref="AT.HEAD"/>. <br/>
        /// If true the patch method can return, false to stop the original method from running.
        /// </summary>
        public bool Overwriting { get; }

        /// <summary>
        /// Required only for <see cref="AT.INVOKE"/> and <see cref="AT.REDIRECT"/>. <br/>
        /// Specifies which occurrence to patch, counted relative to <see cref="StartIndex"/>.
        /// 1 based. Use 0 to patch all matching calls after <see cref="StartIndex"/>.
        /// </summary>
        public uint Occurrence { get; } = 0;

        /// <summary>
        /// Optional only for <see cref="AT.INVOKE"/> and <see cref="AT.REDIRECT"/>. <br/>
        /// The 1 based match index to begin considering replacements.
        /// Matches before this index are ignored.
        /// Use 0 to start from the first match.
        /// </summary>
        public uint StartIndex { get; } = 0;
        
        /// <summary>
        /// Defines a patch for a specific method.
        /// </summary>
        /// <param name="type">The class type containing the method you want to patch.</param>
        /// <param name="methodName">The name of the method you want to patch.</param>
        /// <param name="at">The injection point (HEAD, RETURN, INVOKE, REDIRECT).</param>
        /// <param name="target">
        /// (Optional) For <see cref="AT.INVOKE"/> or <see cref="AT.REDIRECT"/>, this is the name of the method being called inside the target.
        /// </param>
        /// <param name="overwriting">
        /// (Optional) For <see cref="AT.HEAD"/>, set to true to cancel.
        /// </param>
        /// <param name="occurrence">
        /// (Required) For <see cref="AT.INVOKE"/> and <see cref="AT.REDIRECT"/>
        /// Specifies which occurrence to patch, counted relative to <see cref="StartIndex"/>.
        /// 1 based. Use 0 to patch all matching calls after <see cref="StartIndex"/>.
        /// </param>
        /// <param name="startIndex">
        /// (Optional) For <see cref="AT.INVOKE"/> and <see cref="AT.REDIRECT"/>
        /// The 1 based match index to begin considering replacements.
        /// Matches before this index are ignored.
        /// Use 0 to start from the first match.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="methodName"/> cannot be found on <paramref name="type"/>.
        /// </exception>
        public PatchAttribute(Type type, string methodName, AT at, string target = null, uint occurrence = 0, uint startIndex = 0, bool overwriting = false)
        {
            TargetMethod = type.GetMethod(methodName, 
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            
            if (TargetMethod == null)
            {
                throw new ArgumentException($"Could not find method '{methodName}' in type '{type.FullName}'.");
            }

            At = at;
            TargetMember = target;
            Overwriting = overwriting;
            Occurrence = occurrence;
            StartIndex = startIndex;
        }
    }
}
