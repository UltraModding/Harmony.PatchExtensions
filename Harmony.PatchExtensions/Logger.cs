namespace PatchExtensions;

public static class Logger
{
    public static void Log(string log)
    {
        Console.WriteLine($"[Dolfe.MixinSystem | Log] {log}");
    }
    
    public static void LogWarning(string log)
    {
        Console.WriteLine($"[Dolfe.MixinSystem | Warning] {log}");
    }
    
    public static void LogError(string log)
    {
        Console.WriteLine($"[Dolfe.MixinSystem | Error] {log}");
    }
}