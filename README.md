# Harmony.PatchExtensions

Attribute based extensions for Harmony that lets you define mixin style patches.

## Features
- Attribute based patches
- Injection points: HEAD (prefix), RETURN (postfix), INVOKE (insert before call), REDIRECT (replace call)
- Occurrence and start-index targeting
- Conflict detection
- Optional wrapper for overwrite prefixes that return the target's return type

## Install
Use one of the following:
- Reference the project directly in your solution
- Install the published NuGet package if available

Targets: `net8.0`, `net6.0`, `netstandard2.0`, `netstandard2.1`

## Quick Start
```csharp
using System.Reflection;
using HarmonyLib;
using PatchExtensions;

public static class Program
{
    public static void Main()
    {
        var harmony = new Harmony("example.patching");
        MixinLoader.ApplyPatches(harmony, Assembly.GetExecutingAssembly());
    }
}
```

## Patch Examples

### HEAD (prefix)
```csharp
using PatchExtensions;

public class Target
{
    private int Add(int a, int b) => a + b;
}

public static class Patches
{
    [Patch(typeof(Target), "Add", AT.HEAD)]
    private static void AddPrefix(int a, int b)
    {
        // Runs before Target.Add
    }
}
```

### HEAD overwrite (skip original)
If `overwriting: true`, the patch can return `bool` (Harmony style) or the same return type as the target.
```csharp
using PatchExtensions;

public static class OverwritePatches
{
    [Patch(typeof(Target), "Add", AT.HEAD, overwriting: true)]
    private static int AddOverwrite(int a, int b)
    {
        return 68; // the wrapper sets __result and skips original automatically
    }
}
```

### RETURN (postfix)
```csharp
using PatchExtensions;

public static class ReturnPatches
{
    [Patch(typeof(Target), "Add", AT.RETURN)]
    private static void AddPostfix(int a, int b, ref int __result)
    {
        __result += 1;
    }
}
```

### INVOKE (insert before call)
```csharp
using PatchExtensions;

public class TargetCalls
{
    private void Foo()
    {
        Helper.DoThing();
        Helper.DoThing();
    }
}

public static class InvokePatches
{
    [Patch(typeof(TargetCalls), "Foo", AT.INVOKE, target: "Helper.DoThing", occurrence: 2)]
    private static void BeforeSecondCall()
    {
        // Injected before the 2nd call to Helper.DoThing
    }
}
```

### REDIRECT (replace call)
```csharp
using PatchExtensions;

public static class RedirectPatches
{
    [Patch(typeof(TargetCalls), "Foo", AT.REDIRECT, target: "Helper.DoThing")]
    private static void ReplaceCall()
    {
        // Replaces Helper.DoThing with this method
    }
}
```

## Conflict Resolution
When multiple patches/transpilers target the same method, set the resolution strategy:
```csharp
using PatchExtensions;

MixinLoader.ConflictResolutionMethod = MixinLoader.ConflictResolver.Warn;
```

Options:
- `Warn` (default): log conflicts and continue
- `Error`: throw and stop
- `SkipConflicts`: skip conflicting targets

## License
Apache-2.0. See `LICENSE.md`.
