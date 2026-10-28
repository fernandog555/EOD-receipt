using System;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace CorteDeCaja
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length == 2 && args[0] == "--demo")
            {
                RenderDemo(args[1]);
                return;
            }
            Application.Run(new MainForm());
        }

        /// <summary>Writes PNGs of the main window and both receipt layouts, filled with the paper form's example.</summary>
        static void RenderDemo(string dir)
        {
            Directory.CreateDirectory(dir);
            var c = new Corte
            {
                Fecha = "2026-10-25", Agente = "Carlos F.", GuardadoEn = "10/25/2026 6:42 PM",
                W = 1000, WNota = "CF", Envios = 150, OtroIngreso = 100, OtroIngresoNota = "Pago de factura de luz",
                Cash = 200, Maquina = 400, Devolucion = 100, DevolucionPersona = "María López", DevolucionAgencia = "Intermex",
                Compra = 50, CompraNota = "Papel para impresora",
            };
            c.Cheques.Add(new Cheque { Numero = "1043", Monto = 300 });
            c.Cheques.Add(new Cheque { Numero = "5521", Monto = 200 });
            using (var sig = new System.Drawing.Bitmap(440, 150))
            using (var g = System.Drawing.Graphics.FromImage(sig))
            using (var ms = new MemoryStream())
            {
                g.Clear(System.Drawing.Color.White);
                using (var pen = new System.Drawing.Pen(System.Drawing.Color.Black, 2.5f))
                    g.DrawBezier(pen, 60, 100, 120, 20, 200, 140, 330, 60);
                sig.Save(ms, ImageFormat.Png);
                c.FirmaPng = Convert.ToBase64String(ms.ToArray());
            }

            var s = new AppSettings { NombreNegocio = "Mi Negocio" };
            using (var b = Receipt.RenderToBitmap(c, true, s)) b.Save(Path.Combine(dir, "recibo-termica.png"), ImageFormat.Png);
            using (var b = Receipt.RenderToBitmap(c, false, s)) b.Save(Path.Combine(dir, "recibo-hoja.png"), ImageFormat.Png);

            using (var f = new MainForm())
            {
                f.ShowCorte(c);
                f.TopMost = true;
                f.Show();
                // Let the window finish its open animation before capturing it.
                for (int i = 0; i < 20; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
                f.Refresh();
                Application.DoEvents();
                using (var w = new StreamWriter(Path.Combine(dir, "layout.txt")))
                {
                    w.WriteLine("DeviceDpi=" + f.DeviceDpi + " Size=" + f.Size);
                    void Dump(System.Windows.Forms.Control c, int depth)
                    {
                        w.WriteLine(new string(' ', depth * 2) + c.GetType().Name + " " + c.Bounds + " pad=" + c.Padding + " mar=" + c.Margin + (c is System.Windows.Forms.TextBox t ? " font=" + t.Font.Height + " text=" + t.Text : ""));
                        foreach (System.Windows.Forms.Control k in c.Controls) Dump(k, depth + 1);
                    }
                    Dump(f, 0);
                }
                // Capture the screen rather than DrawToBitmap, which draws native text boxes at the wrong size on high-DPI screens.
                void Capture(string name)
                {
                    for (int i = 0; i < 6; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
                    f.Refresh();
                    using (var b = new System.Drawing.Bitmap(f.Width, f.Height))
                    {
                        using (var g = System.Drawing.Graphics.FromImage(b)) g.CopyFromScreen(f.Location, System.Drawing.Point.Empty, f.Size);
                        b.Save(Path.Combine(dir, name), ImageFormat.Png);
                    }
                }
                // Both languages; the switch is not saved, so the user's choice is left alone.
                bool wasEn = Lang.En;
                Lang.Set(false);
                Capture("ventana.png");
                Lang.Set(true);
                Capture("ventana-en.png");
                Lang.Set(wasEn);
                f.Close();
            }
        }
    }
}
