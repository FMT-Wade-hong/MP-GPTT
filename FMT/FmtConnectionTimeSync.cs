using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MissionPlanner.FMT
{
    // Explicitly authorized connection-time message; never changes parameters.
    internal static class FmtConnectionTimeSync
    {
        internal sealed class Session
        {
            internal int Generation;
            private int scheduled;
            internal int Begin() { lock (this) return Interlocked.Increment(ref Generation); }
            internal bool Claim(int generation)
            {
                lock (this)
                {
                    if (generation != Volatile.Read(ref Generation) || scheduled == generation) return false;
                    scheduled = generation;
                    return true;
                }
            }
        }

        private static readonly ConditionalWeakTable<MAVLinkInterface, Session> Sessions =
            new ConditionalWeakTable<MAVLinkInterface, Session>();
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(FmtConnectionTimeSync));

        internal static int Begin(MAVLinkInterface link)
        {
            var session = Sessions.GetValue(link, key =>
            {
                var value = new Session();
                key.CommsClose += (sender, args) => value.Begin();
                return value;
            });
            return session.Begin();
        }

        internal static MAVLink.mavlink_system_time_t CreateMessage(DateTime utc)
        {
            if (utc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required", nameof(utc));
            var ticks = utc.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
            if (ticks <= 0) throw new ArgumentOutOfRangeException(nameof(utc));
            return new MAVLink.mavlink_system_time_t { time_unix_usec = (ulong)(ticks / 10), time_boot_ms = 0 };
        }

        internal static void Schedule(MAVLinkInterface link, int generation)
        {
            Session session;
            if (!Sessions.TryGetValue(link, out session) || !session.Claim(generation)) return;
            var stream = link.BaseStream;
            var sysid = link.sysidcurrent;
            var compid = link.compidcurrent;
            Task.Run(async () =>
            {
                try
                {
                    // Defer while the connection is busy; never resend or wait for an ACK.
                    while (generation == Volatile.Read(ref session.Generation))
                    {
                        if (!ReferenceEquals(stream, link.BaseStream) || stream == null || !stream.IsOpen ||
                            link.logreadmode || link.ReadOnly || link.sysidcurrent != sysid ||
                            link.compidcurrent != compid) return;
                        if (link.giveComport || link.IsParameterListLoading || link.IsLogDownloadActive)
                        {
                            await Task.Delay(500).ConfigureAwait(false);
                            continue;
                        }
                        if (generation != Volatile.Read(ref session.Generation)) return;
                        link.sendPacket(CreateMessage(DateTime.UtcNow), sysid, compid);
                        Log.InfoFormat("FMT SYSTEM_TIME sent once for connection {0}, vehicle {1}/{2} (no ACK expected)", generation, sysid, compid);
                        return;
                    }
                }
                catch (Exception ex) { Log.Warn("FMT connection time sync failed; no automatic retry", ex); }
            });
        }
    }
}
