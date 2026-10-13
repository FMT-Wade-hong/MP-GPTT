using System;
using System.Collections.Generic;
using GMap.NET;

namespace MissionPlanner.FMT
{
    internal static class FmtAirspaceRadius
    {
        internal static decimal WholeKilometres(decimal kilometres)
        {
            return Math.Max(1m, Math.Min(500m, decimal.Round(kilometres, 0, MidpointRounding.AwayFromZero)));
        }

        internal static decimal MetresToKilometres(int metres)
        {
            return Math.Max(1, Math.Min(500000, metres)) / 1000m;
        }

        internal static int KilometresToMetres(decimal kilometres)
        {
            return decimal.ToInt32(decimal.Round(kilometres * 1000m, 0, MidpointRounding.AwayFromZero));
        }

        internal static bool ShouldDisplay(bool enabled, double zoom)
        {
            return enabled && zoom >= 10 && zoom <= 18;
        }

        // Home-centred azimuthal equidistant projection for the Taiwan display filter
        // (up to 500 km), not flight authorization or the independent route-safety check.
        private static void Project(PointLatLng point, PointLatLng home, out double x, out double y)
        {
            const double radians = Math.PI / 180;
            double lat = point.Lat * radians, origin = home.Lat * radians;
            double longitude = (point.Lng - home.Lng) * radians;
            double cosDistance = Math.Sin(origin) * Math.Sin(lat) + Math.Cos(origin) * Math.Cos(lat) * Math.Cos(longitude);
            double distance = Math.Acos(Math.Max(-1, Math.Min(1, cosDistance)));
            double factor = distance < 1e-10 ? 1 : distance / Math.Sin(distance);
            x = 6371000 * factor * Math.Cos(lat) * Math.Sin(longitude);
            y = 6371000 * factor * (Math.Cos(origin) * Math.Sin(lat) - Math.Sin(origin) * Math.Cos(lat) * Math.Cos(longitude));
        }
        internal static bool Intersects(IList<PointLatLng> ring, PointLatLng home, double radius)
        {
            if (ring == null || ring.Count < 3 || radius <= 0) return false;
            var inside = false;
            double ax, ay;
            Project(ring[ring.Count - 1], home, out ax, out ay);
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                double bx, by;
                Project(ring[i], home, out bx, out by);
                if ((ay > 0) != (by > 0) && 0 < ax + (bx - ax) * (-ay) / (by - ay)) inside = !inside;
                var dx = bx - ax;
                var dy = by - ay;
                var length2 = dx * dx + dy * dy;
                var t = length2 == 0 ? 0 : Math.Max(0, Math.Min(1, -(ax * dx + ay * dy) / length2));
                var x = ax + t * dx;
                var y = ay + t * dy;
                if (x * x + y * y <= radius * radius) return true;
                ax = bx;
                ay = by;
            }
            return inside;
        }
    }
}
