using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using GMap.NET.MapProviders;
using MissionPlanner.GCSViews;
using MissionPlanner.Utilities;
using ZedGraph; // GE xml alt reader

namespace MissionPlanner.Controls
{
    [PreventTheming]
    internal sealed class FmtTerrainRiskLabel : System.Windows.Forms.Label
    {
    }

    public partial class ElevationProfile : Form
    {
        List<PointLatLngAlt> gelocs = new List<PointLatLngAlt>();
        List<PointLatLngAlt> srtmlocs = new List<PointLatLngAlt>();
        List<PointLatLngAlt> planlocs = new List<PointLatLngAlt>();
        PointPairList list1 = new PointPairList();
        PointPairList list2 = new PointPairList();
        PointPairList list3 = new PointPairList();

        PointPairList list4terrain = new PointPairList();
        int distance = 0;
        double homealt = 0;
        FlightPlanner.altmode altmode = FlightPlanner.altmode.Relative;

        public ElevationProfile(List<PointLatLngAlt> locs, double homealt, FlightPlanner.altmode altmode)
        {
            InitializeComponent();

            this.altmode = altmode;

            planlocs = locs;

            for (int a = 0; a < planlocs.Count; a++)
            {
                if (planlocs[a] == null || planlocs[a].Tag != null && planlocs[a].Tag.Contains("ROI"))
                {
                    planlocs.RemoveAt(a);
                    a--;
                }
            }

            if (planlocs.Count <= 1)
            {
                CustomMessageBox.Show("Please plan something first", Strings.ERROR);
                return;
            }

            // get total distance
            distance = 0;
            PointLatLngAlt lastloc = null;
            foreach (PointLatLngAlt loc in planlocs)
            {
                if (loc == null)
                    continue;

                if (lastloc != null)
                {
                    distance += (int)loc.GetDistance(lastloc);
                }
                lastloc = loc;
            }

            this.homealt = homealt;

            Form frm = Common.LoadingBox("Loading", "using alt data");

            //gelocs = getGEAltPath(planlocs);

            srtmlocs = getSRTMAltPath(planlocs);

            frm.Close();

            MissionPlanner.Utilities.Tracking.AddPage(this.GetType().ToString(), this.Text);
        }

        private void ElevationProfile_Load(object sender, EventArgs e)
        {
            if (planlocs.Count <= 1)
            {
                this.Close();
                return;
            }
            // GE plot
            /*
            double a = 0;
            double increment = (distance / (float)(gelocs.Count - 1));

            foreach (PointLatLngAlt geloc in gelocs)
            {
                if (geloc == null)
                    continue;

                list2.Add(a * CurrentState.multiplierdist, Convert.ToInt32(geloc.Alt * CurrentState.multiplieralt));

                Console.WriteLine("GE " + geloc.Lng + "," + geloc.Lat + "," + geloc.Alt);

                a += increment;
            }
            */
            // Planner Plot
            double a = 0;
            int count = 0;
            PointLatLngAlt lastloc = null;
            foreach (PointLatLngAlt planloc in planlocs)
            {
                if (planloc == null)
                    continue;

                if (lastloc != null)
                {
                    a += planloc.GetDistance(lastloc);
                }

                // deal with at mode
                if (altmode == FlightPlanner.altmode.Terrain)
                {
                    list1 = list4terrain;
                    break;
                }
                else if (altmode == FlightPlanner.altmode.Relative)
                {
                    // already includes the home alt
                    list1.Add(a * CurrentState.multiplierdist, (planloc.Alt * CurrentState.multiplieralt), 0, planloc.Tag);
                }
                else
                {
                    // abs
                    // already absolute
                    list1.Add(a * CurrentState.multiplierdist, (planloc.Alt * CurrentState.multiplieralt), 0, planloc.Tag);
                }

                lastloc = planloc;
                count++;
            }
            // draw graph
            CreateChart(zg1);
        }

