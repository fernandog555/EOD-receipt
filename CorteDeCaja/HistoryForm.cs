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
        readonly TextBox search = new NoteBox("Buscar por agente o fecha…", 260);
        readonly Label summary = new Label { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(12, 6, 0, 0) };

        public Corte Selected { get; private set; }

        public HistoryForm(AppSettings settings)
        {
            this.settings = settings;
            Text = "Historial de cortes";
            Font = new Font("Segoe UI", 10f);
            Size = new Size(1050, 600);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;

            foreach (var (title, width, right) in new[]
            {
                ("Fecha", 100, false), ("Día", 110, false), ("Agente", 160, false), ("Entradas", 110, true),
                ("Desglose", 110, true), ("# Cheques", 85, true), ("Total cheques", 120, true), ("Estado", 140, false), ("Guardado", 160, false),
            })
                list.Columns.Add(title, width, right ? HorizontalAlignment.Right : HorizontalAlignment.Left);
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
            B("Abrir / editar", OpenSelected);
            B("Imprimir Térmica", () => PrintSelected(true));
            B("Imprimir Hoja", () => PrintSelected(false));
            B("Borrar", DeleteSelected);
            B("Abrir carpeta", () => { Directory.CreateDirectory(Storage.CortesDir); Process.Start("explorer.exe", Storage.CortesDir); });
            B("Cerrar", Close);

            Controls.Add(list);
            Controls.Add(top);
            Controls.Add(bottom);
            Reload();
        }

        void Reload()
        {
            var q = search.Text.Trim().ToLowerInvariant();
            var cortes = Storage.LoadCortes().Where(c =>
                q.Length == 0 ||
                c.Agente.ToLowerInvariant().Contains(q) ||
                Fmt.Date(c.FechaDate).Contains(q) ||
                Fmt.Day(c.FechaDate).ToLowerInvariant().Contains(q)).ToList();

            list.BeginUpdate();
            list.Items.Clear();
            foreach (var c in cortes)
            {
                var item = new ListViewItem(new[]
                {
                    Fmt.Date(c.FechaDate), Fmt.Day(c.FechaDate), c.Agente, Fmt.Money(c.TotalEntradas), Fmt.Money(c.TotalDesglose),
                    c.Cheques.Count.ToString(), Fmt.Money(c.TotalCheques), c.Estado, c.GuardadoEn ?? "",
                }) { Tag = c };
                if (c.Diferencia != 0) item.ForeColor = Color.Firebrick;
                list.Items.Add(item);
            }
            list.EndUpdate();
            summary.Text = cortes.Count + " cortes · " + cortes.Sum(c => c.Cheques.Count) + " cheques por " + Fmt.Money(cortes.Sum(c => c.TotalCheques));
        }

        Corte Current()
        {
            if (list.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Seleccione un corte de la lista.", Text);
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
            if (MessageBox.Show(this, $"¿Borrar el corte del {Fmt.Date(c.FechaDate)} de {c.Agente}?\nEsto no se puede deshacer.", "Borrar",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            Storage.DeleteCorte(c);
            Reload();
        }
    }
}
