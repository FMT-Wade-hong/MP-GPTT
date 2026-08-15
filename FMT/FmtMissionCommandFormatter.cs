using MissionPlanner.Utilities;
using System;
using System.Globalization;

namespace MissionPlanner.FMT
{
    internal sealed class FmtMissionItem
    {
        internal int Sequence { get; set; }
        internal MAVLink.MAV_CMD Command { get; set; }
        internal float Param1 { get; set; }
        internal float Param2 { get; set; }
        internal float Param3 { get; set; }
        internal float Param4 { get; set; }
        internal double Latitude { get; set; }
        internal double Longitude { get; set; }
        internal float Altitude { get; set; }
        internal byte Frame { get; set; }

        // Reserved for a future operator-defined mission item name. The MAVLink mission
        // protocol does not currently provide this field, so it remains empty today.
        internal string Name { get; set; }

        internal ushort CommandId
        {
            get { return (ushort) Command; }
        }

        internal bool IsHighRisk
        {
            get { return FmtMissionCommandFormatter.IsHighRisk(Command); }
        }

        internal bool TryGetLocation(out PointLatLngAlt location)
        {
            location = PointLatLngAlt.Zero;
            if (!Locationwp.isLocationCommand(CommandId) || Command == MAVLink.MAV_CMD.RETURN_TO_LAUNCH)
                return false;

            if (double.IsNaN(Latitude) || double.IsNaN(Longitude) ||
                Latitude < -90 || Latitude > 90 || Longitude < -180 || Longitude > 180 ||
                (Math.Abs(Latitude) < 0.0000001 && Math.Abs(Longitude) < 0.0000001))
                return false;

            location = new PointLatLngAlt(Latitude, Longitude, Altitude, Sequence.ToString(CultureInfo.InvariantCulture));
            return true;
        }
    }

    internal static class FmtMissionCommandFormatter
    {
        internal static FmtMissionItem FromMissionItem(int sequence, MAVLink.mavlink_mission_item_int_t item)
        {
            var location = (Locationwp) item;
            return new FmtMissionItem
            {
                Sequence = sequence,
                Command = (MAVLink.MAV_CMD) item.command,
                Param1 = item.param1,
                Param2 = item.param2,
                Param3 = item.param3,
                Param4 = item.param4,
                Latitude = location.lat,
                Longitude = location.lng,
                Altitude = location.alt,
                Frame = item.frame
            };
        }

