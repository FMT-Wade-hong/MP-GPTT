using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal static class FmtBranding
    {
        // Keep native icon handles alive for the lifetime of the process.
        private static readonly Lazy<Icon> WindowIcon = new Lazy<Icon>(() =>
            (Icon)MissionPlanner.Properties.Resources.mpdesktop.Clone());
        private static readonly Lazy<Icon> SmallWindowIcon = new Lazy<Icon>(() =>
            new Icon(WindowIcon.Value, SystemInformation.SmallIconSize));
        private static readonly Lazy<Icon> LargeWindowIcon = new Lazy<Icon>(() =>
            new Icon(WindowIcon.Value, SystemInformation.IconSize));

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        internal static void ApplyApplicationIcon(Form form)
        {
            if (form == null || MissionPlanner.Properties.Resources.mpdesktop == null)
                return;

            form.ShowIcon = true;
            form.Icon = WindowIcon.Value;
            form.HandleCreated -= RefreshWindowIcon;
            form.HandleCreated += RefreshWindowIcon;
            RefreshWindowIcon(form, EventArgs.Empty);
        }

        private static void RefreshWindowIcon(object sender, EventArgs e)
        {
            var form = sender as Form;
            if (form == null || form.IsDisposed || !form.IsHandleCreated ||
                Environment.OSVersion.Platform != PlatformID.Win32NT) return;

            // Explicitly set both icons used by Windows: title bar and taskbar /
            // Alt-Tab. Reapply after WinForms recreates a window handle.
            const int WM_SETICON = 0x0080;
            SendMessage(form.Handle, WM_SETICON, IntPtr.Zero, SmallWindowIcon.Value.Handle);
            SendMessage(form.Handle, WM_SETICON, new IntPtr(1), LargeWindowIcon.Value.Handle);
        }
    }
}
