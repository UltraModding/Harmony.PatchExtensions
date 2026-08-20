# Harmony.PatchExtensions

Attribute based extensions for Harmony that lets you define mixin style patches.

## Features
- Attribute based patches
- Injection points: HEAD (prefix), POSTFIX, RETURN (At every return), INVOKE (insert before call), REDIRECT (replace call), AFTER (after call), FINALLY, ARG, LOOP_BEFORE/TOP/BOTTOM/AFTER, BRANCH_TRUE/FALSE, LOCAL_WRITE/READ, ARG_WRITE/READ, FIELD_WRITE/READ
- Occurrence and start-index targeting
- Conflict detection
- Optional wrapper for overwriting prefixes that return the target's return type
- Patching variable declarations in methods 

## Install
Use one of the following:
- Reference the project directly in your solution
- Install the published NuGet package: (Insert package when uploaded here)
- If using publicizers add `<DoNotPublicize Include="Harmony.PatchExtensions" />` to an ItemGroup so documentations shows up in the code editor

## Quick Start
```csharp
using System.Reflection;
using HarmonyLib;
using HarmonyLib.PatchExtensions;

public static class Program
{
    public static void Main()
    {
        var harmony = new Harmony("example.patching");
        MixinLoader.ApplyPatches(harmony, Assembly.GetExecutingAssembly());
        // Or for using a single patch class 
        // MixinLoader.ApplyPatches(harmony, Assembly.GetExecutingAssembly(), typeof(Patches));
    }
}
```

## Patch Examples

### HEAD (prefix)
```csharp
using HarmonyLib.PatchExtensions;

public static class Patches
{
    [Patch(typeof(Target), "Add", AT.HEAD)]
    public static void AddPrefix(int a, int b)
    {
        // Will run before Target.Add
        a += 1;
    }
}
```
Result:
```diff
public class Target
{
    public int Add(int a, int b) 
    { 
+       a += 1;
        return a + b;
    }
}
```

### HEAD overwrite (skip original)
If `overwriting: true`, the patch can return `bool` (Harmony style) or the same return type as the target.
```csharp
using HarmonyLib.PatchExtensions;

public static class OverwritePatches
{
    [Patch(typeof(Target), "Add", AT.HEAD, overwriting: true)]
    public static int AddOverwrite(int a, int b)
    {
        return 68; // the wrapper sets __result and skips original automatically
    }
}
```
Result:
```diff
public class Target
{
    public int Add(int a, int b) 
    { 
+        return 68;
-        return a + b;
    }
}
```

### POSTFIX (Just harmony postfix)
```csharp
using HarmonyLib.PatchExtensions;

public static class PostfixPatches
{
    [Patch(typeof(Target), "Add", AT.POSTFIX)]
    public static void AddPostfix(int a, int b, ref int __result)
    {
        __result += 1;
    }
}
```
Result:
```diff
public class Target
{
    public int Add(int a, int b) 
    { 
-        return a + b;
+        __result = a + b;
+        __result += 1;
         return __result;
    }
}
```

### RETURN
```csharp
using HarmonyLib.PatchExtensions;

public static class ReturnPatches
{
    [Patch(typeof(Target), "Foo", AT.RETURN)]
    public static void AddReturn(int a, int b, bool c, ref int __result)
    {
        __result += 1;
    }
}
```
Result:
```diff
public class Target
{
    public int Foo(int a, int b, bool c)
    {
        if (c)
        {
            __result = a;
+           __result += 1;
            return __result;
        }
        else
        {
            __result = b;
+           __result += 1;
            return __result;
        }
    }
}
```

### INVOKE (insert before call)
```csharp
using HarmonyLib.PatchExtensions;

public static class InvokePatches
{
    [Patch(typeof(TargetCalls), "Foo", AT.INVOKE, target: "Helper.DoThing", occurrence: 2)] // occurrence: 0 matches all
    public static void BeforeSecondCall()
    {
        // Injected before the 2nd call to Helper.DoThing
    }
}
```
Result:
```diff
public class TargetCalls
{
    public void Foo()
    {
        Helper.DoThing();
+        InvokePatches.BeforeSecondCall();
        Helper.DoThing();
    }
}
```

