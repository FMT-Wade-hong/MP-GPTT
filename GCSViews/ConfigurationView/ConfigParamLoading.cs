using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner.Controls;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigParamLoading : UserControl, IActivate, IDeactivate
    {
        public bool gotAllParams
        {
            get
            {
                if (MainV2.comPort.MAV.param.TotalReported <= 0 ||
                    MainV2.comPort.MAV.param.TotalReceived < MainV2.comPort.MAV.param.TotalReported)
                {
                    return false;
                }

                return true;
            }
        }

        public ConfigParamLoading()
        {
            InitializeComponent();
            but_forceparams.Text = "重新載入全部參數";
            but_forceparams.AutoSize = true;
            label1.Text = "初始參數尚未完整載入，部分設定頁暫時無法使用。\r\n若載入中斷，請按下方按鈕重新載入全部參數。";
            label1.AutoSize = true;
        }

        public void Activate()
        {
            timer1.Start();
        }

        public void Deactivate()
        {
            timer1.Stop();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (gotAllParams)
                MainV2.View.Reload();
        }

        private void but_forceparams_Click(object sender, EventArgs e)
        {
            timer1.Stop();
            try { FmtPageParameterRefresh.ReloadAll(this, but_forceparams, () => MainV2.View.Reload()); }
            finally { if (!IsDisposed && Visible) timer1.Start(); }
        }
    }
}
