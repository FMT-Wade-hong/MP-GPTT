using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner.Comms;

namespace MissionPlanner.FMT
{
    // Clear only the flight mission, then read back its count before reporting success.
    internal sealed class FmtMissionClear
    {
        private static int active;
        internal static bool IsActive => Volatile.Read(ref active) != 0;
        private readonly MAVLinkInterface port;
        private readonly MAVState mav;
        private readonly ICommsSerial stream;

        internal FmtMissionClear(MAVLinkInterface port)
        {
            this.port = port;
            mav = port?.MAV;
            stream = port?.BaseStream;
        }

        internal string Validate()
        {
            if (!ReferenceEquals(MainV2.comPort, port) || !ReferenceEquals(port?.MAV, mav) ||
                !ReferenceEquals(port?.BaseStream, stream))
                return "連線或飛控已變更，請重新確認要清除的任務。";
            if (stream == null || !stream.IsOpen || mav == null || mav.sysid == 0)
                return "尚未連線飛控。";
            if (port.ReadOnly || port.logreadmode)
                return "唯讀或日誌回放時無法清除飛控任務。";
            if (!FmtRelayControlService.CanLocalStationTransmitControl)
                return "目前工作站沒有飛控控制權。";
            if (port.giveComport || port.IsParameterListLoading || port.IsLogDownloadActive || FmtMissionReset.IsActive)
                return "請等待目前的通訊或任務作業結束。";
            var heartbeat = mav.getPacketLast((uint)MAVLink.MAVLINK_MSG_ID.HEARTBEAT);
            var age = heartbeat == null ? double.MaxValue : (DateTime.UtcNow - heartbeat.rxtime).TotalSeconds;
            if (age < 0 || age > 5)
                return "等待飛控回報最新的連線狀態。";
            return null;
        }

        private void Check(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reason = Validate();
            if (reason != null) throw new InvalidOperationException(reason);
        }

        private static bool AddressedToUs(byte system, byte component)
            => (system == 0 || system == MAVLinkInterface.gcssysid) &&
               (component == 0 || component == (byte)MAVLink.MAV_COMPONENT.MAV_COMP_ID_MISSIONPLANNER);

        private async Task<T> WaitAsync<T>(Task<T> response, CancellationToken cancellationToken)
        {
            var timer = Stopwatch.StartNew();
            while (!response.IsCompleted)
            {
                Check(cancellationToken);
                if (timer.ElapsedMilliseconds >= 3000)
                    throw new TimeoutException("等待飛控清除／讀回回覆逾時；命令可能已執行，請重新讀取任務確認。");
                await Task.WhenAny(response, Task.Delay(100, cancellationToken)).ConfigureAwait(false);
            }
            Check(cancellationToken);
            return await response.ConfigureAwait(false);
        }

        internal async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Check(cancellationToken);
            if (Interlocked.CompareExchange(ref active, 1, 0) != 0)
                throw new InvalidOperationException("正在等待飛控確認任務清除。");
            int? ackSubscription = null, countSubscription = null;
            try
            {
                Check(cancellationToken);
                var ack = new TaskCompletionSource<MAVLink.MAV_MISSION_RESULT>(TaskCreationOptions.RunContinuationsAsynchronously);
                ackSubscription = port.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.MISSION_ACK, message =>
                {
                    var value = message.ToStructure<MAVLink.mavlink_mission_ack_t>();
                    if (value.mission_type == (byte)MAVLink.MAV_MISSION_TYPE.MISSION &&
                        AddressedToUs(value.target_system, value.target_component))
                        ack.TrySetResult((MAVLink.MAV_MISSION_RESULT)value.type);
                    return true;
                }, mav.sysid, mav.compid);
                Check(cancellationToken);
                // Never use mission_type=ALL: fences and rally points must remain intact.
                port.generatePacket(MAVLink.MAVLINK_MSG_ID.MISSION_CLEAR_ALL, new MAVLink.mavlink_mission_clear_all_t
                {
                    target_system = mav.sysid, target_component = mav.compid,
                    mission_type = (byte)MAVLink.MAV_MISSION_TYPE.MISSION
                }, mav.sysid, mav.compid);
                var result = await WaitAsync(ack.Task, cancellationToken).ConfigureAwait(false);
                if (result != MAVLink.MAV_MISSION_RESULT.MAV_MISSION_ACCEPTED)
                    throw new InvalidOperationException("飛控拒絕清除任務：" + result + " (" + (byte)result + ")。若正在執行任務，請先退出 AUTO 模式後再試。");
                port.UnSubscribeToPacketType(ackSubscription.Value);
                ackSubscription = null;

                var count = new TaskCompletionSource<ushort>(TaskCreationOptions.RunContinuationsAsynchronously);
                countSubscription = port.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.MISSION_COUNT, message =>
                {
                    var value = message.ToStructure<MAVLink.mavlink_mission_count_t>();
                    if (value.mission_type == (byte)MAVLink.MAV_MISSION_TYPE.MISSION &&
                        AddressedToUs(value.target_system, value.target_component))
                        count.TrySetResult(value.count);
                    return true;
                }, mav.sysid, mav.compid);
                Check(cancellationToken);
                port.generatePacket(MAVLink.MAVLINK_MSG_ID.MISSION_REQUEST_LIST, new MAVLink.mavlink_mission_request_list_t
                {
                    target_system = mav.sysid, target_component = mav.compid,
                    mission_type = (byte)MAVLink.MAV_MISSION_TYPE.MISSION
                }, mav.sysid, mav.compid);
                var remaining = await WaitAsync(count.Task, cancellationToken).ConfigureAwait(false);
                if (remaining != 0)
                    throw new InvalidOperationException("飛控讀回仍有 " + remaining + " 筆任務資料，尚未確認清除成功。請重新讀取任務。");
                // Complete the empty mission download, then discard the verified stale cache.
                port.generatePacket(MAVLink.MAVLINK_MSG_ID.MISSION_ACK, new MAVLink.mavlink_mission_ack_t
                {
                    target_system = mav.sysid, target_component = mav.compid,
                    mission_type = (byte)MAVLink.MAV_MISSION_TYPE.MISSION,
                    type = (byte)MAVLink.MAV_MISSION_RESULT.MAV_MISSION_ACCEPTED
                }, mav.sysid, mav.compid);
                mav.wps.Clear();
            }
            finally
            {
                if (ackSubscription.HasValue) port.UnSubscribeToPacketType(ackSubscription.Value);
                if (countSubscription.HasValue) port.UnSubscribeToPacketType(countSubscription.Value);
                Interlocked.Exchange(ref active, 0);
            }
        }
    }
}
