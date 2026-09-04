using MissionPlanner.FMT;
using System;

internal static class FmtThrottleModeHarness
{
    private static int passed;

    private static int Main()
    {
        try
        {
            Check(FmtThrottleMode.SetCenteredThrottle(6, true) == 7,
                "enable centered throttle and preserve other bits");
            Check(FmtThrottleMode.SetCenteredThrottle(7, false) == 6,
                "disable centered throttle and preserve other bits");
            Check(FmtThrottleMode.IsCenteredThrottle(5) && !FmtThrottleMode.IsCenteredThrottle(4),
                "read centered throttle bit");
            Check(FmtThrottleMode.CalibrationLooksConsistent(true, 1000, 1500, 2000),
                "centered throttle accepts midpoint trim");
            Check(!FmtThrottleMode.CalibrationLooksConsistent(true, 1000, 1010, 2000),
                "centered throttle rejects low trim");
            Check(FmtThrottleMode.CalibrationLooksConsistent(false, 1000, 1010, 2000),
                "manual throttle accepts low trim");
            Check(!FmtThrottleMode.CalibrationLooksConsistent(false, 1000, 1500, 2000),
                "manual throttle rejects midpoint trim");
            Check(!FmtThrottleMode.CalibrationLooksConsistent(true, 1300, 1500, 1700),
                "reject calibration range that is too short");
            Console.WriteLine("PASS: " + passed + " throttle-mode checks.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void Check(bool result, string label)
    {
        if (!result)
            throw new Exception("FAIL " + label);
        passed++;
        Console.WriteLine("PASS " + label);
    }
}
