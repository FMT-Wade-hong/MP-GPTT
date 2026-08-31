using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MissionPlanner.FMT
{
    internal sealed class FmtMqttBridge : IDisposable
    {
        private readonly object gate = new object();
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private TcpListener listener;
        private TcpClient mp;
        private FmtMqttClient mqtt;
        private Task worker;
        private bool started;
        private long receivedBytes, sentBytes;
        private string mqttStatus = "MQTT 已停止";
        private string tcpStatus = "MP 未連線";
        public event Action<string> Log;
        internal bool IsRunning => started && !stop.IsCancellationRequested && worker?.IsCompleted != true;
        internal string MqttStatus { get { lock (gate) return mqttStatus; } }
        internal string TcpStatus { get { lock (gate) return tcpStatus; } }
        internal long ReceivedBytes => Interlocked.Read(ref receivedBytes);
        internal long SentBytes => Interlocked.Read(ref sentBytes);

        internal void Start(FmtMqttSettings settings, string password)
        {
            settings.Validate();
            FmtMqttSettings.ValidateString(password, "MQTT 密碼");
            if (started || stop.IsCancellationRequested) throw new InvalidOperationException("橋接器已啟動或停止。");
            listener = new TcpListener(IPAddress.Parse(settings.TcpAddress), settings.TcpPort);
            listener.Start(); // Fail before attempting MQTT if the local port is occupied.
            started = true;
            lock (gate) tcpStatus = "等待 MP TCP 連線";
            worker = Task.Run(() => RunAsync(settings, password));
        }

        private async Task RunAsync(FmtMqttSettings settings, string password)
        {
            var mqttTask = RunMqttAsync(settings, password);
            var tcpTask = RunTcpAsync(settings);
            try { await Task.WhenAll(mqttTask, tcpTask).ConfigureAwait(false); }
            finally
            {
                lock (gate) { mqttStatus = "MQTT 已停止"; tcpStatus = "MP 未連線"; }
            }
        }

        private async Task RunMqttAsync(FmtMqttSettings settings, string password)
        {
            int retrySeconds = 1;
            while (!stop.IsCancellationRequested)
            {
                using (var connection = new FmtMqttClient())
                using (var session = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
                using (session.Token.Register(connection.Dispose))
                {
                    Task receive = null, ping = null;
                    try
                    {
                        SetMqttStatus("MQTT 連線中…");
                        session.CancelAfter(TimeSpan.FromSeconds(20));
                        await connection.ConnectAsync(settings, password, session.Token).ConfigureAwait(false);
                        session.CancelAfter(Timeout.Infinite);
                        stop.Token.ThrowIfCancellationRequested();
                        lock (gate) mqtt = connection;
                        retrySeconds = 1;
                        SetMqttStatus("MQTT 已連線／已訂閱");
                        receive = connection.ReceiveAsync(settings.InboundTopic,
                            data => ForwardToMpAsync(data, session.Token), session.Token);
                        ping = connection.PingAsync(session.Token);
                        await await Task.WhenAny(receive, ping).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!stop.IsCancellationRequested)
                    {
                        // Do not echo credentials even if a lower layer includes them in an error.
                        var message = ex is OperationCanceledException || session.IsCancellationRequested
                            ? "連線或 TLS 握手逾時" : Describe(ex);
                        if (!string.IsNullOrEmpty(password)) message = message.Replace(password, "[已隱藏]");
                        SetMqttStatus("MQTT 未連線（重試中）");
                        Log?.Invoke(message + "；" + retrySeconds + " 秒後重試。");
                    }
                    catch (Exception) when (stop.IsCancellationRequested) { }
                    finally
                    {
                        lock (gate) { if (ReferenceEquals(mqtt, connection)) mqtt = null; }
                        session.Cancel();
                        await ObserveAsync(receive).ConfigureAwait(false);
                        await ObserveAsync(ping).ConfigureAwait(false);
                    }
                }
                try { await Task.Delay(TimeSpan.FromSeconds(retrySeconds), stop.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                retrySeconds = Math.Min(retrySeconds * 2, 15);
            }
        }

        private async Task RunTcpAsync(FmtMqttSettings settings)
        {
            while (!stop.IsCancellationRequested)
            {
                TcpClient client = null;
                try
                {
                    client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    client.NoDelay = true;
                    lock (gate)
                    {
                        stop.Token.ThrowIfCancellationRequested();
                        mp = client;
                        tcpStatus = "MP TCP 已連線";
                    }
                    Log?.Invoke("MP TCP 已連線。");
                    var input = client.GetStream();
                    var buffer = new byte[8192];
                    bool warned = false;
                    while (!stop.IsCancellationRequested)
                    {
                        int count = await input.ReadAsync(buffer, 0, buffer.Length, stop.Token).ConfigureAwait(false);
                        if (count == 0) break;
                        FmtMqttClient connection;
                        lock (gate) connection = mqtt;
                        if (connection == null)
                        {
                            if (!warned) Log?.Invoke("MQTT 尚未連線，MP 資料未送出；不暫存飛行指令。");
                            warned = true;
                            continue;
                        }
                        warned = false;
                        var data = new byte[count];
                        Buffer.BlockCopy(buffer, 0, data, 0, count);
                        try
                        {
                            await connection.PublishAsync(settings.OutboundTopic, data, stop.Token).ConfigureAwait(false);
                            Interlocked.Add(ref sentBytes, count);
                        }
                        catch (Exception) when (!stop.IsCancellationRequested)
                        {
                            connection.Dispose();
                            Log?.Invoke("MQTT 傳送失敗；該筆 MP 資料未送出，不重送舊指令。");
                        }
                    }
                }
                catch (Exception) when (stop.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    Log?.Invoke("MP TCP 中斷：" + ex.GetType().Name);
                    try { await Task.Delay(1000, stop.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
                finally
                {
                    lock (gate)
                    {
                        if (ReferenceEquals(mp, client)) mp = null;
                        tcpStatus = stop.IsCancellationRequested ? "MP 未連線" : "等待 MP TCP 連線";
                    }
                    client?.Close();
                }
            }
        }

        private async Task ForwardToMpAsync(byte[] data, CancellationToken ct)
        {
            Interlocked.Add(ref receivedBytes, data.Length);
            TcpClient client;
            lock (gate) client = mp;
            if (client == null) return;
            // Bound writes: a stalled MP must not pin the MQTT receive loop indefinitely.
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            using (timeout.Token.Register(client.Close))
            {
                timeout.CancelAfter(5000);
                try { await client.GetStream().WriteAsync(data, 0, data.Length, timeout.Token).ConfigureAwait(false); }
                catch (Exception) { client.Close(); }
            }
        }

        internal Task StopAsync()
        {
            Dispose();
            return worker ?? Task.CompletedTask;
        }

        public void Dispose()
        {
            stop.Cancel();
            listener?.Stop();
            lock (gate)
            {
                mp?.Close();
                mqtt?.Dispose();
                mqttStatus = "MQTT 已停止";
                tcpStatus = "MP 未連線";
            }
        }

        private void SetMqttStatus(string message)
        {
            lock (gate) mqttStatus = message;
            Log?.Invoke(message);
        }

        private static async Task ObserveAsync(Task task)
        {
            if (task == null) return;
            try { await task.ConfigureAwait(false); } catch (Exception) { }
        }

        private static string Describe(Exception exception)
        {
            var message = exception.Message;
            if (exception.InnerException != null) message += " → " + exception.InnerException.Message;
            return message;
        }
    }
}
