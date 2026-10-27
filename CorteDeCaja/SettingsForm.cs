using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace CorteDeCaja
{
    public class SettingsForm : Form
    {
        public SettingsForm(AppSettings s)
        {
            Text = "Configuración";
            Font = new Font("Segoe UI", 10f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var negocio = new TextBox { Text = s.NombreNegocio, Width = 320 };
            var termica = PrinterCombo(s.ImpresoraTermica);
            var hoja = PrinterCombo(s.ImpresoraHoja);
            var ancho = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            ancho.Items.AddRange(new object[] { "80 mm", "58 mm" });
            ancho.SelectedIndex = s.AnchoTermicaMm == 58 ? 1 : 0;
            var preview = new CheckBox { Text = "Mostrar vista previa antes de imprimir", Checked = s.VistaPrevia, AutoSize = true };

            var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(12) };
            void Row(string label, Control c)
            {
                int r = t.RowCount++;
                t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, r);
                t.Controls.Add(c, 1, r);
            }
            Row("Nombre del negocio (sale en el recibo):", negocio);
            Row("Impresora térmica:", termica);
            Row("Ancho del papel térmico:", ancho);
            Row("Impresora de hojas:", hoja);
            Row("", preview);
            Row("Los cortes se guardan en:", new Label { Text = Storage.CortesDir, AutoSize = true, ForeColor = Color.DimGray });

            var ok = new Button { Text = "Guardar", AutoSize = true, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
            var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            Row("", buttons);
            AcceptButton = ok;
            CancelButton = cancel;
            Controls.Add(t);

            ok.Click += (o, e) =>
            {
                s.NombreNegocio = negocio.Text.Trim();
                s.ImpresoraTermica = termica.SelectedIndex > 0 ? (string)termica.SelectedItem : "";
                s.ImpresoraHoja = hoja.SelectedIndex > 0 ? (string)hoja.SelectedItem : "";
                s.AnchoTermicaMm = ancho.SelectedIndex == 1 ? 58 : 80;
                s.VistaPrevia = preview.Checked;
                Storage.SaveSettings(s);
            };
        }

        static ComboBox PrinterCombo(string selected)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
            c.Items.Add("(preguntar al imprimir)");
            foreach (string p in PrinterSettings.InstalledPrinters) c.Items.Add(p);
            c.SelectedIndex = System.Math.Max(0, c.Items.IndexOf(selected));
            return c;
        }
    }
}
