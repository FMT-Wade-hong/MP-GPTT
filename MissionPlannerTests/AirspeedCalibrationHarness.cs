using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MissionPlanner;
using MissionPlanner.Comms;
using MissionPlanner.GCSViews;

// Exercises the production calibration helper and MAVLink parser/transport, using only memory streams.
internal static class AirspeedCalibrationHarness
{
    private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly MethodInfo Send = typeof(FlightData).GetMethod("SendFmtAirspeedZeroAsync", HiddenStatic);
    private static readonly MethodInfo FailureText = typeof(FlightData).GetMethod("GetFmtAirspeedZeroFailureMessage", HiddenStatic);
    private static int checks;
    private static void Check(bool valid, string reason)
    {
        if (!valid) throw new Exception(reason);
        checks++;
    }
    private static Task<MAVLink.MAV_RESULT?> Execute(MAVLinkInterface link, byte sysid = 17, byte compid = 1)
        => (Task<MAVLink.MAV_RESULT?>)Send.Invoke(null, new object[] { link, sysid, compid });
    private static int Subscriptions(MAVLinkInterface link)
        => ((IEnumerable)typeof(MAVLinkInterface).GetField("Subscriptions", HiddenInstance).GetValue(link))
            .Cast<object>().Count(item => (MAVLink.MAVLINK_MSG_ID)item.GetType().GetField("Item1").GetValue(item)
                == MAVLink.MAVLINK_MSG_ID.COMMAND_ACK);

    private sealed class Vehicle : IDisposable
    {
        internal readonly MAVLinkInterface Link = new MAVLinkInterface();
        internal readonly CommsInjection Stream = new CommsInjection();
        private readonly MAVLink.MavlinkParse parser = new MAVLink.MavlinkParse();
        internal Action Reply;
        internal int Writes;
        internal Vehicle()
        {
            Link.BaseStream = Stream;
            Link.sysidcurrent = 17;
            Link.compidcurrent = 1;
            Link.MAV.apname = MAVLink.MAV_AUTOPILOT.ARDUPILOTMEGA;
            MainV2.comPort = Link;
            Stream.WriteCallback += (sender, data) =>
            {
                Writes++;
                MAVLink.MAVLinkMessage message;
                using (var input = new MemoryStream(data.ToArray())) message = parser.ReadPacket(input);
                Check(message != null && message.msgid == (uint)MAVLink.MAVLINK_MSG_ID.COMMAND_LONG,
                    "Calibration sent an unrelated message");
                var command = message.ToStructure<MAVLink.mavlink_command_long_t>();
                Check(command.command == (ushort)MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION,
                    "Wrong calibration command");
                Check(command.target_system == 17 && command.target_component == 1, "Wrong command target");
                Check(command.param3 == 1 && command.param1 == 0 && command.param2 == 0 &&
                    command.param4 == 0 && command.param5 == 0 && command.param6 == 0 && command.param7 == 0,
                    "ArduPilot requires param3=1 with all other parameters zero");
                Reply?.Invoke();
            };
        }
        internal void Ack(MAVLink.MAV_RESULT result, byte sysid = 17, byte compid = 1,
            MAVLink.MAV_CMD command = MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION)
        {
            Stream.AppendBuffer(parser.GenerateMAVLinkPacket20(MAVLink.MAVLINK_MSG_ID.COMMAND_ACK,
                new MAVLink.mavlink_command_ack_t { command = (ushort)command, result = (byte)result }, false, sysid, compid));
        }
        public void Dispose() { Link.Dispose(); }
    }

