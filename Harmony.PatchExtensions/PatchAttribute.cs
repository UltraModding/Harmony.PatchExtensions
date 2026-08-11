using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace HarmonyLib.PatchExtensions 
{
    /// <summary>
    /// The injection point or strategy for applying a Mixin patch.
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum AT
    {
        /// <summary>
        /// <b>Harmony Prefix</b>. <br/>
        /// Use <see cref="PatchAttribute.Overwriting"/> to skip original method.
        /// </summary>
        HEAD,

        /// <summary>
        /// Injects code at every return in a method
        /// </summary>
        RETURN,
        
        /// <summary>
        /// <b>Harmony Postfix</b>.
        /// </summary>
        POSTFIX,
        
        /// <summary>
        /// <b>Injects</b> code <b>before</b> the method call in the target method. <br/>
        /// You must specify <see cref="PatchAttribute.TargetMember"/> to choose which call to target.
        /// You must also specify <see cref="PatchAttribute.Occurrence"/> to choose a specific occurrence
        /// </summary>
        INVOKE,

        /// <summary>
        /// <b>Replaces</b> a specific method call in the target method with your patch method. <br/>
        /// You must specify <see cref="PatchAttribute.TargetMember"/> to choose which call to replace.
        /// You must also specify <see cref="PatchAttribute.Occurrence"/> to choose a specific occurrence
        /// </summary>
        REDIRECT,
        
        /// <summary>
        /// <b>Injects</b> code <b>after</b> the method call in the target method. <br/>
        /// You must specify <see cref="PatchAttribute.TargetMember"/> to choose which call to target.
        /// You must also specify <see cref="PatchAttribute.Occurrence"/> to choose a specific occurrence
        /// </summary>
        AFTER,
        
        /// <summary>
        /// Replace an argument with a call to your method
        /// You must also specify <see cref="PatchAttribute.Occurrence"/> to choose a specific occurrence
        /// You must also specify <see cref="PatchAttribute.ArgIndex"/> to choose the argument (0 indexed)
        /// </summary>
        ARG,
        
        /// <summary>
        /// Inserts code before a loop runs
        /// You must also specify <see cref="PatchAttribute.Occurrence"/> to choose a specific loop occurrence
        /// </summary>
        LOOP_BEFORE,
        
        /// <summary>
        /// Inserts code at the top of a loop
        /// You must also specify <see cref="PatchAttribute.Occurrence"/> to choose a specific loop occurrence
        /// </summary>
        LOOP_TOP,
        
        /// <summary>
        /// Inserts code at the bottom of a loop
        /// You must also specify <see cref="PatchAttribute.Occurrence"/> to choose a specific loop occurrence
        /// </summary>
        LOOP_BOTTOM,
        
        /// <summary>
        /// Inserts code after a loop
        /// You must also specify <see cref="PatchAttribute.Occurrence"/> to choose a specific loop occurrence
        /// </summary>
        LOOP_AFTER,
        
        /// <summary>
        /// Wraps a method in a try {} finally {} so your code will run regardless
        /// </summary>
        FINALLY,
        
        // MAY NEED TO ADD A else!!!!
        /// <summary>
        /// Runs if the branch evaluates true
        /// You must also specify <see cref="PatchAttribute.Occurrence"/> to choose a specific branch occurrence
        /// </summary>
        BRANCH_TRUE,
        
        /// <summary>
        /// Runs if the branch evaluates false
        /// You must also specify <see cref="PatchAttribute.Occurrence"/> to choose a specific branch occurrence
        /// </summary>
        BRANCH_FALSE,
        
        /// <summary>
        /// If a local value is written to
        /// You must also specify <see cref="PatchAttribute.TargetMember"/> to choose what local to detect writes
        /// </summary>
        LOCAL_WRITE,
        
        /// <summary>
        /// If a local value is read from
        /// You must also specify <see cref="PatchAttribute.TargetMember"/> to choose what local to detect reads
        /// </summary>
        LOCAL_READ,

        /// <summary>
        /// If an arg is written to
        /// You must also specify <see cref="PatchAttribute.TargetMember"/> to choose what arg to detect writes
        /// </summary>
        [Obsolete("Not yet implemented")]
        ARG_WRITE,
        
        /// <summary>
        /// If ab arg is read from
        /// You must also specify <see cref="PatchAttribute.TargetMember"/> to choose what arg to detect reads
        /// </summary>
        [Obsolete("Not yet implemented")]
        ARG_READ,
        
        /// <summary>
        /// If a field is written to
        /// You must also specify <see cref="PatchAttribute.TargetMember"/> to choose what field to detect writes
        /// </summary>
        [Obsolete("Not yet implemented")]
        FIELD_WRITE,
        
        /// <summary>
        /// If a field is read from
        /// You must also specify <see cref="PatchAttribute.TargetMember"/> to choose what field to detect writes
        /// </summary>
        [Obsolete("Not yet implemented")]
        FIELD_READ,
        
        /// <summary>
        /// 
        /// </summary>
        [Obsolete("Not yet implemented")]
        TBD,
    }

    /// <summary>
    /// Declares a Harmony patch to be discovered and applied by <see cref="MixinLoader.ApplyPatches"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class PatchAttribute : Attribute
    {
        internal bool DoNotPatch = true;
        
        /// <summary>
        /// The original method in the that will be modified.
        /// </summary>
        public MethodInfo? TargetMethod { get; }

        /// <summary>
        /// The location or type of patch HEAD, RETURN, POSTFIX, INVOKE, REDIRECT, AFTER
        /// </summary>
        public AT At { get; }

        /// <summary>
        /// Required only for <see cref="AT.INVOKE"/> and <see cref="AT.REDIRECT"/> and <see cref="AT.AFTER"/>. <br/>
        /// The name of the method call inside the <see cref="TargetMethod"/> that you want to target.
        /// </summary>
        public string TargetMember { get; }

        /// <summary>
        /// Only used with <see cref="AT.HEAD"/>. <br/>
        /// If true the patch method can return, false to stop the original method from running.
        /// </summary>
        public bool Overwriting { get; }

        /// <summary>
        /// Required only for <see cref="AT.INVOKE"/> and <see cref="AT.REDIRECT"/> and <see cref="AT.AFTER"/>. <br/>
        /// Specifies which occurrence to patch, counted relative to <see cref="StartIndex"/>.
        /// Use 0 to patch all matching calls after <see cref="StartIndex"/>.
        /// </summary>
        public uint Occurrence { get; } = 0;

        /// <summary>
        /// Optional only for <see cref="AT.INVOKE"/> and <see cref="AT.REDIRECT"/> and <see cref="AT.AFTER"/>. <br/>
        /// Matches before this index are ignored.
        /// Use 0 to start from the first match.
        /// </summary>
        public uint StartIndex { get; } = 0;
        
        /// <summary>
        /// For <see cref="AT.ARG"/>
        /// What argument index to replace
        /// Use 1 to start from the first match (0 gives error)
        /// </summary>
        public uint ArgIndex { get; } = 0;
        
        /// <summary>
        /// Defines a patch for a specific method.
        /// </summary>
        /// <param name="type">The class type containing the method you want to patch.</param>
        /// <param name="methodName">The name of the method you want to patch.</param>
        /// <param name="at">The injection point (HEAD, RETURN, POSTFIX, INVOKE, REDIRECT, AFTER).</param>
        /// <param name="target">
        /// (Optional) For <see cref="AT.INVOKE"/> or <see cref="AT.REDIRECT"/> or <see cref="AT.AFTER"/>, this is the name of the method being called inside the target.
        /// </param>
        /// <param name="overwriting">
        /// (Optional) For <see cref="AT.HEAD"/>, set to true to cancel.
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
        public PatchAttribute(Type type, string methodName, AT at, string? target = null, uint occurrence = 0, uint startIndex = 0, uint argIndex = 0, bool overwriting = false)
        {
            TargetMethod = type.GetMethod(methodName, 
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ?? null;
            
            if (TargetMethod == null)
            {
                Logger.LogError($"Could not find method '{methodName}' in type '{type.FullName}', not running this patch.");
                return;
            }

            if (string.IsNullOrEmpty(target) && at is AT.INVOKE or AT.REDIRECT or AT.AFTER)
            {
                Logger.LogError($"target is null or empty, not running this patch.");
                return;
            }
            TargetMember = target!;
            
            if (at is AT.ARG && argIndex == 0)
            {
                Logger.LogError($"argIndex not set when required for ARG, not running this patch.");
                return;
            }
            ArgIndex = argIndex;
            
            // if (occurrence == uint.MaxValue && at is AT.INVOKE or AT.AFTER or AT.REDIRECT)
            // {
                // Logger.LogError($"occurrence not set when required for INVOKE, REDIRECT and AFTER, not running this patch.");
                // return;
            // }
            Occurrence = occurrence;
            
            At = at;
            StartIndex = startIndex;
            
            if (overwriting && at is not AT.HEAD)
            {
                Logger.LogWarning($"FYI, overwriting set on a non head AT does nothing");
            }
            Overwriting = overwriting;
            
            DoNotPatch = false;
        }
    }
}
