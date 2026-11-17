using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Tidy Lite")]
[assembly: AssemblyDescription("See what is slowing your PC down and what is filling your drives. Native, tiny, no web runtime.")]
[assembly: AssemblyCompany("248Tech LLC")]
[assembly: AssemblyProduct("Tidy Lite")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Joe Wayne / 248Tech LLC. MIT License.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace TidyLite
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Ui.S = g.DpiX / 96f;
            Theme.Load();
            var form = new MainForm();
            for (int i = 0; i < args.Length - 1; i++)
            {
                int n;
                if (args[i] == "--page" && int.TryParse(args[i + 1], out n) && n >= 1 && n <= 4) form.StartPage = n - 1;
                if (args[i] == "--scan") { form.StartPage = 2; form.StartScan = args[i + 1]; }
            }
            Application.Run(form);
        }

        public static void DrawLogo(Graphics g, RectangleF r)
        {
            using (var p = Theme.Round(r, r.Width * 0.28f))
            using (var b = new LinearGradientBrush(r, Color.FromArgb(139, 123, 255), Color.FromArgb(61, 214, 196), 45f))
                g.FillPath(b, p);
            using (var pen = new Pen(Color.White, r.Width * 0.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[] {
                    new PointF(r.X + r.Width * 0.3f, r.Y + r.Height * 0.53f),
                    new PointF(r.X + r.Width * 0.44f, r.Y + r.Height * 0.67f),
                    new PointF(r.X + r.Width * 0.71f, r.Y + r.Height * 0.37f) });
        }

        public static void Avatar(Graphics g, RectangleF r, string name)
        {
            int[] hues = { 0x8b7bff, 0x5fa8ff, 0x3dd6c4, 0xf472b6, 0xfb923c, 0x34d399 };
            int sum = 0;
            foreach (char c in name ?? "?") sum += c;
            Color col = Color.FromArgb(hues[sum % hues.Length] | unchecked((int)0xFF000000));
            Theme.Card(g, r, col, r.Width * 0.28f, false);
            Theme.Str(g, string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpper(), Theme.F(11f, FontStyle.Bold), Color.White, r, false, true);
        }

        static Icon icon;
        public static Icon AppIcon()
        {
            if (icon != null) return icon;
            try { icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            return icon;
        }
    }
}
