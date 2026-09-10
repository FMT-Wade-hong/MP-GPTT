using MissionPlanner.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MissionPlanner.FMT
{
    internal sealed class FmtRelayPeerState
    {
        internal int StationNumber { get; set; }
        internal bool FlightLinkConnected { get; set; }
        internal bool JoystickReady { get; set; }
        internal DateTime LastSeenUtc { get; set; }
        internal string RemoteAddress { get; set; }
        internal int LatencyMilliseconds { get; set; }

        internal bool IsOnline
        {
            get { return DateTime.UtcNow - LastSeenUtc <= TimeSpan.FromSeconds(4); }
        }
    }

    /// <summary>
    /// Lightweight station-coordination side channel. MAVLink remains on the
    /// configured forwarding links; this channel carries only station presence,
    /// GCS position, takeover requests and the station-1 control decision.
    /// A station-specific UDP port also permits several stations on one computer.
    /// </summary>
    internal static class FmtRelayControlService
    {
        private const string Protocol = "FMT_RELAY_CONTROL_V1";
        private const int BasePort = 14650;
        private static readonly TimeSpan PeerTimeout = TimeSpan.FromSeconds(4);
        private static readonly object Sync = new object();
        private static readonly Dictionary<int, FmtRelayPeerState> Peers =
            new Dictionary<int, FmtRelayPeerState>();
        private static readonly Dictionary<int, IPEndPoint> LearnedEndpoints =
            new Dictionary<int, IPEndPoint>();
        private static readonly string InstanceId = Guid.NewGuid().ToString("N");
        private static UdpClient client;
        private static Timer heartbeatTimer;
        private static int listenerGeneration;
        private static int listenerStation;
        private static bool initialized;
        private static bool localFlightLinkConnected;
        private static bool localJoystickReady;
        private static int activeStation;
        private static int pendingRequestStation;
        private static DateTime localRequestSentUtc = DateTime.MinValue;
        private static DateTime lastMainAuthorityUtc = DateTime.MinValue;
        private static string trustedMainInstance;
        private static string lastError;

        internal static int CoordinationPort(int stationNumber)
        {
            return BasePort + Math.Max(1, Math.Min(5, stationNumber));
        }

        internal static string LastError
        {
            get { EnsureStarted(); lock (Sync) return lastError; }
        }

        internal static int ActiveStation
        {
            get
            {
                EnsureStarted();
                lock (Sync)
                {
                    if (listenerStation != 1 && DateTime.UtcNow - lastMainAuthorityUtc > PeerTimeout)
                        return 0;
                    return activeStation;
                }
            }
        }

        internal static int PendingRequestStation
        {
            get
            {
                EnsureStarted();
                lock (Sync)
                {
                    if (listenerStation != 1)
                        return 0;
                    FmtRelayPeerState peer;
                    if (pendingRequestStation > 1 &&
                        (!Peers.TryGetValue(pendingRequestStation, out peer) || !peer.IsOnline))
                        pendingRequestStation = 0;
                    return pendingRequestStation;
                }
            }
        }

        internal static bool LocalRequestPending
        {
            get
            {
                EnsureStarted();
                lock (Sync)
                    return listenerStation != 1 && activeStation != listenerStation &&
                           DateTime.UtcNow - localRequestSentUtc <= TimeSpan.FromSeconds(10);
            }
        }

        internal static bool HasFreshMainAuthority
        {
            get
            {
                EnsureStarted();
                lock (Sync)
                    return listenerStation == 1 || DateTime.UtcNow - lastMainAuthorityUtc <= PeerTimeout;
            }
        }

        internal static bool CanLocalStationTransmitControl
        {
            get
            {
                EnsureStarted();
                lock (Sync)
                {
                    if (activeStation != listenerStation)
                        return false;
                    return listenerStation == 1 || DateTime.UtcNow - lastMainAuthorityUtc <= PeerTimeout;
                }
            }
        }

        internal static void UpdateLocalRuntime(bool flightLinkConnected, bool joystickReady)
        {
            EnsureStarted();
            lock (Sync)
            {
                localFlightLinkConnected = flightLinkConnected;
                localJoystickReady = joystickReady;
                if (listenerStation != 1 && DateTime.UtcNow - lastMainAuthorityUtc > PeerTimeout &&
                    activeStation != 0)
                {
                    activeStation = 0;
                    FmtGroundStationPositionStore.SetActiveController(0);
                }
            }
        }

        internal static FmtRelayPeerState GetPeer(int stationNumber)
        {
            EnsureStarted();
            lock (Sync)
            {
                FmtRelayPeerState peer;
                if (!Peers.TryGetValue(stationNumber, out peer))
                    return null;
                return Clone(peer);
            }
        }

        internal static bool TrySetActiveStation(int stationNumber, out string error)
        {
            EnsureStarted();
            error = null;
            lock (Sync)
            {
                if (listenerStation != 1)
                {
                    error = "只有 1 號主站可以授權、撤銷或停止接力控制。";
                    return false;
                }
                if (stationNumber < 0 || stationNumber > 5)
                {
                    error = "控制站號必須介於 1 到 5，或設為未指派。";
                    return false;
                }
                if (stationNumber == 1 && (!localFlightLinkConnected || !localJoystickReady))
                {
                    error = "1 號主站尚未通過飛控連線與搖桿檢查。";
                    return false;
                }
                if (stationNumber > 1)
                {
                    FmtRelayPeerState peer;
                    if (!Peers.TryGetValue(stationNumber, out peer) || !peer.IsOnline ||
                        !peer.FlightLinkConnected || !peer.JoystickReady)
                    {
                        error = stationNumber + " 號站尚未通過連線與搖桿檢查。";
                        return false;
                    }
                }

                activeStation = stationNumber;
                pendingRequestStation = 0;
                lastMainAuthorityUtc = DateTime.UtcNow;
                FmtGroundStationPositionStore.SetActiveController(stationNumber);
            }

            SendPacket("authority", 0);
            return true;
        }

        internal static bool RequestLocalControl(out string error)
        {
            EnsureStarted();
            error = null;
            lock (Sync)
            {
                if (listenerStation == 1)
                {
                    error = "1 號站不需申請控制權。";
                    return false;
                }
                if (!localFlightLinkConnected || !localJoystickReady)
                {
                    error = listenerStation + " 號站尚未通過飛控連線與搖桿檢查。";
                    return false;
                }
                if (DateTime.UtcNow - lastMainAuthorityUtc > PeerTimeout)
                {
                    error = "尚未連上 1 號主站，無法送出接管申請。";
                    return false;
                }
            }

            SendPacket("request", FmtRelayStationIdentity.StationNumber);
            lock (Sync)
                localRequestSentUtc = DateTime.UtcNow;
            return true;
        }

        internal static bool CanAcceptMavlinkWriteFrom(int stationNumber)
        {
            EnsureStarted();
            lock (Sync)
            {
                if (listenerStation != 1 || stationNumber < 2 || stationNumber > 5 ||
                    activeStation != stationNumber)
                    return false;
                FmtRelayPeerState peer;
                return Peers.TryGetValue(stationNumber, out peer) && peer.IsOnline &&
                       peer.FlightLinkConnected && peer.JoystickReady;
            }
        }

        internal static bool TryResolveStationByAddress(string address, out int stationNumber)
        {
            EnsureStarted();
            stationNumber = 0;
            if (string.IsNullOrWhiteSpace(address))
                return false;

            var matches = new HashSet<int>();
            lock (Sync)
            {
                foreach (var pair in Peers)
                    if (pair.Value.IsOnline && string.Equals(pair.Value.RemoteAddress, address,
                            StringComparison.OrdinalIgnoreCase))
                        matches.Add(pair.Key);
            }
            for (var number = 2; number <= 5; number++)
            {
                var configured = ReadStationAddress(number);
                if (string.Equals(configured, address, StringComparison.OrdinalIgnoreCase))
                    matches.Add(number);
            }
            if (matches.Count != 1)
                return false;
            stationNumber = matches.First();
            return true;
        }

        private static void EnsureStarted()
        {
            lock (Sync)
            {
                if (!initialized)
                {
                    initialized = true;
                    FmtRelayStationIdentity.StationNumberChanged += (previous, current) => Restart(current);
                    heartbeatTimer = new Timer(Heartbeat, null, Timeout.Infinite, Timeout.Infinite);
                }

                var stationNumber = FmtRelayStationIdentity.StationNumber;
                if (client == null || listenerStation != stationNumber)
                    StartListener(stationNumber);
            }
        }

        private static void Restart(int stationNumber)
        {
            lock (Sync)
                StartListener(stationNumber);
        }

        private static void StartListener(int stationNumber)
        {
            listenerGeneration++;
            try { client?.Close(); } catch { }
            client = null;
            Peers.Clear();
            LearnedEndpoints.Clear();
            trustedMainInstance = null;
            pendingRequestStation = 0;
            localRequestSentUtc = DateTime.MinValue;
            listenerStation = stationNumber;
            activeStation = stationNumber == 1 ? 1 : 0;
            lastMainAuthorityUtc = stationNumber == 1 ? DateTime.UtcNow : DateTime.MinValue;
            FmtGroundStationPositionStore.SetActiveController(activeStation);

            try
            {
                var udp = new UdpClient(AddressFamily.InterNetwork);
                udp.Client.ExclusiveAddressUse = true;
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, CoordinationPort(stationNumber)));
                client = udp;
                lastError = null;
                var generation = listenerGeneration;
                ThreadPool.QueueUserWorkItem(state => ReceiveLoop(udp, generation));
                heartbeatTimer.Change(0, 1000);
            }
            catch (Exception ex)
            {
                lastError = "接力協調埠 " + CoordinationPort(stationNumber) + " 無法啟用：" + ex.Message;
                heartbeatTimer.Change(1000, 1000);
            }
        }

        private static void Heartbeat(object state)
        {
            try
            {
                EnsureStarted();
                SendPacket("status", 0);
                if (FmtRelayStationIdentity.StationNumber == 1)
                    SendPacket("authority", 0);
                else if (!HasFreshMainAuthority)
                    FmtGroundStationPositionStore.SetActiveController(0);
            }
            catch
            {
                // A later heartbeat retries transient network and settings failures.
            }
        }

        private static void ReceiveLoop(UdpClient receiver, int generation)
        {
            while (true)
            {
                try
                {
                    var endpoint = new IPEndPoint(IPAddress.Any, 0);
                    var bytes = receiver.Receive(ref endpoint);
                    lock (Sync)
                        if (receiver != client || generation != listenerGeneration)
                            return;
                    ProcessPacket(bytes, endpoint);
                }
                catch (ObjectDisposedException) { return; }
                catch (SocketException)
                {
                    lock (Sync)
                        if (receiver != client || generation != listenerGeneration)
                            return;
                }
                catch
                {
                    // Ignore malformed/untrusted datagrams and continue listening.
                }
            }
        }

        private static void ProcessPacket(byte[] bytes, IPEndPoint endpoint)
        {
            RelayPacket packet;
            try
            {
                packet = JsonConvert.DeserializeObject<RelayPacket>(Encoding.UTF8.GetString(bytes));
            }
            catch { return; }
            if (packet == null || packet.Protocol != Protocol || packet.InstanceId == InstanceId ||
                packet.StationNumber < 1 || packet.StationNumber > 5)
                return;

            var now = DateTime.UtcNow;
            var latency = 0;
            try
            {
                latency = (int)Math.Min(9999, Math.Abs((now - new DateTime(packet.SentUtcTicks,
                    DateTimeKind.Utc)).TotalMilliseconds));
            }
            catch { }

            lock (Sync)
            {
                Peers[packet.StationNumber] = new FmtRelayPeerState
                {
                    StationNumber = packet.StationNumber,
                    FlightLinkConnected = packet.FlightLinkConnected,
                    JoystickReady = packet.JoystickReady,
                    LastSeenUtc = now,
                    RemoteAddress = endpoint.Address.ToString(),
                    LatencyMilliseconds = latency
                };
                LearnedEndpoints[packet.StationNumber] = endpoint;

                if (packet.Kind == "authority" && packet.StationNumber == 1 && listenerStation != 1)
                {
                    if (trustedMainInstance == null || trustedMainInstance == packet.InstanceId ||
                        now - lastMainAuthorityUtc > PeerTimeout)
                    {
                        trustedMainInstance = packet.InstanceId;
                        activeStation = packet.ActiveStation >= 0 && packet.ActiveStation <= 5
                            ? packet.ActiveStation : 0;
                        if (activeStation == listenerStation)
                            localRequestSentUtc = DateTime.MinValue;
                        lastMainAuthorityUtc = now;
                        FmtGroundStationPositionStore.SetActiveController(activeStation);
                    }
                }
                else if (packet.Kind == "request" && listenerStation == 1 &&
                         packet.StationNumber > 1 && packet.RequestStation == packet.StationNumber)
                {
                    pendingRequestStation = packet.StationNumber;
                }
            }

            if (packet.HasPosition)
                FmtGroundStationPositionStore.Update(packet.StationNumber, packet.Latitude,
                    packet.Longitude, packet.AccuracyMeters,
                    string.IsNullOrWhiteSpace(packet.PositionSource) ? "接力站定位" : packet.PositionSource,
                    now);

            if (listenerStation == 1 && packet.Kind == "status")
                SendPacket("authority", 0);
        }

        private static void SendPacket(string kind, int requestStation)
        {
            UdpClient sender;
            int stationNumber;
            lock (Sync)
            {
                sender = client;
                stationNumber = listenerStation;
            }
            if (sender == null || stationNumber < 1 || stationNumber > 5)
                return;

            FmtGroundStationPosition position;
            var hasPosition = FmtGroundStationPositionStore.TryGet(stationNumber,
                TimeSpan.FromSeconds(60), out position);
            RelayPacket packet;
            lock (Sync)
            {
                packet = new RelayPacket
                {
                    Protocol = Protocol,
                    Kind = kind,
                    InstanceId = InstanceId,
                    StationNumber = stationNumber,
                    ActiveStation = activeStation,
                    RequestStation = requestStation,
                    FlightLinkConnected = localFlightLinkConnected,
                    JoystickReady = localJoystickReady,
                    HasPosition = hasPosition,
                    Latitude = hasPosition ? position.Latitude : 0,
                    Longitude = hasPosition ? position.Longitude : 0,
                    AccuracyMeters = hasPosition ? position.AccuracyMeters : -1,
                    PositionSource = hasPosition ? position.Source : null,
                    SentUtcTicks = DateTime.UtcNow.Ticks
                };
            }
            var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet));

            var destinations = BuildDestinations(stationNumber);
            foreach (var destination in destinations)
            {
                try { sender.Send(payload, payload.Length, destination); }
                catch (Exception ex)
                {
                    lock (Sync)
                        lastError = "無法傳送接力協調封包至 " + destination + "：" + ex.Message;
                }
            }
        }

        private static List<IPEndPoint> BuildDestinations(int localStationNumber)
        {
            var results = new Dictionary<string, IPEndPoint>(StringComparer.OrdinalIgnoreCase);
            for (var stationNumber = 1; stationNumber <= 5; stationNumber++)
            {
                if (stationNumber == localStationNumber)
                    continue;
                AddDestination(results, new IPEndPoint(IPAddress.Loopback, CoordinationPort(stationNumber)));
                foreach (var address in ResolveAddresses(ReadStationAddress(stationNumber)))
                    AddDestination(results, new IPEndPoint(address, CoordinationPort(stationNumber)));
            }
            lock (Sync)
                foreach (var endpoint in LearnedEndpoints.Values)
                    AddDestination(results, endpoint);
            return results.Values.ToList();
        }

        private static void AddDestination(IDictionary<string, IPEndPoint> results, IPEndPoint endpoint)
        {
            if (endpoint == null || endpoint.AddressFamily != AddressFamily.InterNetwork)
                return;
            results[endpoint.Address + ":" + endpoint.Port.ToString(CultureInfo.InvariantCulture)] = endpoint;
        }

        private static IEnumerable<IPAddress> ResolveAddresses(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return new IPAddress[0];
            try
            {
                IPAddress parsed;
                if (IPAddress.TryParse(host, out parsed))
                    return parsed.AddressFamily == AddressFamily.InterNetwork
                        ? new[] { parsed } : new IPAddress[0];
                return Dns.GetHostAddresses(host).Where(item => item.AddressFamily == AddressFamily.InterNetwork);
            }
            catch { return new IPAddress[0]; }
        }

        private static string ReadStationAddress(int stationNumber)
        {
            try { return (Settings.Instance["FMT_RelayStation" + stationNumber + "Address"] ?? "").Trim(); }
            catch { return string.Empty; }
        }

        private static FmtRelayPeerState Clone(FmtRelayPeerState source)
        {
            return new FmtRelayPeerState
            {
                StationNumber = source.StationNumber,
                FlightLinkConnected = source.FlightLinkConnected,
                JoystickReady = source.JoystickReady,
                LastSeenUtc = source.LastSeenUtc,
                RemoteAddress = source.RemoteAddress,
                LatencyMilliseconds = source.LatencyMilliseconds
            };
        }

        private sealed class RelayPacket
        {
            public string Protocol { get; set; }
            public string Kind { get; set; }
            public string InstanceId { get; set; }
            public int StationNumber { get; set; }
            public int ActiveStation { get; set; }
            public int RequestStation { get; set; }
            public bool FlightLinkConnected { get; set; }
            public bool JoystickReady { get; set; }
            public bool HasPosition { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double AccuracyMeters { get; set; }
            public string PositionSource { get; set; }
            public long SentUtcTicks { get; set; }
        }
    }
}
