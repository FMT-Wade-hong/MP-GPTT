namespace MissionPlanner.Utilities
{
    /// <summary>
    /// Geographic and request limits for the Taiwan ADS-B map layer.
    /// </summary>
    public static class AdsbTaiwanCoverage
    {
        public const int QueryRadiusNm = 100;
        public const int PollIntervalMilliseconds = 2000;

        // CAA eAIP ENR 5.2.3, effective 2026-10-01:
        // https://ais.caa.gov.tw/eaip/AIRAC%20AIP%20AMDT%2004-26_2026_10_01/eAIP/RC-ENR%205.2-zh-TW.html
        // (lat, lon): (21,117.5), (21,121.5), (22.5,123), (29,123), (29,117.5).
        public static bool Contains(double latitude, double longitude)
        {
            // Positive comparisons also reject NaN/infinity. The final half-plane
            // excludes the southeast corner outside the published diagonal boundary.
            return latitude >= 21 && latitude <= 29 &&
                   longitude >= 117.5 && longitude <= 123 &&
                   longitude <= latitude + 100.5;
        }

        public static string CreateQueryUrl(string server, PointLatLngAlt center)
        {
            // Do not request foreign traffic when the vehicle/map center leaves the ADIZ.
            if (string.IsNullOrWhiteSpace(server) || center == null || !Contains(center.Lat, center.Lng))
                return null;

            return AdsbApi.CreatePointQuery(server, center.Lat, center.Lng, QueryRadiusNm);
        }
    }
}
