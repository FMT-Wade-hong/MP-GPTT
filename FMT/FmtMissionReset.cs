using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner.Comms;

namespace MissionPlanner.FMT
{
    // An operator-confirmed rewind of the mission stored on the selected ArduPilot vehicle.
    internal sealed class FmtMissionReset
    {
        private static int active;
        internal static bool IsActive => Volatile.Read(ref active) != 0;
        private readonly MAVLinkInterface port;
        private readonly MAVState mav;
        private readonly ICommsSerial stream;
        private readonly KeyValuePair<int, MAVLink.mavlink_mission_item_int_t>[] mission;

        internal FmtMissionReset(MAVLinkInterface port)
        {
            this.port = port;
            mav = port?.MAV;
            stream = port?.BaseStream;
            mission = Snapshot(mav);
        }

        private static KeyValuePair<int, MAVLink.mavlink_mission_item_int_t>[] Snapshot(MAVState vehicle)
            => vehicle?.wps?.OrderBy(pair => pair.Key).ToArray() ??
               new KeyValuePair<int, MAVLink.mavlink_mission_item_int_t>[0];

        internal static string UnavailableReason(MAVLinkInterface link)
        {
            if (link?.BaseStream == null || !link.BaseStream.IsOpen || link.MAV == null || link.MAV.sysid == 0)
                return "尚未連線飛控。";
            if (link.ReadOnly || link.logreadmode)
                return "唯讀或日誌回放時無法重置任務。";
            if (!FmtRelayControlService.CanLocalStationTransmitControl)
                return "目前工作站沒有飛控控制權。";
            if (link.MAV.apname != MAVLink.MAV_AUTOPILOT.ARDUPILOTMEGA)
                return "此重置功能適用於 ArduPilot 飛控。";
            var heartbeat = link.MAV.getPacketLast((uint)MAVLink.MAVLINK_MSG_ID.HEARTBEAT);
            var age = heartbeat == null ? double.MaxValue : (DateTime.UtcNow - heartbeat.rxtime).TotalSeconds;
            if (age < 0 || age > 5)
                return "等待飛控回報最新的連線狀態。";
            if (link.giveComport || link.IsParameterListLoading || link.IsLogDownloadActive || FmtMissionClear.IsActive)
                return "請等待目前的通訊或下載作業結束。";
            if (link.MAV.wps == null || !link.MAV.wps.ContainsKey(1))
                return "尚未從飛控載入可重置的任務。";
            return null;
        }

        internal string Validate()
        {
            if (!ReferenceEquals(MainV2.comPort, port) || !ReferenceEquals(port?.MAV, mav) ||
                !ReferenceEquals(port?.BaseStream, stream))
                return "連線或飛控已變更，請重新確認任務。";
            var reason = UnavailableReason(port);
            if (reason != null) return reason;
            return mission.SequenceEqual(Snapshot(mav)) ? null : "任務內容已變更，請重新確認任務。";
        }

        internal async Task<MAVLink.MAV_RESULT> ExecuteAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reason = Validate();
            if (reason != null) throw new InvalidOperationException(reason);
            if (Interlocked.CompareExchange(ref active, 1, 0) != 0)
                throw new InvalidOperationException("正在等待飛控確認任務重置。");
            int subscription = 0;
            bool subscribed = false;
            try
            {
                var reply = new TaskCompletionSource<MAVLink.MAV_RESULT>(TaskCreationOptions.RunContinuationsAsynchronously);
                subscription = port.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.COMMAND_ACK, message =>
                {
                    var ack = message.ToStructure<MAVLink.mavlink_command_ack_t>();
                    if (ack.command == (ushort)MAVLink.MAV_CMD.DO_SET_MISSION_CURRENT &&
                        (ack.target_system == 0 || ack.target_system == MAVLinkInterface.gcssysid) &&
                        (ack.target_component == 0 || ack.target_component == (byte)MAVLink.MAV_COMPONENT.MAV_COMP_ID_MISSIONPLANNER) &&
                        ack.result != (byte)MAVLink.MAV_RESULT.IN_PROGRESS)
                        reply.TrySetResult((MAVLink.MAV_RESULT)ack.result);
                    return true;
                }, mav.sysid, mav.compid);
                subscribed = true;
                reason = Validate();
                if (reason != null) throw new InvalidOperationException(reason);
                cancellationToken.ThrowIfCancellationRequested();

                // In ArduPilot 4.6/4.7, sequence 0 restarts from the first mission command
                // and resets DO_JUMP counters. Keep param2=0: param2=1 also calls resume().
                // A running mission can immediately execute its first commands even with param2=0.
                // Use the acknowledged command, without changing mode, arming or uploading a mission.
                port.generatePacket(MAVLink.MAVLINK_MSG_ID.COMMAND_LONG, new MAVLink.mavlink_command_long_t
                {
                    target_system = mav.sysid, target_component = mav.compid,
                    command = (ushort)MAVLink.MAV_CMD.DO_SET_MISSION_CURRENT,
                    param1 = 0, param2 = 0
                }, mav.sysid, mav.compid);

                var timer = Stopwatch.StartNew();
                while (!reply.Task.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    reason = Validate();
                    if (reason != null) throw new InvalidOperationException(reason + " 重置命令可能已送達，請重新讀取任務進度確認。");
                    if (timer.ElapsedMilliseconds >= 5000)
                        throw new TimeoutException("等待重置回覆逾時；命令可能已執行，請查看任務進度後再操作。");
                    await Task.WhenAny(reply.Task, Task.Delay(100, cancellationToken)).ConfigureAwait(false);
                }
                cancellationToken.ThrowIfCancellationRequested();
                reason = Validate();
                if (reason != null) throw new InvalidOperationException(reason + " 請重新讀取任務進度確認重置結果。");
                return await reply.Task.ConfigureAwait(false);
            }
            finally
            {
                if (subscribed) port.UnSubscribeToPacketType(subscription);
                Interlocked.Exchange(ref active, 0);
            }
        }
    }
}
