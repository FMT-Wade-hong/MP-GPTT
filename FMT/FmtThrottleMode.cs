namespace MissionPlanner.FMT
{
    internal static class FmtThrottleMode
    {
        internal const int CenteredThrottleFeedbackBit = 1;

        internal static bool IsCenteredThrottle(int pilotThrottleBehavior)
        {
            return (pilotThrottleBehavior & CenteredThrottleFeedbackBit) != 0;
        }

        internal static int SetCenteredThrottle(int pilotThrottleBehavior, bool centeredThrottle)
        {
            return centeredThrottle
                ? pilotThrottleBehavior | CenteredThrottleFeedbackBit
                : pilotThrottleBehavior & ~CenteredThrottleFeedbackBit;
        }

        internal static bool CalibrationLooksConsistent(bool centeredThrottle, int minimum, int trim, int maximum)
        {
            if (minimum < 800 || maximum > 2200 || maximum - minimum < 500 || trim < minimum || trim > maximum)
                return false;

            var tolerance = System.Math.Max(100, (maximum - minimum) * 15 / 100);
            var expected = centeredThrottle ? (minimum + maximum) / 2 : minimum;
            return System.Math.Abs(trim - expected) <= tolerance;
        }
    }
}
