using GMap.NET;
using GMap.NET.WindowsForms;
using System.Drawing;
using System.Globalization;

namespace MissionPlanner.FMT
{
    internal sealed class FmtWaypointDistanceMarker : GMapMarker
    {
        private readonly string text;
        private static readonly Font Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        private static readonly Brush Background = new SolidBrush(Color.FromArgb(220, 41, 171, 226));
        private static readonly Pen Border = new Pen(Color.White, 1);

        internal FmtWaypointDistanceMarker(PointLatLng position, string text) : base(position)
        {
            this.text = text;
            IsHitTestVisible = false;
        }

        internal static string FormatDistance(double metres)
        {
            return "<-" + metres.ToString("0", CultureInfo.InvariantCulture) + "M->";
        }

        internal static Rectangle LabelBounds(Point midpoint, int width, int height)
        {
            // The insertion '+' occupies the segment midpoint; leave a clear gap above it.
            return new Rectangle(midpoint.X - width / 2, midpoint.Y - height - 16, width, height);
        }

        public override void OnRender(System.IGraphics graphics)
        {
            var size = graphics.MeasureString(text, Font);
            var width = (int)size.Width + 12;
            var height = (int)size.Height + 6;
            var rectangle = LabelBounds(LocalPosition, width, height);
            var x = rectangle.X;
            var y = rectangle.Y;

            graphics.FillRectangle(Background, rectangle);
            graphics.DrawRectangle(Border, rectangle);
            graphics.DrawString(text, Font, Brushes.White, new PointF(x + 6, y + 3));
        }
    }
}
