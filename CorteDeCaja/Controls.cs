using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CorteDeCaja
{
    /// <summary>Text box for a dollar amount. Enter moves to the next field.</summary>
    public class MoneyBox : TextBox
    {
        public MoneyBox()
        {
            TextAlign = HorizontalAlignment.Right;
            Width = 120;
        }

        public decimal Value
        {
            get => Fmt.TryParseMoney(Text, out var v) ? v : 0;
            set => Text = value == 0 ? "" : value.ToString("0.00");
        }

        public bool IsValid => Fmt.TryParseMoney(Text, out _);

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.' && e.KeyChar != ',' && e.KeyChar != '$')
                e.Handled = true;
            base.OnKeyPress(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; FindForm()?.SelectNextControl(this, true, true, true, true); }
            base.OnKeyDown(e);
        }

        // Clicking into an amount selects it, so a new number replaces the old one.
        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            if (IsHandleCreated) BeginInvoke((Action)SelectAll);
        }

        protected override void OnLeave(EventArgs e)
        {
            if (Text.Trim().Length > 0 && Fmt.TryParseMoney(Text, out var v)) Text = v.ToString("0.00");
            base.OnLeave(e);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            BackColor = ReadOnly ? Theme.ReadOnlyFill : IsValid ? Color.White : Theme.DangerSoft;
            base.OnTextChanged(e);
        }
    }

    /// <summary>Single-line text box with grey hint text. Enter moves to the next field.</summary>
    public class NoteBox : TextBox
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        const int EM_SETCUEBANNER = 0x1501;

        readonly string cue;

        public NoteBox(string cue, int width = 180)
        {
            this.cue = cue;
            Width = width;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SendMessage(Handle, EM_SETCUEBANNER, (IntPtr)1, cue);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; FindForm()?.SelectNextControl(this, true, true, true, true); }
            base.OnKeyDown(e);
        }
    }

    /// <summary>Signature box: sign with the mouse, a touchscreen or a pen.</summary>
    public class SignaturePad : Control
    {
        Bitmap ink;
        Point? last;

        public bool HasInk { get; private set; }
        public event EventHandler Changed;

        public SignaturePad(int width, int height)
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Cursor = Cursors.Cross;
            Size = new Size(width, height);
        }

        float DpiScale => Dpi.K;

        // The pad grows on high-DPI screens, so keep the ink bitmap matched to the control size.
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width <= 0 || Height <= 0 || (ink != null && ink.Size == Size)) return;
            var old = ink;
            ink = new Bitmap(Width, Height);
            using (var g = Graphics.FromImage(ink))
            {
                g.Clear(Color.White);
                if (old != null) g.DrawImage(old, 0, 0, Width, Height);
            }
            old?.Dispose();
            Invalidate();
        }

        public void Clear(bool raise = true)
        {
            using (var g = Graphics.FromImage(ink)) g.Clear(Color.White);
            HasInk = false;
            Invalidate();
            if (raise) Changed?.Invoke(this, EventArgs.Empty);
        }

        public string ToBase64()
        {
            if (!HasInk) return null;
            using (var ms = new MemoryStream())
            {
                ink.Save(ms, ImageFormat.Png);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        public void LoadBase64(string b64)
        {
            Clear(false);
            if (string.IsNullOrEmpty(b64)) return;
            try
            {
                using (var ms = new MemoryStream(Convert.FromBase64String(b64)))
                using (var img = Image.FromStream(ms))
                using (var g = Graphics.FromImage(ink))
                {
                    // A signature saved at a different screen scaling is fitted into the current pad.
                    float k = Math.Min((float)ink.Width / img.Width, (float)ink.Height / img.Height);
                    g.DrawImage(img, 0, 0, img.Width * k, img.Height * k);
                }
                HasInk = true;
            }
            catch { }
            Invalidate();
        }

        /// <summary>Decodes a saved signature and trims the white space around the ink, for printing.</summary>
        public static Bitmap LoadCropped(string b64)
        {
            if (string.IsNullOrEmpty(b64)) return null;
            try
            {
                Bitmap full;
                using (var ms = new MemoryStream(Convert.FromBase64String(b64)))
                using (var img = Image.FromStream(ms))
                    full = new Bitmap(img);

                int minX = full.Width, minY = full.Height, maxX = -1, maxY = -1;
                for (int y = 0; y < full.Height; y++)
                    for (int x = 0; x < full.Width; x++)
                        if (full.GetPixel(x, y).GetBrightness() < 0.8f)
                        {
                            if (x < minX) minX = x; if (x > maxX) maxX = x;
                            if (y < minY) minY = y; if (y > maxY) maxY = y;
                        }
                if (maxX < 0) { full.Dispose(); return null; }

                const int pad = 4;
                var rect = Rectangle.FromLTRB(Math.Max(0, minX - pad), Math.Max(0, minY - pad),
                    Math.Min(full.Width, maxX + pad + 1), Math.Min(full.Height, maxY + pad + 1));
                var cropped = full.Clone(rect, full.PixelFormat);
                full.Dispose();
                return cropped;
            }
            catch { return null; }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Capture = true;
            last = e.Location;
            Stroke(e.Location, new Point(e.X + 1, e.Y));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (last == null || (e.Button & MouseButtons.Left) == 0) return;
            Stroke(last.Value, e.Location);
            last = e.Location;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            last = null;
            Capture = false;
        }

        void Stroke(Point a, Point b)
        {
            using (var g = Graphics.FromImage(ink))
            using (var pen = new Pen(Color.Black, 2.5f * DpiScale))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawLine(pen, a, b);
            }
            HasInk = true;
            Invalidate();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImage(ink, 0, 0);
            float k = DpiScale, y = Height - 28 * k;
            using (var pen = new Pen(Theme.BorderStrong)) g.DrawLine(pen, 30 * k, y, Width - 16 * k, y);
            using (var f = new Font("Segoe UI", 13f))
            using (var b = new SolidBrush(Theme.Hint)) g.DrawString("X", f, b, 10 * k, y - 24 * k);
            if (!HasInk)
                using (var f = new Font("Segoe UI", 9f))
                using (var b = new SolidBrush(Theme.Hint))
                    g.DrawString("Firme aquí con el mouse o el dedo", f, b, 30 * k, y + 4 * k);
            // Rounded border; the corners outside it are painted in the card color behind the pad.
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var outside = new System.Drawing.Drawing2D.GraphicsPath())
            using (var round = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 6 * k))
            using (var bg = new SolidBrush(Parent?.BackColor ?? Color.White))
            using (var pen = new Pen(Theme.Border))
            {
                outside.AddRectangle(new Rectangle(-1, -1, Width + 2, Height + 2));
                outside.AddPath(round, false);
                g.FillPath(bg, outside);
                g.DrawPath(pen, round);
            }
        }
    }
}
