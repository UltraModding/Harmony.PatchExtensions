# Harmony.PatchExtensions

Attribute based extensions for Harmony that lets you define mixin style patches.

## Features
- Attribute based patches
- Injection points: HEAD (prefix), POSTFIX, RETURN (At every return), INVOKE (insert before call), REDIRECT (replace call), AFTER (after call)
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
    private static void AddPrefix(int a, int b)
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
    private int Add(int a, int b) 
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
    private static int AddOverwrite(int a, int b)
    {
        return 68; // the wrapper sets __result and skips original automatically
    }
}
```
Result:
```diff
public class Target
{
    private int Add(int a, int b) 
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
    private static void AddPostfix(int a, int b, ref int __result)
    {
        __result += 1;
    }
}
```
Result:
```diff
public class Target
{
    private int Add(int a, int b) 
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
    private static void AddReturn(int a, int b, bool c, ref int __result)
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
    private static void BeforeSecondCall()
    {
        // Injected before the 2nd call to Helper.DoThing
    }
}
```
Result:
```diff
public class TargetCalls
{
    private void Foo()
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
    private static void ReplaceCall()
    {
        // Replaces Helper.DoThing with this method
    }
}
```
Result:
```diff
public class TargetCalls
{
    private void Foo()
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
    private static void ReplaceCall()
    {
        // Replaces Helper.DoThing with this method
    }
}
```
Result:
```diff
public class TargetCalls
{
    private void Foo()
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
    private static void AfterCall()
    {
        // Puts code after call
    }
}
```
Result:
```diff
public class TargetCalls
{
    private void Foo()
    {
        Helper.DoThing();
        Helper.DoThing();
+       AfterPatches.AfterCall();
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

## License
Apache-2.0. See `LICENSE.md`.
