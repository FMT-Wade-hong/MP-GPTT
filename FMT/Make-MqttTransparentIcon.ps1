param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
# Local processing explicitly approved by the user. No API/image-generation call.
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class MqttIconMatte
{
    static double Clamp(double value) { return Math.Max(0, Math.Min(1, value)); }
    public static void Run(string root)
    {
        string source = Path.Combine(root, "FMT/Assets/mqtt-social-720.jpg");
        string output = Path.Combine(root, "FMT/Assets/mqtt-toolbar-transparent.png");
        string previews = Path.Combine(root, "tmp/mqtt-icon");
        Directory.CreateDirectory(previews);
        using (var input = new Bitmap(source))
        using (var matte = new Bitmap(input.Width, input.Height, PixelFormat.Format32bppArgb))
        {
            if (input.Width != 720 || input.Height != 480) throw new InvalidDataException("Expected the original 720x480 MQTT image.");
            int w = input.Width, h = input.Height;
            var pixels = new Color[w, h];
            var blue = new bool[w, h];
            var white = new bool[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = input.GetPixel(x, y);
                    pixels[x, y] = c;
                    // Foreground cores: saturated cyan clouds/digits and the white wordmark.
                    blue[x, y] = c.B - c.R > 90 && c.G - c.R > 35;
                    white[x, y] = y >= 275 && y <= 405 && x >= 145 && x <= 605 &&
                        c.R >= 245 && c.G >= 245 && c.B >= 232;
                }
            int left = w, top = h, right = 0, bottom = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color p = pixels[x, y];
                    double alpha = 0;
                    Color foreground = Color.Transparent;
                    if (blue[x, y] || white[x, y]) { alpha = 1; foreground = p; }
                    else
                    {
                        int nearest = int.MaxValue;
                        // JPEG anti-aliasing has a narrow edge. Do not retain broad gray shadows
                        // or background noise: only permit partial alpha next to a solid core.
                        for (int dy = -2; dy <= 2; dy++)
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                int nx = x + dx, ny = y + dy, distance = dx * dx + dy * dy;
                                if (nx < 0 || nx >= w || ny < 0 || ny >= h || distance >= nearest) continue;
                                Color candidate = pixels[nx, ny];
                                if (blue[nx, ny] && p.B - p.R > 28)
                                {
                                    alpha = Clamp((p.B - p.R - 25.0) / (candidate.B - candidate.R - 25.0));
                                    foreground = candidate; nearest = distance;
                                }
                                else if (white[nx, ny] && p.R > 205 && p.G > 205 && p.B > 205)
                                {
                                    alpha = Clamp((Math.Min(p.R, p.G) - 205.0) / (Math.Min(candidate.R, candidate.G) - 205.0));
                                    foreground = candidate; nearest = distance;
                                }
                            }
                    }
                    int a = alpha < 0.04 ? 0 : (int)Math.Round(alpha * 255);
                    matte.SetPixel(x, y, a == 0 ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(a, foreground));
                    if (a > 0) { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
                }
            if (right <= left) throw new InvalidDataException("No foreground found.");
            // Tight framing improves readability in the existing 42x28 toolbar slot.
            var crop = Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
            using (var result = new Bitmap(crop.Width + 12, crop.Height + 12, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(result))
                {
                    g.Clear(Color.Transparent);
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(matte, new Rectangle(6, 6, crop.Width, crop.Height), crop, GraphicsUnit.Pixel);
                }
                result.Save(output, ImageFormat.Png);
                Console.WriteLine("RGBA asset: " + output + " (" + result.Width + "x" + result.Height + ")");
                Preview(result, Color.FromArgb(24, 24, 24), Path.Combine(previews, "mqtt-on-dark.png"));
                Preview(result, Color.FromArgb(225, 230, 235), Path.Combine(previews, "mqtt-on-light.png"));
            }
        }
    }
    static void Preview(Bitmap icon, Color background, string path)
    {
        using (var canvas = new Bitmap(icon.Width + 32, icon.Height + 32))
        using (var g = Graphics.FromImage(canvas))
        {
            g.Clear(background);
            g.DrawImageUnscaled(icon, 16, 16);
            canvas.Save(path, ImageFormat.Png);
        }
    }
}
'@
[MqttIconMatte]::Run($ProjectRoot)
