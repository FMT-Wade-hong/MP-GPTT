using System;
using MissionPlanner.Comms;

namespace MissionPlanner.FMT
{
    // Read-only observer. Never requests a stream, sends a command or changes a parameter.
    internal sealed class FmtSurfaceTelemetry : IDisposable
    {
        private readonly object sync = new object();
        private MAVLinkInterface link;
        private MAVState target;
        private ICommsSerial stream;
        private readonly ushort[] outputs = new ushort[32];
        private readonly DateTime[] received = new DateTime[32];
        internal int Generation { get; private set; }

        internal bool SetSource(MAVLinkInterface nextLink, MAVState nextTarget, ICommsSerial nextStream)
        {
            lock (sync)
            {
                if (ReferenceEquals(link, nextLink) && ReferenceEquals(target, nextTarget) && ReferenceEquals(stream, nextStream)) return false;
                if (link != null) link.OnPacketReceived -= OnPacket;
                link = nextLink; target = nextTarget; stream = nextStream;
                Array.Clear(outputs, 0, outputs.Length); Array.Clear(received, 0, received.Length);
                Generation++;
                if (link != null) link.OnPacketReceived += OnPacket;
                return true;
            }
        }

        private void OnPacket(object sender, MAVLink.MAVLinkMessage message)
        {
            if (message.msgid != (uint)MAVLink.MAVLINK_MSG_ID.SERVO_OUTPUT_RAW) return;
            lock (sync)
            {
                if (!ReferenceEquals(sender, link) || target == null || message.sysid != target.sysid || message.compid != target.compid) return;
                var raw = message.ToStructure<MAVLink.mavlink_servo_output_raw_t>();
                if (raw.port > 1) return;
                var values = new[] { raw.servo1_raw, raw.servo2_raw, raw.servo3_raw, raw.servo4_raw,
                    raw.servo5_raw, raw.servo6_raw, raw.servo7_raw, raw.servo8_raw, raw.servo9_raw,
                    raw.servo10_raw, raw.servo11_raw, raw.servo12_raw, raw.servo13_raw, raw.servo14_raw,
                    raw.servo15_raw, raw.servo16_raw };
                int start = raw.port * 16;
                // Use receipt time, not the aircraft clock. Old replay packets are never live.
                var now = DateTime.UtcNow;
                for (int i = 0; i < values.Length; i++) { outputs[start + i] = values[i]; received[start + i] = now; }
            }
        }

        internal int? Read(int channel, DateTime now)
        {
            if (channel < 1 || channel > 32) return null;
            lock (sync)
            {
                double age = (now - received[channel - 1]).TotalSeconds;
                return age >= 0 && age <= 3 && outputs[channel - 1] != 0 ? (int?)outputs[channel - 1] : null;
            }
        }
        public void Dispose() => SetSource(null, null, null);
    }
}
