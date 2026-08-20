using System.Reflection;

namespace HarmonyLib.PatchExtensions;

public static class Logger
{
    public static string LogPath { get; private set; } 
    
    private static StreamWriter _writer;
    static Logger()
    {
        LogPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), $"LogFile.txt");
        Logger.Log($"Logging to file at: {LogPath}");
        // File.Delete(LogPath);
        // File.Create(LogPath);
        _writer = new StreamWriter(LogPath);
    }
    
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
    
    public static void LogFile(string toString)
    {
        _writer.WriteLine(toString);
        _writer.Flush();
    }
}