        internal static string Describe(FmtMissionItem item)
        {
            if (item == null)
                return "任務狀態未知";

            string text;
            switch (item.Command)
            {
                case MAVLink.MAV_CMD.TAKEOFF:
                    text = "起飛";
                    break;
                case MAVLink.MAV_CMD.VTOL_TAKEOFF:
                    text = "VTOL 垂直起飛";
                    break;
                case MAVLink.MAV_CMD.WAYPOINT:
                    text = "前往 WP" + item.Sequence;
                    break;
                case MAVLink.MAV_CMD.SPLINE_WAYPOINT:
                    text = "前往 WP" + item.Sequence + "（曲線航線）";
                    break;
                case MAVLink.MAV_CMD.LOITER_UNLIM:
                    text = "定點盤旋";
                    break;
                case MAVLink.MAV_CMD.LOITER_TIME:
                    text = item.Param1 > 0
                        ? "定點盤旋 " + FormatNumber(item.Param1) + " 秒"
                        : "定點盤旋";
                    break;
                case MAVLink.MAV_CMD.LOITER_TURNS:
                    text = item.Param1 > 0
                        ? "定點盤旋 " + FormatNumber(item.Param1) + " 圈"
                        : "定點盤旋";
                    break;
                case MAVLink.MAV_CMD.RETURN_TO_LAUNCH:
                    text = "返航";
                    break;
                case MAVLink.MAV_CMD.LAND:
                    text = "降落";
                    break;
                case MAVLink.MAV_CMD.VTOL_LAND:
                    text = "VTOL 垂直降落";
                    break;
                case MAVLink.MAV_CMD.DELAY:
                case MAVLink.MAV_CMD.CONDITION_DELAY:
                    text = item.Param1 > 0
                        ? "等待 " + FormatNumber(item.Param1) + " 秒"
                        : "等待";
                    break;
                case MAVLink.MAV_CMD.DO_SET_SERVO:
                    text = "執行 Servo 動作";
                    if (item.Param1 > 0)
                        text += "（CH" + FormatNumber(item.Param1) + " → " + FormatNumber(item.Param2) + "）";
                    break;
                case MAVLink.MAV_CMD.DO_DIGICAM_CONTROL:
                    text = "執行拍照";
                    break;
                case MAVLink.MAV_CMD.IMAGE_START_CAPTURE:
                    text = "開始拍照";
                    break;
                case MAVLink.MAV_CMD.IMAGE_STOP_CAPTURE:
                    text = "停止拍照";
                    break;
                case MAVLink.MAV_CMD.DO_VTOL_TRANSITION:
                    text = DescribeVtolTransition(item.Param1);
                    break;
                case MAVLink.MAV_CMD.DO_SET_ROI:
                case MAVLink.MAV_CMD.DO_SET_ROI_LOCATION:
                    text = "設定相機目標";
                    break;
                case MAVLink.MAV_CMD.DO_MOUNT_CONTROL:
                    text = "調整雲台";
                    break;
                case MAVLink.MAV_CMD.CONDITION_DISTANCE:
                    text = "等待接近下一任務點";
                    break;
                case MAVLink.MAV_CMD.CONDITION_YAW:
                    text = "調整航向";
                    break;
                default:
                    text = "任務指令 CMD " + item.CommandId;
                    break;
            }

            if (!string.IsNullOrWhiteSpace(item.Name))
                text += "｜" + item.Name.Trim();

            return text;
        }

        internal static bool IsHighRisk(MAVLink.MAV_CMD command)
        {
            return command == MAVLink.MAV_CMD.LAND ||
                   command == MAVLink.MAV_CMD.VTOL_LAND ||
                   command == MAVLink.MAV_CMD.RETURN_TO_LAUNCH ||
                   command == MAVLink.MAV_CMD.TAKEOFF ||
                   command == MAVLink.MAV_CMD.VTOL_TAKEOFF;
        }

        internal static string HighRiskWarning(FmtMissionItem item)
        {
            if (item == null)
                return "⚠ 即將變更飛行任務";

            switch (item.Command)
            {
                case MAVLink.MAV_CMD.LAND:
                    return "⚠ 即將執行降落任務";
                case MAVLink.MAV_CMD.VTOL_LAND:
                    return "⚠ 即將執行 VTOL 垂直降落任務";
                case MAVLink.MAV_CMD.RETURN_TO_LAUNCH:
                    return "⚠ 即將執行返航任務";
                case MAVLink.MAV_CMD.TAKEOFF:
                    return "⚠ 即將執行起飛任務";
                case MAVLink.MAV_CMD.VTOL_TAKEOFF:
                    return "⚠ 即將執行 VTOL 垂直起飛任務";
                default:
                    return "⚠ 即將變更飛行任務";
            }
        }

        private static string DescribeVtolTransition(float state)
        {
            var rounded = (int) Math.Round(state);
            if (rounded == (int) MAVLink.MAV_VTOL_STATE.FW ||
                rounded == (int) MAVLink.MAV_VTOL_STATE.TRANSITION_TO_FW)
                return "切換固定翼";
            if (rounded == (int) MAVLink.MAV_VTOL_STATE.MC ||
                rounded == (int) MAVLink.MAV_VTOL_STATE.TRANSITION_TO_MC)
                return "切換 VTOL";
            return "切換飛行構型";
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