### REDIRECT (replace call)
```csharp
using HarmonyLib.PatchExtensions;

public static class RedirectPatches
{
    [Patch(typeof(TargetCalls), "Foo", AT.REDIRECT, target: "Helper.DoThing", occurrence: 0)] // occurrence can also be specified to target
    public static void ReplaceCall()
    {
        // Replaces Helper.DoThing with this method
    }
}
```
Result:
```diff
public class TargetCalls
{
    public void Foo()
    {
-        Helper.DoThing();
-        Helper.DoThing();
+        RedirectPatches.ReplaceCall();
+        RedirectPatches.ReplaceCall();
    }
}
```

### REDIRECT with occurrence
```csharp
using HarmonyLib.PatchExtensions;

public static class RedirectPatches
{
    [Patch(typeof(TargetCalls), "Foo", AT.REDIRECT, target: "Helper.DoThing", occurrence: 1)] // occurrence can also be specified to target
    public static void ReplaceCall()
    {
        // Replaces Helper.DoThing with this method
    }
}
```
Result:
```diff
public class TargetCalls
{
    public void Foo()
    {
-        Helper.DoThing();
+        RedirectPatches.ReplaceCall();
        Helper.DoThing();
    }
}
```

### AFTER (after call)
```csharp
using HarmonyLib.PatchExtensions;

public static class AfterPatches
{
    [Patch(typeof(TargetCalls), "Foo", AT.AFTER, target: "Helper.DoThing", occurrence: 2)]
    public static void AfterCall()
    {
        // Puts code after call
    }
}
```
Result:
```diff
public class TargetCalls
{
    public void Foo()
    {
        Helper.DoThing();
        Helper.DoThing();
+       AfterPatches.AfterCall();
    }
}
```

### ARG (Replacing an argument)
```csharp
using HarmonyLib.PatchExtensions;

public static class AfterPatches
{
    [Patch(typeof(TargetCalls), "Foo", AT.ARG, target: "Helper.DoMath", occurrence: 1, ArgIndex: 1)]
    public static float AfterCall(float original)
    {
        return original * 2;
    }
}
```
Result:
```diff
public class TargetCalls
{
    public void Foo(float a, float b)
    {
-        Helper.DoMath(a, b);
+        Helper.DoMath(AfterPatches.AfterCall(a), b);
    }
}
```

### FINALLY (runs after the method, on any outcome)
```csharp
using HarmonyLib.PatchExtensions;

public static class FinallyPatches
{
    [Patch(typeof(Target), "DivideOrThrow", AT.FINALLY)]
    public static void OnFinally(Exception __exception)
    {
        // Runs after Target.DivideOrThrow even if it returns or throws
        // __exception is null if it doesn't throw
    }
}
```
Result:
```diff
public class Target
{
    public int DivideOrThrow(int value, int divisor)
    {
+       try
+       {
            return value / divisor;
+       }
+       finally
+       {
+           FinallyPatches.OnFinally(__exception);
+       }
    }
}
```

### BRANCH (TRUE, FALSE)
```csharp
using HarmonyLib.PatchExtensions;

public static class BranchPatches
{
    [Patch(typeof(TargetClass), "BranchTest", AT.BRANCH_TRUE, occurrence: 0)]
    [Patch(typeof(TargetClass), "BranchTest", AT.BRANCH_FALSE, occurrence: 0)]
    public static void BranchPoint()
    {
        Console.WriteLine($"Branch hit!");
    }
}
```
Result:
```diff
public class Target
{
    public void BranchTest(bool a)
    {
        if (a)
        {
+           Console.WriteLine($"Branch hit!");
            Console.WriteLine($"A is true!");
        }
        else
        {
+           Console.WriteLine($"Branch hit!");
            Console.WriteLine($"A is false!");
        }
    }
}
```

