using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace MissionPlanner
{
    public partial class Splash : Form
    {
        public Splash()
        {
            InitializeComponent();

            Text = FMT.FmtAuthentication.ProductTitle;
            AutoScaleMode = AutoScaleMode.None;
            MinimumSize = Size.Empty;
            MaximumSize = Size.Empty;
            ClientSize = new Size(920, 532);
            BackColor = Color.Black;
            BackgroundImage = FMT.FmtVisualAssets.LoadClientBackground(FMT.FmtVisualAssets.SplashBackground, 86);
            BackgroundImageLayout = ImageLayout.Stretch;

            // Product, company and version are part of the approved FMT splash artwork.
            // Hide the legacy Mission Planner overlays so the design stays uncluttered.
            label1.Visible = false;
            TXT_version.Visible = false;
            pictureBox1.Visible = false;

            string strVersion = typeof(Splash).GetType().Assembly.GetName().Version.ToString();

            Console.WriteLine(strVersion);

            Console.WriteLine("Splash .ctor");
        }
    }
}
