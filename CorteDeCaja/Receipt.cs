using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace CorteDeCaja
{
    /// <summary>Lays out a corte as a receipt and prints it on a thermal roll or a normal sheet.</summary>
    public class Receipt
    {
        enum Kind { Text, Rule, Space, Image }

        class Item
        {
            public Kind Kind;
            public string Left, Right;
            public bool Bold, Big, Center;
            public Image Img;
        }

        readonly List<Item> items = new List<Item>();
        readonly bool thermal;
        readonly Font font, bold, big;
        readonly float contentWidth; // hundredths of an inch
        readonly int paperWidth;
        bool preview;
        int next;

        Receipt(Corte c, bool thermal, AppSettings s)
        {
            this.thermal = thermal;
            float size = !thermal ? 11f : s.AnchoTermicaMm == 58 ? 8f : 9f;
            font = new Font("Arial", size);
            bold = new Font("Arial", size, FontStyle.Bold);
            big = new Font("Arial", size * 1.35f, FontStyle.Bold);
            // Printable width of common 80mm / 58mm roll printers is 72mm / 48mm.
            contentWidth = !thermal ? 600 : s.AnchoTermicaMm == 58 ? 189 : 283;
            paperWidth = (int)Math.Round(s.AnchoTermicaMm / 25.4 * 100);
            Build(c, s);
        }

        public static void Print(Corte c, bool thermal, AppSettings s, IWin32Window owner)
        {
            var r = new Receipt(c, thermal, s);
            using (var doc = new PrintDocument())
            {
                doc.DocumentName = "Corte de caja " + c.Fecha;
                doc.OriginAtMargins = false;
                doc.BeginPrint += (o, e) =>
                {
                    r.next = 0;
                    r.preview = doc.PrintController is PreviewPrintController;
                };
                doc.PrintPage += r.OnPrintPage;

                var saved = thermal ? s.ImpresoraTermica : s.ImpresoraHoja;
                if (saved.Length > 0) doc.PrinterSettings.PrinterName = saved;
                bool known = saved.Length > 0 && doc.PrinterSettings.IsValid;
                if (!known) doc.PrinterSettings = new PrinterSettings(); // Windows default printer
                r.ApplyPageSettings(doc);

                // Windows 11's own print dialog can't preview for this kind of app, so use our own window.
                if (!known || !s.ImprimirDirecto)
                {
                    var title = thermal ? "Vista previa — recibo térmico" : "Vista previa — hoja";
                    using (var f = new PrintPreviewForm(doc, r.ApplyPageSettings, title))
                        if (f.ShowDialog(owner) != DialogResult.OK) return;
                    if (doc.PrinterSettings.PrinterName != saved)
                    {
                        if (thermal) s.ImpresoraTermica = doc.PrinterSettings.PrinterName;
                        else s.ImpresoraHoja = doc.PrinterSettings.PrinterName;
                        Storage.SaveSettings(s);
                    }
                }

                try
                {
                    doc.Print();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(owner, "No se pudo imprimir:\n" + ex.Message, "Imprimir", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>Renders to an image at 100 dpi (used by the --demo option to check the layout).</summary>
        public static Bitmap RenderToBitmap(Corte c, bool thermal, AppSettings s)
        {
            var r = new Receipt(c, thermal, s);
            int w = thermal ? r.paperWidth : 850;
            int h = (int)r.MeasureHeight() + 40;
            var bmp = new Bitmap(w, h);
            bmp.SetResolution(100, 100);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                float x = (w - r.contentWidth) / 2f, y = 20;
                foreach (var it in r.items) y += r.DrawItem(g, it, x, y, true);
            }
            return bmp;
        }

        // Re-applied whenever the printer changes, since switching printers resets the page settings.
        void ApplyPageSettings(PrintDocument doc)
        {
            if (thermal)
            {
                // One long page sized to the content; roll printers cut after the last line.
                int height = Math.Max(300, (int)MeasureHeight() + 60);
                doc.DefaultPageSettings.PaperSize = new PaperSize("Recibo", paperWidth, height);
                doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            }
            else
            {
                doc.DefaultPageSettings.Margins = new Margins(75, 75, 75, 75);
            }
        }

        void OnPrintPage(object sender, PrintPageEventArgs e)
        {
            var g = e.Graphics;
            // On a real printer (0,0) is the corner of the printable area, in preview it is the paper corner.
            float hx = preview ? 0 : e.PageSettings.HardMarginX;
            float hy = preview ? 0 : e.PageSettings.HardMarginY;
            float x, y, top, bottom;
            if (thermal)
            {
                x = Math.Max(0, (e.PageBounds.Width - contentWidth) / 2f - hx);
                y = top = 10;
                bottom = float.MaxValue;
            }
            else
            {
                float w = Math.Min(e.MarginBounds.Width, contentWidth);
                x = e.MarginBounds.Left + (e.MarginBounds.Width - w) / 2f - hx;
                y = top = e.MarginBounds.Top - hy;
                bottom = e.MarginBounds.Bottom - hy;
            }

            while (next < items.Count)
            {
                float h = DrawItem(g, items[next], x, y, false);
                if (y + h > bottom && y > top) { e.HasMorePages = true; return; }
                DrawItem(g, items[next], x, y, true);
                y += h;
                next++;
            }
            e.HasMorePages = false;
        }

        float MeasureHeight()
        {
            using (var bmp = new Bitmap(1, 1))
            {
                bmp.SetResolution(100, 100);
                using (var g = Graphics.FromImage(bmp))
                {
                    float y = 0;
                    foreach (var it in items) y += DrawItem(g, it, 0, y, false);
                    return y;
                }
            }
        }

        /// <summary>Returns the item's height; draws it only when <paramref name="draw"/> is true.</summary>
        float DrawItem(Graphics g, Item it, float x, float y, bool draw)
        {
            float w = contentWidth;
            switch (it.Kind)
            {
                case Kind.Rule:
                    if (draw) using (var pen = new Pen(Color.Black, 1f)) g.DrawLine(pen, x, y + 5, x + w, y + 5);
                    return 10;

                case Kind.Space:
                    return font.GetHeight(g) * 0.7f;

                case Kind.Image:
                {
                    float iw = Math.Min(w * 0.8f, it.Img.Width * 1.2f);
                    float ih = it.Img.Height * iw / it.Img.Width;
                    if (draw) g.DrawImage(it.Img, x + (w - iw) / 2, y, iw, ih);
                    return ih;
                }

                default:
                {
                    var f = it.Big ? big : it.Bold ? bold : font;
                    if (it.Right != null)
                    {
                        float rw = g.MeasureString(it.Right, f).Width;
                        float lw = Math.Max(20, w - rw - 4);
                        float h = Math.Max(f.GetHeight(g), g.MeasureString(it.Left, f, (int)lw).Height);
                        if (draw)
                        {
                            g.DrawString(it.Left, f, Brushes.Black, new RectangleF(x, y, lw, h));
                            g.DrawString(it.Right, f, Brushes.Black, x + w - rw, y);
                        }
                        return h;
                    }
                    using (var sf = new StringFormat { Alignment = it.Center ? StringAlignment.Center : StringAlignment.Near })
                    {
                        float h = g.MeasureString(it.Left, f, (int)w, sf).Height;
                        if (draw) g.DrawString(it.Left, f, Brushes.Black, new RectangleF(x, y, w, h), sf);
                        return h;
                    }
                }
            }
        }

        void Build(Corte c, AppSettings s)
        {
            void Text(string t, bool center = false, bool isBold = false, bool isBig = false) =>
                items.Add(new Item { Kind = Kind.Text, Left = t, Center = center, Bold = isBold, Big = isBig });
            void Line(string label, decimal amount, bool isBold = false) =>
                items.Add(new Item { Kind = Kind.Text, Left = label, Right = Fmt.Money(amount), Bold = isBold });
            void Note(string t) { if (t.Trim().Length > 0) Text("    " + t.Trim()); }
            void Rule() => items.Add(new Item { Kind = Kind.Rule });
            void Space() => items.Add(new Item { Kind = Kind.Space });

            var date = c.FechaDate;
            if (s.NombreNegocio.Trim().Length > 0) Text(s.NombreNegocio.Trim(), true, isBig: true);
            Text("CORTE DE CAJA", true, isBig: true);
            Text("DÍA " + Fmt.Day(date), true, true);
            Text(Fmt.Date(date), true);
            Text("Agente: " + c.Agente, true);
            Rule();

            Text("ENTRADAS", isBold: true);
            Line("W" + (c.WNota.Trim().Length > 0 ? "   X " + c.WNota.Trim() : ""), c.W);
            Line("+ Envíos", c.Envios);
            Line("Otro ingreso", c.OtroIngreso);
            Note(c.OtroIngresoNota);
            Rule();
            Line("TOTAL ENTRADAS", c.TotalEntradas, true);
            Space();

            Text("DESGLOSE", isBold: true);
            Line("Cash", c.Cash);
            Line("Cheques (" + c.Cheques.Count + ")", c.TotalCheques);
            foreach (var ch in c.Cheques) Line("    # " + ch.Numero, ch.Monto);
            Line("Máquina", c.Maquina);
            Line("Devolución", c.Devolucion);
            if (c.DevolucionPersona.Trim().Length > 0) Note("Persona: " + c.DevolucionPersona);
            if (c.DevolucionAgencia.Trim().Length > 0) Note("Agencia: " + c.DevolucionAgencia);
            Line("Compra", c.Compra);
            Note(c.CompraNota);
            Line("Otras cosas", c.Otros);
            Note(c.OtrosNota);
            Rule();
            Line("TOTAL DESGLOSE", c.TotalDesglose, true);
            Space();
            Text("*** " + c.Estado + " ***", true, true);
            Space();
            Line("TOTAL DE CHEQUES (" + c.Cheques.Count + ")", c.TotalCheques, true);
            Space();
            Space();

            var sig = SignaturePad.LoadCropped(c.FirmaPng);
            if (sig != null) items.Add(new Item { Kind = Kind.Image, Img = sig });
            Text("X______________________________", true);
            Text("Firma: " + c.Agente, true);
            if (!thermal)
            {
                Space(); Space(); Space();
                Text("Recibido por: ______________________________", true);
            }
            Space();
            if (!string.IsNullOrEmpty(c.GuardadoEn)) Text("Guardado: " + c.GuardadoEn, true);
            if (thermal) { Space(); Space(); Space(); }
        }
    }
}
