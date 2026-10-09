using System;
using System.Drawing;
using GMap.NET;
using GMap.NET.WindowsForms;
using MissionPlanner.Utilities;

namespace MissionPlanner.Maps
{
    [Serializable]
    public class GMapMarkerBoat : GMapMarkerBase
    {
        private static readonly Bitmap DefaultIcon = LoadDefaultIcon();
        public static Bitmap CreateDefaultPreview() { return new Bitmap(DefaultIcon); }
        private static Bitmap LoadDefaultIcon()
        {
            using (var stream = typeof(GMapMarkerBoat).Assembly.GetManifestResourceStream("MissionPlanner.Maps.FMTMapBoat.png"))
            {
                if (stream == null) return new Bitmap(Resources.boat);
                using (var source = new Bitmap(stream))
                {
                    var result = new Bitmap(64, 64);
                    using (var graphics = Graphics.FromImage(result))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(source, new Rectangle(0, 0, 64, 64));
                    }
                    return result;
                }
            }
        }
        static readonly System.Drawing.Size SizeSt =
            DefaultIcon.Size;

        float heading = 0;
        float cog = -1;
        float target = -1;
        float nav_bearing = -1;

        public GMapMarkerBoat(PointLatLng p, float heading, float cog, float nav_bearing, float target)
            : base(p)
        {
            this.heading = heading;
            this.cog = cog;
            this.target = target;
            this.nav_bearing = nav_bearing;
            Size = SizeSt;
        }

        public override void OnRender(IGraphics g)
        {
            if(IsHidden)
            {
                return;
            }
            
            var temp = g.Transform;
            g.TranslateTransform(LocalPosition.X, LocalPosition.Y);

            g.RotateTransform(-Overlay.Control.Bearing);

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
                g.RotateTransform(heading);
            }
            catch
            {
            }

            if (!DrawCustomIcon(g, "Boat"))
            {
#if NET472_OR_GREATER
            var img = DefaultIcon;
            using (var ia = new System.Drawing.Imaging.ImageAttributes())
            {
            if(IsTransparent)
            {
                // Draw image with transparency using a color matrix
                var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.39f };
                ia.SetColorMatrix(cm, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
            }
            g.DrawImage(img, new Rectangle(-img.Width / 2, -img.Height / 2, img.Width, img.Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
            }
#else
            g.DrawImageUnscaled(DefaultIcon,
                Size.Width / -2,
                Size.Height / -2);
#endif
            }

            g.Transform = temp;
        }
    }
}
