namespace BongoAutoChest
{
    // One request per observed ready cycle. A timeout never rearms a chest.
    public sealed class ReadyGate
    {
        public bool Attempted { get; private set; }
        public bool Ready { get; private set; }
        public bool Observe(bool ready)
        {
            bool cleared = Attempted && !ready;
            Ready = ready;
            if (!ready) Attempted = false;
            return cleared;
        }
        public bool CanAttempt { get { return Ready && !Attempted; } }
        public void MarkAttempt() { Attempted = true; }
    }
}