        List<PointLatLngAlt> getSRTMAltPath(List<PointLatLngAlt> list)
        {
            List<PointLatLngAlt> answer = new List<PointLatLngAlt>();

            PointLatLngAlt last = null;

            double disttotal = 0;

            foreach (PointLatLngAlt loc in list)
            {
                if (loc == null)
                    continue;

                if (last == null)
                {
                    last = loc;
                    if (altmode == FlightPlanner.altmode.Terrain)
                        loc.Alt -= srtm.getAltitude(loc.Lat, loc.Lng).alt;
                    continue;
                }

                double dist = last.GetDistance(loc);

                if (altmode == FlightPlanner.altmode.Terrain)
                    loc.Alt -= srtm.getAltitude(loc.Lat, loc.Lng).alt;

                int points = (int)(dist / 10) + 1;

                double deltalat = (last.Lat - loc.Lat);
                double deltalng = (last.Lng - loc.Lng);
                double deltaalt = last.Alt - loc.Alt;

                double steplat = deltalat / points;
                double steplng = deltalng / points;
                double stepalt = deltaalt / points;

                PointLatLngAlt lastpnt = last;

                for (int a = 0; a <= points; a++)
                {
                    double lat = last.Lat - steplat * a;
                    double lng = last.Lng - steplng * a;
                    double alt = last.Alt - stepalt * a;

                    var newpoint = new PointLatLngAlt(lat, lng, srtm.getAltitude(lat, lng).alt, "");

                    double subdist = lastpnt.GetDistance(newpoint);

                    disttotal += subdist;

                    // srtm alts
                    list3.Add(disttotal * CurrentState.multiplierdist, Convert.ToInt32(newpoint.Alt * CurrentState.multiplieralt));

                    // terrain alt
                    list4terrain.Add(disttotal * CurrentState.multiplierdist, Convert.ToInt32((newpoint.Alt + alt) * CurrentState.multiplieralt));

                    lastpnt = newpoint;
                }

                answer.Add(new PointLatLngAlt(loc.Lat, loc.Lng, srtm.getAltitude(loc.Lat, loc.Lng).alt, ""));

                last = loc;
            }

            return answer;
        }

