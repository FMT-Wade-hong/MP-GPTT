using System.Drawing;
using System.Reflection;

namespace MissionPlanner.FMT
{
    internal static class FmtVisualAssets
    {
        internal const string SplashBackground = "MissionPlanner.FMT.Assets.fmt-splash-v111.png";
        internal const string LoginBackground = "MissionPlanner.FMT.Assets.fmt-login-v111.png";
        internal const string MqttTrafficIcon = "MissionPlanner.FMT.Assets.mqtt-toolbar-transparent.png";

        internal static Bitmap LoadBitmap(string resourceName)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return null;

                using (var source = Image.FromStream(stream))
                    return new Bitmap(source);
            }
        }

        internal static Bitmap LoadClientBackground(string resourceName, int titleBarHeight)
        {
            using (var source = LoadBitmap(resourceName))
            {
                if (source == null)
                    return null;

                var cropTop = System.Math.Max(0, System.Math.Min(titleBarHeight, source.Height - 1));
                var result = new Bitmap(source.Width, source.Height - cropTop);
                using (var graphics = Graphics.FromImage(result))
                {
                    graphics.DrawImage(source,
                        new Rectangle(0, 0, result.Width, result.Height),
                        new Rectangle(0, cropTop, source.Width, source.Height - cropTop),
                        GraphicsUnit.Pixel);
                }

                return result;
            }
        }
    }
}
