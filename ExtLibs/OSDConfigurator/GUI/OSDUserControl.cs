using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OSDConfigurator.Models;

namespace OSDConfigurator.GUI
{
    public partial class OSDUserControl : UserControl
    {
        private OSDConfiguration config;
        
        public IItemCaptionProvider CaptionProvider { get; set; }

        // Index zero is global options; subsequent tabs are individual OSD screens.
        public IEnumerable<string> CurrentParameterNames
        {
            get
            {
                if (config == null) return Enumerable.Empty<string>();
                var index = tabControl.SelectedIndex;
                if (index <= 0) return config.Options.Select(option => option.Name).ToArray();
                if (index > config.Screens.Length) return Enumerable.Empty<string>();
                var screen = config.Screens[index - 1];
                return screen.Options.Concat(screen.Items.SelectMany(item => item.Options))
                    .Select(setting => setting.Name).ToArray();
            }
        }

        public OSDUserControl()
        {
            InitializeComponent();
        }

        public void ApplySettings(IList<IOSDSetting> settings)
        {
            var selectedIndex = tabControl.SelectedIndex;
            ClearOptions();
            ClearScreens();

            ScreenControl.ScreenToCopy = null;

            config = ConfigFactory.Create(settings, Enumerable.Range(1, 6));

            FillGlobalOptions();

            foreach (var scr in config.Screens)
                AddScreen(scr);
            tabControl.SelectedIndex = Math.Max(0, Math.Min(selectedIndex, tabControl.TabPages.Count - 1));
        }
        
        private void FillGlobalOptions()
        {
            foreach(var ctr in OptionControlFactory.Create(config.Options))
            {
                ctr.Dock = DockStyle.Top;
                tabSettings.Controls.Add(ctr);
            }
            
        }

        private void ClearOptions()
        {
            tabSettings.Controls.Clear();
        }
        
        private void AddScreen(OSDScreen screen)
        {
            var screenControl = new ScreenControl(screen, CaptionProvider);
            screenControl.Dock = DockStyle.Fill;

            var tab = new TabPage($"   {screen.Name}   ");
            tab.Controls.Add(screenControl);

            tabControl.TabPages.Add(tab);
        }

        private void ClearScreens()
        {
            while (tabControl.TabPages.Count > 1)
                tabControl.TabPages.RemoveAt(1);
        }
    }
}