### LOOP (BEFORE, TOP, BOTTOM, AFTER)
```csharp
using HarmonyLib.PatchExtensions;

public static class LoopPatches
{
    [Patch(typeof(TargetClass), "LoopFor", AT.LOOP_BEFORE, occurrence: 0)]
    [Patch(typeof(TargetClass), "LoopFor", AT.LOOP_TOP, occurrence: 0)]
    [Patch(typeof(TargetClass), "LoopFor", AT.LOOP_BOTTOM, occurrence: 0)]
    [Patch(typeof(TargetClass), "LoopFor", AT.LOOP_AFTER, occurrence: 0)]
    public static void AtPointInLoop()
    {
        Console.WriteLine($"Point in loop hit!");
    }
}
```
Result:
```diff
public class Target
{
    public void LoopFor(int max)
    {
+       Console.WriteLine($"Point in loop hit!");
        for (int i = 0; i < max; i++)
        {
+           Console.WriteLine($"Point in loop hit!");
            Console.WriteLine($"{i}");
+           Console.WriteLine($"Point in loop hit!");
        }
+       Console.WriteLine($"Point in loop hit!");
    }
}
```

### LOCAL_READ/WRITE
There is also ARG and FIELD
```csharp
using HarmonyLib.PatchExtensions;

public static class LocalPatches
{
    [Patch(typeof(Target), nameof(Target.ReadAndWriteVars), AT.LOCAL_READ, target: "locFloat", occurrence: 0)]
    public static void LocalRead(float val)
    {
        Console.WriteLine($"Local read: {val}");
    }
    
    [Patch(typeof(Target), nameof(Target.ReadAndWriteVars), AT.LOCAL_WRITE, target: "locFloat", occurrence: 0)]
    public static void LocalWrite(float val)
    {
        Console.WriteLine($"Local write: {val}");
    }
}
```
Result:
```diff
public class Target
{
    public float ClassVar;
    public void ReadAndWriteVars(float num)
    {
+       Console.WriteLine($"Local write: {locFloat}");
        float locFloat = 99f;
        
-        if (locFloat > num)
+        if (locFloat /* locFloat is read and the method is called, but it's hard to show */ > num)
        {
            ClassVar = locFloat;
+           Console.WriteLine($"Local read: {locFloat}");
        }
        else
        {
            ClassVar = num;
        }
    }
}
```

### Assembly FIELD_READ/WRITE
```csharp
using HarmonyLib.PatchExtensions;

public static class FieldPatches
{
    [AssemblyPatch(fieldDeclaringType: typeof(Target), fieldName: nameof(NewMovement.Variable), at: AT.FIELD_READ, scanEntireAssembly: true, occurrence: 0)]
    public static void FieldRead(float val)
    {
        Console.WriteLine($"Variable read: {val}");
    }
    
    [AssemblyPatch(fieldDeclaringType: typeof(Target), fieldName: nameof(NewMovement.Variable), at: AT.FIELD_WRITE, scanEntireAssembly: true, occurrence: 0)]
    public static void FieldWrite(float val)
    {
        Console.WriteLine($"Variable written: {val}");
    }
}
```
Result:
```diff
public class Target
{
    public float Variable;
    
    public void DoWhatever(float writeVar)
    {
+        Console.WriteLine($"Variable written: {writeVar}");
        Variable = writeVar;
    }
    
    public void SomethingElse(ref float someVar)
    {
        someVar = Variable;
+        Console.WriteLine($"Variable read: {Variable}");
    }
}
```


## Conflict Resolution
When multiple patches/transpilers target the same method, set the resolution strategy:
```csharp
MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Warn;
```

Options:
- `Warn` (default): log conflicts and continue
- `Error`: throw and stop
- `SkipConflicts`: skip conflicting targets

TODO:
Write tests fir FINALLY, BRANCH and LOOP

## License
Apache-2.0. See `LICENSE.md`.
