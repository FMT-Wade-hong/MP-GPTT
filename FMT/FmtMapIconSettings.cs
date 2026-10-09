using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using MissionPlanner.Maps;
using MissionPlanner.Utilities;
using Newtonsoft.Json;

namespace MissionPlanner.FMT
{
    // Local display preferences only: never access MAVLink parameters or commands.
    internal static class FmtMapIconSettings
    {
        internal static readonly string[] Kinds = { "Plane", "Copter", "Heli", "VTOL", "Boat", "Rover" };
        internal static readonly string[] Names = { "固定翼", "多旋翼", "直升機", "VTOL", "船", "車" };
        internal const string Key = "FMT_MapIcons_V1";
        internal sealed class Choice
        {
            public string Style = "Default";
            public int Size = 64;
            public int Opacity = 100;
            public int Rotation;
            public int ColorArgb = Color.DeepSkyBlue.ToArgb();
            public string Png;
        }
        private static Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();
        internal static Dictionary<string, Choice> Load()
        {
            Dictionary<string, Choice> result;
            try { result = JsonConvert.DeserializeObject<Dictionary<string, Choice>>(Settings.Instance[Key] ?? "{}"); }
            catch { result = null; }
            result = result ?? new Dictionary<string, Choice>();
            foreach (var kind in Kinds)
                if (!result.ContainsKey(kind) || result[kind] == null) result[kind] = new Choice();
            return result;
        }
        internal static void Initialize() { Apply(Load(), false); }
        internal static void Apply(Dictionary<string, Choice> choices, bool save)
        {
            var next = new Dictionary<string, Bitmap>();
            foreach (var kind in Kinds)
            {
                var c = choices[kind];
                try {
                    var bitmap = Render(c, false);
                    if (bitmap != null) { next[kind] = bitmap; next[kind + "Dim"] = Render(c, true); }
                } catch {
                    if (save) {
                        foreach(var image in next.Values) image?.Dispose();
                        throw new InvalidDataException("自訂圖片無效，請重新匯入 PNG 或恢復預設。");
                    }
                    // Invalid persisted local images fall back to the original marker.
                }
            }
            if (save) { Settings.Instance[Key] = JsonConvert.SerializeObject(choices); Settings.Instance.Save(); }
            lock (GMapMarkerBase.CustomIconLock)
            {
                foreach (var image in cache.Values) image?.Dispose();
                cache = next;
                GMapMarkerBase.CustomIconProvider = (kind, active) =>
                {
                    Bitmap image;
                    var suffix = !active && GMapMarkerBase.InactiveDisplayStyle == GMapMarkerBase.InactiveDisplayStyleEnum.Transparent ? "Dim" : "";
                    return cache.TryGetValue(kind + suffix, out image) ? image : null;
                };
            }
        }
        internal static Bitmap Render(Choice c, bool dim)
        {
            if (c.Style == "Default") return null;
            int size = Math.Max(24, Math.Min(160, c.Size));
            using (var source = new Bitmap(192, 192))
            {
                using (var g = Graphics.FromImage(source))
                using (var pen = new Pen(Color.FromArgb(c.ColorArgb), 9))
                using (var brush = new SolidBrush(Color.FromArgb(c.ColorArgb)))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TranslateTransform(96, 96);
                    g.RotateTransform(c.Rotation);
                    if (c.Style == "PNG")
                    {
                        var bytes = Convert.FromBase64String(c.Png ?? "");
                        if (bytes.Length > 2 * 1024 * 1024) throw new InvalidDataException("Image too large");
                        using (var stream = new MemoryStream(bytes))
                        using (var img = Image.FromStream(stream))
                        {
                            if (img.Width > 2048 || img.Height > 2048) throw new InvalidDataException("Image too large");
                            float ratio = 150f / Math.Max(img.Width, img.Height);
                            g.DrawImage(img, -img.Width * ratio / 2, -img.Height * ratio / 2, img.Width * ratio, img.Height * ratio);
                        }
                    }
                    else if (c.Style == "Boat")
                    {
                        g.FillPolygon(brush, new[] { new Point(0,-75), new Point(40,-15), new Point(32,65), new Point(-32,65), new Point(-40,-15) });
                        using (var cabin = new SolidBrush(Color.White)) g.FillRectangle(cabin, -15,-10,30,35);
                    }
                    else if (c.Style == "Rover")
                    {
                        g.FillRectangle(brush,-32,-65,64,130);
                        foreach (int y in new[] {-45,35}) { g.FillRectangle(brush,-48,y,16,30); g.FillRectangle(brush,32,y,16,30); }
                        using(var glass = new SolidBrush(Color.White)) g.FillRectangle(glass,-23,-42,46,18);
                    }
                    else if (c.Style == "Copter")
                    {
                        g.DrawLine(pen,-48,-48,48,48); g.DrawLine(pen,48,-48,-48,48);
                        foreach(int x in new[] {-48,48}) foreach(int y in new[] {-48,48}) g.DrawEllipse(pen,x-22,y-22,44,44);
                        g.FillPolygon(brush,new[] {new Point(0,-35),new Point(17,16),new Point(-17,16)});
                    }
                    else if (c.Style == "Heli")
                    {
                        g.FillEllipse(brush,-20,-48,40,72); g.DrawLine(pen,0,15,0,72);
                        g.DrawLine(pen,-65,-10,65,-10); g.DrawLine(pen,0,-70,0,48); g.DrawLine(pen,-20,65,20,65);
                    }
                    else
                    {
                        g.FillPolygon(brush,new[] {new Point(0,-78),new Point(12,-10),new Point(74,26),new Point(74,38),new Point(12,23),new Point(9,56),new Point(30,70),new Point(-30,70),new Point(-9,56),new Point(-12,23),new Point(-74,38),new Point(-74,26),new Point(-12,-10)});
                        if(c.Style == "VTOL") foreach(int x in new[] {-48,48}) { g.DrawEllipse(pen,x-18,-35,36,36); g.DrawEllipse(pen,x-18,25,36,36); }
                    }
                }
                var result = new Bitmap(size, size);
                using (var g = Graphics.FromImage(result))
                using (var attr = new ImageAttributes())
                {
                    attr.SetColorMatrix(new ColorMatrix { Matrix33 = Math.Max(10,Math.Min(100,c.Opacity)) / 100f * (dim ? 0.39f : 1f) });
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(source,new Rectangle(0,0,size,size),0,0,192,192,GraphicsUnit.Pixel,attr);
                }
                return result;
            }
        }
    }

    internal sealed class FmtMapIconSettingsForm : Form
    {
        private readonly Dictionary<string, FmtMapIconSettings.Choice> choices = FmtMapIconSettings.Load();
        private readonly ComboBox kind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox style = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly NumericUpDown size = new NumericUpDown { Minimum=24, Maximum=160 };
        private readonly NumericUpDown opacity = new NumericUpDown { Minimum=10, Maximum=100 };
        private readonly NumericUpDown rotation = new NumericUpDown { Minimum=-180, Maximum=180 };
        private readonly PictureBox preview = new PictureBox { Dock=DockStyle.Fill, SizeMode=PictureBoxSizeMode.CenterImage, BackColor=Color.FromArgb(28,45,55) };
        private readonly string[] styles = { "Default", "Plane", "Copter", "Heli", "VTOL", "Boat", "Rover", "PNG" };
        private bool loading;
        private readonly Button color = new Button { Text="選擇內建圖示顏色", Dock=DockStyle.Fill };
        private FmtMapIconSettings.Choice Current => choices[FmtMapIconSettings.Kinds[kind.SelectedIndex]];
        internal FmtMapIconSettingsForm()
        {
            Text="地圖載具圖示｜本機自訂"; ClientSize=new Size(760,500); MinimumSize=new Size(680,480);
            StartPosition=FormStartPosition.CenterParent; FmtBranding.ApplyApplicationIcon(this);
            var panel = new TableLayoutPanel { Dock=DockStyle.Left, Width=365, ColumnCount=2, RowCount=10, Padding=new Padding(12) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            for(int i=0;i<9;i++) panel.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            kind.Items.AddRange(FmtMapIconSettings.Names);
            style.Items.AddRange(new[] {"原始圖示（預設）","固定翼","多旋翼","直升機","VTOL","船","車","自訂 PNG"});
            AddRow(panel,0,"套用載具類型",kind); AddRow(panel,1,"圖示樣式",style); AddRow(panel,2,"大小（像素）",size);
            AddRow(panel,3,"不透明度（%）",opacity); AddRow(panel,4,"方向修正（度）",rotation);
            color.Click += (s,e) => { using(var dialog = new ColorDialog {Color=Color.FromArgb(Current.ColorArgb)}) if(dialog.ShowDialog(this)==DialogResult.OK) { Current.ColorArgb=dialog.Color.ToArgb(); UpdatePreview(); } };
            panel.Controls.Add(color,0,5); panel.SetColumnSpan(color,2);
            var import = new Button { Text="匯入 PNG（朝上為前方）", Dock=DockStyle.Fill };
            import.Click += Import; panel.Controls.Add(import,0,6); panel.SetColumnSpan(import,2);
            var reset = new Button {Text="恢復此類型預設",Dock=DockStyle.Fill};
            reset.Click += (s,e) => { choices[FmtMapIconSettings.Kinds[kind.SelectedIndex]]=new FmtMapIconSettings.Choice(); LoadChoice(); };
            panel.Controls.Add(reset,0,7); panel.SetColumnSpan(reset,2);
            var apply = new Button {Text="套用並儲存",Dock=DockStyle.Fill};
            apply.Click += (s,e) => { try { FmtMapIconSettings.Apply(choices,true); DialogResult=DialogResult.OK; Close(); } catch(Exception ex) { MessageBox.Show(this,ex.Message,"無法儲存"); } };
            panel.Controls.Add(apply,0,8); panel.SetColumnSpan(apply,2);
            var note = new Label { Text="只改本機地圖顯示，不改飛控機型或參數。\r\n依飛控回報類型套用；關閉視窗不儲存。\r\nPNG 保留原色，內建圖示可改色。", Dock=DockStyle.Fill };
            panel.Controls.Add(note,0,9); panel.SetColumnSpan(note,2);
            Controls.Add(preview); Controls.Add(panel);
            kind.SelectedIndexChanged += (s,e) => LoadChoice();
            style.SelectedIndexChanged += (s,e) => Changed(); size.ValueChanged += (s,e) => Changed();
            opacity.ValueChanged += (s,e) => Changed(); rotation.ValueChanged += (s,e) => Changed();
            kind.SelectedIndex=0;
            FormClosed += (s,e) => preview.Image?.Dispose();
        }
        private static void AddRow(TableLayoutPanel panel,int row,string text,Control control)
        { panel.Controls.Add(new Label {Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row); control.Dock=DockStyle.Fill; panel.Controls.Add(control,1,row); }
        private void LoadChoice()
        {
            loading=true; var c=Current;
            style.SelectedIndex=Math.Max(0,Array.IndexOf(styles,c.Style));
            size.Value=Math.Max(24,Math.Min(160,c.Size)); opacity.Value=Math.Max(10,Math.Min(100,c.Opacity)); rotation.Value=Math.Max(-180,Math.Min(180,c.Rotation));
            loading=false; UpdatePreview();
        }
        private void Changed()
        {
            if(loading || kind.SelectedIndex<0 || style.SelectedIndex<0) return;
            Current.Style=styles[style.SelectedIndex]; Current.Size=(int)size.Value; Current.Opacity=(int)opacity.Value; Current.Rotation=(int)rotation.Value; UpdatePreview();
        }
        private void UpdatePreview()
        {
            Image next=null;
            var isDefault = Current.Style == "Default";
            size.Enabled = opacity.Enabled = rotation.Enabled = !isDefault;
            color.Enabled = !isDefault && Current.Style != "PNG";
            var defaultLabel = kind.SelectedIndex == 0 ? "飛貓飛機（預設）" : kind.SelectedIndex == 4 ? "木船藍帆（預設）" : "原始圖示（預設）";
            if (!Equals(style.Items[0], defaultLabel)) style.Items[0] = defaultLabel;
            try {
                if (isDefault && kind.SelectedIndex == 4) next = GMapMarkerBoat.CreateDefaultPreview();
                else if (isDefault && (kind.SelectedIndex == 0 || kind.SelectedIndex == 3)) next = GMapMarkerPlane.CreateDefaultPreview(kind.SelectedIndex == 3);
                else next=FmtMapIconSettings.Render(Current,false);
            }
            catch { }
            var old=preview.Image; preview.Image=next; old?.Dispose();
            Text="地圖載具圖示｜"+FmtMapIconSettings.Names[kind.SelectedIndex]+(Current.Style=="Default" ? "（保留原始圖示）" : next==null ? "（請匯入有效 PNG）" : "");
        }
        private void Import(object sender,EventArgs e)
        {
            using(var dialog=new OpenFileDialog {Filter="PNG 圖片|*.png",CheckFileExists=true})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK) return;
                try {
                    var info=new FileInfo(dialog.FileName); if(info.Length>2*1024*1024) throw new InvalidDataException("PNG 不可超過 2 MB。");
                    using(var image=Image.FromFile(dialog.FileName))
                    {
                        if(image.RawFormat.Guid!=ImageFormat.Png.Guid || image.Width>2048 || image.Height>2048) throw new InvalidDataException("請使用最大 2048 × 2048 的 PNG。");
                        using(var bitmap=new Bitmap(256,256))
                        using(var g=Graphics.FromImage(bitmap))
                        using(var stream=new MemoryStream())
                        {
                            float ratio=256f/Math.Max(image.Width,image.Height);
                            g.DrawImage(image,(256-image.Width*ratio)/2,(256-image.Height*ratio)/2,image.Width*ratio,image.Height*ratio);
                            bitmap.Save(stream,ImageFormat.Png); Current.Png=Convert.ToBase64String(stream.ToArray());
                        }
                    }
                    style.SelectedIndex=7; Changed();
                } catch(Exception ex) { MessageBox.Show(this,ex.Message,"匯入失敗"); }
            }
        }
    }
}