        List<PointLatLngAlt> getGEAltPath(List<PointLatLngAlt> list)
        {
            double alt = 0;
            double lat = 0;
            double lng = 0;

            int pos = 0;

            List<PointLatLngAlt> answer = new List<PointLatLngAlt>();

            //http://code.google.com/apis/maps/documentation/elevation/
            //http://maps.google.com/maps/api/elevation/xml
            string coords = "";

            foreach (PointLatLngAlt loc in list)
            {
                if (loc == null)
                    continue;

                coords = coords + loc.Lat.ToString(new System.Globalization.CultureInfo("en-US")) + "," +
                         loc.Lng.ToString(new System.Globalization.CultureInfo("en-US")) + "|";
            }
            coords = coords.Remove(coords.Length - 1);

            if (list.Count < 2 || coords.Length > (2048 - 256))
            {
                CustomMessageBox.Show("Too many/few WP's or to Big a Distance " + (distance / 1000) + "km", Strings.ERROR);
                return answer;
            }

            try
            {
                using (
                    XmlTextReader xmlreader =
                        new XmlTextReader("https://maps.google.com/maps/api/elevation/xml?path=" + coords + "&samples=" +
                                          (distance / 100).ToString(new System.Globalization.CultureInfo("en-US")) +
                                          "&sensor=false&key=" + GoogleMapProvider.APIKey))
                {
                    while (xmlreader.Read())
                    {
                        xmlreader.MoveToElement();
                        switch (xmlreader.Name)
                        {
                            case "elevation":
                                alt = double.Parse(xmlreader.ReadString(), new System.Globalization.CultureInfo("en-US"));
                                Console.WriteLine("DO it " + lat + " " + lng + " " + alt);
                                PointLatLngAlt loc = new PointLatLngAlt(lat, lng, alt, "");
                                answer.Add(loc);
                                pos++;
                                break;
                            case "lat":
                                lat = double.Parse(xmlreader.ReadString(), new System.Globalization.CultureInfo("en-US"));
                                break;
                            case "lng":
                                lng = double.Parse(xmlreader.ReadString(), new System.Globalization.CultureInfo("en-US"));
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch
            {
                CustomMessageBox.Show("Error getting GE data", Strings.ERROR);
            }

            return answer;
        }

        public void CreateChart(ZedGraphControl zgc)
        {
            GraphPane myPane = zgc.GraphPane;

            // Set the titles and axis labels
            myPane.Title.Text = "飛行高度與地形剖面";
            myPane.XAxis.Title.Text = "航線距離（" + CurrentState.DistanceUnit + "）";
            myPane.YAxis.Title.Text = "高度（" + CurrentState.AltUnit + "）";
            myPane.Legend.IsVisible = false;

            LineItem myCurve;

            myCurve = myPane.AddCurve("規劃航線", list1, Color.Red, SymbolType.None);
            //myCurve = myPane.AddCurve("Google", list2, Color.Green, SymbolType.None);
            myCurve = myPane.AddCurve("數值地形（DEM）", list3, Color.Blue, SymbolType.None);

            UpdateFmtTerrainClearanceSummary(myPane);

            foreach (PointPair pp in list1)
            {
                // Add a another text item to to point out a graph feature
                TextObj text = new TextObj((string)pp.Tag, pp.X, pp.Y);
                // rotate the text 90 degrees
                text.FontSpec.Angle = 90;
                text.FontSpec.FontColor = Color.White;
                // Align the text such that the Right-Center is at (700, 50) in user scale coordinates
                text.Location.AlignH = AlignH.Right;
                text.Location.AlignV = AlignV.Center;
                // Disable the border and background fill options for the text
                text.FontSpec.Fill.IsVisible = false;
                text.FontSpec.Border.IsVisible = false;
                myPane.GraphObjList.Add(text);
            }

            // Show the x axis grid
            myPane.XAxis.MajorGrid.IsVisible = true;

            myPane.XAxis.Scale.Min = 0;
            myPane.XAxis.Scale.Max = distance * CurrentState.multiplierdist;

            // Make the Y axis scale red
            myPane.YAxis.Scale.FontSpec.FontColor = Color.Red;
            myPane.YAxis.Title.FontSpec.FontColor = Color.Red;
            // turn off the opposite tics so the Y tics don't show up on the Y2 axis
            myPane.YAxis.MajorTic.IsOpposite = false;
            myPane.YAxis.MinorTic.IsOpposite = false;
            // Don't display the Y zero line
            myPane.YAxis.MajorGrid.IsZeroLine = true;
            // Align the Y axis labels so they are flush to the axis
            myPane.YAxis.Scale.Align = AlignP.Inside;
            // Manually set the axis range
            //myPane.YAxis.Scale.Min = -1;
            //myPane.YAxis.Scale.Max = 1;

            // Fill the axis background with a gradient
            //myPane.Chart.Fill = new Fill(Color.White, Color.LightGray, 45.0f);

            // Calculate the Axis Scale Ranges
            try
            {
                zg1.AxisChange();
            }
            catch
            {
            }
        }

        private void UpdateFmtTerrainClearanceSummary(GraphPane pane)
        {
            var planStats = GetFmtStats(list1);
            var terrainStats = GetFmtStats(list3);
            labelPlanSummary.Text = string.Format("規劃航線（紅）\r\n最低 {0:0.0}／最高 {1:0.0} {2}",
                planStats.Min, planStats.Max, CurrentState.AltUnit);
            labelTerrainSummary.Text = string.Format("地形剖面（藍）\r\n最低 {0:0.0}／最高 {1:0.0} {2}",
                terrainStats.Min, terrainStats.Max, CurrentState.AltUnit);

            var collisionPoints = new PointPairList();
            var minimumClearance = double.MaxValue;
            var collisionCount = 0;
            var riskStart = GetFmtFirstMissionDistance();
            var riskEnd = GetFmtLastMissionDistance();
            foreach (PointPair terrainPoint in list3)
            {
                // Home is normally located on the ground. Including the Home-to-first-WP
                // origin in the 30 m test creates a permanent false warning near 0 m AGL.
                if (terrainPoint.X < riskStart || terrainPoint.X > riskEnd)
                    continue;

                var plannedAltitude = InterpolateFmtPlannedAltitude(terrainPoint.X);
                if (double.IsNaN(plannedAltitude))
                    continue;

                var clearance = plannedAltitude - terrainPoint.Y;
                minimumClearance = Math.Min(minimumClearance, clearance);
                if (clearance <= 0)
                {
                    collisionCount++;
                    collisionPoints.Add(terrainPoint.X, terrainPoint.Y);
                }
            }

            if (collisionPoints.Count > 0)
            {
                var danger = pane.AddCurve("撞地風險", collisionPoints, Color.Yellow, SymbolType.Circle);
                danger.Line.IsVisible = false;
                danger.Symbol.Size = 5F;
                danger.Symbol.Fill = new Fill(Color.Red);
                danger.Symbol.Border.Color = Color.Yellow;
            }

            if (minimumClearance == double.MaxValue)
            {
                labelCollisionWarning.BackColor = Color.FromArgb(80, 88, 96);
                labelCollisionWarning.ForeColor = Color.White;
                labelCollisionWarning.Text = "無足夠任務航段資料\r\n無法計算地形淨空";
            }
            else if (collisionCount > 0)
            {
                labelCollisionWarning.BackColor = Color.FromArgb(183, 28, 28);
                labelCollisionWarning.ForeColor = Color.White;
                labelCollisionWarning.Text = string.Format("⚠ 任務航段撞地警告\r\n{0} 個取樣點地形高於航線，最差淨空 {1:0.0} {2}",
                    collisionCount, minimumClearance, CurrentState.AltUnit);
            }
            else if (minimumClearance < 30 * CurrentState.multiplieralt)
            {
                labelCollisionWarning.BackColor = Color.FromArgb(255, 196, 0);
                labelCollisionWarning.ForeColor = Color.FromArgb(20, 24, 28);
                labelCollisionWarning.Text = string.Format("⚠ 任務航段淨空不足\r\n最低淨空 {0:0.0} {1}（建議至少 30 m）",
                    minimumClearance, CurrentState.AltUnit);
            }
            else
            {
                labelCollisionWarning.BackColor = Color.FromArgb(0, 120, 70);
                labelCollisionWarning.ForeColor = Color.White;
                labelCollisionWarning.Text = string.Format("✓ 任務航段未偵測撞地風險\r\n最低地形淨空 {0:0.0} {1}",
                    minimumClearance, CurrentState.AltUnit);
            }
        }

        private double GetFmtFirstMissionDistance()
        {
            foreach (PointPair point in list1)
            {
                var tag = point.Tag as string;
                if (!string.Equals(tag, "H", StringComparison.OrdinalIgnoreCase))
                    return point.X;
            }

            return list1.Count > 0 ? list1[0].X : 0;
        }

        private double GetFmtLastMissionDistance()
        {
            for (var index = list1.Count - 1; index >= 0; index--)
            {
                var point = list1[index];
                var tag = point.Tag as string;
                if (!string.Equals(tag, "H", StringComparison.OrdinalIgnoreCase))
                    return point.X;
            }

            return list1.Count > 0 ? list1[list1.Count - 1].X : 0;
        }

        private double InterpolateFmtPlannedAltitude(double distanceAlongRoute)
        {
            if (list1.Count == 0)
                return double.NaN;
            if (distanceAlongRoute <= list1[0].X)
                return list1[0].Y;

            for (var index = 1; index < list1.Count; index++)
            {
                var previous = list1[index - 1];
                var current = list1[index];
                if (distanceAlongRoute > current.X)
                    continue;

                var span = current.X - previous.X;
                if (Math.Abs(span) < double.Epsilon)
                    return current.Y;
                var ratio = (distanceAlongRoute - previous.X) / span;
                return previous.Y + ((current.Y - previous.Y) * ratio);
            }

            return list1[list1.Count - 1].Y;
        }

        private static FmtElevationStats GetFmtStats(PointPairList points)
        {
            if (points == null || points.Count == 0)
                return new FmtElevationStats();

            var minimum = double.MaxValue;
            var maximum = double.MinValue;
            var total = 0.0;
            foreach (PointPair point in points)
            {
                minimum = Math.Min(minimum, point.Y);
                maximum = Math.Max(maximum, point.Y);
                total += point.Y;
            }

            return new FmtElevationStats
            {
                Min = minimum,
                Max = maximum,
                Mean = total / points.Count
            };
        }

        private struct FmtElevationStats
        {
            public double Min;
            public double Max;
            public double Mean;
        }
    }
}
