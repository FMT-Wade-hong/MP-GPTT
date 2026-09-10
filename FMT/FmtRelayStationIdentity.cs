using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.IO;

namespace MissionPlanner.FMT
{
    /// <summary>
    /// Process-local relay station identity.  The configured station number is
    /// persisted per computer, while an exclusive lock prevents two FMTPlanner
    /// instances on the same Windows session from silently claiming the same
    /// station number during bench testing.
    /// </summary>
    internal static class FmtRelayStationIdentity
    {
        private const string SettingName = "FMT_RelayLocalStationNumber";
        private static readonly object Sync = new object();
        private static bool initialized;
        private static int stationNumber = 1;
        private static FileStream stationClaim;
        private static bool automaticallyAssigned;

        internal static event Action<int, int> StationNumberChanged;

        internal static int StationNumber
        {
            get
            {
                EnsureInitialized();
                lock (Sync)
                    return stationNumber;
            }
        }

        internal static bool AutomaticallyAssigned
        {
            get
            {
                EnsureInitialized();
                lock (Sync)
                    return automaticallyAssigned;
            }
        }

        internal static bool TrySetStationNumber(int requestedStationNumber, bool persist,
            out string error)
        {
            error = null;
            if (requestedStationNumber < 1 || requestedStationNumber > 5)
            {
                error = "本機站號必須介於 1 到 5。";
                return false;
            }

            EnsureInitialized();
            int previousStationNumber;
            lock (Sync)
            {
                if (requestedStationNumber == stationNumber)
                {
                    if (persist)
                        SavePreferredStationNumber(requestedStationNumber);
                    automaticallyAssigned = false;
                    return true;
                }

                FileStream replacement;
                if (!TryClaim(requestedStationNumber, out replacement))
                {
                    error = "同一台電腦已有另一個 FMTPlanner 使用 " + requestedStationNumber +
                            " 號站，請選擇其他站號。";
                    return false;
                }

                previousStationNumber = stationNumber;
                var previousClaim = stationClaim;
                stationNumber = requestedStationNumber;
                stationClaim = replacement;
                automaticallyAssigned = false;
                previousClaim?.Dispose();
                if (persist)
                    SavePreferredStationNumber(requestedStationNumber);
            }

            FmtGroundStationPositionStore.Move(previousStationNumber, requestedStationNumber);
            StationNumberChanged?.Invoke(previousStationNumber, requestedStationNumber);
            return true;
        }

        private static void EnsureInitialized()
        {
            lock (Sync)
            {
                if (initialized)
                    return;

                initialized = true;
                var preferred = ReadPreferredStationNumber();
                var candidates = new List<int> { preferred };
                for (var candidate = 1; candidate <= 5; candidate++)
                    if (!candidates.Contains(candidate))
                        candidates.Add(candidate);

                foreach (var candidate in candidates)
                {
                    FileStream claim;
                    if (!TryClaim(candidate, out claim))
                        continue;

                    stationNumber = candidate;
                    stationClaim = claim;
                    automaticallyAssigned = candidate != preferred;
                    return;
                }

                // More than five local instances is outside the supported relay
                // topology. Keep a valid value so the UI remains usable, but do
                // not pretend an exclusive station identity was obtained.
                stationNumber = preferred;
                stationClaim = null;
                automaticallyAssigned = true;
            }
        }

        private static int ReadPreferredStationNumber()
        {
            try
            {
                int parsed;
                var value = Settings.Instance[SettingName];
                if (int.TryParse(value, out parsed) && parsed >= 1 && parsed <= 5)
                    return parsed;
            }
            catch
            {
                // Settings may not be available during very early initialization.
            }

            return 1;
        }

        private static void SavePreferredStationNumber(int value)
        {
            try
            {
                Settings.Instance[SettingName] = value.ToString();
            }
            catch
            {
                // The process-local identity remains valid even if persistence fails.
            }
        }

        private static bool TryClaim(int value, out FileStream claim)
        {
            claim = null;
            try
            {
                var path = Path.Combine(Path.GetTempPath(),
                    "FMTPlanner-RelayStation-" + value + ".lock");
                claim = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                    FileShare.None, 1, FileOptions.DeleteOnClose);
                return true;
            }
            catch (IOException)
            {
                claim?.Dispose();
                claim = null;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                claim?.Dispose();
                claim = null;
                return false;
            }
        }
    }
}
