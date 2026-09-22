namespace BongoAutoChest
{
    // One request per observed ready cycle. A timeout never rearms a chest.
    public sealed class ReadyGate
    {
        public bool Attempted { get; private set; }
        public bool Ready { get; private set; }
        public double DiscoveredAt { get; private set; }
        public double ReadyAt { get; private set; }
        public bool Observe(bool ready, double now, double delay)
        {
            bool cleared = Attempted && !ready;
            if (ready && !Ready)
            {
                DiscoveredAt = now;
                ReadyAt = now + delay;
            }
            Ready = ready;
            if (!ready) { Attempted = false; DiscoveredAt = ReadyAt = 0; }
            return cleared;
        }
        // Discovery and dispatcher cooldowns overlap; they are never added together.
        public bool CanAttemptAt(double now, double nextAction)
        { return Ready && !Attempted && now >= ReadyAt && now >= nextAction; }
        public void MarkAttempt() { Attempted = true; }
    }
}
