using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CorteDeCaja
{
    /// <summary>List of saved cortes: reopen, reprint or delete them.</summary>
    public class HistoryForm : Form
    {
        readonly AppSettings settings;
        readonly ListView list = new ListView { View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = false, Dock = DockStyle.Fill, HideSelection = false };
        readonly TextBox search = new NoteBox(Lang.T("Buscar por agente o fecha…", "Search by agent or date…"), 260);
        readonly Label summary = new Label { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(12, 6, 0, 0) };

        public Corte Selected { get; private set; }

        public HistoryForm(AppSettings settings)
        {
            this.settings = settings;
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = Lang.T("Historial de cortes", "Count history");
            Font = new Font("Segoe UI", 10f);
            Size = new Size(1050, 600);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;

            foreach (var (title, width, right) in new[]
            {
                (Lang.T("Fecha", "Date"), 100, false), (Lang.T("Día", "Day"), 110, false), (Lang.T("Agente", "Agent"), 160, false), (Lang.T("Entradas", "Money in"), 110, true),
                (Lang.T("Desglose", "Breakdown"), 110, true), (Lang.T("# Cheques", "# Checks"), 85, true), (Lang.T("Total cheques", "Checks total"), 120, true),
                (Lang.T("Estado", "Status"), 140, false), (Lang.T("Guardado", "Saved"), 160, false),
            })
                // Auto-scaling doesn't reach ListView columns, so scale them by hand.
                list.Columns.Add(title, LogicalToDeviceUnits(width), right ? HorizontalAlignment.Right : HorizontalAlignment.Left);
            list.DoubleClick += (s, e) => OpenSelected();

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            top.Controls.Add(search);
            top.Controls.Add(summary);
            search.TextChanged += (s, e) => Reload();

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
            void B(string text, Action click)
            {
                var b = new Button { Text = text, AutoSize = true, Padding = new Padding(6, 2, 6, 2) };
                b.Click += (s, e) => click();
                bottom.Controls.Add(b);
            }
            B(Lang.T("Abrir / editar", "Open / edit"), OpenSelected);
            B(Lang.T("Imprimir térmica", "Print thermal"), () => PrintSelected(true));
            B(Lang.T("Imprimir hoja", "Print sheet"), () => PrintSelected(false));
            B(Lang.T("Borrar", "Delete"), DeleteSelected);
            B(Lang.T("Abrir carpeta", "Open folder"), () => { Directory.CreateDirectory(Storage.CortesDir); Process.Start("explorer.exe", Storage.CortesDir); });
            B(Lang.T("Cerrar", "Close"), Close);

            Controls.Add(list);
            Controls.Add(top);
            Controls.Add(bottom);
            Reload();
            ResumeLayout(false);
            PerformLayout();
        }

        void Reload()
        {
            var q = search.Text.Trim().ToLowerInvariant();
            var cortes = Storage.LoadCortes().Where(c =>
                q.Length == 0 ||
                c.Agente.ToLowerInvariant().Contains(q) ||
                Fmt.Date(c.FechaDate).Contains(q) ||
                Lang.DayName(c.FechaDate).ToLowerInvariant().Contains(q)).ToList();

            list.BeginUpdate();
            list.Items.Clear();
            foreach (var c in cortes)
            {
                var item = new ListViewItem(new[]
                {
                    Fmt.Date(c.FechaDate), Lang.DayName(c.FechaDate), c.Agente, Fmt.Money(c.TotalEntradas), Fmt.Money(c.TotalDesglose),
                    c.Cheques.Count.ToString(), Fmt.Money(c.TotalCheques), Lang.Status(c), c.GuardadoEn ?? "",
                }) { Tag = c };
                if (c.Diferencia != 0) item.ForeColor = Color.Firebrick;
                list.Items.Add(item);
            }
            list.EndUpdate();
            summary.Text = cortes.Count + Lang.T(" cortes · ", " counts · ") + Lang.Checks(cortes.Sum(c => c.Cheques.Count)) + Lang.T(" por ", " for ") + Fmt.Money(cortes.Sum(c => c.TotalCheques));
        }

        Corte Current()
        {
            if (list.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, Lang.T("Seleccione un corte de la lista.", "Select a count from the list."), Text);
                return null;
            }
            return (Corte)list.SelectedItems[0].Tag;
        }

        void OpenSelected()
        {
            var c = Current();
            if (c == null) return;
            Selected = c;
            DialogResult = DialogResult.OK;
        }

        void PrintSelected(bool thermal)
        {
            var c = Current();
            if (c != null) Receipt.Print(c, thermal, settings, this);
        }

        void DeleteSelected()
        {
            var c = Current();
            if (c == null) return;
            if (MessageBox.Show(this, Lang.T($"¿Borrar el corte del {Fmt.Date(c.FechaDate)} de {c.Agente}?\nEsto no se puede deshacer.", $"Delete the {Fmt.Date(c.FechaDate)} count by {c.Agente}?\nThis can't be undone."), Lang.T("Borrar", "Delete"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            Storage.DeleteCorte(c);
            Reload();
        }
    }
}
