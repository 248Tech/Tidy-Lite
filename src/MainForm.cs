using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace TidyLite
{
    internal static class Ui
    {
        public static float S = 1f;
        public static int D(float v) { return (int)Math.Round(v * S); }
        public static float F(float v) { return v * S; }
    }

    internal class MainForm : Form
    {
        public readonly Metrics Metrics = new Metrics();
        public readonly ProcMonitor Procs = new ProcMonitor();
        public readonly Scanner Scanner = new Scanner();
        public List<AppGroup> Groups = new List<AppGroup>();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Sidebar side;
        readonly Panel host;
        readonly Toast toast;
        readonly Page[] pages;
        int current = -1;
        volatile bool sampling;

        public MainForm()
        {
            Text = "Tidy Lite";
            Icon = Program.AppIcon();
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(10f);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Ui.D(1100), Ui.D(720));
            MinimumSize = new Size(Ui.D(880), Ui.D(580));
            DoubleBuffered = true;

            pages = new Page[] { new OverviewPage(this), new AppsPage(this), new StoragePage(this), new CleanPage(this) };
            host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            toast = new Toast { Visible = false };
            side = new Sidebar(this) { Dock = DockStyle.Left, Width = Ui.D(212) };
            Controls.Add(host);
            Controls.Add(side);
            host.Controls.Add(toast);
            foreach (var p in pages) { p.Dock = DockStyle.Fill; p.Visible = false; host.Controls.Add(p); }
            Go(0);

            timer.Interval = 1500;
            timer.Tick += delegate { Sample(); };
            timer.Start();
            Sample();
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D4) { Go(e.KeyCode - Keys.D1); e.Handled = true; }
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitleBar(Handle, Theme.Dark);
        }

        public void Go(int i)
        {
            if (i == current) return;
            current = i;
            for (int k = 0; k < pages.Length; k++) pages[k].Visible = k == i;
            pages[i].BringToFront();
            toast.BringToFront();
            pages[i].OnShown();
            side.Invalidate();
            Sample();
        }

        public int Current { get { return current; } }
        public int StartPage;
        public string StartScan;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Go(StartPage);
            if (StartScan != null) ((StoragePage)pages[2]).Begin(StartScan);
        }

        // Sample metrics + processes off the UI thread, then refresh the visible page.
        void Sample()
        {
            if (sampling || WindowState == FormWindowState.Minimized) return;
            sampling = true;
            bool wantProcs = current == 0 || current == 1;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<AppGroup> gs = null;
                try
                {
                    Metrics.Sample();
                    if (wantProcs) gs = Procs.Snapshot();
                }
                catch { }
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (gs != null) Groups = gs;
                        sampling = false;
                        side.Invalidate();
                        if (current >= 0) pages[current].OnTick();
                    });
                }
                catch { sampling = false; }
            });
        }

        public void Notify(string title, string body, string action = null, Action run = null, bool good = true)
        {
            toast.Show(title, body, action, run, good);
            toast.Location = new Point(host.Width - toast.Width - Ui.D(24), host.Height - toast.Height - Ui.D(24));
            toast.BringToFront();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (toast != null && host != null) toast.Location = new Point(host.Width - toast.Width - Ui.D(24), host.Height - toast.Height - Ui.D(24));
        }
    }

    // ───────── sidebar ─────────
    internal class Sidebar : Control
    {
        readonly MainForm main;
        readonly string[] items = { "Overview", "Apps", "Storage", "Clean up" };
        readonly string[] hints = { "How your PC is doing", "What's running", "What's taking space", "Clear safe clutter" };
        int hover = -1;

        public Sidebar(MainForm m)
        {
            main = m;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        Rectangle Item(int i) { return new Rectangle(Ui.D(12), Ui.D(84) + i * Ui.D(46), Width - Ui.D(24), Ui.D(40)); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1;
            for (int i = 0; i < items.Length; i++) if (Item(i).Contains(e.Location)) h = i;
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            for (int i = 0; i < items.Length; i++) if (Item(i).Contains(e.Location)) main.Go(i);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Side);
            Theme.Smooth(g);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, Width - 1, 0, Width - 1, Height);
            Program.DrawLogo(g, new RectangleF(Ui.F(22), Ui.F(24), Ui.F(30), Ui.F(30)));
            Theme.Str(g, "Tidy Lite", Theme.F(12f, FontStyle.Bold), Theme.Text, new RectangleF(Ui.F(62), Ui.F(20), Width, Ui.F(22)));
            Theme.Str(g, "Native edition", Theme.F(8.25f), Theme.Faint, new RectangleF(Ui.F(62), Ui.F(40), Width - Ui.F(70), Ui.F(18)));

            for (int i = 0; i < items.Length; i++)
            {
                var r = Item(i);
                bool on = main.Current == i;
                if (on) Theme.Card(g, r, Theme.Soft(Theme.Accent), 10, false);
                else if (hover == i) Theme.Card(g, r, Theme.Hover, 10, false);
                if (on)
                    using (var b = new LinearGradientBrush(new Rectangle(0, r.Y, 4, r.Height), Theme.Accent, Theme.Accent2, 90f))
                    using (var p = Theme.Round(new RectangleF(0, r.Y + Ui.F(9), Ui.F(3), r.Height - Ui.F(18)), 2)) g.FillPath(b, p);
                DrawNavIcon(g, i, new RectangleF(r.X + Ui.F(12), r.Y + Ui.F(11), Ui.F(18), Ui.F(18)), on ? Theme.Accent : Theme.Muted);
                Theme.Str(g, items[i], Theme.F(10f, on ? FontStyle.Bold : FontStyle.Regular), on || hover == i ? Theme.Text : Theme.Muted,
                    new RectangleF(r.X + Ui.F(42), r.Y, r.Width - Ui.F(42), r.Height));
            }

            // live mini stats at the bottom
            var m = main.Metrics;
            if (m.MemTotal > 0)
            {
                var card = new RectangleF(Ui.F(14), Height - Ui.F(118), Width - Ui.F(28), Ui.F(100));
                Theme.Card(g, card, Theme.Surface2, 12);
                double mp = 100.0 * m.MemUsed / m.MemTotal;
                Theme.Str(g, "Processor", Theme.F(8.5f), Theme.Muted, new RectangleF(card.X + Ui.F(12), card.Y + Ui.F(8), Ui.F(100), Ui.F(18)));
                Theme.Str(g, m.Cpu.ToString("0") + "%", Theme.F(8.5f, FontStyle.Bold), Theme.Text, new RectangleF(card.X, card.Y + Ui.F(8), card.Width - Ui.F(12), Ui.F(18)), true);
                Theme.Bar(g, new RectangleF(card.X + Ui.F(12), card.Y + Ui.F(30), card.Width - Ui.F(24), Ui.F(4)), m.Cpu / 100, Theme.Accent, Theme.Accent2);
                Theme.Str(g, "Memory", Theme.F(8.5f), Theme.Muted, new RectangleF(card.X + Ui.F(12), card.Y + Ui.F(50), Ui.F(100), Ui.F(18)));
                Theme.Str(g, mp.ToString("0") + "%", Theme.F(8.5f, FontStyle.Bold), Theme.Text, new RectangleF(card.X, card.Y + Ui.F(50), card.Width - Ui.F(12), Ui.F(18)), true);
                Theme.Bar(g, new RectangleF(card.X + Ui.F(12), card.Y + Ui.F(72), card.Width - Ui.F(24), Ui.F(4)), mp / 100, Theme.Accent, Theme.Accent2);
            }
        }

        static void DrawNavIcon(Graphics g, int i, RectangleF r, Color c)
        {
            using (var pen = new Pen(c, Ui.F(1.7f)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float x = r.X, y = r.Y, w = r.Width, h = r.Height;
                switch (i)
                {
                    case 0: // gauge
                        g.DrawArc(pen, x, y + h * 0.1f, w, w, 160, 220);
                        g.DrawLine(pen, x + w / 2, y + h * 0.6f, x + w * 0.78f, y + h * 0.32f);
                        break;
                    case 1: // grid
                        float s = w * 0.42f;
                        using (var p1 = Theme.Round(new RectangleF(x, y, s, s), 2)) g.DrawPath(pen, p1);
                        using (var p2 = Theme.Round(new RectangleF(x + w - s, y, s, s), 2)) g.DrawPath(pen, p2);
                        using (var p3 = Theme.Round(new RectangleF(x, y + h - s, s, s), 2)) g.DrawPath(pen, p3);
                        using (var p4 = Theme.Round(new RectangleF(x + w - s, y + h - s, s, s), 2)) g.DrawPath(pen, p4);
                        break;
                    case 2: // drive
                        using (var p5 = Theme.Round(new RectangleF(x, y + h * 0.25f, w, h * 0.55f), 3)) g.DrawPath(pen, p5);
                        g.DrawLine(pen, x + w * 0.7f, y + h * 0.55f, x + w * 0.72f, y + h * 0.55f);
                        break;
                    default: // sparkle
                        float cx = x + w * 0.45f, cy = y + h * 0.55f, k = w * 0.38f;
                        g.DrawLine(pen, cx, cy - k, cx, cy + k);
                        g.DrawLine(pen, cx - k, cy, cx + k, cy);
                        g.DrawLine(pen, x + w * 0.85f, y, x + w * 0.85f, y + h * 0.3f);
                        g.DrawLine(pen, x + w * 0.7f, y + h * 0.15f, x + w, y + h * 0.15f);
                        break;
                }
            }
        }
    }

    // ───────── toast ─────────
    internal class Toast : Control
    {
        string title, body, action;
        Action run;
        bool good;
        readonly System.Windows.Forms.Timer hide = new System.Windows.Forms.Timer();

        public Toast()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Size = new Size(Ui.D(380), Ui.D(70));
            hide.Tick += delegate { hide.Stop(); Visible = false; };
        }

        public void Show(string t, string b, string a, Action r, bool ok)
        {
            title = t; body = b; action = a; run = r; good = ok;
            hide.Stop(); hide.Interval = a != null ? 9000 : 5000; hide.Start();
            Visible = true; Invalidate();
        }

        Rectangle ActionRect { get { return new Rectangle(Width - Ui.D(86), Ui.D(18), Ui.D(70), Ui.D(34)); } }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (action != null && ActionRect.Contains(e.Location) && run != null) { Visible = false; run(); }
            else Visible = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            Theme.Smooth(g);
            Theme.Card(g, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.Surface2, 14);
            Color c = good ? Theme.Good : Theme.Bad;
            Theme.Card(g, new RectangleF(Ui.F(14), Ui.F(17), Ui.F(36), Ui.F(36)), Theme.Soft(c), 10, false);
            using (var pen = new Pen(c, Ui.F(2.2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                if (good) { g.DrawLine(pen, Ui.F(25), Ui.F(35), Ui.F(30), Ui.F(40)); g.DrawLine(pen, Ui.F(30), Ui.F(40), Ui.F(39), Ui.F(30)); }
                else { g.DrawLine(pen, Ui.F(32), Ui.F(27), Ui.F(32), Ui.F(37)); g.DrawLine(pen, Ui.F(32), Ui.F(42), Ui.F(32), Ui.F(42.5f)); }
            }
            float tw = Width - Ui.F(action != null ? 160 : 76);
            Theme.Str(g, title, Theme.F(10f, FontStyle.Bold), Theme.Text, new RectangleF(Ui.F(62), Ui.F(14), tw, Ui.F(22)));
            Theme.Str(g, body ?? "", Theme.F(9f), Theme.Muted, new RectangleF(Ui.F(62), Ui.F(36), tw, Ui.F(20)));
            if (action != null)
            {
                var r = ActionRect;
                Theme.Card(g, r, Theme.Surface3, 8);
                Theme.Str(g, action, Theme.F(9.5f, FontStyle.Bold), Theme.Text, r, false, true);
            }
        }
    }

    // ───────── page base ─────────
    internal class Page : UserControl
    {
        protected readonly MainForm Main;
        protected string Title, Subtitle;

        public Page(MainForm m)
        {
            Main = m;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        public virtual void OnShown() { }
        public virtual void OnTick() { }

        protected int Pad { get { return Ui.D(32); } }
        protected int HeadH { get { return Ui.D(92); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            Theme.Str(g, Title, Theme.F(19f, FontStyle.Bold), Theme.Text, new RectangleF(Pad - Ui.F(2), Ui.F(22), Width - Pad * 2, Ui.F(40)));
            Theme.Str(g, Subtitle ?? "", Theme.F(10f), Theme.Muted, new RectangleF(Pad, Ui.F(58), Width - Pad * 2, Ui.F(22)));
            PaintBody(g);
        }

        protected virtual void PaintBody(Graphics g) { }

        protected RowList MakeList(int rowH)
        {
            var l = new RowList { RowHeight = Ui.D(rowH), BackColor = Theme.Surface };
            return l;
        }

        protected static void DrawCheck(Graphics g, RectangleF r, bool on)
        {
            if (on)
            {
                Theme.Card(g, r, Theme.Accent, 5, false);
                using (var pen = new Pen(Color.White, Ui.F(2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(pen, r.X + r.Width * 0.25f, r.Y + r.Height * 0.52f, r.X + r.Width * 0.43f, r.Y + r.Height * 0.7f);
                    g.DrawLine(pen, r.X + r.Width * 0.43f, r.Y + r.Height * 0.7f, r.X + r.Width * 0.76f, r.Y + r.Height * 0.32f);
                }
            }
            else using (var p = Theme.Round(r, 5)) using (var pen = new Pen(Theme.Mix(Theme.Border, Theme.Text, 0.25f), Ui.F(1.4f))) g.DrawPath(pen, p);
        }

        protected static void DrawFolder(Graphics g, RectangleF r, Color c)
        {
            Theme.Card(g, r, Theme.Soft(c), 8, false);
            using (var pen = new Pen(c, Ui.F(1.6f)) { LineJoin = LineJoin.Round })
            {
                float x = r.X + r.Width * 0.24f, y = r.Y + r.Height * 0.3f, w = r.Width * 0.52f, h = r.Height * 0.42f;
                var pts = new[] { new PointF(x, y + h), new PointF(x, y), new PointF(x + w * 0.38f, y), new PointF(x + w * 0.5f, y + h * 0.18f), new PointF(x + w, y + h * 0.18f), new PointF(x + w, y + h) };
                g.DrawPolygon(pen, pts);
            }
        }

        protected static void DrawFileGlyph(Graphics g, RectangleF r, Color c)
        {
            Theme.Card(g, r, Theme.Soft(c), 8, false);
            using (var pen = new Pen(c, Ui.F(1.6f)) { LineJoin = LineJoin.Round })
            {
                float x = r.X + r.Width * 0.3f, y = r.Y + r.Height * 0.24f, w = r.Width * 0.4f, h = r.Height * 0.52f;
                g.DrawPolygon(pen, new[] { new PointF(x, y), new PointF(x + w * 0.62f, y), new PointF(x + w, y + h * 0.3f), new PointF(x + w, y + h), new PointF(x, y + h) });
            }
        }
    }

    // ───────── overview ─────────
    internal class OverviewPage : Page
    {
        public OverviewPage(MainForm m) : base(m) { Title = "Overview"; }

        public override void OnTick()
        {
            int h = DateTime.Now.Hour;
            Subtitle = (h < 12 ? "Good morning" : h < 18 ? "Good afternoon" : "Good evening") + ". Your PC has been on for " + Fmt.Uptime(Native.GetTickCount64() / 1000) + ".";
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Y > HeadH + Ui.D(200)) Main.Go(1);
        }

        protected override void PaintBody(Graphics g)
        {
            var m = Main.Metrics;
            if (m.MemTotal == 0) return;
            DriveInfo sys = null;
            try { sys = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)); } catch { }
            double memPct = 100.0 * m.MemUsed / m.MemTotal;
            double diskPct = sys != null && sys.TotalSize > 0 ? 100.0 * (sys.TotalSize - sys.TotalFreeSpace) / sys.TotalSize : 0;

            float gap = Ui.F(16), top = HeadH, cw = (Width - Pad * 2 - gap * 2) / 3f, ch = Ui.F(176);
            Gauge(g, new RectangleF(Pad, top, cw, ch), "Processor", m.Cpu,
                m.Cpu < 25 ? "Relaxed" : m.Cpu < 60 ? "Busy" : m.Cpu < 85 ? "Working hard" : "Maxed out",
                m.Cpu < 60 ? "good" : m.Cpu < 85 ? "warn" : "bad",
                m.Cpu < 25 ? "Taking it easy." : m.Cpu < 60 ? "Working steadily - totally normal." : "Something is keeping it busy.");
            Gauge(g, new RectangleF(Pad + cw + gap, top, cw, ch), "Memory", memPct,
                memPct < 60 ? "Plenty free" : memPct < 80 ? "Comfortable" : memPct < 92 ? "Getting full" : "Almost full",
                memPct < 80 ? "good" : memPct < 92 ? "warn" : "bad",
                Fmt.Bytes(m.MemUsed) + " of " + Fmt.Bytes(m.MemTotal) + " in use.");
            Gauge(g, new RectangleF(Pad + (cw + gap) * 2, top, cw, ch), "Storage " + (sys != null ? sys.Name.TrimEnd('\\') : ""), diskPct,
                diskPct < 75 ? "Roomy" : diskPct < 90 ? "Filling up" : "Nearly full",
                diskPct < 75 ? "good" : diskPct < 90 ? "warn" : "bad",
                sys != null ? Fmt.Bytes(sys.TotalFreeSpace) + " free of " + Fmt.Bytes(sys.TotalSize) + "." : "");

            // heaviest apps
            float ly = top + ch + Ui.F(28);
            Theme.Str(g, "Using the most memory", Theme.F(11.5f, FontStyle.Bold), Theme.Text, new RectangleF(Pad, ly, Ui.F(300), Ui.F(24)));
            Theme.Str(g, "Click to see all apps  >", Theme.F(9f), Theme.Accent, new RectangleF(Pad, ly, Width - Pad * 2, Ui.F(24)), true);
            var apps = Main.Groups.Where(x => x.Kind != "system").Take(6).ToList();
            if (apps.Count == 0) return;
            var card = new RectangleF(Pad, ly + Ui.F(34), Width - Pad * 2, Math.Min(Height - ly - Ui.F(58), Ui.F(14) + apps.Count * Ui.F(48)));
            Theme.Card(g, card, Theme.Surface, 14);
            long max = Math.Max(1, apps[0].Mem);
            var old = g.Clip;
            g.SetClip(card);
            for (int i = 0; i < apps.Count; i++)
            {
                var a = apps[i];
                float y = card.Y + Ui.F(7) + i * Ui.F(48);
                var icon = Main.Procs.Icon(a.Exe);
                var ir = new RectangleF(card.X + Ui.F(16), y + Ui.F(8), Ui.F(32), Ui.F(32));
                if (icon != null) g.DrawImage(icon, ir); else Program.Avatar(g, ir, a.Name);
                Theme.Str(g, a.Name, Theme.F(10f, FontStyle.Bold), Theme.Text, new RectangleF(card.X + Ui.F(60), y + Ui.F(4), card.Width * 0.45f, Ui.F(22)));
                Theme.Str(g, Fmt.Bytes(a.Mem), Theme.F(10f), Theme.Muted, new RectangleF(card.X, y + Ui.F(4), card.Width - Ui.F(18), Ui.F(22)), true);
                Theme.Bar(g, new RectangleF(card.X + Ui.F(60), y + Ui.F(30), card.Width - Ui.F(78), Ui.F(5)), (double)a.Mem / max, Theme.Accent, Theme.Accent2);
            }
            g.Clip = old;
        }

        void Gauge(Graphics g, RectangleF r, string title, double pct, string mood, string tone, string line)
        {
            Theme.Card(g, r, Theme.Surface, 14);
            Theme.Str(g, title, Theme.F(10.5f, FontStyle.Bold), Theme.Text, new RectangleF(r.X + Ui.F(18), r.Y + Ui.F(14), r.Width, Ui.F(24)));
            var badgeW = TextRenderer.MeasureText(mood, Theme.F(8.25f, FontStyle.Bold)).Width + Ui.F(14);
            var br = new RectangleF(r.Right - Ui.F(16) - badgeW, r.Y + Ui.F(16), badgeW, Ui.F(21));
            Color tc = Theme.Tone(tone);
            Theme.Card(g, br, Theme.Soft(tc), 6, false);
            Theme.Str(g, mood, Theme.F(8.25f, FontStyle.Bold), tc, br, false, true);
            float rs = Ui.F(92);
            var rr = new RectangleF(r.X + Ui.F(18), r.Y + Ui.F(58), rs, rs);
            Color c1 = tone == "good" ? Theme.Accent : tc, c2 = tone == "good" ? Theme.Accent2 : Theme.Mix(tc, Color.White, 0.2);
            Theme.Ring(g, rr, pct, Ui.F(9), c1, c2);
            Theme.Str(g, pct.ToString("0") + "%", Theme.F(15f, FontStyle.Bold), Theme.Text, rr, false, true);
            Theme.Wrap(g, line, Theme.F(9.5f), Theme.Muted, new RectangleF(rr.Right + Ui.F(16), rr.Y + Ui.F(18), r.Right - rr.Right - Ui.F(30), Ui.F(64)));
        }
    }

    // ───────── apps ─────────
    internal class AppsPage : Page
    {
        readonly RowList list;
        readonly TextBox search;
        readonly Btn sysBtn;
        bool showSystem;
        List<AppGroup> rows = new List<AppGroup>();

        public AppsPage(MainForm m) : base(m)
        {
            Title = "Apps";
            list = MakeList(56);
            list.Empty = "Loading apps...";
            list.DrawRow = DrawRow;
            list.RowClick = OnRowClick;
            Controls.Add(list);

            search = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Surface2, ForeColor = Theme.Text, Font = Theme.F(10.5f) };
            search.TextChanged += delegate { Refilter(); list.ScrollTop(); };
            search.HandleCreated += delegate { Native.SendMessage(search.Handle, 0x1501, (IntPtr)1, "Find an app..."); };
            Controls.Add(search);

            sysBtn = new Btn("Show Windows parts", "ghost");
            sysBtn.Click += delegate { showSystem = !showSystem; sysBtn.Text = showSystem ? "Hide Windows parts" : "Show Windows parts"; sysBtn.AutoWidth(); Layout2(); Refilter(); };
            Controls.Add(sysBtn);
            Resize += delegate { Layout2(); };
        }

        void Layout2()
        {
            int sw = Ui.D(240);
            search.SetBounds(Width - Pad - sw + Ui.D(34), Ui.D(36), sw - Ui.D(44), Ui.D(22));
            sysBtn.Location = new Point(Width - Pad - sw - Ui.D(10) - sysBtn.Width, Ui.D(28));
            list.SetBounds(Pad, HeadH + Ui.D(30), Width - Pad * 2, Height - HeadH - Ui.D(30) - Pad);
            Invalidate();
        }

        public override void OnShown() { Layout2(); search.Focus(); }

        public override void OnTick()
        {
            long total = Main.Groups.Sum(x => x.Mem);
            Subtitle = Main.Groups.Count(x => x.Kind == "app") + " open apps and " + Main.Groups.Count(x => x.Kind == "background") + " background helpers are using " + Fmt.Bytes(total) + " of memory.";
            Refilter();
            Invalidate(new Rectangle(0, 0, Width, HeadH));
        }

        void Refilter()
        {
            string q = search.Text.Trim();
            rows = Main.Groups
                .Where(x => (showSystem || x.Kind != "system" || q.Length > 0) && (q.Length == 0 || x.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || (x.Exe ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(x => x.Kind == "app" ? 0 : x.Kind == "background" ? 1 : 2).ThenByDescending(x => x.Mem).ToList();
            list.Empty = Main.Groups.Count == 0 ? "Loading apps..." : "No apps match \"" + q + "\"";
            list.SetCount(rows.Count);
        }

        protected override void PaintBody(Graphics g)
        {
            var sr = new RectangleF(Width - Pad - Ui.D(240), Ui.F(26), Ui.F(240), Ui.F(40));
            Theme.Card(g, sr, Theme.Surface2, 10);
            using (var pen = new Pen(Theme.Faint, Ui.F(1.6f)))
            {
                g.DrawEllipse(pen, sr.X + Ui.F(13), sr.Y + Ui.F(12), Ui.F(12), Ui.F(12));
                g.DrawLine(pen, sr.X + Ui.F(23), sr.Y + Ui.F(22), sr.X + Ui.F(27), sr.Y + Ui.F(26));
            }
            float y = HeadH + Ui.F(4);
            var cols = Cols(Width - Pad * 2);
            using (var f = Theme.F(8f, FontStyle.Bold))
            {
                Theme.Str(g, "NAME", f, Theme.Faint, new RectangleF(Pad + Ui.F(12), y, Ui.F(100), Ui.F(20)));
                Theme.Str(g, "MEMORY", f, Theme.Faint, new RectangleF(Pad + cols[0], y, Ui.F(100), Ui.F(20)));
                Theme.Str(g, "PROCESSOR", f, Theme.Faint, new RectangleF(Pad + cols[1], y, Ui.F(100), Ui.F(20)));
            }
        }

        float[] Cols(float w) { return new[] { w * 0.46f, w * 0.68f, w * 0.78f }; }

        RectangleF CloseRect(Rectangle r) { return new RectangleF(r.Right - Ui.F(158), r.Y + Ui.F(12), Ui.F(64), Ui.F(32)); }
        RectangleF ForceRect(Rectangle r) { return new RectangleF(r.Right - Ui.F(88), r.Y + Ui.F(12), Ui.F(80), Ui.F(32)); }

        void DrawRow(Graphics g, int i, Rectangle r, bool hover)
        {
            var a = rows[i];
            var cols = Cols(r.Width);
            var icon = Main.Procs.Icon(a.Exe);
            var ir = new RectangleF(r.X + Ui.F(12), r.Y + Ui.F(12), Ui.F(32), Ui.F(32));
            if (icon != null) g.DrawImage(icon, ir); else Program.Avatar(g, ir, a.Name);
            float nameW = cols[0] - Ui.F(70);
            Theme.Str(g, a.Name, Theme.F(10f, FontStyle.Bold), Theme.Text, new RectangleF(r.X + Ui.F(56), r.Y + Ui.F(8), nameW, Ui.F(22)));
            string sub = (string.IsNullOrEmpty(a.Publisher) ? (a.Kind == "system" ? "Windows" : Path.GetFileName(a.Exe)) : a.Publisher) + (a.Pids.Count > 1 ? "  |  " + a.Pids.Count + " parts" : "");
            Theme.Str(g, sub, Theme.F(8.5f), Theme.Faint, new RectangleF(r.X + Ui.F(56), r.Y + Ui.F(28), nameW, Ui.F(20)));
            if (a.Kind == "app")
            {
                // small "open" dot
                using (var b = new SolidBrush(Theme.Good)) g.FillEllipse(b, r.X + Ui.F(40), r.Y + Ui.F(38), Ui.F(8), Ui.F(8));
            }
            long max = Math.Max(1, Main.Groups.Count > 0 ? Main.Groups[0].Mem : 1);
            Theme.Str(g, Fmt.Bytes(a.Mem), Theme.F(9.5f, FontStyle.Bold), Theme.Text, new RectangleF(r.X + cols[0], r.Y + Ui.F(8), Ui.F(120), Ui.F(22)));
            Theme.Bar(g, new RectangleF(r.X + cols[0], r.Y + Ui.F(33), cols[1] - cols[0] - Ui.F(24), Ui.F(4)), (double)a.Mem / max, Theme.Accent, Theme.Accent2);
            Color cc = a.Cpu > 50 ? Theme.Bad : a.Cpu > 15 ? Theme.Warn : Theme.Muted;
            Theme.Str(g, (a.Cpu < 0.1 ? "0" : a.Cpu.ToString(a.Cpu < 10 ? "0.0" : "0")) + "%", Theme.F(9.5f), cc, new RectangleF(r.X + cols[1], r.Y, Ui.F(80), r.Height));

            if (a.Protect == "blocked")
            {
                Theme.Str(g, "Protected", Theme.F(8.5f, FontStyle.Bold), Theme.Faint, new RectangleF(r.X, r.Y, r.Width - Ui.F(14), r.Height), true);
            }
            else if (hover)
            {
                if (a.Kind == "app")
                {
                    var c = CloseRect(r);
                    Theme.Card(g, c, Theme.Surface3, 8);
                    Theme.Str(g, "Close", Theme.F(9f, FontStyle.Bold), Theme.Text, c, false, true);
                }
                var f = ForceRect(r);
                Theme.Card(g, f, Theme.Soft(Theme.Bad), 8, false);
                Theme.Str(g, "Force quit", Theme.F(9f, FontStyle.Bold), Theme.Bad, f, false, true);
            }
        }

        void OnRowClick(int i, Point p, MouseButtons b)
        {
            var a = rows[i];
            var r = new Rectangle(0, 0, list.Width - 12, list.RowHeight);
            bool force = ForceRect(r).Contains(p), close = a.Kind == "app" && CloseRect(r).Contains(p);
            if (a.Protect == "blocked") { Dialog.Info(Main, a.Name + " is protected", a.Reason); return; }
            if (!force && !close)
            {
                Dialog.Info(Main, a.Name,
                    (string.IsNullOrEmpty(a.Publisher) ? "" : "By " + a.Publisher + "\n") +
                    "Memory: " + Fmt.Bytes(a.Mem) + "    Processor: " + a.Cpu.ToString("0.0") + "%    Parts: " + a.Pids.Count + "\n" +
                    (string.IsNullOrEmpty(a.Exe) ? "" : "\n" + a.Exe) + (a.Reason != null ? "\n\nHeads up: " + a.Reason : ""));
                return;
            }
            string parts = a.Pids.Count > 1 ? " (all " + a.Pids.Count + " parts)" : "";
            bool ok = force
                ? Dialog.Ask(Main, "Force quit " + a.Name + "?", "This stops it immediately" + parts + ". Anything you haven't saved in it will be lost.", "Force quit", true, a.Protect == "caution" ? "Heads up: " + a.Reason : null)
                : Dialog.Ask(Main, "Close " + a.Name + "?", "Tidy Lite will ask it to close, just like clicking its X button. It may ask you to save your work first.", "Close app", false, a.Protect == "caution" ? "Heads up: " + a.Reason : null);
            if (!ok) return;
            int n;
            string err = ProcMonitor.Stop(a, force, out n);
            if (err != null) Main.Notify("Couldn't " + (force ? "stop " : "close ") + a.Name, err, null, null, false);
            else Main.Notify(force ? a.Name + " stopped" : "Asked " + a.Name + " to close", force ? "About " + Fmt.Bytes(a.Mem) + " of memory freed." : "If it has unsaved work it may ask you first.");
        }
    }

    // ───────── storage ─────────
    internal class StoragePage : Page
    {
        readonly FlowLayoutPanel drives;
        readonly RowList folders, files;
        readonly Btn cancel, up, del;
        readonly System.Windows.Forms.Timer progress = new System.Windows.Forms.Timer { Interval = 250 };
        DirNode node;
        List<DirNode> folderRows = new List<DirNode>();
        List<FileItem> fileRows = new List<FileItem>();
        readonly Dictionary<string, FileItem> selected = new Dictionary<string, FileItem>(StringComparer.OrdinalIgnoreCase);

        public StoragePage(MainForm m) : base(m)
        {
            Title = "Storage";
            Subtitle = "Pick a drive or folder to see what's taking up space. Scanning only reads - nothing is changed.";
            drives = new FlowLayoutPanel { BackColor = Theme.Bg, WrapContents = false, AutoScroll = false };
            Controls.Add(drives);

            folders = MakeList(50);
            folders.Empty = "Scan a drive to see its folders";
            folders.DrawRow = DrawFolder;
            folders.RowClick = ClickFolder;
            files = MakeList(50);
            files.Empty = "The biggest files will show here";
            files.DrawRow = DrawFile;
            files.RowClick = ClickFile;
            Controls.Add(folders); Controls.Add(files);

            cancel = new Btn("Stop scan") { Visible = false };
            cancel.Click += delegate { Main.Scanner.Cancel = true; };
            up = new Btn("Up", "ghost") { Visible = false };
            up.Click += delegate { if (node != null && node.Parent != null) Show(node.Parent); };
            del = new Btn("Move to Recycle Bin", "danger") { Visible = false };
            del.MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) DeleteSelected((Control.ModifierKeys & Keys.Shift) != 0); };
            Controls.Add(cancel); Controls.Add(up); Controls.Add(del);
            progress.Tick += delegate { Invalidate(); };
            Resize += delegate { Layout2(); };
        }

        public override void OnShown()
        {
            drives.Controls.Clear();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || (d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable)) continue;
                    var b = new Btn(d.Name.TrimEnd('\\') + "   " + Fmt.Bytes(d.TotalFreeSpace) + " free");
                    string root = d.Name;
                    b.Click += delegate { Start(root); };
                    b.Margin = new Padding(0, 0, Ui.D(8), 0);
                    drives.Controls.Add(b);
                }
                catch { }
            }
            var pick = new Btn("Choose a folder...", "ghost");
            pick.Click += delegate
            {
                using (var fb = new FolderBrowserDialog { Description = "Pick a folder to look through" })
                    if (fb.ShowDialog(Main) == DialogResult.OK) Start(fb.SelectedPath);
            };
            drives.Controls.Add(pick);
            Layout2();
        }

        void Layout2()
        {
            drives.SetBounds(Pad, HeadH, Width - Pad * 2, Ui.D(40));
            int top = HeadH + Ui.D(132), bottom = Height - Pad - Ui.D(52);
            int half = (Width - Pad * 2 - Ui.D(16)) / 2;
            folders.SetBounds(Pad, top, half, bottom - top);
            files.SetBounds(Pad + half + Ui.D(16), top, half, bottom - top);
            cancel.Location = new Point(Width - Pad - cancel.Width, HeadH + Ui.D(54));
            up.Location = new Point(Pad + half - up.Width, top - Ui.D(40));
            del.Location = new Point(Width - Pad - del.Width, Height - Pad - Ui.D(40));
            Invalidate();
        }

        public void Begin(string path) { Start(path); }

        void Start(string path)
        {
            if (Main.Scanner.Running) return;
            selected.Clear();
            node = null; folderRows.Clear(); fileRows.Clear();
            folders.SetCount(0); files.SetCount(0);
            folders.Empty = "Looking through " + path + "...";
            cancel.Visible = true; up.Visible = false; UpdateDel();
            progress.Start();
            Main.Scanner.Start(path, () => BeginInvoke((Action)Done));
        }

        void Done()
        {
            progress.Stop();
            cancel.Visible = false;
            var s = Main.Scanner;
            if (s.Cancel) { folders.Empty = "Scan stopped."; Invalidate(); return; }
            Show(s.Root);
            fileRows = s.Biggest(300);
            files.SetCount(fileRows.Count);
            Invalidate();
        }

        void Show(DirNode n)
        {
            node = n;
            folderRows = n.Children.Where(c => c.Size > 0).ToList();
            folders.Empty = "No sub-folders here";
            folders.SetCount(folderRows.Count);
            folders.ScrollTop();
            up.Visible = n.Parent != null;
            Invalidate();
        }

        protected override void PaintBody(Graphics g)
        {
            var s = Main.Scanner;
            float y = HeadH + Ui.F(52);
            if (s.Running)
            {
                Theme.Str(g, "Scanning...  " + Fmt.Bytes(s.Bytes) + " found in " + s.Files.ToString("N0") + " files", Theme.F(11f, FontStyle.Bold), Theme.Text, new RectangleF(Pad, y, Width - Pad * 2 - Ui.F(120), Ui.F(24)));
                Theme.Str(g, s.Current, Theme.F(8.5f), Theme.Faint, new RectangleF(Pad, y + Ui.F(24), Width - Pad * 2 - Ui.F(120), Ui.F(18)));
            }
            else if (node != null)
            {
                Theme.Str(g, node.Path, Theme.F(11f, FontStyle.Bold), Theme.Text, new RectangleF(Pad, y, Width - Pad * 2, Ui.F(24)));
                Theme.Str(g, Fmt.Bytes(node.Size) + (s.Denied > 0 ? "   |   " + s.Denied + " folders were off-limits (run as admin to include them)" : ""), Theme.F(9f), Theme.Muted, new RectangleF(Pad, y + Ui.F(24), Width - Pad * 2, Ui.F(18)));
            }
            float ly = folders.Top - Ui.F(30);
            Theme.Str(g, "Folders", Theme.F(10.5f, FontStyle.Bold), Theme.Text, new RectangleF(folders.Left + Ui.F(4), ly, Ui.F(200), Ui.F(24)));
            Theme.Str(g, "Biggest files", Theme.F(10.5f, FontStyle.Bold), Theme.Text, new RectangleF(files.Left + Ui.F(4), ly, Ui.F(200), Ui.F(24)));
            Theme.Card(g, new RectangleF(folders.Left - 1, folders.Top - 1, folders.Width + 1, folders.Height + 1), Theme.Surface, 2);
            Theme.Card(g, new RectangleF(files.Left - 1, files.Top - 1, files.Width + 1, files.Height + 1), Theme.Surface, 2);
            if (selected.Count > 0)
            {
                long total = selected.Values.Sum(f => f.Size);
                Theme.Str(g, selected.Count + " selected  |  " + Fmt.Bytes(total) + " will be freed     (hold Shift to delete forever)", Theme.F(10f), Theme.Muted,
                    new RectangleF(Pad, Height - Pad - Ui.F(40), Width - Pad * 2 - del.Width - Ui.F(20), Ui.F(36)));
            }
        }

        void DrawFolder(Graphics g, int i, Rectangle r, bool hover)
        {
            var d = folderRows[i];
            bool on = selected.ContainsKey(d.Path);
            DrawCheck(g, new RectangleF(r.X + Ui.F(12), r.Y + Ui.F(16), Ui.F(18), Ui.F(18)), on);
            DrawFolder(g, new RectangleF(r.X + Ui.F(40), r.Y + Ui.F(9), Ui.F(32), Ui.F(32)), Theme.Accent);
            Theme.Str(g, d.Name, Theme.F(9.75f, FontStyle.Bold), Theme.Text, new RectangleF(r.X + Ui.F(82), r.Y + Ui.F(6), r.Width - Ui.F(180), Ui.F(22)));
            Theme.Str(g, Fmt.Bytes(d.Size), Theme.F(9.25f), Theme.Muted, new RectangleF(r.X, r.Y + Ui.F(6), r.Width - Ui.F(12), Ui.F(22)), true);
            double frac = node != null && node.Size > 0 ? (double)d.Size / Math.Max(1, folderRows[0].Size) : 0;
            Theme.Bar(g, new RectangleF(r.X + Ui.F(82), r.Y + Ui.F(32), r.Width - Ui.F(96), Ui.F(4)), frac, Theme.Accent, Theme.Accent2);
        }

        void DrawFile(Graphics g, int i, Rectangle r, bool hover)
        {
            var f = fileRows[i];
            bool on = selected.ContainsKey(f.Path);
            DrawCheck(g, new RectangleF(r.X + Ui.F(12), r.Y + Ui.F(16), Ui.F(18), Ui.F(18)), on);
            DrawFileGlyph(g, new RectangleF(r.X + Ui.F(40), r.Y + Ui.F(9), Ui.F(32), Ui.F(32)), Theme.Accent2);
            Theme.Str(g, f.Name, Theme.F(9.75f, FontStyle.Bold), Theme.Text, new RectangleF(r.X + Ui.F(82), r.Y + Ui.F(5), r.Width - Ui.F(180), Ui.F(22)));
            Theme.Str(g, Path.GetDirectoryName(f.Path) + "   |   " + Fmt.Ago(f.Modified), Theme.F(8.25f), Theme.Faint, new RectangleF(r.X + Ui.F(82), r.Y + Ui.F(26), r.Width - Ui.F(96), Ui.F(18)));
            Theme.Str(g, Fmt.Bytes(f.Size), Theme.F(9.25f, FontStyle.Bold), Theme.Text, new RectangleF(r.X, r.Y + Ui.F(5), r.Width - Ui.F(12), Ui.F(22)), true);
        }

        void Toggle(FileItem f)
        {
            if (selected.ContainsKey(f.Path)) selected.Remove(f.Path); else selected[f.Path] = f;
            UpdateDel();
            folders.Invalidate(); files.Invalidate(); Invalidate();
        }

        void UpdateDel() { del.Visible = selected.Count > 0; }

        void ClickFolder(int i, Point p, MouseButtons b)
        {
            var d = folderRows[i];
            if (p.X < Ui.D(38)) Toggle(new FileItem { Path = d.Path, Name = d.Name, Size = d.Size, IsDir = true });
            else if (d.Children.Count > 0) Show(d);
            else Toggle(new FileItem { Path = d.Path, Name = d.Name, Size = d.Size, IsDir = true });
        }

        void ClickFile(int i, Point p, MouseButtons b) { Toggle(fileRows[i]); }

        void DeleteSelected(bool permanent)
        {
            var items = selected.Values.ToList();
            if (items.Count == 0) return;
            long total = items.Sum(f => f.Size);
            string what = items.Count == 1 ? "\"" + items[0].Name + "\"" : items.Count + " items";
            string list = string.Join("\n", items.Take(5).Select(f => "  " + f.Path + "   (" + Fmt.Bytes(f.Size) + ")")) + (items.Count > 5 ? "\n  and " + (items.Count - 5) + " more..." : "");
            if (!Dialog.Ask(Main, permanent ? "Delete " + what + " forever?" : "Move " + what + " to the Recycle Bin?",
                (permanent ? "This frees " + Fmt.Bytes(total) + " right away, but it can't be undone." : "This frees " + Fmt.Bytes(total) + ". You can undo it, or restore it from the Recycle Bin later.") + "\n\n" + list,
                permanent ? "Delete forever" : "Move to Recycle Bin", true)) return;

            var done = new List<FileItem>();
            string firstErr = null;
            foreach (var f in items)
            {
                if (!permanent && Trash.TooBigForBin(f.Path, f.Size)) { firstErr = firstErr ?? "\"" + f.Name + "\" is too big for the Recycle Bin. Hold Shift to delete it forever."; continue; }
                string err = Trash.Delete(f.Path, permanent);
                if (err == null) { done.Add(f); Main.Scanner.Forget(f.Path, f.Size); selected.Remove(f.Path); }
                else if (firstErr == null) firstErr = f.Name + ": " + err;
            }
            if (node != null) Show(node);
            fileRows = Main.Scanner.Biggest(300);
            files.SetCount(fileRows.Count);
            UpdateDel(); Invalidate();
            if (done.Count > 0)
            {
                long freed = done.Sum(f => f.Size);
                Main.Notify((permanent ? "Deleted " : "Moved ") + done.Count + " item" + (done.Count > 1 ? "s" : "") + (permanent ? "" : " to the Recycle Bin"), Fmt.Bytes(freed) + " freed",
                    permanent ? null : "Undo", permanent ? (Action)null : () => Undo(done));
            }
            if (firstErr != null) Main.Notify("Some items weren't removed", firstErr, null, null, false);
        }

        void Undo(List<FileItem> items)
        {
            int ok = 0; string err = null;
            foreach (var f in items) { var e = Trash.Restore(f.Path); if (e == null) ok++; else if (err == null) err = e; }
            if (ok > 0) Main.Notify("Restored " + ok + " item" + (ok > 1 ? "s" : ""), "Everything is back where it was. Rescan to see it here.");
            if (err != null) Main.Notify("Some items couldn't be restored", err + " You can still restore them from the Recycle Bin.", null, null, false);
        }
    }

    // ───────── quick clean ─────────
    internal class CleanPage : Page
    {
        readonly RowList list;
        readonly Btn go;
        List<CleanTarget> targets;
        long binSize, binCount;
        bool binChecked = true, measuring;

        public CleanPage(MainForm m) : base(m)
        {
            Title = "Clean up";
            Subtitle = "Safe clutter Tidy Lite found on your PC. Pick what to clear - files in use are skipped automatically.";
            list = MakeList(76);
            list.Empty = "Measuring...";
            list.DrawRow = DrawRow;
            list.RowClick = (i, p, b) =>
            {
                if (i < targets.Count) targets[i].Checked = !targets[i].Checked; else binChecked = !binChecked;
                list.Invalidate(); Invalidate();
            };
            Controls.Add(list);
            go = new Btn("Clean up", "primary") { Height = Ui.D(44) };
            go.Click += delegate { Run(); };
            Controls.Add(go);
            Resize += delegate { Layout2(); };
        }

        void Layout2()
        {
            list.SetBounds(Pad, HeadH + Ui.D(4), Width - Pad * 2, Height - HeadH - Pad - Ui.D(96));
            go.Width = Ui.D(150);
            go.Location = new Point(Width - Pad - go.Width, Height - Pad - Ui.D(58));
            Invalidate();
        }

        public override void OnShown() { Layout2(); if (targets == null) Measure(); }

        void Measure()
        {
            if (measuring) return;
            measuring = true;
            go.Enabled = false;
            var ts = Cleaner.Targets();
            ThreadPool.QueueUserWorkItem(delegate
            {
                foreach (var t in ts) Cleaner.Measure(t);
                long s, c;
                Trash.BinInfo(out s, out c);
                BeginInvoke((Action)delegate
                {
                    targets = ts; binSize = s; binCount = c; measuring = false;
                    foreach (var t in targets) t.Checked = t.Size > 0;
                    binChecked = binSize > 0;
                    list.SetCount(targets.Count + 1);
                    go.Enabled = true;
                    Invalidate();
                });
            });
        }

        long Total()
        {
            if (targets == null) return 0;
            return targets.Where(t => t.Checked).Sum(t => t.Size) + (binChecked ? binSize : 0);
        }

        void DrawRow(Graphics g, int i, Rectangle r, bool hover)
        {
            bool recycle = i == targets.Count;
            string title = recycle ? "Recycle Bin" : targets[i].Title;
            string desc = recycle ? "Things you already deleted (" + binCount.ToString("N0") + " items). Emptying it frees the space for good." : targets[i].Desc;
            long size = recycle ? binSize : targets[i].Size;
            bool on = recycle ? binChecked : targets[i].Checked;
            DrawCheck(g, new RectangleF(r.X + Ui.F(16), r.Y + Ui.F(28), Ui.F(20), Ui.F(20)), on);
            Theme.Str(g, title, Theme.F(10.5f, FontStyle.Bold), Theme.Text, new RectangleF(r.X + Ui.F(52), r.Y + Ui.F(12), r.Width - Ui.F(200), Ui.F(24)));
            Theme.Str(g, desc, Theme.F(9f), Theme.Muted, new RectangleF(r.X + Ui.F(52), r.Y + Ui.F(38), r.Width - Ui.F(200), Ui.F(22)));
            Theme.Str(g, size > 0 ? Fmt.Bytes(size) : "All clean", Theme.F(11f, FontStyle.Bold), size > 0 ? Theme.Text : Theme.Good, new RectangleF(r.X, r.Y, r.Width - Ui.F(18), r.Height), true);
        }

        protected override void PaintBody(Graphics g)
        {
            var box = new RectangleF(list.Left - 1, list.Top - 1, list.Width + 1, list.Height + 1);
            Theme.Card(g, box, Theme.Surface, 2);
            float y = Height - Pad - Ui.F(66);
            Theme.Str(g, "Selected", Theme.F(9f), Theme.Faint, new RectangleF(Pad, y, Ui.F(200), Ui.F(20)));
            using (var b = new LinearGradientBrush(new RectangleF(Pad, y + Ui.F(18), Ui.F(220), Ui.F(44)), Theme.Accent, Theme.Accent2, 0f))
            using (var sf = new StringFormat())
                g.DrawString(measuring ? "..." : Fmt.Bytes(Total()), Theme.F(20f, FontStyle.Bold), b, new PointF(Pad - Ui.F(4), y + Ui.F(18)), sf);
        }

        void Run()
        {
            long total = Total();
            if (total <= 0) return;
            if (!Dialog.Ask(Main, "Clean up " + Fmt.Bytes(total) + "?", "These files are safe to remove. Anything currently in use is skipped automatically.", "Clean up", false,
                binChecked && binSize > 0 ? "Emptying the Recycle Bin is permanent." : null)) return;
            go.Enabled = false;
            var chosen = targets.Where(t => t.Checked).ToList();
            bool bin = binChecked;
            ThreadPool.QueueUserWorkItem(delegate
            {
                long freed = 0, skipped = 0;
                foreach (var t in chosen) { long sk; freed += Cleaner.Clean(t, out sk); skipped += sk; }
                if (bin) { long s, c; Trash.BinInfo(out s, out c); if (Native.SHEmptyRecycleBin(IntPtr.Zero, null, 0x7) == 0) freed += s; }
                BeginInvoke((Action)delegate
                {
                    Main.Notify("Freed " + Fmt.Bytes(freed), skipped > 0 ? skipped.ToString("N0") + " files were in use and were left alone." : "Your PC is a little tidier now.");
                    targets = null;
                    Measure();
                });
            });
        }
    }
}
