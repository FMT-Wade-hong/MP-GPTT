using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;

namespace MissionPlanner.Maps
{
    [Serializable]
    public class GMapMarkerWP : GMarkerGoogle
    {
        string wpno = "";
        public bool selected = false;
        SizeF txtsize = SizeF.Empty;
        static Dictionary<string, Bitmap> fontBitmaps = new Dictionary<string, Bitmap>();
        static Font font;
        private static readonly Bitmap homeSymbol = CreateHomeSymbol();

        private static Bitmap CreateHomeSymbol()
        {
            var bitmap = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            using (var pen = new Pen(Color.Yellow, 3.5f))
            using (var letterFont = new Font(FontFamily.GenericSansSerif, 7, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                for (int quadrant = 0; quadrant < 4; quadrant++)
                    g.DrawArc(pen, 10, 10, 44, 44, quadrant * 90 + 13, 64);
                // Bold H, with its centre exactly at the geographic anchor.
                using (var hPen = new Pen(Color.Yellow, 5))
                {
                    g.DrawLine(hPen, 24, 22, 24, 42);
                    g.DrawLine(hPen, 40, 22, 40, 42);
                    g.DrawLine(hPen, 24, 32, 40, 32);
                }
                var directions = new[] { "N", "E", "S", "W" };
                for (int direction = 0; direction < 4; direction++)
                {
                    var saved = g.Save();
                    g.TranslateTransform(32, 32);
                    g.RotateTransform(direction * 90);
                    g.FillPolygon(Brushes.Yellow, new[] { new PointF(0, -31), new PointF(-4, -25), new PointF(0, -27), new PointF(4, -25) });
                    g.Restore(saved);
                    double angle = (direction * 90 - 90) * Math.PI / 180;
                    g.DrawString(directions[direction], letterFont, Brushes.Yellow,
                        32 + (float)Math.Cos(angle) * 22, 32 + (float)Math.Sin(angle) * 22, format);
                }
            }
            return bitmap;
        }

        public GMapMarkerWP(PointLatLng p, string wpno)
            : base(p, GMarkerGoogleType.green)
        {
            this.wpno = wpno;
            if (wpno == "H")
            {
                Size = homeSymbol.Size;
                Offset = new Point(-Size.Width / 2, -Size.Height / 2);
                return;
            }
            if (font == null)
                font = SystemFonts.DefaultFont;

            if (!fontBitmaps.ContainsKey(wpno))
            {
                Bitmap temp = new Bitmap(100,40, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(temp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    txtsize = g.MeasureString(wpno, font);

                    g.DrawString(wpno, font, Brushes.Black, new PointF(0, 0));
                }
                fontBitmaps[wpno] = temp;
            }
        }

        public override void OnRender(IGraphics g)
        {
            if (wpno == "H")
            {
                g.DrawImageUnscaled(homeSymbol, LocalPosition.X, LocalPosition.Y);
                return;
            }
            if (selected)
            {
                g.FillEllipse(Brushes.Red, new Rectangle(this.LocalPosition, this.Size));
                g.DrawArc(Pens.Red, new Rectangle(this.LocalPosition, this.Size), 0, 360);
            }
            
            base.OnRender(g);

            var midw = LocalPosition.X + 10;
            var midh = LocalPosition.Y + 3;

            if (txtsize.Width > 15)
                midw -= 4;

            if (Overlay.Control.Zoom> 16 || IsMouseOver)
                g.DrawImageUnscaled(fontBitmaps[wpno], midw,midh);
        }
    }
}
