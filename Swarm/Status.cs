using System.Windows.Forms;

namespace MissionPlanner.Swarm
{
    public partial class Status : UserControl
    {
        public Label Armed
        {
            get { return this.lbl_armed; }
        }

        public Label GPS
        {
            get { return this.lbl_gps; }
        }

        public Label Mode
        {
            get { return this.lbl_mode; }
        }

        public Label MAV
        {
            get { return this.lbl_mav; }
        }

        public Label Guided
        {
            get { return this.lbl_guided; }
        }

        public Label Location1
        {
            get { return this.lbl_loc; }
        }

        public Label Speed
        {
            get { return this.lbl_spd; }
        }

        public Status()
        {
            InitializeComponent();
        }

        public void ApplyTraditionalChinese()
        {
            label1.Text = "GPS";
            label2.Text = "解鎖";
            label3.Text = "模式";
            label4.Text = "導引目標";
            label6.Text = "位置";
            label8.Text = "速度";
        }

        public void ApplyFormationLayout()
        {
            ApplyTraditionalChinese();
            BackColor = System.Drawing.Color.FromArgb(30, 48, 58);
            ForeColor = System.Drawing.Color.WhiteSmoke;
            Size = new System.Drawing.Size(258, 235);
            Margin = new Padding(3, 3, 3, 8);
            BorderStyle = BorderStyle.FixedSingle;
            lbl_mav.SetBounds(8, 8, 240, 24);
            lbl_mav.AutoSize = false;
            var captions = new[] { label2, label3, label1, label4, label6, label8 };
            var values = new[] { lbl_armed, lbl_mode, lbl_gps, lbl_guided, lbl_loc, lbl_spd };
            for (int i = 0; i < captions.Length; i++)
            {
                captions[i].AutoSize = values[i].AutoSize = false;
                captions[i].SetBounds(8, 36 + i * 32, 62, 30);
                values[i].SetBounds(72, 36 + i * 32, 178, 30);
                values[i].AutoEllipsis = true;
            }
        }
    }
}
