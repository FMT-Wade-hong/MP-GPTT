using System;
using System.Threading;

namespace MissionPlanner.FMT
{
    // Bounds both the refresh rate and the number of outstanding UI callbacks.
    internal sealed class FmtUiUpdateGate
    {
        private int pending;
        private int lastTick;
        private bool started;

        internal bool TryEnter(int tick, uint interval)
        {
            if (Interlocked.CompareExchange(ref pending, 1, 0) != 0)
                return false;
            if (started && unchecked((uint)(tick - lastTick)) < interval)
            {
                Complete();
                return false;
            }
            started = true;
            lastTick = tick;
            return true;
        }

        internal void Complete()
        {
            Interlocked.Exchange(ref pending, 0);
        }
    }
}
