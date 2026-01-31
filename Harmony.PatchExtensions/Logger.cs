namespace HarmonyLib.PatchExtensions;

public static class Logger
{
    public static void Log(string log)
    {
        Console.WriteLine($"[HarmonyLib.PatchExtensions | Log] {log}");
    }
    
    public static void LogWarning(string log)
    {
        Console.WriteLine($"[HarmonyLib.PatchExtensions | Warning] {log}");
    }
    
    public static void LogError(string log)
    {
        Console.WriteLine($"[HarmonyLib.PatchExtensions | Error] {log}");
    }
}