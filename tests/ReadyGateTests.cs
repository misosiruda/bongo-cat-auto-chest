using System;
using BongoAutoChest;
class ReadyGateTests
{
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static void Main()
    {
        var g = new ReadyGate();
        Check(!g.CanAttempt, "unknown state cannot dispatch");
        g.Observe(false); Check(!g.CanAttempt, "not ready cannot dispatch");
        g.Observe(true); Check(g.CanAttempt, "ready allows one dispatch");
        g.MarkAttempt(); Check(!g.CanAttempt, "attempt locks current cycle");
        for (int i = 0; i < 10000; i++) g.Observe(true);
        Check(!g.CanAttempt, "repeated ready observations cannot cause duplicate spending");
        Check(g.Observe(false), "ready clear is observable");
        Check(!g.CanAttempt, "clear does not dispatch");
        g.Observe(true); Check(g.CanAttempt, "new ready cycle rearms");
        var other = new ReadyGate(); other.Observe(true);
        g.MarkAttempt(); Check(other.CanAttempt, "players and chest types have independent gates");
        Console.WriteLine("9 checks passed.");
    }
}
