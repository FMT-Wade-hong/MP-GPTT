using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner;
using MissionPlanner.Comms;

internal static class CrashGuardHarness
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
    private static Type refresh;
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static IDisposable Begin() => (IDisposable)refresh.GetMethod("TryBeginWrite", Hidden).Invoke(null, null);
    private static Task<double> Run(Func<Task<bool>> write, Func<Task<double>> read,
        Func<bool> current, CancellationToken token = default(CancellationToken)) =>
        (Task<double>)refresh.GetMethod("WriteReadAsync", Hidden).Invoke(null, new object[] { write, read, current, token });

    private static async Task Transactions()
    {
        using (var lease = Begin())
        {
            var pending = new TaskCompletionSource<bool>();
            int reads = 0;
            var task = Run(() => pending.Task, () => { reads++; return Task.FromResult(4.5); }, () => true);
            Check(!task.IsCompleted && reads == 0, "Read raced pending write acknowledgement");
            for (int i = 0; i < 1000; i++) Check(Begin() == null, "Repeated write acquired shared read/write gate");
            var gate = (SemaphoreSlim)refresh.GetField("Gate", Hidden).GetValue(null);
            Check(!gate.Wait(0), "Page read could overlap an ICE write");
            pending.SetResult(true);
            Check(await task == 4.5 && reads == 1, "Readback did not complete exactly once");
        }
        foreach (var failure in new[] { "denied", "timeout", "cancel", "target" })
        using (var lease = Begin())
        using (var cancel = new CancellationTokenSource())
        {
            int reads = 0;
            bool current = true;
            Func<Task<bool>> write = () =>
            {
                if (failure == "timeout") throw new TimeoutException("simulated timeout");
                if (failure == "cancel") cancel.Cancel();
                if (failure == "target") current = false;
                return Task.FromResult(failure != "denied");
            };
            bool failed = false;
            try { await Run(write, () => { reads++; return Task.FromResult(0d); }, () => current, cancel.Token); }
            catch (Exception ex)
            {
                failed = failure == "timeout" ? ex is TimeoutException :
                    failure == "cancel" ? ex is OperationCanceledException : ex is InvalidOperationException;
            }
            Check(failed && reads == 0, "Failure/leave/target change continued readback or reported success: " + failure);
        }
        var first = Begin(); first.Dispose();
        var second = Begin(); first.Dispose();
        Check(Begin() == null, "Old completion released a newer transfer"); second.Dispose();
        using (var lease = Begin())
        {
            Check(lease != null, "Failure left gate locked");
            Check(await Run(() => Task.FromResult(true), () => Task.FromResult(1d), () => true) == 1,
                "Rejected/clamped readback was replaced by requested value");
        }
    }

    private static async Task Transport()
    {
        using (var link = new MAVLinkInterface())
        {
            var stream = new CommsInjection(); // Entirely in-memory; no port/network opened.
            link.BaseStream = stream;
            var mav = link.MAVlist[1, 1];
            mav.apname = MAVLink.MAV_AUTOPILOT.ARDUPILOTMEGA;
            mav.param["ICE_TEST"] = new MAVLink.MAVLinkParam("ICE_TEST", 1, MAVLink.MAV_PARAM_TYPE.REAL32);
            mav.param_types["ICE_TEST"] = MAVLink.MAV_PARAM_TYPE.REAL32;
            int packets = 0; bool current = true;
            stream.WriteCallback += (s, bytes) => { packets++; current = false; };
            try { await link.setParamCheckedAsync(1, 1, "ICE_TEST", 2, false, () => current); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { }
            Check(packets == 1 && !link.giveComport, "Cancelled transfer retried or left transport busy");
            try { await link.setParamCheckedAsync(1, 1, "ICE_TEST", 2, false, () => false); throw new Exception("Initial cancellation ignored"); }
            catch (OperationCanceledException) { }
            Check(packets == 1 && !link.giveComport, "Cancelled request sent a packet");
            MAVLink.MAV_PARAM_TYPE ignored;
            mav.param_types.TryRemove("ICE_TEST", out ignored);
            try { await link.setParamCheckedAsync(1, 1, "ICE_TEST", 2, false, () => true); throw new Exception("Missing type unexpectedly accepted"); }
            catch (System.Collections.Generic.KeyNotFoundException) { }
            Check(!link.giveComport, "Preparation failure permanently claimed transport");
            Check(typeof(MAVLinkInterface).GetMethod("setParamAsync", new[] {typeof(byte), typeof(byte), typeof(string), typeof(double), typeof(bool)}) != null,
                "Existing plugin API signature changed");
        }
    }

    private static int Main(string[] args)
    {
        var assembly = Assembly.LoadFrom(args[0]);
        refresh = assembly.GetType("MissionPlanner.FmtPageParameterRefresh", true);
        Transactions().GetAwaiter().GetResult();
        Transport().GetAwaiter().GetResult();
        var append = assembly.GetType("MissionPlanner.FMT.FmtCrashDiagnostics", true).GetMethod("Append", Hidden);
        var path = Path.Combine(args[1], "diagnostic-test.log");
        for (int i = 0; i < 90; i++) append.Invoke(null, new object[] {path, "offline-test", new Exception(new string('x', 24000))});
        Check(File.Exists(path + ".2") && !File.Exists(path + ".3"), "Crash record rotation failed");
        foreach (var file in Directory.GetFiles(args[1], "diagnostic-test.log*"))
            Check(new FileInfo(file).Length <= 256 * 1024, "Crash diagnostic exceeded file limit");
        Console.WriteLine("PASS: 1000 duplicate writes, shared read/write lease, timeout/denied recovery, cancellation/target change, real MAVLink cancellation before retries, transport recovery, plugin compatibility, bounded local diagnostics.");
        return 0;
    }
}
