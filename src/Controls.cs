using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TidyLite
{
    internal static class Theme
    {
        public static bool Dark = true;
        public static Color Bg, Side, Surface, Surface2, Surface3, Border, Text, Muted, Faint, Accent, Accent2, Good, Warn, Bad, Hover;

        public static void Load()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    Dark = k == null || !(k.GetValue("AppsUseLightTheme") is int) || (int)k.GetValue("AppsUseLightTheme") == 0;
            }
            catch { }
            Apply();
        }

        public static void Apply()
        {
            Accent = Dark ? Color.FromArgb(139, 123, 255) : Color.FromArgb(108, 92, 245);
            Accent2 = Color.FromArgb(61, 214, 196);
            if (Dark)
            {
                Bg = Color.FromArgb(11, 12, 17); Side = Color.FromArgb(16, 18, 25); Surface = Color.FromArgb(19, 21, 29);
                Surface2 = Color.FromArgb(26, 29, 39); Surface3 = Color.FromArgb(35, 39, 54); Border = Color.FromArgb(34, 37, 48);
                Text = Color.FromArgb(238, 240, 247); Muted = Color.FromArgb(161, 167, 184); Faint = Color.FromArgb(108, 114, 134);
                Good = Color.FromArgb(52, 211, 153); Warn = Color.FromArgb(251, 191, 36); Bad = Color.FromArgb(251, 113, 133);
                Hover = Color.FromArgb(28, 31, 42);
            }
            else
            {
                Bg = Color.FromArgb(244, 245, 249); Side = Color.FromArgb(250, 250, 252); Surface = Color.White;
                Surface2 = Color.FromArgb(246, 247, 251); Surface3 = Color.FromArgb(232, 234, 242); Border = Color.FromArgb(226, 228, 236);
                Text = Color.FromArgb(20, 23, 36); Muted = Color.FromArgb(91, 98, 117); Faint = Color.FromArgb(139, 145, 163);
                Good = Color.FromArgb(16, 185, 129); Warn = Color.FromArgb(217, 119, 6); Bad = Color.FromArgb(225, 29, 72);
                Hover = Color.FromArgb(240, 241, 247);
            }
        }

        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Soft(Color c) { return Mix(Surface, c, Dark ? 0.16 : 0.12); }

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        static string family;
        public static Font F(float size, FontStyle style = FontStyle.Regular)
        {
            if (family == null)
            {
                family = "Segoe UI";
                using (var col = new InstalledFontCollection())
                    foreach (var f in col.Families) if (f.Name == "Segoe UI Variable Text") { family = f.Name; break; }
            }
            string key = size + "|" + style;
            Font font;
            if (!fonts.TryGetValue(key, out font)) { font = new Font(family, size, style, GraphicsUnit.Point); fonts[key] = font; }
            return font;
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Card(Graphics g, RectangleF r, Color fill, float rad = 12, bool border = true)
        {
            using (var p = Round(r, rad))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, p);
                if (border) using (var pen = new Pen(Border)) g.DrawPath(pen, p);
            }
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        public static void Str(Graphics g, string s, Font f, Color c, RectangleF r, bool right = false, bool center = false)
        {
            var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
                (right ? TextFormatFlags.Right : center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left);
            TextRenderer.DrawText(g, s, f, Rectangle.Round(r), c, flags);
        }

        public static void Wrap(Graphics g, string s, Font f, Color c, RectangleF r)
        {
            TextRenderer.DrawText(g, s, f, Rectangle.Round(r), c, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        public static void Bar(Graphics g, RectangleF r, double frac, Color c1, Color c2)
        {
            using (var p = Round(r, r.Height / 2)) using (var b = new SolidBrush(Surface3)) g.FillPath(b, p);
            float w = (float)Math.Max(r.Height, r.Width * Math.Max(0, Math.Min(1, frac)));
            if (frac <= 0) return;
            var fr = new RectangleF(r.X, r.Y, w, r.Height);
            using (var p = Round(fr, r.Height / 2))
            using (var b = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y, r.Width + 2, r.Height), c1, c2, 0f))
                g.FillPath(b, p);
        }

        public static void Ring(Graphics g, RectangleF r, double pct, float thick, Color c1, Color c2)
        {
            var rr = new RectangleF(r.X + thick / 2, r.Y + thick / 2, r.Width - thick, r.Height - thick);
            using (var pen = new Pen(Surface3, thick)) g.DrawEllipse(pen, rr);
            float sweep = (float)(360 * Math.Max(0, Math.Min(100, pct)) / 100);
            if (sweep < 1) return;
            using (var b = new LinearGradientBrush(r, c1, c2, 45f))
            using (var pen = new Pen(b, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(pen, rr, -90, sweep);
        }

        public static Color Tone(string tone)
        {
            return tone == "bad" ? Bad : tone == "warn" ? Warn : tone == "good" ? Good : Accent;
        }
    }

    // A flat, rounded, custom-drawn button.
    internal class Btn : Control
    {
        public string Kind = "normal"; // normal | primary | danger | ghost
        bool hover, down;

        public Btn(string text, string kind = "normal")
        {
            Text = text; Kind = kind;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            Font = Theme.F(9.75f, FontStyle.Bold);
            Height = MainFormUi(36);
            AutoWidth();
        }

        static int MainFormUi(float v) { return Ui.D(v); }

        public void AutoWidth() { Width = TextRenderer.MeasureText(Text, Font).Width + MainFormUi(32); }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Theme.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            if (down) r.Inflate(-1, -1);
            Color fg = Theme.Text;
            using (var p = Theme.Round(r, 9))
            {
                if (Kind == "primary" || Kind == "danger")
                {
                    Color a = Kind == "primary" ? Theme.Accent : Theme.Bad, b = Kind == "primary" ? Color.FromArgb(95, 168, 255) : Theme.Mix(Theme.Bad, Color.Black, 0.1);
                    if (hover) { a = Theme.Mix(a, Color.White, 0.1); b = Theme.Mix(b, Color.White, 0.1); }
                    using (var br = new LinearGradientBrush(r, a, b, 0f)) g.FillPath(br, p);
                    fg = Color.White;
                }
                else if (Kind == "ghost")
                {
                    if (hover) using (var br = new SolidBrush(Theme.Hover)) g.FillPath(br, p);
                    fg = hover ? Theme.Text : Theme.Muted;
                }
                else
                {
                    using (var br = new SolidBrush(hover ? Theme.Surface3 : Theme.Surface2)) g.FillPath(br, p);
                    using (var pen = new Pen(Theme.Mix(Theme.Border, Theme.Text, 0.08))) g.DrawPath(pen, p);
                }
            }
            if (!Enabled) fg = Theme.Faint;
            TextRenderer.DrawText(g, Text, Font, Rectangle.Round(r), fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    // A fast, owner-drawn, scrollable list. Rows are drawn by a callback.
    internal class RowList : Control
    {
        public int RowHeight = 52;
        public int Count;
        public Action<Graphics, int, Rectangle, bool> DrawRow;
        public Action<int, Point, MouseButtons> RowClick;
        public string Empty = "Nothing here";
        int scroll, hoverRow = -1;

        public RowList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        int MaxScroll { get { return Math.Max(0, Count * RowHeight - Height); } }
        public int HoverRow { get { return hoverRow; } }
        public Point Mouse { get; private set; }

        public void SetCount(int n) { Count = n; scroll = Math.Min(scroll, MaxScroll); Invalidate(); }
        public void ScrollTop() { scroll = 0; Invalidate(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scroll = Math.Max(0, Math.Min(MaxScroll, scroll - e.Delta * RowHeight / 120 * 2));
            UpdateHover(e.Location);
            Invalidate();
        }

        void UpdateHover(Point p)
        {
            Mouse = p;
            int r = (p.Y + scroll) / RowHeight;
            int nh = r >= 0 && r < Count && ClientRectangle.Contains(p) ? r : -1;
            if (nh != hoverRow) hoverRow = nh;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e) { UpdateHover(e.Location); base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hoverRow = -1; Invalidate(); base.OnMouseLeave(e); }

        bool dragging; int dragOffset;
        Rectangle Thumb()
        {
            if (MaxScroll <= 0) return Rectangle.Empty;
            int total = Count * RowHeight;
            int h = Math.Max(30, Height * Height / total);
            int y = (int)((Height - h) * (double)scroll / MaxScroll);
            return new Rectangle(Width - 8, y, 6, h);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            var t = Thumb();
            if (!t.IsEmpty && e.X >= Width - 12) { dragging = true; dragOffset = e.Y - t.Y; return; }
            int r = (e.Y + scroll) / RowHeight;
            if (r >= 0 && r < Count && RowClick != null) RowClick(r, new Point(e.X, e.Y + scroll - r * RowHeight), e.Button);
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; base.OnMouseUp(e); }

        protected override void OnMouseCaptureChanged(EventArgs e) { dragging = false; base.OnMouseCaptureChanged(e); }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x0200 && dragging) // WM_MOUSEMOVE while dragging the scrollbar
            {
                var p = PointToClient(Cursor.Position);
                var t = Thumb();
                double frac = (double)(p.Y - dragOffset) / Math.Max(1, Height - t.Height);
                scroll = (int)Math.Max(0, Math.Min(MaxScroll, frac * MaxScroll));
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.Smooth(g);
            if (Count == 0)
            {
                Theme.Str(g, Empty, Theme.F(10f), Theme.Faint, new RectangleF(0, 0, Width, Math.Min(Height, 120)), false, true);
                return;
            }
            int first = scroll / RowHeight, last = Math.Min(Count - 1, (scroll + Height) / RowHeight);
            for (int i = first; i <= last; i++)
            {
                var r = new Rectangle(0, i * RowHeight - scroll, Width - 12, RowHeight);
                if (i == hoverRow) Theme.Card(g, new RectangleF(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4), Theme.Hover, 9, false);
                if (DrawRow != null) DrawRow(g, i, r, i == hoverRow);
            }
            var t = Thumb();
            if (!t.IsEmpty) Theme.Card(g, t, Theme.Surface3, 3, false);
        }
    }

    // Themed confirm / message dialog.
    internal class Dialog : Form
    {
        public static bool Ask(IWin32Window owner, string title, string body, string ok, bool danger, string note = null)
        {
            using (var d = new Dialog(title, body, ok, danger, note, true)) return d.ShowDialog(owner) == DialogResult.OK;
        }

        public static void Info(IWin32Window owner, string title, string body)
        {
            using (var d = new Dialog(title, body, "OK", false, null, false)) d.ShowDialog(owner);
        }

        readonly string title, body, note;
        readonly bool danger;

        Dialog(string title, string body, string ok, bool danger, string note, bool cancel)
        {
            this.title = title; this.body = body; this.note = note; this.danger = danger;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Theme.Surface;
            Font = Theme.F(10f);
            DoubleBuffered = true;
            int w = Ui.D(440);
            int textH = TextRenderer.MeasureText(body, Theme.F(10f), new Size(w - Ui.D(56), 0), TextFormatFlags.WordBreak).Height;
            int noteH = note == null ? 0 : TextRenderer.MeasureText(note, Theme.F(9f), new Size(w - Ui.D(80), 0), TextFormatFlags.WordBreak).Height + Ui.D(24);
            Size = new Size(w, Ui.D(184) + textH + noteH);
            var okBtn = new Btn(ok, danger ? "danger" : "primary");
            okBtn.Location = new Point(Width - Ui.D(28) - okBtn.Width, Height - Ui.D(60));
            okBtn.Click += delegate { DialogResult = DialogResult.OK; };
            Controls.Add(okBtn);
            AcceptButton = null;
            if (cancel)
            {
                var c = new Btn("Cancel");
                c.Location = new Point(okBtn.Left - Ui.D(10) - c.Width, okBtn.Top);
                c.Click += delegate { DialogResult = DialogResult.Cancel; };
                Controls.Add(c);
            }
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
                if (e.KeyCode == Keys.Enter) DialogResult = DialogResult.OK;
            };
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } // CS_DROPSHADOW
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            using (var pen = new Pen(Theme.Mix(Theme.Border, Theme.Text, 0.1))) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            Color tone = danger ? Theme.Bad : Theme.Accent;
            Theme.Card(g, new RectangleF(Ui.F(28), Ui.F(26), Ui.F(42), Ui.F(42)), Theme.Soft(tone), 12, false);
            using (var pen = new Pen(tone, Ui.F(2.4f)) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
            {
                if (danger) { g.DrawLine(pen, Ui.F(49), Ui.F(37), Ui.F(49), Ui.F(49)); g.DrawLine(pen, Ui.F(49), Ui.F(55), Ui.F(49), Ui.F(55.5f)); }
                else { g.DrawLine(pen, Ui.F(41), Ui.F(47), Ui.F(47), Ui.F(53)); g.DrawLine(pen, Ui.F(47), Ui.F(53), Ui.F(58), Ui.F(41)); }
            }
            Theme.Str(g, title, Theme.F(13f, FontStyle.Bold), Theme.Text, new RectangleF(Ui.F(28), Ui.F(80), Width - Ui.F(56), Ui.F(30)));
            var bodyR = new RectangleF(Ui.F(28), Ui.F(114), Width - Ui.F(56), Height - Ui.F(114));
            Theme.Wrap(g, body, Theme.F(10f), Theme.Muted, bodyR);
            if (note != null)
            {
                int textH = TextRenderer.MeasureText(body, Theme.F(10f), new Size(Width - Ui.D(56), 0), TextFormatFlags.WordBreak).Height;
                var nr = new RectangleF(Ui.F(28), Ui.F(124) + textH, Width - Ui.F(56), Height - Ui.F(124) - textH - Ui.F(72));
                Theme.Card(g, nr, Theme.Soft(Theme.Warn), 9, false);
                Theme.Wrap(g, note, Theme.F(9f), Theme.Text, new RectangleF(nr.X + Ui.F(12), nr.Y + Ui.F(10), nr.Width - Ui.F(24), nr.Height - Ui.F(12)));
            }
        }
    }
}
