using System;
using System.Collections.Generic;
using GMap.NET;

namespace MissionPlanner.FMT
{
    internal static class FmtAirspaceRadius
    {
        internal static decimal WholeKilometres(decimal kilometres)
        {
            return Math.Max(1m, Math.Min(30m, decimal.Round(kilometres, 0, MidpointRounding.AwayFromZero)));
        }

        internal static decimal MetresToKilometres(int metres)
        {
            return Math.Max(1, Math.Min(30000, metres)) / 1000m;
        }

        internal static int KilometresToMetres(decimal kilometres)
        {
            return decimal.ToInt32(decimal.Round(kilometres * 1000m, 0, MidpointRounding.AwayFromZero));
        }

        internal static bool ShouldDisplay(bool enabled, double zoom)
        {
            return enabled && zoom >= 10 && zoom <= 18;
        }

        // Local tangent-plane distances in metres. Intended for the Taiwan display filter
        // (up to 30 km), not flight authorization or the independent route-safety check.
        internal static bool Intersects(IList<PointLatLng> ring, PointLatLng home, double radius)
        {
            if (ring == null || ring.Count < 3 || radius <= 0) return false;
            var inside = false;
            var scaleX = 111320.0 * Math.Cos(home.Lat * Math.PI / 180);
            const double scaleY = 111320.0;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                var ax = (ring[j].Lng - home.Lng) * scaleX;
                var ay = (ring[j].Lat - home.Lat) * scaleY;
                var bx = (ring[i].Lng - home.Lng) * scaleX;
                var by = (ring[i].Lat - home.Lat) * scaleY;
                if ((ay > 0) != (by > 0) && 0 < ax + (bx - ax) * (-ay) / (by - ay)) inside = !inside;
                var dx = bx - ax;
                var dy = by - ay;
                var length2 = dx * dx + dy * dy;
                var t = length2 == 0 ? 0 : Math.Max(0, Math.Min(1, -(ax * dx + ay * dy) / length2));
                var x = ax + t * dx;
                var y = ay + t * dy;
                if (x * x + y * y <= radius * radius) return true;
            }
            return inside;
        }
    }
}
