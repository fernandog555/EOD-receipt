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
                f.Show();
                Application.DoEvents();
                using (var b = new System.Drawing.Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(b, new System.Drawing.Rectangle(0, 0, f.Width, f.Height));
                    b.Save(Path.Combine(dir, "ventana.png"), ImageFormat.Png);
                }
                f.Close();
            }
        }
    }
}
