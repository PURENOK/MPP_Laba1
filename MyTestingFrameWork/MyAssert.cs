using System;

namespace MyTestingFramework;

public static class MyAssert
{
    public static void IsTrue(bool b) { if (!b) throw new TestFailedException("Expected True"); }
    public static void IsFalse(bool b) { if (b) throw new TestFailedException("Expected False"); }
    public static void AreEqual(object? e, object? a) { if (!Equals(e, a)) throw new TestFailedException($"Expected {e}, Got {a}"); }
    public static void AreNotEqual(object? e, object? a) { if (Equals(e, a)) throw new TestFailedException("Values are equal!"); }
    public static void IsNull(object? o) { if (o != null) throw new TestFailedException("Expected Null"); }
    public static void IsNotNull(object? o) { if (o == null) throw new TestFailedException("Expected Not Null"); }
    public static void IsEmpty(string? s) { if (!string.IsNullOrEmpty(s)) throw new TestFailedException("String not empty"); }
    public static void Contains(string sub, string full) { if (full == null || !full.Contains(sub)) throw new TestFailedException($"Missing '{sub}'"); }
    public static void GreaterThan(int v, int l) { if (v <= l) throw new TestFailedException($"{v} not > {l}"); }
    public static void IsInstanceOf<T>(object? o) { if (o is not T) throw new TestFailedException($"Not instance of {typeof(T).Name}"); }
}