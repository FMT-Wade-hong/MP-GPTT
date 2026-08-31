using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MissionPlanner.FMT
{
    // MQTT 3.1.1, clean session, QoS 0, no retained flight commands.
    // Each instance owns exactly one connection; reconnect creates a fresh instance.
    internal sealed class FmtMqttClient : IDisposable
    {
        private const int MaximumPacketLength = 1024 * 1024;
        private readonly TcpClient tcp = new TcpClient { NoDelay = true };
        private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
        private Stream stream;
        private int disposed;
        private int pingPending;
        private DateTime pingSent;

        internal async Task ConnectAsync(FmtMqttSettings settings, string password, CancellationToken ct)
        {
            await tcp.ConnectAsync(settings.Host, settings.Port).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            stream = tcp.GetStream();
            if (settings.UseTls)
            {
                var ssl = new SslStream(stream, false); // Default chain AND hostname validation.
                stream = ssl;
                await ssl.AuthenticateAsClientAsync(settings.Host, null, SslProtocols.None, true).ConfigureAwait(false);
            }
            ct.ThrowIfCancellationRequested();
            using (var body = new MemoryStream())
            {
                WriteString(body, "MQTT");
                body.WriteByte(4); body.WriteByte(0xC2); body.WriteByte(0); body.WriteByte(60);
                WriteString(body, settings.ClientId); WriteString(body, settings.Username); WriteString(body, password);
                var packet = Wrap(0x10, body.ToArray());
                try { await SendAsync(packet, ct).ConfigureAwait(false); }
                finally { Array.Clear(packet, 0, packet.Length); }
            }
            var response = await ReadAsync(ct).ConfigureAwait(false);
            if (response.Header != 0x20 || response.Body.Length != 2 || response.Body[1] != 0)
                throw new IOException("MQTT 登入遭拒（CONNACK " + (response.Body.Length == 2 ? response.Body[1].ToString() : "格式錯誤") + "）。");
            using (var body = new MemoryStream())
            {
                body.WriteByte(0); body.WriteByte(1);
                WriteString(body, settings.InboundTopic); body.WriteByte(0);
                await SendAsync(Wrap(0x82, body.ToArray()), ct).ConfigureAwait(false);
            }
            response = await ReadAsync(ct).ConfigureAwait(false);
            if (response.Header != 0x90 || response.Body.Length != 3 || response.Body[0] != 0 || response.Body[1] != 1 || response.Body[2] != 0)
                throw new IOException("MQTT 訂閱遭拒，請確認飛控 → MP Topic 的讀取權限。");
        }

        internal Task PublishAsync(string topic, byte[] data, CancellationToken ct)
        {
            using (var body = new MemoryStream())
            {
                WriteString(body, topic); body.Write(data, 0, data.Length);
                return SendAsync(Wrap(0x30, body.ToArray()), ct);
            }
        }

        internal async Task ReceiveAsync(string expectedTopic, Func<byte[], Task> received, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var packet = await ReadAsync(ct).ConfigureAwait(false);
                if (packet.Header == 0xD0 && packet.Body.Length == 0)
                {
                    Interlocked.Exchange(ref pingPending, 0);
                    continue;
                }
                if ((packet.Header >> 4) != 3) continue;
                if (packet.Body.Length < 2 || ((packet.Header >> 1) & 3) != 0)
                    throw new IOException("無效的 MQTT QoS 0 資料封包。");
                int length = (packet.Body[0] << 8) | packet.Body[1];
                if (2 + length > packet.Body.Length) throw new IOException("MQTT Topic 長度錯誤。");
                if (Encoding.UTF8.GetString(packet.Body, 2, length) != expectedTopic) continue;
                // Never feed historical retained MAVLink into a live flight connection.
                if ((packet.Header & 1) != 0) continue;
                var payload = new byte[packet.Body.Length - 2 - length];
                Buffer.BlockCopy(packet.Body, 2 + length, payload, 0, payload.Length);
                await received(payload).ConfigureAwait(false);
            }
        }

        internal async Task PingAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(20), ct).ConfigureAwait(false);
                if (Volatile.Read(ref pingPending) != 0)
                {
                    if (DateTime.UtcNow - pingSent >= TimeSpan.FromSeconds(40)) throw new IOException("MQTT 心跳回應逾時。");
                    continue;
                }
                pingSent = DateTime.UtcNow;
                Interlocked.Exchange(ref pingPending, 1);
                await SendAsync(new byte[] { 0xC0, 0 }, ct).ConfigureAwait(false);
            }
        }

        private async Task SendAsync(byte[] packet, CancellationToken ct)
        {
            // NetworkStream cancellation is not sufficient to interrupt every
            // .NET Framework socket write. Close this session on timeout too.
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            using (timeout.Token.Register(Dispose))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                await writeLock.WaitAsync(timeout.Token).ConfigureAwait(false);
                try
                {
                    timeout.Token.ThrowIfCancellationRequested();
                    if (Volatile.Read(ref disposed) != 0) throw new ObjectDisposedException(nameof(FmtMqttClient));
                    await stream.WriteAsync(packet, 0, packet.Length, timeout.Token).ConfigureAwait(false);
                }
                finally { writeLock.Release(); }
            }
        }

        private sealed class Packet
        {
            internal int Header;
            internal byte[] Body;
        }

        private async Task<Packet> ReadAsync(CancellationToken ct)
        {
            int header = await ReadByteAsync(ct).ConfigureAwait(false);
            int remaining = 0, multiplier = 1;
            for (int i = 0; ; i++)
            {
                int digit = await ReadByteAsync(ct).ConfigureAwait(false);
                remaining += (digit & 127) * multiplier;
                if (remaining > MaximumPacketLength || (i == 3 && (digit & 128) != 0))
                    throw new IOException("MQTT 封包超過安全長度限制。");
                if ((digit & 128) == 0) break;
                multiplier *= 128;
            }
            var body = new byte[remaining];
            for (int offset = 0; offset < remaining;)
            {
                int count = await stream.ReadAsync(body, offset, remaining - offset, ct).ConfigureAwait(false);
                if (count == 0) throw new EndOfStreamException("MQTT 連線已關閉。");
                offset += count;
            }
            return new Packet { Header = header, Body = body };
        }

        private async Task<int> ReadByteAsync(CancellationToken ct)
        {
            var data = new byte[1];
            if (await stream.ReadAsync(data, 0, 1, ct).ConfigureAwait(false) == 0) throw new EndOfStreamException("MQTT 連線已關閉。");
            return data[0];
        }

        private static void WriteString(Stream output, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > 65535) throw new InvalidDataException("MQTT 字串過長。");
            output.WriteByte((byte)(bytes.Length >> 8)); output.WriteByte((byte)bytes.Length);
            output.Write(bytes, 0, bytes.Length);
        }

        private static byte[] Wrap(byte header, byte[] body)
        {
            using (var packet = new MemoryStream())
            {
                packet.WriteByte(header);
                int length = body.Length;
                do
                {
                    int digit = length % 128; length /= 128;
                    packet.WriteByte((byte)(digit | (length > 0 ? 128 : 0)));
                } while (length > 0);
                packet.Write(body, 0, body.Length);
                return packet.ToArray();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            tcp.Close(); // Also interrupts pending .NET Framework connect/read/TLS operations.
            try { stream?.Dispose(); } catch (IOException) { }
        }
    }
}
