using System;
using System.Collections.Generic;
using System.Linq;

namespace MissionPlanner.FMT
{
    internal sealed class FmtGroundStationPosition
    {
        internal int StationNumber { get; set; }
        internal double Latitude { get; set; }
        internal double Longitude { get; set; }
        internal double AccuracyMeters { get; set; }
        internal string Source { get; set; }
        internal DateTime UpdatedUtc { get; set; }
        internal bool IsActiveController { get; set; }
    }

    /// <summary>
    /// Shared position registry for the local GCS and authenticated relay peers.
    /// A future relay transport should call Update for stations 2-5 only after
    /// authenticating the peer and validating the supplied coordinate.
    /// </summary>
    internal static class FmtGroundStationPositionStore
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<int, FmtGroundStationPosition> Positions =
            new Dictionary<int, FmtGroundStationPosition>();
        private static int activeController;

        internal static void Update(int stationNumber, double latitude, double longitude,
            double accuracyMeters, string source, DateTime updatedUtc)
        {
            if (stationNumber < 1 || stationNumber > 5 || !IsValidCoordinate(latitude, longitude))
                return;

            lock (Sync)
            {
                Positions[stationNumber] = new FmtGroundStationPosition
                {
                    StationNumber = stationNumber,
                    Latitude = latitude,
                    Longitude = longitude,
                    AccuracyMeters = double.IsNaN(accuracyMeters) || double.IsInfinity(accuracyMeters)
                        ? -1
                        : Math.Max(0, accuracyMeters),
                    Source = string.IsNullOrWhiteSpace(source) ? "未知來源" : source,
                    UpdatedUtc = updatedUtc.Kind == DateTimeKind.Utc ? updatedUtc : updatedUtc.ToUniversalTime(),
                    IsActiveController = stationNumber == activeController
                };
            }
        }

        internal static void Remove(int stationNumber)
        {
            lock (Sync)
                Positions.Remove(stationNumber);
        }

        internal static void Move(int previousStationNumber, int newStationNumber)
        {
            if (previousStationNumber == newStationNumber || newStationNumber < 1 || newStationNumber > 5)
                return;

            lock (Sync)
            {
                FmtGroundStationPosition position;
                if (!Positions.TryGetValue(previousStationNumber, out position))
                    return;

                Positions.Remove(previousStationNumber);
                position.StationNumber = newStationNumber;
                if (activeController == previousStationNumber)
                    activeController = newStationNumber;
                position.IsActiveController = newStationNumber == activeController;
                Positions[newStationNumber] = position;
            }
        }

        internal static void SetActiveController(int stationNumber)
        {
            lock (Sync)
            {
                activeController = stationNumber >= 1 && stationNumber <= 5 ? stationNumber : 0;
                foreach (var position in Positions.Values)
                    position.IsActiveController = position.StationNumber == activeController;
            }
        }

        internal static bool TryGet(int stationNumber, TimeSpan maximumAge,
            out FmtGroundStationPosition position)
        {
            position = Snapshot(maximumAge).FirstOrDefault(item => item.StationNumber == stationNumber);
            return position != null;
        }

        internal static List<FmtGroundStationPosition> Snapshot(TimeSpan maximumAge)
        {
            var now = DateTime.UtcNow;
            lock (Sync)
            {
                return Positions.Values
                    .Where(item => now - item.UpdatedUtc <= maximumAge)
                    .Select(Clone)
                    .OrderBy(item => item.StationNumber)
                    .ToList();
            }
        }

        private static FmtGroundStationPosition Clone(FmtGroundStationPosition source)
        {
            return new FmtGroundStationPosition
            {
                StationNumber = source.StationNumber,
                Latitude = source.Latitude,
                Longitude = source.Longitude,
                AccuracyMeters = source.AccuracyMeters,
                Source = source.Source,
                UpdatedUtc = source.UpdatedUtc,
                IsActiveController = source.IsActiveController
            };
        }

        private static bool IsValidCoordinate(double latitude, double longitude)
        {
            return !double.IsNaN(latitude) && !double.IsInfinity(latitude) &&
                   !double.IsNaN(longitude) && !double.IsInfinity(longitude) &&
                   latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180 &&
                   (Math.Abs(latitude) > 0.000001 || Math.Abs(longitude) > 0.000001);
        }
    }
}
