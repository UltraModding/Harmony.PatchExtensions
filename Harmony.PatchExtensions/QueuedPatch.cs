using System.Reflection;

namespace HarmonyLib.PatchExtensions;

internal class QueuedPatch // for checking for conflicts
{
    public QueuedPatch(HarmonyMethod harmonyMethod, AT type, bool overwriting, MethodInfo patchMethod)
    {
        HarmonyMethod = harmonyMethod;
        Type = type;
        Overwriting = overwriting;
        PatchMethod = patchMethod;
    }
    
    public HarmonyMethod HarmonyMethod;
    public AT Type;
    public bool Overwriting;
    public MethodInfo PatchMethod;
}