    private static async Task Run()
    {
        Check(Send != null && FailureText != null, "Missing production calibration path");
        foreach (MAVLink.MAV_RESULT response in new[] { MAVLink.MAV_RESULT.ACCEPTED,
            MAVLink.MAV_RESULT.UNSUPPORTED, MAVLink.MAV_RESULT.TEMPORARILY_REJECTED,
            MAVLink.MAV_RESULT.DENIED, MAVLink.MAV_RESULT.FAILED })
        {
            Console.WriteLine("TEST: terminal ACK " + response);
            using (var vehicle = new Vehicle())
            {
                vehicle.Reply = () => vehicle.Ack(response);
                Check(await Execute(vehicle.Link) == response, "Lost actual ACK result: " + response);
                Check(vehicle.Writes == 1, "Terminal ACK caused a repeated command");
                Check(!vehicle.Link.giveComport && Subscriptions(vehicle.Link) == 0,
                    "Transport/subscription leak: busy=" + vehicle.Link.giveComport + ", subscriptions=" + Subscriptions(vehicle.Link));
                if (response != MAVLink.MAV_RESULT.ACCEPTED)
                    foreach (bool chinese in new[] { true, false })
                        Check(((string)FailureText.Invoke(null, new object[] { response, chinese })).Contains(response.ToString()),
                            "Failure message omitted ACK reason");
            }
        }

        // Wrong sender/component/command ACKs must not terminate the operation; IN_PROGRESS is not success.
        Console.WriteLine("TEST: progress and unrelated ACKs");
        using (var vehicle = new Vehicle())
        {
            vehicle.Reply = () =>
            {
                vehicle.Ack(MAVLink.MAV_RESULT.ACCEPTED, 18);
                vehicle.Ack(MAVLink.MAV_RESULT.ACCEPTED, 17, 2);
                vehicle.Ack(MAVLink.MAV_RESULT.ACCEPTED, 17, 1, MAVLink.MAV_CMD.DO_SET_MODE);
                vehicle.Ack(MAVLink.MAV_RESULT.IN_PROGRESS);
            };
            var task = Task.Run(() => Execute(vehicle.Link));
            await Task.Delay(250);
            Check(!task.IsCompleted && vehicle.Link.giveComport, "Progress/unrelated ACK reported success");
            vehicle.Ack(MAVLink.MAV_RESULT.ACCEPTED);
            Check(await task == MAVLink.MAV_RESULT.ACCEPTED, "Final calibration ACK was lost");
            Check(vehicle.Writes == 1 && !vehicle.Link.giveComport && Subscriptions(vehicle.Link) == 0,
                "Progress handling retried/leaked the command");
        }
        using (var vehicle = new Vehicle())
        {
            vehicle.Reply = () => { vehicle.Ack(MAVLink.MAV_RESULT.IN_PROGRESS); vehicle.Ack(MAVLink.MAV_RESULT.FAILED); };
            Check(await Execute(vehicle.Link) == MAVLink.MAV_RESULT.FAILED, "Failure after progress became success");
            Check(!vehicle.Link.giveComport && Subscriptions(vehicle.Link) == 0, "Failed calibration leaked resources");
        }

        foreach (string state in new[] { "armed", "readonly", "busy", "replay", "closed", "missingstream",
            "wrongvehicle", "wrongcomponent", "newconnection", "parameters", "logs" })
        {
            Console.WriteLine("TEST: guard " + state);
            using (var vehicle = new Vehicle())
            using (var otherLink = new MAVLinkInterface())
            {
                switch (state)
                {
                    case "armed": vehicle.Link.MAV.cs.armed = true; break;
                    case "readonly": vehicle.Link.ReadOnly = true; break;
                    case "busy": vehicle.Link.giveComport = true; break;
                    case "replay": vehicle.Link.logreadmode = true; break;
                    case "closed": vehicle.Stream.Close(); break;
                    case "missingstream": vehicle.Link.BaseStream = null; break;
                    case "wrongvehicle": vehicle.Link.sysidcurrent = 18; break;
                    case "wrongcomponent": vehicle.Link.compidcurrent = 2; break;
                    case "newconnection": MainV2.comPort = otherLink; break;
                    case "parameters": typeof(MAVLinkInterface).GetField("activeParamDownloads", HiddenInstance).SetValue(vehicle.Link, 1); break;
                    case "logs": typeof(MAVLinkInterface).GetField("activeLogDownloads", HiddenInstance).SetValue(vehicle.Link, 1); break;
                }
                bool rejected = false;
                try { await Execute(vehicle.Link); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && vehicle.Writes == 0 && Subscriptions(vehicle.Link) == 0,
                    "Calibration was transmitted despite guard: " + state);
                if (state == "busy") Check(vehicle.Link.giveComport, "Cleared another operation's transport ownership");
                vehicle.Link.BaseStream = vehicle.Stream;
            }
        }
        // Once the flight controller has started calibration, timeout must release resources without resending.
        Console.WriteLine("TEST: in-progress timeout (25 seconds)");
        using (var vehicle = new Vehicle())
        {
            vehicle.Reply = () => vehicle.Ack(MAVLink.MAV_RESULT.IN_PROGRESS);
            bool timedOut = false;
            try { await Execute(vehicle.Link); } catch (TimeoutException) { timedOut = true; }
            Check(timedOut && vehicle.Writes == 1, "In-progress timeout resent calibration or reported success");
            Check(!vehicle.Link.giveComport && Subscriptions(vehicle.Link) == 0, "Timeout leaked resources");
        }
    }

    private static int Main()
    {
        try
        {
            Run().GetAwaiter().GetResult();
            Console.WriteLine("PASS: " + checks + " airspeed checks; actual MAVLink packets, terminal/progress ACKs, target isolation, state guards and timeout cleanup. Memory streams only.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
