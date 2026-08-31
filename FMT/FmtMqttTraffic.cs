using System;
using System.Globalization;

namespace MissionPlanner.FMT
{
    // Session identity prevents a newly started bridge from inheriting the previous sample.
    internal struct FmtMqttTrafficSnapshot
    {
        internal readonly object Session;
        internal readonly long ReceivedBytes;
        internal readonly long SentBytes;
        internal bool IsRunning => Session != null;

        internal FmtMqttTrafficSnapshot(object session, long receivedBytes, long sentBytes)
        {
            Session = session;
            ReceivedBytes = receivedBytes;
            SentBytes = sentBytes;
        }
    }

    internal sealed class FmtMqttTrafficRate
    {
        private FmtMqttTrafficSnapshot previous;
        private double previousSeconds;
        internal double ReceivedPerSecond { get; private set; }
        internal double SentPerSecond { get; private set; }

        // Caller supplies monotonic seconds, not the wall clock (which can jump).
        internal void Sample(FmtMqttTrafficSnapshot current, double seconds)
        {
            ReceivedPerSecond = SentPerSecond = 0;
            double elapsed = seconds - previousSeconds;
            if (current.IsRunning && ReferenceEquals(current.Session, previous.Session) && elapsed > 0 &&
                current.ReceivedBytes >= previous.ReceivedBytes && current.SentBytes >= previous.SentBytes)
            {
                ReceivedPerSecond = (current.ReceivedBytes - previous.ReceivedBytes) / elapsed;
                SentPerSecond = (current.SentBytes - previous.SentBytes) / elapsed;
            }
            previous = current;
            previousSeconds = seconds;
        }

        internal static string Format(double bytesPerSecond)
        {
            if (double.IsNaN(bytesPerSecond) || double.IsInfinity(bytesPerSecond) || bytesPerSecond <= 0)
                return "0 B/s";
            var units = new[] { "B/s", "KiB/s", "MiB/s", "GiB/s" };
            int unit = 0;
            // Promote before rounded output would display 1024.0 in the smaller unit.
            while (bytesPerSecond >= 1023.95 && unit < units.Length - 1)
            {
                bytesPerSecond /= 1024;
                unit++;
            }
            return bytesPerSecond.ToString(unit == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) + " " + units[unit];
        }
    }
}
