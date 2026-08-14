using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using GMap.NET;
using GMap.NET.WindowsForms;
using MissionPlanner.Utilities;

namespace MissionPlanner.Maps
{
    [Serializable]
    public class GMapMarkerPlane : GMapMarkerBase
    {
        private static readonly Bitmap FixedWingIcon = LoadFmtIcon("FMTMapFixedWing.png");
        private static readonly Bitmap FixedWingGlowIcon = LoadFmtIcon("FMTMapFixedWingGlow.png");
        private static readonly Bitmap VtolIcon = LoadFmtIcon("FMTMapVtol.png");
        private static readonly Bitmap VtolGlowIcon = LoadFmtIcon("FMTMapVtolGlow.png");

        static SolidBrush shadow = new SolidBrush(Color.FromArgb(50, Color.Black));

        static Point[] plane = new Point[] {
            new Point(28,0),
            new Point(32,13),
            new Point(53,27),
            new Point(55,32),
            new Point(31,28),
            new Point(30,35),
            new Point(30,43),
            new Point(37,48),
            new Point(37,50),
            new Point(29,50),
            new Point(29,53),
            // inverse
            new Point(inv(29,28),53),
            new Point(inv(29,28),50),
            new Point(inv(37,28),50),
            new Point(inv(37,28),48),
            new Point(inv(30,28),43),
            new Point(inv(30,28),35),
            new Point(inv(31,28),28),
            new Point(inv(55,28),32),
            new Point(inv(53,28),27),
            new Point(inv(32,28),13),
            new Point(inv(28,28),0),
            };

        private static int inv(int input, int mid)
        {
            var delta = input - mid;

            return mid - delta;
        }

        float cog = -1;
        float heading = 0;
        float nav_bearing = -1;
        float radius = -1;
        float target = -1;
        int which = 0;
        public bool IsVtol { get; set; }

        public GMapMarkerPlane(int which, PointLatLng p, float heading, float cog, float nav_bearing, float target,
            float radius, bool isVtol = false)
            : base(p)
        {
            this.heading = heading;
            this.cog = cog;
            this.target = target;
            this.nav_bearing = nav_bearing;
            this.radius = radius;
            this.which = which;
            IsVtol = isVtol;
            Size = FixedWingGlowIcon.Size;
            Offset = new Point(-Size.Width / 2, -Size.Height / 2);
        }

        public float Cog { get => cog; set => cog = value; }
        public float Heading { get => heading; set => heading = value; }
        public float Nav_bearing { get => nav_bearing; set => nav_bearing = value; }
        public float Radius { get => radius; set => radius = value; }
        public float Target { get => target; set => target = value; }

        public override void OnRender(IGraphics g)
        {
            if(IsHidden)
            {
                return;
            }

            var temp = g.Transform;
            g.TranslateTransform(LocalPosition.X, LocalPosition.Y);

            g.RotateTransform(-Overlay.Control.Bearing);

            if (IsActive)
            {
                using (var activeRing = new Pen(Color.FromArgb(45, 169, 220), 2F))
                    g.DrawArc(activeRing, -28, -28, 56, 56, 0, 360);
            }

            // anti NaN
            try
            {
                if (DisplayHeading)
                    g.DrawLine(new Pen(Color.Red, 2), 0.0f, 0.0f,
                        (float) Math.Cos((heading - 90) * MathHelper.deg2rad) * length,
                        (float) Math.Sin((heading - 90) * MathHelper.deg2rad) * length);
            }
            catch
            {
            }

            if (DisplayNavBearing)
                g.DrawLine(new Pen(Color.Green, 2), 0.0f, 0.0f,
                    (float) Math.Cos((nav_bearing - 90) * MathHelper.deg2rad) * length,
                    (float) Math.Sin((nav_bearing - 90) * MathHelper.deg2rad) * length);
            if (DisplayCOG)
                g.DrawLine(new Pen(Color.Black, 2), 0.0f, 0.0f,
                    (float) Math.Cos((cog - 90) * MathHelper.deg2rad) * length,
                    (float) Math.Sin((cog - 90) * MathHelper.deg2rad) * length);
            if (DisplayTarget)
                g.DrawLine(new Pen(Color.Orange, 2), 0.0f, 0.0f,
                    (float) Math.Cos((target - 90) * MathHelper.deg2rad) * length,
                    (float) Math.Sin((target - 90) * MathHelper.deg2rad) * length);
            // anti NaN
            try
            {
                if (DisplayRadius)
                {
                    float desired_lead_dist = 100;

                    double width =
                        (Overlay.Control.MapProvider.Projection.GetDistance(Overlay.Control.FromLocalToLatLng(0, 0),
                             Overlay.Control.FromLocalToLatLng(Overlay.Control.Width, 0)) * 1000.0);
                    double m2pixelwidth = Overlay.Control.Width / width;

                    float alpha = (float) (((desired_lead_dist * (float) m2pixelwidth) / radius) * MathHelper.rad2deg);

                    var scaledradius = radius * (float) m2pixelwidth;

                    if (radius < -1 && alpha < -1)
                    {
                        // fixme 

                        float p1 = (float) Math.Cos((cog) * MathHelper.deg2rad) * scaledradius + scaledradius;

                        float p2 = (float) Math.Sin((cog) * MathHelper.deg2rad) * scaledradius + scaledradius;

                        g.DrawArc(new Pen(Color.HotPink, 2), p1, p2, Math.Abs(scaledradius) * 2,
                            Math.Abs(scaledradius) * 2, cog, alpha);
                    }

                    else if (radius > 1 && alpha > 1)
                    {
                        // correct

                        float p1 = (float) Math.Cos((cog - 180) * MathHelper.deg2rad) * scaledradius + scaledradius;

                        float p2 = (float) Math.Sin((cog - 180) * MathHelper.deg2rad) * scaledradius + scaledradius;

                        g.DrawArc(new Pen(Color.HotPink, 2), -p1, -p2, scaledradius * 2, scaledradius * 2, cog - 180,
                            alpha);
                    }
                }
            }
            catch
            {
            }

            try
            {
                g.RotateTransform(heading);
            }
            catch
            {
            }

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            var fmtIcon = IsVtol
                ? (IsActive ? VtolGlowIcon : VtolIcon)
                : (IsActive ? FixedWingGlowIcon : FixedWingIcon);
            g.DrawImage(fmtIcon, -fmtIcon.Width / 2, -fmtIcon.Height / 2, fmtIcon.Width, fmtIcon.Height);

            g.Transform = temp;
        }

        private static Bitmap LoadFmtIcon(string fileName)
        {
            var stream = typeof(GMapMarkerPlane).GetTypeInfo().Assembly
                .GetManifestResourceStream("MissionPlanner.Maps." + fileName);
            if (stream == null)
                return global::MissionPlanner.Maps.Resources.planeicon;
            using (stream)
            using (var source = new Bitmap(stream))
                return new Bitmap(source);
        }
    }
}
