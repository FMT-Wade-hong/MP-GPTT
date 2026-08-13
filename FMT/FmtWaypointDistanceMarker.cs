using GMap.NET;
using GMap.NET.WindowsForms;
using System.Drawing;

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

        public override void OnRender(System.IGraphics graphics)
        {
            var size = graphics.MeasureString(text, Font);
            var width = (int)size.Width + 12;
            var height = (int)size.Height + 6;
            var x = LocalPosition.X - width / 2;
            var y = LocalPosition.Y - height / 2;
            var rectangle = new Rectangle(x, y, width, height);

            graphics.FillRectangle(Background, rectangle);
            graphics.DrawRectangle(Border, rectangle);
            graphics.DrawString(text, Font, Brushes.White, new PointF(x + 6, y + 3));
        }
    }
}
