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
    
    public int Double(int value)
    {
        return PatchingHelper.Double(value);
    }

    public float CallBarThenFoo(float value)
    {
        float foo = PatchingHelper.Bar(value);
        float result = foo + 2.5f;
        PatchingHelper.Foo(foo);
        return result;
    }

    public float CallBarThenFooAdjusted(float value, float offset)
    {
        float foo = PatchingHelper.Bar(value);
        float adjusted = (foo + offset) * 1.25f;
        PatchingHelper.Foo(adjusted);
        return adjusted;
    }

    public void CallHelpersTwice()
    {
        PatchingHelper.Nothin();
        PatchingHelper.Nothin();
    }
    
    public void CallHelpersTwice2()
    {
        PatchingHelper.Nothin();
        PatchingHelper.Nothin();
    }
    
    public void CallHelpersTwice3()
    {
        PatchingHelper.Nothin();
        PatchingHelper.Nothin();
    }
    
    public void CallHelpersTwice4()
    {
        PatchingHelper.Nothin();
        PatchingHelper.Nothin();
    }
    
    public void CallHelpersTwice5()
    {
        PatchingHelper.Nothin();
        PatchingHelper.Nothin();
    }
    
    public void CallHelpersTwice6()
    {
        PatchingHelper.Nothin();
        PatchingHelper.Nothin();
    }
    
    public void CallHelpersTwice7()
    {
        PatchingHelper.Nothin();
        PatchingHelper.Nothin();
    }

    public int SomeField;
    
    public void AccessFieldTwice()
    {
        SomeField = 10;
        SomeField = 20;
    }

    public static class CallCounter
    {
        public static int AddCalls;
        public static int AddCalls2;
        public static int AddCalls3;
        public static int NothinCalls;
        public static int FieldAccessCalls;
        public static int InvokeStackCalls;
        public static int RedirectStackCalls;
        public static int AfterStackCalls;

        public static void Reset()
        {
            AddCalls = 0;
            AddCalls2 = 0;
            AddCalls3 = 0;
            NothinCalls = 0;
            FieldAccessCalls = 0;
            InvokeStackCalls = 0;
            RedirectStackCalls = 0;
            AfterStackCalls = 0;
        }
    }

    public static class PatchingHelper
    {
        public static int NothinCalls;
        public static int DoubleCalls;
        public static int BarCalls;
        public static int FooCalls;
        public static float LastFooValue;

        public static void Nothin()
        {
            NothinCalls++;
        }

        public static int Double(int value)
        {
            DoubleCalls++;
            return value * 2;
        }

        public static float Bar(float value)
        {
            BarCalls++;
            return (value * 1.5f) + 0.5f;
        }

        public static void Foo(float value)
        {
            FooCalls++;
            LastFooValue = value;
        }

        public static void Reset()
        {
            NothinCalls = 0;
            DoubleCalls = 0;
            BarCalls = 0;
            FooCalls = 0;
            LastFooValue = 0f;
        }
    }
}
