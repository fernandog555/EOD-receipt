using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CorteDeCaja
{
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
            float k = c.DeviceDpi / 96f, w = borderWidth * k;
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
            new Panel { Size = new Size(width, 1), BackColor = Border, Margin = new Padding(0, 6, 0, 8) };

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
            Padding = new Padding(16, 12, 16, 14);
            Margin = new Padding(0, 0, 0, 12);
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
            Size = new Size(width, height);
            Padding = new Padding(2);
            Margin = new Padding(0);
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
            Margin = new Padding(0, 4, 8, 4);
            // Room on the left for the "$" sign in amount fields.
            Padding = new Padding(money ? 24 : 10, 6, 10, 6);
            box.BorderStyle = BorderStyle.None;
            box.Margin = Padding.Empty;
            Width = width;
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
            Width = width;
            Margin = new Padding(0, 4, 12, 4);
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
        void FitHeight() => Height = TextRenderer.MeasureText("Ág", Font).Height + LogicalToDeviceUnits(12) + 1;

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
            if (drop != null && drop.Visible) { drop.Close(); return; }
            var cal = new MonthCalendar { MaxSelectionCount = 1, SelectionStart = date, ShowTodayCircle = true, Margin = Padding.Empty };
            cal.DateSelected += (s, e) => { Value = e.Start; drop.Close(); };
            drop = new ToolStripDropDown { Padding = Padding.Empty, DropShadowEnabled = true };
            drop.Items.Add(new ToolStripControlHost(cal) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = cal.Size });
            drop.Closed += (s, e) => { var d = (ToolStripDropDown)s; BeginInvoke((Action)d.Dispose); Invalidate(); };
            drop.Show(this, new Point(0, Height + 2));
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var border = Focused ? Theme.Accent : hover ? Theme.BorderStrong : Theme.Border;
            Theme.PaintBox(g, this, Color.White, border, 6, Focused ? 1.6f : 1f);
            int pad = LogicalToDeviceUnits(10), icon = LogicalToDeviceUnits(22);
            var text = date.ToString("ddd", Es).TrimEnd('.');
            text = char.ToUpper(text[0], Es) + text.Substring(1) + "  " + Fmt.Date(date);
            TextRenderer.DrawText(g, text, Font, new Rectangle(pad, 0, Width - pad - icon - pad, Height), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, "", Theme.Icons, new Rectangle(Width - pad - icon, 0, icon, Height), Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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
            Padding = new Padding(14, 7, 16, 7);
            Margin = new Padding(0, 0, 8, 0);
        }

        int GlyphWidth => glyph == null ? 0 : TextRenderer.MeasureText(glyph, Theme.Icons, Size.Empty, TextFormatFlags.NoPadding).Width + LogicalToDeviceUnits(8);

        public override Size GetPreferredSize(Size proposed)
        {
            var t = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            return new Size(t.Width + GlyphWidth + Padding.Horizontal, Math.Max(t.Height, LogicalToDeviceUnits(18)) + Padding.Vertical + 2);
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
                float k = DeviceDpi / 96f;
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
            int iw = LogicalToDeviceUnits(26), x = (Width - t.Width - iw) / 2;
            TextRenderer.DrawText(e.Graphics, icon, Theme.Icons, new Rectangle(x, 0, iw, Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(x + iw, 0, t.Width + 2, Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
