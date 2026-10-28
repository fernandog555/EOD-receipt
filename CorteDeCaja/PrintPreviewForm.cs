using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace CorteDeCaja
{
    /// <summary>Shows the receipt as it will print, with a printer picker. OK means "print now".</summary>
    public class PrintPreviewForm : Form
    {
        public PrintPreviewForm(PrintDocument doc, Action<PrintDocument> applyPageSettings, string title)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = title;
            Font = new Font("Segoe UI", 10f);
            Size = new Size(760, 900);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;

            var preview = new PrintPreviewControl { Document = doc, Dock = DockStyle.Fill, AutoZoom = true, UseAntiAlias = true };

            var printers = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330, Margin = new Padding(3, 6, 12, 3) };
            foreach (string p in PrinterSettings.InstalledPrinters) printers.Items.Add(p);
            printers.SelectedItem = doc.PrinterSettings.PrinterName;
            printers.SelectedIndexChanged += (s, e) =>
            {
                doc.PrinterSettings.PrinterName = (string)printers.SelectedItem;
                applyPageSettings(doc);
                preview.InvalidatePreview();
            };

            var print = new Button { Text = Lang.T("Imprimir", "Print"), AutoSize = true, DialogResult = DialogResult.OK, Padding = new Padding(10, 2, 10, 2),
                BackColor = Color.FromArgb(0, 90, 160), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            var cancel = new Button { Text = Lang.T("Cancelar", "Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel, Padding = new Padding(6, 2, 6, 2) };
            AcceptButton = print;
            CancelButton = cancel;

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = false };
            bar.Controls.Add(new Label { Text = Lang.T("Impresora:", "Printer:"), AutoSize = true, Margin = new Padding(3, 9, 3, 3) });
            bar.Controls.Add(printers);
            bar.Controls.Add(print);
            bar.Controls.Add(cancel);

            Controls.Add(preview);
            Controls.Add(bar);
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
