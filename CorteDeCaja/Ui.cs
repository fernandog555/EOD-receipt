using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CorteDeCaja
{
    /// <summary>
    /// The main window does its own DPI scaling: .NET Framework's auto-scaling scales Padding and Margin of nested
    /// containers several times over. Every size in the main window is written at 100% and passed through here once.
    /// </summary>
    static class Dpi
    {
        public static readonly float K = ReadK();

        static float ReadK()
        {
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) return Math.Max(1f, g.DpiX / 96f); }
            catch { return 1f; }
        }

        public static int S(int v) => (int)Math.Round(v * K);
        public static Padding P(int all) => new Padding(S(all));
        public static Padding P(int l, int t, int r, int b) => new Padding(S(l), S(t), S(r), S(b));
        public static Size Sz(int w, int h) => new Size(S(w), S(h));
        public static Point Pt(int x, int y) => new Point(S(x), S(y));
    }

    /// <summary>Colors and fonts shared by the whole window, so every control draws from one palette.</summary>
    static class Theme
    {
        public static readonly Color Page = Color.FromArgb(243, 245, 248);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(222, 227, 234);
        public static readonly Color BorderStrong = Color.FromArgb(196, 204, 214);
        public static readonly Color Text = Color.FromArgb(27, 36, 48);
        public static readonly Color Muted = Color.FromArgb(94, 104, 118);
        public static readonly Color Hint = Color.FromArgb(140, 150, 164);
        public static readonly Color Accent = Color.FromArgb(10, 92, 172);
        public static readonly Color AccentHover = Color.FromArgb(8, 79, 150);
        public static readonly Color AccentPressed = Color.FromArgb(6, 65, 124);
        public static readonly Color AccentSoft = Color.FromArgb(230, 240, 251);
        public static readonly Color Hover = Color.FromArgb(234, 238, 243);
        public static readonly Color Pressed = Color.FromArgb(222, 228, 236);
        public static readonly Color ReadOnlyFill = Color.FromArgb(246, 248, 250);
        public static readonly Color Success = Color.FromArgb(22, 120, 66);
        public static readonly Color SuccessSoft = Color.FromArgb(225, 244, 233);
        public static readonly Color Danger = Color.FromArgb(190, 38, 38);
        public static readonly Color DangerSoft = Color.FromArgb(253, 235, 235);
        public static readonly Color Warning = Color.FromArgb(166, 98, 0);

        public static readonly Font Body = new Font("Segoe UI", 10f);
        public static readonly Font Small = new Font("Segoe UI", 8.75f);
        public static readonly Font FieldLabel = new Font("Segoe UI Semibold", 8.75f);
        public static readonly Font CardTitle = new Font("Segoe UI Semibold", 11.5f);
        public static readonly Font PageTitle = new Font("Segoe UI Semibold", 18f);
        public static readonly Font Total = new Font("Segoe UI Semibold", 13f);
        public static readonly Font Stat = new Font("Segoe UI Semibold", 12f);
        public static readonly Font Button = new Font("Segoe UI Semibold", 9.75f);
        /// <summary>Icon font built into Windows 10 and 11.</summary>
        public static readonly Font Icons = new Font("Segoe MDL2 Assets", 10.5f);

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>Paints a rounded box over the parent's color, so the corners blend into what is behind it.</summary>
        public static void PaintBox(Graphics g, Control c, Color fill, Color border, float radius, float borderWidth = 1f)
        {
            g.Clear(c.Parent?.BackColor ?? Page);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float k = Dpi.K, w = borderWidth * k;
            var r = new RectangleF(w / 2, w / 2, c.Width - w - 0.5f, c.Height - w - 0.5f);
            using (var path = Round(r, radius * k))
            using (var b = new SolidBrush(fill))
            using (var pen = new Pen(border, w))
            {
                g.FillPath(b, path);
                g.DrawPath(pen, path);
            }
        }

        public static Label MakeLabel(string text, Font font, Color color, Padding margin) =>
            new Label { Text = text, AutoSize = true, Font = font, ForeColor = color, Margin = margin };

        public static Panel Separator(int width) =>
            new Panel { Size = new Size(Dpi.S(width), 1), BackColor = Border, Margin = Dpi.P(0, 6, 0, 8) };

        // Windows 11: paint the title bar in the page color so the window reads as one surface. Ignored on Windows 10.
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void TintTitleBar(Form f)
        {
            try
            {
                int color = Page.R | (Page.G << 8) | (Page.B << 16);
                DwmSetWindowAttribute(f.Handle, 35, ref color, sizeof(int)); // DWMWA_CAPTION_COLOR
            }
            catch { }
        }
    }

    /// <summary>White rounded panel that groups one section of the form.</summary>
    // A one-column TableLayoutPanel: it sizes itself to its rows and honors Padding at any display scaling.
    public class Card : TableLayoutPanel
    {
        public Card()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            Padding = Dpi.P(16, 12, 16, 14);
            Margin = Dpi.P(0, 0, 0, 12);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ColumnCount = 1;
        }

        public T Add<T>(T c) where T : Control
        {
            Controls.Add(c, 0, RowCount++);
            return c;
        }

        protected override void OnPaintBackground(PaintEventArgs e) => Theme.PaintBox(e.Graphics, this, Theme.Card, Theme.Border, 10);
    }

    /// <summary>Thin rounded frame around a control that has no border of its own (the cheques grid).</summary>
    public class Frame : Panel
    {
        public Frame(Control inner, int width, int height)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Size = Dpi.Sz(width, height);
            Padding = Dpi.P(2);
            Margin = Dpi.P(0);
            inner.Dock = DockStyle.Fill;
            Controls.Add(inner);
        }

        protected override void OnPaintBackground(PaintEventArgs e) => Theme.PaintBox(e.Graphics, this, Color.White, Theme.Border, 6);
    }

    /// <summary>
    /// Modern text field: a borderless TextBox inside a rounded box that highlights on focus and turns red
    /// when a MoneyBox holds something that isn't a number. Clicking anywhere in the box focuses the text.
    /// </summary>
    public class Field : Panel
    {
        public readonly TextBox Box;
        readonly bool money;
        bool hover;

        public Field(TextBox box, int width)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Box = box;
            money = box is MoneyBox;
            Cursor = Cursors.IBeam;
            Margin = Dpi.P(0, 4, 8, 4);
            // Room on the left for the "$" sign in amount fields.
            Padding = Dpi.P(money ? 24 : 10, 6, 10, 6);
            box.BorderStyle = BorderStyle.None;
            box.Margin = Padding.Empty;
            Width = Dpi.S(width);
            Controls.Add(box);

            box.GotFocus += (s, e) => Invalidate();
            box.LostFocus += (s, e) => Invalidate();
            box.TextChanged += (s, e) => { if (money) Invalidate(); };
            box.BackColorChanged += (s, e) => Invalidate();
            box.SizeChanged += (s, e) => PerformLayout();
            box.MouseEnter += (s, e) => SetHover(true);
            box.MouseLeave += (s, e) => SetHover(ClientRectangle.Contains(PointToClient(MousePosition)));
        }

        void SetHover(bool on) { if (hover != on) { hover = on; Invalidate(); } }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); SetHover(true); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); SetHover(Box.ClientRectangle.Contains(Box.PointToClient(MousePosition))); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Box.Focus(); }

        // The box's height comes from its font; the field wraps it with even padding.
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int h = Box.Height + Padding.Vertical;
            if (Height != h) Height = h;
            Box.SetBounds(Padding.Left, Padding.Top, Math.Max(10, Width - Padding.Horizontal), Box.Height);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            bool invalid = Box is MoneyBox m && !m.IsValid;
            bool focused = Box.Focused;
            var border = invalid ? Theme.Danger : focused ? Theme.Accent : hover && !Box.ReadOnly ? Theme.BorderStrong : Theme.Border;
            Theme.PaintBox(e.Graphics, this, Box.BackColor, border, 6, focused || invalid ? 1.6f : 1f);
            if (money)
                TextRenderer.DrawText(e.Graphics, "$", Box.Font, new Rectangle(0, Box.Top, Padding.Left - 2, Box.Height), Theme.Hint,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Date field that matches the text fields. Click it for a calendar; ↑ ↓ move one day.</summary>
    public class DateField : Control
    {
        static readonly System.Globalization.CultureInfo Es = new System.Globalization.CultureInfo("es-MX");
        DateTime date = DateTime.Today;
        bool hover;
        ToolStripDropDown drop;

        public event EventHandler ValueChanged;

        public DateField(int width)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Width = Dpi.S(width);
            Margin = Dpi.P(0, 4, 12, 4);
        }

        public DateTime Value
        {
            get => date;
            set
            {
                value = value.Date;
                if (date == value) return;
                date = value;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Same height as a Field: one line of text plus 6px above and below.
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); FitHeight(); }
        protected override void OnParentChanged(EventArgs e) { base.OnParentChanged(e); FitHeight(); }
        protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); FitHeight(); }
        void FitHeight() => Height = TextRenderer.MeasureText("Ág", Font).Height + Dpi.S(12) + 1;

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button == MouseButtons.Left) ShowCalendar();
        }

        protected override bool IsInputKey(Keys k) => k == Keys.Up || k == Keys.Down || base.IsInputKey(k);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Up) Value = Value.AddDays(1);
            else if (e.KeyCode == Keys.Down) Value = Value.AddDays(-1);
            else if (e.KeyCode == Keys.Space || e.KeyCode == Keys.F4 || (e.Alt && e.KeyCode == Keys.Down)) ShowCalendar();
            else if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; FindForm()?.SelectNextControl(this, true, true, true, true); }
        }

        void ShowCalendar()
        {
            if (drop != null && !drop.IsDisposed && drop.Visible) { drop.Close(); return; }
            var cal = new MonthPicker(date);
            cal.Picked += (s, d) => { Value = d; drop.Close(); Focus(); };
            cal.Cancelled += (s, e) => { drop.Close(); Focus(); };
            drop = new ToolStripDropDown { Padding = Padding.Empty, DropShadowEnabled = true, Renderer = new PopupRenderer() };
            var host = new ToolStripControlHost(cal) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = cal.Size };
            drop.Items.Add(host);
            drop.Closed += (s, e) => { var d = (ToolStripDropDown)s; BeginInvoke((Action)d.Dispose); Invalidate(); };
            drop.Opened += (s, e) => cal.Focus();
            drop.Show(this, new Point(0, Height + Dpi.S(4)));
        }

        /// <summary>Plain 1px border for the popup instead of the default menu frame.</summary>
        class PopupRenderer : ToolStripRenderer
        {
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using (var pen = new Pen(Theme.BorderStrong))
                    e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var border = Focused ? Theme.Accent : hover ? Theme.BorderStrong : Theme.Border;
            Theme.PaintBox(g, this, Color.White, border, 6, Focused ? 1.6f : 1f);
            int pad = Dpi.S(10), icon = Dpi.S(22);
            var text = date.ToString("ddd", Es).TrimEnd('.');
            text = char.ToUpper(text[0], Es) + text.Substring(1) + "  " + Fmt.Date(date);
            TextRenderer.DrawText(g, text, Font, new Rectangle(pad, 0, Width - pad - icon - pad, Height), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, "", Theme.Icons, new Rectangle(Width - pad - icon, 0, icon, Height), Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>
    /// Month calendar drawn in the app's style: Spanish month and weekday names, selected day in a blue circle,
    /// today ringed, a "Hoy" button. Keys: arrows move a day/week, Re Pág / Av Pág a month, Enter picks, Esc closes.
    /// </summary>
    public class MonthPicker : Control
    {
        static readonly System.Globalization.CultureInfo Es = new System.Globalization.CultureInfo("es-MX");
        const int Cell = 38, Pad = 12, HeadH = 44, WeekH = 26, FootH = 46;

        DateTime month, cursor;
        readonly DateTime selected;
        int hoverDay = -1;   // index 0..41 in the grid, or -1
        string hoverBtn;     // "prev", "next", "hoy" or null

        public event EventHandler<DateTime> Picked;
        public event EventHandler Cancelled;

        public MonthPicker(DateTime selected)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            this.selected = selected.Date;
            cursor = selected.Date;
            month = new DateTime(selected.Year, selected.Month, 1);
            BackColor = Color.White;
            Font = Theme.Body;
            Size = Dpi.Sz(Pad * 2 + Cell * 7, Pad + HeadH + WeekH + Cell * 6 + FootH);
        }

        DayOfWeek FirstDay => Es.DateTimeFormat.FirstDayOfWeek;
        DateTime GridStart => month.AddDays(-(((int)month.DayOfWeek - (int)FirstDay + 7) % 7));

        Rectangle DayRect(int i) => new Rectangle(Dpi.S(Pad + (i % 7) * Cell), Dpi.S(Pad + HeadH + WeekH + (i / 7) * Cell), Dpi.S(Cell), Dpi.S(Cell));
        Rectangle PrevRect => new Rectangle(Width - Dpi.S(Pad + 72), Dpi.S(Pad + 4), Dpi.S(34), Dpi.S(32));
        Rectangle NextRect => new Rectangle(Width - Dpi.S(Pad + 36), Dpi.S(Pad + 4), Dpi.S(34), Dpi.S(32));
        Rectangle HoyRect
        {
            get
            {
                int w = TextRenderer.MeasureText("Hoy", Theme.Button).Width + Dpi.S(24);
                return new Rectangle(Dpi.S(Pad), Height - Dpi.S(FootH - 6), w, Dpi.S(32));
            }
        }

        int HitDay(Point p)
        {
            for (int i = 0; i < 42; i++) if (DayRect(i).Contains(p)) return i;
            return -1;
        }

        string HitBtn(Point p) => PrevRect.Contains(p) ? "prev" : NextRect.Contains(p) ? "next" : HoyRect.Contains(p) ? "hoy" : null;

        void ShowMonth(DateTime m) { month = new DateTime(m.Year, m.Month, 1); Invalidate(); }

        void MoveCursor(DateTime d)
        {
            cursor = d.Date;
            if (cursor.Year != month.Year || cursor.Month != month.Month) month = new DateTime(cursor.Year, cursor.Month, 1);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int d = HitDay(e.Location); var b = HitBtn(e.Location);
            if (d != hoverDay || b != hoverBtn) { hoverDay = d; hoverBtn = b; Invalidate(); }
            Cursor = d >= 0 || b != null ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverDay = -1; hoverBtn = null; Invalidate(); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            switch (HitBtn(e.Location))
            {
                case "prev": ShowMonth(month.AddMonths(-1)); return;
                case "next": ShowMonth(month.AddMonths(1)); return;
                case "hoy": Picked?.Invoke(this, DateTime.Today); return;
            }
            int i = HitDay(e.Location);
            if (i >= 0) Picked?.Invoke(this, GridStart.AddDays(i));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ShowMonth(month.AddMonths(e.Delta > 0 ? -1 : 1));
        }

        protected override bool IsInputKey(Keys k)
        {
            switch (k & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: case Keys.Enter: case Keys.Escape: case Keys.PageUp: case Keys.PageDown: case Keys.Home:
                    return true;
            }
            return base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left: MoveCursor(cursor.AddDays(-1)); break;
                case Keys.Right: MoveCursor(cursor.AddDays(1)); break;
                case Keys.Up: MoveCursor(cursor.AddDays(-7)); break;
                case Keys.Down: MoveCursor(cursor.AddDays(7)); break;
                case Keys.PageUp: MoveCursor(cursor.AddMonths(-1)); break;
                case Keys.PageDown: MoveCursor(cursor.AddMonths(1)); break;
                case Keys.Home: MoveCursor(DateTime.Today); break;
                case Keys.Enter: Picked?.Invoke(this, cursor); break;
                case Keys.Escape: Cancelled?.Invoke(this, EventArgs.Empty); break;
                default: return;
            }
            e.Handled = e.SuppressKeyPress = true;
        }

        static string Cap(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Es) + s.Substring(1);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

            // Header: "Octubre 2026" and the two arrows.
            var title = Cap(month.ToString("MMMM", Es)) + " " + month.Year;
            TextRenderer.DrawText(g, title, Theme.CardTitle, new Rectangle(Dpi.S(Pad + 6), Dpi.S(Pad), Width, Dpi.S(40)), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            foreach (var (key, rect, glyph) in new[] { ("prev", PrevRect, "\uE76B"), ("next", NextRect, "\uE76C") })
            {
                if (hoverBtn == key)
                    using (var path = Theme.Round(rect, Dpi.S(6)))
                    using (var b = new SolidBrush(Theme.Hover)) g.FillPath(b, path);
                TextRenderer.DrawText(g, glyph, Theme.Icons, rect, Theme.Text, center);
            }

            // Weekday initials, starting on the culture's first day.
            var abbrev = Es.DateTimeFormat.ShortestDayNames;
            for (int c = 0; c < 7; c++)
            {
                var name = Cap(abbrev[((int)FirstDay + c) % 7].TrimEnd('.'));
                var r = new Rectangle(Dpi.S(Pad + c * Cell), Dpi.S(Pad + HeadH), Dpi.S(Cell), Dpi.S(WeekH));
                TextRenderer.DrawText(g, name, Theme.FieldLabel, r, Theme.Muted, center);
            }

            // Day grid.
            var start = GridStart;
            int inset = Dpi.S(3);
            for (int i = 0; i < 42; i++)
            {
                var day = start.AddDays(i);
                var r = DayRect(i);
                var circle = new Rectangle(r.X + inset, r.Y + inset, r.Width - inset * 2, r.Height - inset * 2);
                bool inMonth = day.Month == month.Month;
                bool isSel = day == selected, isToday = day == DateTime.Today, isCursor = day == cursor && Focused;
                var fore = inMonth ? Theme.Text : Theme.Hint;

                if (isSel)
                {
                    using (var b = new SolidBrush(i == hoverDay ? Theme.AccentHover : Theme.Accent)) g.FillEllipse(b, circle);
                    fore = Color.White;
                }
                else if (i == hoverDay)
                    using (var b = new SolidBrush(Theme.Hover)) g.FillEllipse(b, circle);

                if (isToday && !isSel)
                {
                    using (var pen = new Pen(Theme.Accent, 1.5f * Dpi.K)) g.DrawEllipse(pen, circle);
                    fore = Theme.Accent;
                }
                if (isCursor && !isSel)
                    using (var pen = new Pen(Theme.BorderStrong, 1.5f * Dpi.K) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot }) g.DrawEllipse(pen, circle);

                var font = isSel || isToday ? Theme.Button : Theme.Body;
                TextRenderer.DrawText(g, day.Day.ToString(), font, r, fore, center);
            }

            // Footer: separator, "Hoy" button and the selected date spelled out.
            int fy = Height - Dpi.S(FootH);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, Dpi.S(Pad), fy, Width - Dpi.S(Pad), fy);
            var hoy = HoyRect;
            using (var path = Theme.Round(hoy, Dpi.S(6)))
            {
                using (var b = new SolidBrush(hoverBtn == "hoy" ? Theme.AccentSoft : Color.White)) g.FillPath(b, path);
                using (var pen = new Pen(Theme.BorderStrong)) g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, "Hoy", Theme.Button, hoy, Theme.Accent, center);
            var sel = selected.ToString("d 'de' MMM yyyy", Es).Replace(".", "");
            TextRenderer.DrawText(g, sel, Theme.Small, new Rectangle(hoy.Right, hoy.Y, Width - hoy.Right - Dpi.S(Pad), hoy.Height), Theme.Muted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    public enum ButtonKind { Primary, Secondary, Ghost }

    /// <summary>Flat rounded button with an optional Segoe MDL2 icon in front of the text.</summary>
    public class UiButton : Button
    {
        readonly ButtonKind kind;
        readonly string glyph;
        bool hover, down;

        public UiButton(string text, ButtonKind kind, string glyph = null)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.kind = kind;
            this.glyph = glyph;
            Text = text;
            Font = Theme.Button;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = Dpi.P(14, 7, 16, 7);
            Margin = Dpi.P(0, 0, 8, 0);
        }

        int GlyphWidth => glyph == null ? 0 : TextRenderer.MeasureText(glyph, Theme.Icons, Size.Empty, TextFormatFlags.NoPadding).Width + Dpi.S(8);

        public override Size GetPreferredSize(Size proposed)
        {
            var t = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            return new Size(t.Width + GlyphWidth + Padding.Horizontal, Math.Max(t.Height, Dpi.S(18)) + Padding.Vertical + 2);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); down = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color fill, border, fore;
            switch (kind)
            {
                case ButtonKind.Primary:
                    fill = down ? Theme.AccentPressed : hover ? Theme.AccentHover : Theme.Accent;
                    border = fill; fore = Color.White; break;
                case ButtonKind.Secondary:
                    fill = down ? Theme.Pressed : hover ? Theme.Hover : Color.White;
                    border = Theme.BorderStrong; fore = Theme.Text; break;
                default:
                    fill = down ? Theme.Pressed : hover ? Theme.Hover : Parent?.BackColor ?? Theme.Page;
                    border = fill; fore = Theme.Text; break;
            }
            if (!Enabled) { fore = Theme.Hint; if (kind == ButtonKind.Primary) fill = border = Theme.BorderStrong; }
            Theme.PaintBox(g, this, fill, border, 7);
            if (Focused && ShowFocusCues)
            {
                float k = Dpi.K;
                using (var path = Theme.Round(new RectangleF(2.5f * k, 2.5f * k, Width - 5.5f * k, Height - 5.5f * k), 5 * k))
                using (var pen = new Pen(kind == ButtonKind.Primary ? Color.White : Theme.Accent, 1.5f * k) { DashStyle = DashStyle.Dot })
                    g.DrawPath(pen, path);
            }

            var tsize = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            int total = tsize.Width + GlyphWidth, x = (Width - total) / 2;
            if (glyph != null)
            {
                TextRenderer.DrawText(g, glyph, Theme.Icons, new Rectangle(x, 0, GlyphWidth, Height), fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                x += GlyphWidth;
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, tsize.Width + 2, Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    public enum PillKind { Neutral, Success, Danger }

    /// <summary>Rounded status badge: "Cuadra" in green, "Faltan $X" in red, or a grey hint.</summary>
    public class StatusPill : Control
    {
        PillKind kind;

        public StatusPill()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.Stat;
        }

        public void Set(PillKind k, string text) { kind = k; Text = text; Invalidate(); }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color fill, fore; string icon;
            switch (kind)
            {
                case PillKind.Success: fill = Theme.SuccessSoft; fore = Theme.Success; icon = ""; break;
                case PillKind.Danger: fill = Theme.DangerSoft; fore = Theme.Danger; icon = ""; break;
                default: fill = Theme.ReadOnlyFill; fore = Theme.Muted; icon = ""; break;
            }
            Theme.PaintBox(e.Graphics, this, fill, fill, 8);
            var t = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            int iw = Dpi.S(26), x = (Width - t.Width - iw) / 2;
            TextRenderer.DrawText(e.Graphics, icon, Theme.Icons, new Rectangle(x, 0, iw, Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(x + iw, 0, t.Width + 2, Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
