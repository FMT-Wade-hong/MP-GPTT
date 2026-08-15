namespace MissionPlanner.Controls
{
    partial class ElevationProfile
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.zg1 = new ZedGraph.ZedGraphControl();
            this.label1 = new System.Windows.Forms.Label();
            this.summaryPanel = new System.Windows.Forms.Panel();
            this.labelCollisionWarning = new MissionPlanner.Controls.FmtTerrainRiskLabel();
            this.labelTerrainSummary = new System.Windows.Forms.Label();
            this.labelPlanSummary = new System.Windows.Forms.Label();
            this.summaryPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // zg1
            // 
            this.zg1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.zg1.Location = new System.Drawing.Point(0, 88);
            this.zg1.Name = "zg1";
            this.zg1.ScrollGrace = 0D;
            this.zg1.ScrollMaxX = 0D;
            this.zg1.ScrollMaxY = 0D;
            this.zg1.ScrollMaxY2 = 0D;
            this.zg1.ScrollMinX = 0D;
            this.zg1.ScrollMinY = 0D;
            this.zg1.ScrollMinY2 = 0D;
            this.zg1.Size = new System.Drawing.Size(834, 374);
            this.zg1.TabIndex = 30;
            // 
            // label1
            // 
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(834, 28);
            this.label1.TabIndex = 31;
            this.label1.Text = "注意：地形高度資料以約 100 公尺間距取樣，僅供飛行規劃參考，請搭配正式地形與航管資料確認。";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // summaryPanel
            //
            this.summaryPanel.Controls.Add(this.labelCollisionWarning);
            this.summaryPanel.Controls.Add(this.labelTerrainSummary);
            this.summaryPanel.Controls.Add(this.labelPlanSummary);
            this.summaryPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.summaryPanel.Location = new System.Drawing.Point(0, 28);
            this.summaryPanel.Name = "summaryPanel";
            this.summaryPanel.Padding = new System.Windows.Forms.Padding(8, 6, 8, 6);
            this.summaryPanel.Size = new System.Drawing.Size(834, 60);
            this.summaryPanel.TabIndex = 32;
            //
            // labelCollisionWarning
            //
            this.labelCollisionWarning.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelCollisionWarning.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold);
            this.labelCollisionWarning.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelCollisionWarning.Location = new System.Drawing.Point(526, 6);
            this.labelCollisionWarning.Name = "labelCollisionWarning";
            this.labelCollisionWarning.Padding = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.labelCollisionWarning.Size = new System.Drawing.Size(300, 48);
            this.labelCollisionWarning.TabIndex = 2;
            this.labelCollisionWarning.Text = "正在檢查地形淨空…";
            this.labelCollisionWarning.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // labelTerrainSummary
            //
            this.labelTerrainSummary.Dock = System.Windows.Forms.DockStyle.Left;
            this.labelTerrainSummary.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.labelTerrainSummary.ForeColor = System.Drawing.Color.DeepSkyBlue;
            this.labelTerrainSummary.Location = new System.Drawing.Point(266, 6);
            this.labelTerrainSummary.Name = "labelTerrainSummary";
            this.labelTerrainSummary.Padding = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.labelTerrainSummary.Size = new System.Drawing.Size(260, 48);
            this.labelTerrainSummary.TabIndex = 1;
            this.labelTerrainSummary.Text = "地形剖面";
            this.labelTerrainSummary.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // labelPlanSummary
            //
            this.labelPlanSummary.Dock = System.Windows.Forms.DockStyle.Left;
            this.labelPlanSummary.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.labelPlanSummary.ForeColor = System.Drawing.Color.Red;
            this.labelPlanSummary.Location = new System.Drawing.Point(8, 6);
            this.labelPlanSummary.Name = "labelPlanSummary";
            this.labelPlanSummary.Padding = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.labelPlanSummary.Size = new System.Drawing.Size(258, 48);
            this.labelPlanSummary.TabIndex = 0;
            this.labelPlanSummary.Text = "規劃航線";
            this.labelPlanSummary.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // ElevationProfile
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(39)))), ((int)(((byte)(40)))));
            this.ClientSize = new System.Drawing.Size(834, 462);
            this.Controls.Add(this.zg1);
            this.Controls.Add(this.summaryPanel);
            this.Controls.Add(this.label1);
            this.ForeColor = System.Drawing.Color.White;
            this.MinimumSize = new System.Drawing.Size(760, 500);
            this.Name = "ElevationProfile";
            this.Text = "ElevationProfile";
            this.Load += new System.EventHandler(this.ElevationProfile_Load);
            this.summaryPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private ZedGraph.ZedGraphControl zg1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel summaryPanel;
        private System.Windows.Forms.Label labelPlanSummary;
        private System.Windows.Forms.Label labelTerrainSummary;
        private MissionPlanner.Controls.FmtTerrainRiskLabel labelCollisionWarning;
    }
}
