using System;
using BongoAutoChest;
class ReadyGateTests
{
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static void Main()
    {
        var g = new ReadyGate();
        Check(!g.CanAttemptAt(100,0), "unknown state cannot dispatch");
        g.Observe(false,100,0); Check(!g.CanAttemptAt(100,0), "not ready cannot dispatch");
        g.Observe(true,100,5);
        Check(!g.CanAttemptAt(100,0), "idle dispatcher still waits after discovery");
        Check(!g.CanAttemptAt(104.999,0), "discovery delay cannot be bypassed just before deadline");
        for (int i = 0; i < 10000; i++) g.Observe(true,104,8);
        Check(g.ReadyAt == 105 && g.DiscoveredAt == 100, "repeated observations do not restart the timer");
        Check(g.CanAttemptAt(105,0), "ready exactly at discovery deadline");
        Check(!g.CanAttemptAt(105,108), "longer dispatcher cooldown still applies");
        Check(g.CanAttemptAt(108,108), "overlapping cooldowns are not added together");
        g.MarkAttempt(); Check(!g.CanAttemptAt(1000,0), "elapsed delay cannot repeat an attempted chest");
        Check(g.Observe(false,1001,0), "ready clear is observable");
        Check(!g.CanAttemptAt(1001,0), "clear does not dispatch");
        g.Observe(true,1010,3);
        Check(!g.CanAttemptAt(1012,0) && g.CanAttemptAt(1013,0), "new ready cycle gets a fresh delay");
        var other = new ReadyGate(); other.Observe(true,1012,8);
        Check(!other.CanAttemptAt(1013,0) && g.CanAttemptAt(1013,0), "players and chest types wait independently");
        other.Observe(false,1014,0); other.Observe(true,1015,3);
        Check(!other.CanAttemptAt(1017,0) && other.CanAttemptAt(1018,0), "disappearing before dispatch cancels the old delay");
        g.MarkAttempt(); Check(other.CanAttemptAt(1020,0), "another chest attempt does not reset discovery time");
        Console.WriteLine("15 checks passed.");
    }
}
