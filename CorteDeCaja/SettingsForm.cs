using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace CorteDeCaja
{
    public class SettingsForm : Form
    {
        public SettingsForm(AppSettings s)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = Lang.T("Configuración", "Settings");
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
            var direct = new CheckBox { Text = Lang.T("Imprimir directo, sin vista previa", "Print directly, without preview"), Checked = s.ImprimirDirecto, AutoSize = true };

            var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(12) };
            void Row(string label, Control c)
            {
                int r = t.RowCount++;
                t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, r);
                t.Controls.Add(c, 1, r);
            }
            Row(Lang.T("Nombre del negocio (sale en el recibo):", "Business name (printed on the receipt):"), negocio);
            Row(Lang.T("Impresora térmica:", "Thermal printer:"), termica);
            Row(Lang.T("Ancho del papel térmico:", "Thermal paper width:"), ancho);
            Row(Lang.T("Impresora de hojas:", "Sheet printer:"), hoja);
            Row("", direct);
            Row(Lang.T("Los cortes se guardan en:", "Counts are saved in:"), new Label { Text = Storage.CortesDir, AutoSize = true, ForeColor = Color.DimGray });

            var ok = new Button { Text = Lang.T("Guardar", "Save"), AutoSize = true, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = Lang.T("Cancelar", "Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
            var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            Row("", buttons);
            AcceptButton = ok;
            CancelButton = cancel;
            Controls.Add(t);
            ResumeLayout(false);
            PerformLayout();

            ok.Click += (o, e) =>
            {
                s.NombreNegocio = negocio.Text.Trim();
                s.ImpresoraTermica = termica.SelectedIndex > 0 ? (string)termica.SelectedItem : "";
                s.ImpresoraHoja = hoja.SelectedIndex > 0 ? (string)hoja.SelectedItem : "";
                s.AnchoTermicaMm = ancho.SelectedIndex == 1 ? 58 : 80;
                s.ImprimirDirecto = direct.Checked;
                Storage.SaveSettings(s);
            };
        }

        static ComboBox PrinterCombo(string selected)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
            c.Items.Add(Lang.T("(elegir al imprimir)", "(choose when printing)"));
            foreach (string p in PrinterSettings.InstalledPrinters) c.Items.Add(p);
            c.SelectedIndex = System.Math.Max(0, c.Items.IndexOf(selected));
            return c;
        }
    }
}
