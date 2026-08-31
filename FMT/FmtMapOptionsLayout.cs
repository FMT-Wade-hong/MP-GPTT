using System.Drawing;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal static class FmtMapOptionsLayout
    {
        // Logical pixels; the form's normal DPI scaling still applies.
        internal const int RowHeight = 40;

        internal static FlowLayoutPanel Configure(Panel host, Control coordinates, params CheckBox[] choices)
        {
            host.SuspendLayout();
            try
            {
                // The TableLayoutPanel owns the row height. Its child must not
                // grow the row in response to preferred size or resize events.
                host.AutoSize = false;
                host.Dock = DockStyle.Fill;
                host.Margin = Padding.Empty;
                host.Padding = Padding.Empty;
                host.AutoScroll = true;
                var contents = new FlowLayoutPanel
                {
                    Name = "fmtMapOptionsPanel",
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    WrapContents = false,
                    FlowDirection = FlowDirection.LeftToRight,
                    Location = Point.Empty,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left,
                    BackColor = Color.Transparent,
                    Padding = new Padding(4, 1, 4, 0),
                    Margin = Padding.Empty
                };
                coordinates.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                coordinates.Margin = new Padding(0, 0, 12, 0);
                contents.Controls.Add(coordinates);
                foreach (var choice in choices)
                {
                    choice.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                    choice.AutoSize = true;
                    choice.Margin = new Padding(0, 2, 18, 0);
                    choice.Padding = Padding.Empty;
                    choice.TextAlign = ContentAlignment.MiddleLeft;
                    contents.Controls.Add(choice);
                }
                host.Controls.Add(contents);
                contents.BringToFront();
                return contents;
            }
            finally { host.ResumeLayout(true); }
        }
    }
}
