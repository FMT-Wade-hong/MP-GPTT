using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner;
using MissionPlanner.ArduPilot;
using MissionPlanner.Comms;
using MissionPlanner.GCSViews.ConfigurationView;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;

internal static class ParameterWriteReadbackHarness
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        using (var link = new MAVLinkInterface())
        using (var page = new ConfigArducopter())
        {
            var stream = new CommsInjection();
            link.BaseStream = stream;
            link.sysidcurrent = 1; link.compidcurrent = 1;
            var mav = link.MAVlist[1, 1];
            mav.apname = MAVLink.MAV_AUTOPILOT.ARDUPILOTMEGA;
            mav.param["ATC_ANG_RLL_P"] = new MAVLink.MAVLinkParam("ATC_ANG_RLL_P", 4, MAVLink.MAV_PARAM_TYPE.REAL32);
            mav.param_types["ATC_ANG_RLL_P"] = MAVLink.MAV_PARAM_TYPE.REAL32;
            MainV2.comPort = link;
            var changes = (Hashtable)typeof(ConfigArducopter).GetField("changes", Hidden).GetValue(page);
            mav.cs.firmware = Firmwares.ArduCopter2;
            mav.VersionString = "ArduCopter V4.7.2";
            ParameterMetaDataRepository.VehicleTypeResolver = vehicle => "Copter4.7.2";
            bool activationFailed = false;
            try { page.Activate(); } catch (ArgumentNullException) { activationFailed = true; }
            var editor = (MavlinkNumericUpDown)typeof(ConfigArducopter).GetField("STB_RLL_P", Hidden).GetValue(page);
            editor.Text = "6";
            typeof(NumericUpDown).GetMethod("ValidateEditText", Hidden).Invoke(editor, null);
            if (args.Contains("--baseline"))
            {
                Console.WriteLine("BASELINE activation: null-binding exception="+activationFailed+", pending edits="+changes.Count);
                Check(activationFailed && changes.Count == 0, "Expected activation failure to suppress all editing");
            }
            else
            {
                Check(!activationFailed && changes.ContainsKey("ATC_ANG_RLL_P") && (float)changes["ATC_ANG_RLL_P"] == 6,
                    "Actual typed edit was ignored after partial parameter-list activation");
                Check(editor.BackColor == System.Drawing.Color.Green, "Actual edit was not highlighted");
            }
            int sets = 0, reads = 0;
            float reply = 4;
            var parser = new MAVLink.MavlinkParse();
            stream.WriteCallback += (sender, data) =>
            {
                MAVLink.MAVLinkMessage message;
                using (var input = new MemoryStream(data.ToArray())) message = parser.ReadPacket(input);
                if (message == null) return;
                if (message.msgid == (uint)MAVLink.MAVLINK_MSG_ID.PARAM_SET) sets++;
                else if (message.msgid == (uint)MAVLink.MAVLINK_MSG_ID.PARAM_REQUEST_READ) reads++;
                else return;
                var name = new byte[16]; Encoding.ASCII.GetBytes("ATC_ANG_RLL_P").CopyTo(name, 0);
                stream.AppendBuffer(parser.GenerateMAVLinkPacket10(MAVLink.MAVLINK_MSG_ID.PARAM_VALUE,
                    new MAVLink.mavlink_param_value_t {param_id=name, param_value=reply,
                        param_type=(byte)MAVLink.MAV_PARAM_TYPE.REAL32, param_count=1, param_index=0}, 1, 1));
            };
            changes["ATC_ANG_RLL_P"] = 6f;
            if (args.Contains("--baseline"))
            {
                typeof(ConfigArducopter).GetMethod("BUT_writePIDS_Click", Hidden).Invoke(page, new object[] {null, EventArgs.Empty});
                Console.WriteLine("BASELINE: requested=6, vehicle=4, sets="+sets+", readbacks="+reads+", pending="+changes.Count);
                Check(sets == 1 && reads == 0 && changes.Count == 0 && mav.param["ATC_ANG_RLL_P"].Value == 4,
                    "Expected rejected value to be incorrectly cleared by the old page");
                return 0;
            }
            var write = typeof(ConfigArducopter).GetMethod("WriteStagedParameterAsync", Hidden);
            Func<Task> execute = () => (Task)write.Invoke(page, new object[] {link, mav, "ATC_ANG_RLL_P", 6f,
                (Func<bool>)(() => true), CancellationToken.None});
            var rejected = execute();
            while (!rejected.IsCompleted) { Application.DoEvents(); Thread.Sleep(1); }
            try { rejected.GetAwaiter().GetResult(); throw new Exception("Rejected value reported success"); }
            catch (InvalidOperationException ex) { Check(ex.Message.Contains("6") && ex.Message.Contains("4"), "Missing requested/readback values"); }
            Check(sets == 1 && reads == 1 && changes.ContainsKey("ATC_ANG_RLL_P"), "Rejected edit lost or readback missing");
            reply = 6;
            var accepted = execute();
            while (!accepted.IsCompleted) { Application.DoEvents(); Thread.Sleep(1); }
            accepted.GetAwaiter().GetResult();
            Check(sets == 2 && reads == 2 && changes.Count == 0, "Confirmed retry did not clear the edit");
            // A stale optimistic cache must not suppress transmission of an explicit staged edit.
            changes["ATC_ANG_RLL_P"] = 6f;
            var sameCache = execute();
            while (!sameCache.IsCompleted) { Application.DoEvents(); Thread.Sleep(1); }
            sameCache.GetAwaiter().GetResult();
            Check(sets == 3 && reads == 3 && changes.Count == 0 && !link.giveComport, "Cache shortcut skipped write/readback");
            float smallChange = 6.000001f;
            changes["ATC_ANG_RLL_P"] = smallChange;
            var tiny = (Task)write.Invoke(page, new object[] {link, mav, "ATC_ANG_RLL_P", smallChange,
                (Func<bool>)(() => true), CancellationToken.None});
            while (!tiny.IsCompleted) { Application.DoEvents(); Thread.Sleep(1); }
            try { tiny.GetAwaiter().GetResult(); throw new Exception("Small rejected PID change was hidden by a fixed tolerance"); }
            catch (InvalidOperationException) { }
            Check(changes.ContainsKey("ATC_ANG_RLL_P"), "Small rejected edit disappeared");
            int sentBeforeCancel = sets;
            var cancelled = (Task)write.Invoke(page, new object[] {link, mav, "ATC_ANG_RLL_P", smallChange,
                (Func<bool>)(() => true), new CancellationToken(true)});
            while (!cancelled.IsCompleted) { Application.DoEvents(); Thread.Sleep(1); }
            try { cancelled.GetAwaiter().GetResult(); throw new Exception("Cancelled edit sent"); }
            catch (OperationCanceledException) { }
            Check(sets == sentBeforeCancel && changes.ContainsKey("ATC_ANG_RLL_P"), "Cancellation sent/lost the edit");
            Console.WriteLine("PASS: actual tuning page, rejected readback retained, confirmed retry, explicit write despite equal cache, small PID mismatch, cancellation; in-memory MAVLink only.");
        }
        return 0;
    }
}
