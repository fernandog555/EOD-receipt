using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace CorteDeCaja
{
    public class MainForm : Form
    {
        static readonly Color Accent = Color.FromArgb(0, 90, 160);

        readonly AppSettings settings;
        Corte current;
        bool dirty, loading;

        readonly DateTimePicker dtFecha = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "MM/dd/yyyy", Width = 130 };
        readonly Label lblDia = new Label { AutoSize = true, Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = Accent, Margin = new Padding(10, 2, 20, 0) };
        readonly NoteBox txtAgente = new NoteBox("Nombre del agente", 200);

        readonly MoneyBox mW = new MoneyBox(), mEnvios = new MoneyBox(), mOtroIng = new MoneyBox();
        readonly NoteBox nW = new NoteBox("X (iniciales / nota)"), nOtroIng = new NoteBox("¿Qué fue? (obligatorio)", 366);

        readonly MoneyBox mCash = new MoneyBox(), mMaquina = new MoneyBox(), mDevol = new MoneyBox(), mCompra = new MoneyBox(), mOtros = new MoneyBox();
        readonly TextBox tCheques = new TextBox { ReadOnly = true, TabStop = false, TextAlign = HorizontalAlignment.Right, Width = 120 };
        readonly Label lblChequesInfo = new Label { AutoSize = true, ForeColor = Color.DimGray, Anchor = AnchorStyles.Left };
        readonly NoteBox nDevPersona = new NoteBox("Nombre de la persona"), nDevAgencia = new NoteBox("Agencia");
        readonly NoteBox nCompra = new NoteBox("¿Qué se compró? (ej. papel)", 366), nOtros = new NoteBox("¿Qué fue? (obligatorio)", 366);

        readonly Label lblTotEntradas = TotalLabel(), lblTotDesglose = TotalLabel();
        readonly Label lblEstado = new Label { AutoSize = true, Font = new Font("Segoe UI", 16f, FontStyle.Bold), Margin = new Padding(3, 10, 3, 3) };
        readonly Label lblGuardado = new Label { AutoSize = true, ForeColor = Color.DimGray, Anchor = AnchorStyles.Left, Margin = new Padding(12, 0, 0, 0) };

        readonly DataGridView grid = new DataGridView();
        readonly SignaturePad firma = new SignaturePad(440, 150);

        public MainForm()
        {
            settings = Storage.LoadSettings();
            Text = "Corte de Caja";
            Font = new Font("Segoe UI", 10f);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Color.White;

            var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 3, Padding = new Padding(12), Location = new Point(0, 0) };
            root.Controls.Add(BuildHeader(), 0, 0);
            root.SetColumnSpan(root.Controls[0], 2);
            root.Controls.Add(BuildMoneyTable(), 0, 1);
            root.Controls.Add(BuildRightSide(), 1, 1);
            root.Controls.Add(BuildButtons(), 0, 2);
            root.SetColumnSpan(root.Controls[3], 2);
            Controls.Add(root);

            foreach (var box in new[] { mW, mEnvios, mOtroIng, mCash, mMaquina, mDevol, mCompra, mOtros })
                box.TextChanged += (s, e) => Changed();
            foreach (var box in new TextBox[] { txtAgente, nW, nOtroIng, nDevPersona, nDevAgencia, nCompra, nOtros })
                box.TextChanged += (s, e) => Changed();
            dtFecha.ValueChanged += (s, e) => { lblDia.Text = "DÍA " + Fmt.Day(dtFecha.Value); Changed(); };
            firma.Changed += (s, e) => Changed();

            ShowCorte(new Corte());
        }

        static Label TotalLabel() => new Label
        {
            AutoSize = false, Width = 120, Height = 26, TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold), Anchor = AnchorStyles.Left,
        };

        Control BuildHeader()
        {
            var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
            p.Controls.Add(new Label { Text = "CORTE DE CAJA", AutoSize = true, Font = new Font("Segoe UI", 16f, FontStyle.Bold), Margin = new Padding(0, 0, 20, 0) });
            p.Controls.Add(new Label { Text = "Fecha:", AutoSize = true, Margin = new Padding(3, 7, 3, 0) });
            p.Controls.Add(dtFecha);
            p.Controls.Add(lblDia);
            p.Controls.Add(new Label { Text = "Agente:", AutoSize = true, Margin = new Padding(3, 7, 3, 0) });
            p.Controls.Add(txtAgente);
            foreach (Control c in p.Controls) if (c is TextBox || c is DateTimePicker) c.Margin = new Padding(3, 4, 3, 0);
            return p;
        }

        Control BuildMoneyTable()
        {
            var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 4, Margin = new Padding(0, 0, 20, 0) };

            void Section(string title)
            {
                int r = t.RowCount++;
                var l = new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = Accent, Margin = new Padding(3, r == 0 ? 0 : 14, 3, 4) };
                t.Controls.Add(l, 0, r);
                t.SetColumnSpan(l, 4);
            }
            void Row(string label, Control amount, params Control[] extras)
            {
                int r = t.RowCount++;
                t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 0, 10, 0) }, 0, r);
                t.Controls.Add(amount, 1, r);
                int col = 2;
                foreach (var x in extras)
                {
                    t.Controls.Add(x, col, r);
                    if (extras.Length == 1 && x.Width > 300) t.SetColumnSpan(x, 2);
                    col++;
                }
            }
            void TotalRow(string label, Label total)
            {
                int r = t.RowCount++;
                t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 11f, FontStyle.Bold) }, 0, r);
                t.Controls.Add(total, 1, r);
            }

            Section("ENTRADAS — dinero que entró");
            Row("W", mW, nW);
            Row("+ Envíos", mEnvios);
            Row("Otro ingreso", mOtroIng, nOtroIng);
            TotalRow("TOTAL", lblTotEntradas);

            Section("DESGLOSE — cómo se entrega");
            Row("Cash", mCash);
            Row("Cheques", tCheques, lblChequesInfo);
            t.SetColumnSpan(lblChequesInfo, 2);
            Row("Máquina", mMaquina);
            Row("Devolución", mDevol, nDevPersona, nDevAgencia);
            Row("Compra", mCompra, nCompra);
            Row("Otras cosas", mOtros, nOtros);
            TotalRow("TOTAL", lblTotDesglose);

            int er = t.RowCount++;
            t.Controls.Add(lblEstado, 0, er);
            t.SetColumnSpan(lblEstado, 4);
            return t;
        }

        Control BuildRightSide()
        {
            var p = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            p.Controls.Add(new Label { Text = "CHEQUES CAMBIADOS", AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = Accent, Margin = new Padding(3, 0, 3, 4) });

            grid.Size = new Size(440, 230);
            grid.BackgroundColor = Color.White;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersWidth = 30;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            grid.EnableHeadersVisualStyles = false;
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Numero", HeaderText = "Número de cheque", FillWeight = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Monto", HeaderText = "Monto", FillWeight = 40,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight },
            });
            grid.CellValueChanged += (s, e) => Changed();
            grid.RowsRemoved += (s, e) => Changed();
            grid.CellEndEdit += (s, e) =>
            {
                if (e.ColumnIndex != 1) return;
                var cell = grid.Rows[e.RowIndex].Cells[1];
                if (Fmt.TryParseMoney(cell.Value as string, out var v) && v != 0) cell.Value = v.ToString("0.00");
            };
            p.Controls.Add(grid);
            p.Controls.Add(new Label { Text = "Escriba cada cheque en una línea. Para quitar uno: selecciónelo a la izquierda y presione Supr.", AutoSize = true, MaximumSize = new Size(440, 0), ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8.5f) });

            p.Controls.Add(new Label { Text = "FIRMA DEL AGENTE", AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = Accent, Margin = new Padding(3, 14, 3, 4) });
            p.Controls.Add(firma);
            var clear = new Button { Text = "Borrar firma", AutoSize = true, TabStop = false };
            clear.Click += (s, e) => firma.Clear();
            p.Controls.Add(clear);
            return p;
        }

        Control BuildButtons()
        {
            var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 14, 0, 0) };
            Button B(string text, EventHandler click, bool primary = false)
            {
                var b = new Button { Text = text, AutoSize = true, Padding = new Padding(8, 4, 8, 4), Margin = new Padding(0, 0, 8, 0) };
                if (primary) { b.BackColor = Accent; b.ForeColor = Color.White; b.FlatStyle = FlatStyle.Flat; }
                b.Click += click;
                p.Controls.Add(b);
                return b;
            }
            B("Guardar", (s, e) => SaveCurrent(), true);
            B("Imprimir Térmica", (s, e) => PrintCurrent(true));
            B("Imprimir Hoja", (s, e) => PrintCurrent(false));
            B("Nuevo corte", (s, e) => { if (ConfirmDiscard()) ShowCorte(new Corte()); });
            B("Historial", (s, e) => OpenHistory());
            B("Configuración", (s, e) => { using (var f = new SettingsForm(settings)) f.ShowDialog(this); });
            p.Controls.Add(lblGuardado);
            return p;
        }

        void Changed()
        {
            if (loading) return;
            dirty = true;
            lblGuardado.Text = "Sin guardar";
            UpdateTotals();
        }

        void UpdateTotals()
        {
            var c = ReadForm(false);
            lblTotEntradas.Text = Fmt.Money(c.TotalEntradas);
            lblTotDesglose.Text = Fmt.Money(c.TotalDesglose);
            tCheques.Text = Fmt.Money(c.TotalCheques);
            lblChequesInfo.Text = c.Cheques.Count == 0 ? "← agréguelos en la lista de la derecha" : c.Cheques.Count + (c.Cheques.Count == 1 ? " cheque" : " cheques");

            if (c.TotalEntradas == 0 && c.TotalDesglose == 0) lblEstado.Text = "";
            else lblEstado.Text = c.Diferencia == 0 ? "✔ CUADRA" : c.Estado;
            lblEstado.ForeColor = c.Diferencia == 0 ? Color.ForestGreen : Color.Firebrick;
        }

        Corte ReadForm(bool withSignature = true)
        {
            var c = new Corte
            {
                Id = current.Id,
                GuardadoEn = current.GuardadoEn,
                Fecha = dtFecha.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Agente = txtAgente.Text.Trim(),
                W = mW.Value, WNota = nW.Text.Trim(),
                Envios = mEnvios.Value,
                OtroIngreso = mOtroIng.Value, OtroIngresoNota = nOtroIng.Text.Trim(),
                Cash = mCash.Value,
                Maquina = mMaquina.Value,
                Devolucion = mDevol.Value, DevolucionPersona = nDevPersona.Text.Trim(), DevolucionAgencia = nDevAgencia.Text.Trim(),
                Compra = mCompra.Value, CompraNota = nCompra.Text.Trim(),
                Otros = mOtros.Value, OtrosNota = nOtros.Text.Trim(),
                FirmaPng = withSignature ? firma.ToBase64() : null,
            };
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                var num = (row.Cells[0].Value as string ?? "").Trim();
                var text = row.Cells[1].Value as string;
                if (num.Length == 0 && string.IsNullOrWhiteSpace(text)) continue;
                Fmt.TryParseMoney(text, out var monto);
                c.Cheques.Add(new Cheque { Numero = num, Monto = monto });
            }
            return c;
        }

        internal void ShowCorte(Corte c)
        {
            loading = true;
            current = c;
            dtFecha.Value = c.FechaDate;
            lblDia.Text = "DÍA " + Fmt.Day(c.FechaDate);
            txtAgente.Text = c.Agente;
            mW.Value = c.W; nW.Text = c.WNota;
            mEnvios.Value = c.Envios;
            mOtroIng.Value = c.OtroIngreso; nOtroIng.Text = c.OtroIngresoNota;
            mCash.Value = c.Cash;
            mMaquina.Value = c.Maquina;
            mDevol.Value = c.Devolucion; nDevPersona.Text = c.DevolucionPersona; nDevAgencia.Text = c.DevolucionAgencia;
            mCompra.Value = c.Compra; nCompra.Text = c.CompraNota;
            mOtros.Value = c.Otros; nOtros.Text = c.OtrosNota;
            grid.Rows.Clear();
            foreach (var ch in c.Cheques) grid.Rows.Add(ch.Numero, ch.Monto.ToString("0.00"));
            firma.LoadBase64(c.FirmaPng);
            loading = false;
            dirty = false;
            lblGuardado.Text = c.GuardadoEn == null ? "" : "Guardado " + c.GuardadoEn;
            UpdateTotals();
        }

        List<string> FindProblems(Corte c)
        {
            var errors = new List<string>();
            foreach (var box in new[] { mW, mEnvios, mOtroIng, mCash, mMaquina, mDevol, mCompra, mOtros })
                if (!box.IsValid) { errors.Add("Hay una cantidad que no es un número (marcada en rojo)."); break; }
            foreach (DataGridViewRow row in grid.Rows)
                if (!row.IsNewRow && !Fmt.TryParseMoney(row.Cells[1].Value as string, out _)) { errors.Add("Hay un cheque con monto que no es un número."); break; }
            if (c.Agente.Length == 0) errors.Add("Escriba el nombre del agente.");
            if (c.OtroIngreso != 0 && c.OtroIngresoNota.Length == 0) errors.Add("Otro ingreso: escriba qué fue.");
            if (c.Devolucion != 0 && c.DevolucionPersona.Length == 0) errors.Add("Devolución: escriba el nombre de la persona.");
            if (c.Devolucion != 0 && c.DevolucionAgencia.Length == 0) errors.Add("Devolución: escriba la agencia.");
            if (c.Compra != 0 && c.CompraNota.Length == 0) errors.Add("Compra: escriba qué se compró.");
            if (c.Otros != 0 && c.OtrosNota.Length == 0) errors.Add("Otras cosas: escriba qué fue.");
            if (c.Cheques.Exists(ch => ch.Numero.Length == 0)) errors.Add("Cada cheque necesita su número.");
            if (c.Cheques.Exists(ch => ch.Monto == 0)) errors.Add("Cada cheque necesita su monto.");
            if (c.FirmaPng == null) errors.Add("Falta la firma del agente.");
            return errors;
        }

        bool SaveCurrent()
        {
            var c = ReadForm();
            var errors = FindProblems(c);
            if (errors.Count > 0)
            {
                MessageBox.Show(this, "No se puede guardar todavía:\n\n• " + string.Join("\n• ", errors), "Falta información", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (c.Diferencia != 0 &&
                MessageBox.Show(this, "El corte NO cuadra: " + c.Estado + ".\n\n¿Guardar de todos modos?", "No cuadra", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return false;

            c.GuardadoEn = DateTime.Now.ToString("MM/dd/yyyy h:mm tt", CultureInfo.InvariantCulture);
            try { Storage.SaveCorte(c); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo guardar:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            current = c;
            dirty = false;
            lblGuardado.Text = "✔ Guardado " + c.GuardadoEn;
            return true;
        }

        // Printing always saves first, so every printed corte is also in the history.
        void PrintCurrent(bool thermal)
        {
            if ((dirty || current.GuardadoEn == null) && !SaveCurrent()) return;
            Receipt.Print(current, thermal, settings, this);
        }

        void OpenHistory()
        {
            using (var f = new HistoryForm(settings))
            {
                if (f.ShowDialog(this) == DialogResult.OK && f.Selected != null && ConfirmDiscard())
                    ShowCorte(f.Selected);
            }
        }

        bool ConfirmDiscard() =>
            !dirty || MessageBox.Show(this, "Hay cambios sin guardar. ¿Descartarlos?", "Corte de Caja", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !ConfirmDiscard()) e.Cancel = true;
            base.OnFormClosing(e);
        }

    }
}
