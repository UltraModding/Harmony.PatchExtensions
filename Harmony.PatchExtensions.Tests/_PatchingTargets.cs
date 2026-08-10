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
    
    public int DivideOrThrow(int value, int divisor)
    {
        try
        {
            return value / divisor;
        }
        finally
        {
            CallCounter.FinallyCalls++;
        }
    }
    
    public int DivideOrThrow2(int value, int divisor)
    {
        return value / divisor;
    }
    
    public int SumLoop(int count)
    {
        int sum = 0;
        for (int i = 0; i < count; i++)
        {
            sum += i;
        }
        
        return sum;
    }
    
    public int SumLoopWithBreak(int count, int breakAt)
    {
        int sum = 0;
        for (int i = 0; i < count; i++)
        {
            if (i == breakAt) break;
            
            sum += i;
        }
        
        return sum;
    }
    
    public int SumLoopWithContinue(int count, int skip)
    {
        int sum = 0;
        for (int i = 0; i < count; i++)
        {
            if (i == skip) continue;
            
            sum += i;
        }
        
        return sum;
    }
    
    public int Branch(bool condition)
    {
        if (condition)
        {
            return PatchingHelper.Double(1);
        }
        else
        {
            return PatchingHelper.Double(2);
        }
    }
    
    public int Branch2(bool condition)
    {
        if (condition)
        {
            return PatchingHelper.Double(1);
        }
        else
        {
            return PatchingHelper.Double(2);
        }
    }
    
    public int LocalWriteTwice(int a, int b)
    {
        int total = a;
        total += b;
        
        return total;
    }
    
    public int LocalWriteTwice2(int a, int b)
    {
        int total = a;
        total += b;
        
        return total;
    }
    
    public int SomeField2;
    
    public void AccessFieldTwice2()
    {
        SomeField2 = 10;
        SomeField2 = 20;
    }
    
    public void AccessFieldWithValue(int value)
    {
        SomeField = value;
    }
    
    public float BarWithTwoArgs(float value, float offset)
    {
        return PatchingHelper.BarTwoArgs(value, offset);
    }
    
    public float BarWithTwoArgs2(float value, float offset)
    {
        return PatchingHelper.BarTwoArgs(value, offset);
    }
    
    public (float, float) BarWithTwoArgsTwice(float value, float offset)
    {
        float res1 = PatchingHelper.BarTwoArgs(value, offset);
        return (res1, PatchingHelper.BarTwoArgs(value, offset));
    }
    
    public (float, float) BarWithTwoArgsTwice2(float value, float offset)
    {
        float res1 = PatchingHelper.BarTwoArgs(value, offset);
        return (res1, PatchingHelper.BarTwoArgs(value, offset));
    }
    
    public (float, float) BarWithTwoArgsTwice3(float value, float offset)
    {
        float res1 = PatchingHelper.BarTwoArgs(value, offset);
        return (res1, PatchingHelper.BarTwoArgs(value, offset));
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
        public static int FinallyCalls;
        public static int LoopTopCalls;
        public static int LoopBottomCalls;
        public static int BeforeLoopCalls;
        public static int LoopExitCalls;
        public static int BranchTrueCalls;
        public static int BranchFalseCalls;
        public static int LocalWriteCalls;
        
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
            FinallyCalls = 0;
            LoopTopCalls = 0;
            LoopBottomCalls = 0;
            BeforeLoopCalls = 0;
            LoopExitCalls = 0;
            BranchTrueCalls = 0;
            BranchFalseCalls = 0;
            LocalWriteCalls = 0;
        }
    }
    
    public static class PatchingHelper
    {
        public static int NothinCalls;
        public static int DoubleCalls;
        public static int BarCalls;
        public static int FooCalls;
        public static int BarTwoArgsCalls;
        public static float LastFooValue;
        public static float BarTwoArgsValueValue;
        public static float BarTwoArgsOffsetValue;
        
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
        
        public static float BarTwoArgs(float value, float offset)
        {
            BarTwoArgsCalls++;
            BarTwoArgsValueValue = value;
            BarTwoArgsOffsetValue = offset;
            
            return (value * 1.5f) + offset;
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
            BarTwoArgsCalls = 0;
            LastFooValue = 0f;
            BarTwoArgsValueValue = 0f;
            BarTwoArgsOffsetValue = 0f;
        }
    }
}