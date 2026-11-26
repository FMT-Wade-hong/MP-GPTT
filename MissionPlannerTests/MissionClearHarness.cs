using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using GMap.NET;
using MissionPlanner;
using MissionPlanner.Comms;
using MissionPlanner.GCSViews;
using MissionPlanner.Utilities;

internal static class MissionClearHarness
{
    private static int checks;
    private static void Check(bool value, string reason)
    {
        if (!value) throw new Exception(reason);
        checks++;
    }

    private static void Click(Control button)
    {
        typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(button, new object[] { EventArgs.Empty });
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("zh-TW");
            Settings.CustomUserDataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mission-clear-test-data");
            Settings.Instance["fmt_airspace_visible"] = "False";
            GMaps.Instance.Mode = AccessMode.CacheOnly;
            MissionClearVehicleTests.Run().GetAwaiter().GetResult();
            MissionClearVehicleTests.UiTests();
            int writes = 0, prompts = 0;
            var reply = CustomMessageBox.DialogResult.No;
            CustomMessageBox.ShowEvent += (text, caption, buttons, icon, yes, no) =>
            {
                prompts++;
                Check(caption == "清除任務", "Unexpected dialog: " + text);
                if (buttons == CustomMessageBox.MessageBoxButtons.YesNo)
                    Check(text.Contains("HOME") && text.Contains("飛控內"), "Missing clear scope");
                return reply;
            };
            using (var link = new MAVLinkInterface())
            {
                var stream = new CommsInjection();
                stream.Close(); // Disconnected planning still supports local-only clearing.
                stream.WriteCallback += (sender, data) => writes++;
                link.BaseStream = stream;
                link.sysidcurrent = 17;
                link.compidcurrent = 1;
                MainV2.comPort = link;
                for (int i = 0; i < 3; i++)
                    link.MAV.wps[i] = new MAVLink.mavlink_mission_item_int_t
                    { seq = (ushort)i, command = (ushort)MAVLink.MAV_CMD.WAYPOINT, x = 240000000, y = 1210000000, z = 30 };
                var vehicleMission = link.MAV.wps.ToArray();
                using (var planner = new FlightPlanner())
                {
                    planner.BindingContext = new BindingContext();
                    var missionType = (ComboBox)planner.Controls.Find("cmb_missiontype", true).Single();
                    missionType.SelectedValue = MAVLink.MAV_MISSION_TYPE.MISSION;
                    var altitudeMode = (ComboBox)planner.Controls.Find("CMB_altmode", true).Single();
                    altitudeMode.SelectedValue = FlightPlanner.altmode.Relative;
                    var homeNames = new[] { "TXT_homelat", "TXT_homelng", "TXT_homealt" };
                    var home = homeNames.Select(name => planner.Controls.Find(name, true).Single()).ToArray();
                    home[0].Text = "24"; home[1].Text = "121"; home[2].Text = "100";
                    var homeBefore = home.Select(control => control.Text).ToArray();
                    planner.clearMissionToolStripMenuItem_Click(null, EventArgs.Empty);
                    planner.AddCommand(MAVLink.MAV_CMD.WAYPOINT, 0, 0, 0, 0, 24.001, 121.001, 30);
                    // Exercise the clear guard without opening the unrelated fence help dialog.
                    var selectionHandler = (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), planner,
                        typeof(FlightPlanner).GetMethod("Cmb_missiontype_SelectedIndexChanged", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
                    missionType.SelectedIndexChanged -= selectionHandler;
                    planner.AddCommand(MAVLink.MAV_CMD.WAYPOINT, 0, 0, 0, 0, 24.002, 121.002, 40);
                    Check(planner.Commands.Rows.Count == 2, "Fixture did not load waypoints: " + planner.Commands.Rows.Count);
                    var overlayBefore = FlightPlanner.instance.MainMap.Overlays.Single(o => o.Id == "WPOverlay");
                    Check(overlayBefore.Routes.Any(r => r.Points.Count > 1), "Fixture did not draw a route");
                    foreach (var cancel in new[] { CustomMessageBox.DialogResult.No, CustomMessageBox.DialogResult.Cancel })
                    {
                        reply = cancel;
                        Click(planner.but_writewpfast);
                        Check(planner.Commands.Rows.Count == 2, "Cancelled clear discarded waypoints");
                        Check(planner.MainMap.Overlays.Contains(overlayBefore), "Cancelled clear replaced route");
                    }
                    reply = CustomMessageBox.DialogResult.Yes;
                    Click(planner.but_writewpfast);
                    Check(planner.Commands.Rows.Count == 0, "Confirmed clear did not remove waypoints");
                    var overlayAfter = planner.MainMap.Overlays.Single(o => o.Id == "WPOverlay");
                    Check(!planner.MainMap.Overlays.Contains(overlayBefore), "Old route still on map");
                    Check(!overlayAfter.Routes.Any(r => r.Points.Count > 1), "Cleared mission still has route legs");
                    Check(home.Select(control => control.Text).SequenceEqual(homeBefore), "HOME changed");
                    Check(link.MAV.wps.ToArray().SequenceEqual(vehicleMission), "Vehicle mission cache changed");
                    int beforeEmpty = prompts;
                    Click(planner.but_writewpfast);
                    Check(prompts == beforeEmpty, "Empty plan should be a no-op");
                    planner.AddCommand(MAVLink.MAV_CMD.WAYPOINT, 0, 0, 0, 0, 24.001, 121.001, 30);
                    foreach (var type in new[] { MAVLink.MAV_MISSION_TYPE.FENCE, MAVLink.MAV_MISSION_TYPE.RALLY })
                    {
                        missionType.SelectedValue = type;
                        int before = planner.Commands.Rows.Count;
                        Click(planner.but_writewpfast);
                        Check(planner.Commands.Rows.Count == before, "Clear mission removed fence/rally data");
                    }
                    Check(writes == 0, "Planner clear sent a vehicle command");
                    Check(planner.but_writewpfast.Text == "清除任務", "Button label was not replaced");
                }
            }
            Console.WriteLine("PASS: " + checks + " offline mission-clear checks; cancel/confirm, route, HOME, vehicle cache, fence/rally guard; zero vehicle writes.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
