namespace HarmonyLib.PatchExtensions.Tests;

// _ so it appears on top
public class PatchingTargets
{
    public int Add(int a, int b)
    {
        return a + b;
    }
    
    public int Add2(int a, int b)
    {
        return a + b;
    }

    public int AddWithCounter(int a, int b)
    {
        CallCounter.AddCalls++;
        return a + b;
    }
    
    public int AddWithCounter2(int a, int b)
    {
        CallCounter.AddCalls++;
        return a + b;
    }

    public void Nothin()
    {
        CallCounter.NothinCalls++;
    }
    
    public int CallHelper(int value)
    {
        return PatchingHelper.Double(value);
    }

    public void CallHelpersTwice()
    {
        PatchingHelper.Nothin();
        PatchingHelper.Nothin();
    }

    public static class CallCounter
    {
        public static int AddCalls;
        public static int NothinCalls;

        public static void Reset()
        {
            AddCalls = 0;
            NothinCalls = 0;
        }
    }

    public static class PatchingHelper
    {
        public static int NothinCalls;
        public static int DoubleCalls;

        public static void Nothin()
        {
            NothinCalls++;
        }

        public static int Double(int value)
        {
            DoubleCalls++;
            return value * 2;
        }

        public static void Reset()
        {
            NothinCalls = 0;
            DoubleCalls = 0;
        }
    }
}
