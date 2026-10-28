using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace CorteDeCaja
{
    public class MainForm : Form
    {
        // Layout grid, in pixels at 100% scaling (WinForms scales everything on high-DPI screens).
        // Left column holds the money, right column the cheques and signature; cards add 16px padding each side.
        const int LeftW = 560, RightW = 420, Gap = 16, CardPadX = 16;
        const int LabelW = 112, AmountW = 132, NoteW = LeftW - LabelW - 8 - AmountW - 8;

        static readonly CultureInfo Es = new CultureInfo("es-MX");

        readonly AppSettings settings;
        Corte current;
        bool dirty, loading;

        readonly DateField dtFecha = new DateField(160);
        readonly Label lblDia = Theme.MakeLabel("", Theme.Body, Theme.Muted, Dpi.P(0));
        readonly Label lblGuardado = Theme.MakeLabel("", Theme.Body, Theme.Muted, Dpi.P(0));
        readonly NoteBox txtAgente = new NoteBox("Nombre del agente");

        readonly MoneyBox mW = new MoneyBox(), mEnvios = new MoneyBox(), mOtroIng = new MoneyBox();
        readonly NoteBox nW = new NoteBox("X (iniciales / nota)"), nOtroIng = new NoteBox("¿Qué fue? (obligatorio)");

        readonly MoneyBox mCash = new MoneyBox(), mMaquina = new MoneyBox(), mDevol = new MoneyBox(), mCompra = new MoneyBox(), mOtros = new MoneyBox();
        readonly TextBox tCheques = new TextBox { ReadOnly = true, TabStop = false, TextAlign = HorizontalAlignment.Right, BackColor = Theme.ReadOnlyFill, ForeColor = Theme.Text };
        readonly Label lblChequesInfo = Theme.MakeLabel("", Theme.Small, Theme.Muted, Dpi.P(0));
        readonly NoteBox nDevPersona = new NoteBox("Nombre de la persona"), nDevAgencia = new NoteBox("Agencia");
        readonly NoteBox nCompra = new NoteBox("¿Qué se compró? (ej. papel)"), nOtros = new NoteBox("¿Qué fue? (obligatorio)");

        readonly Label lblTotEntradas = HeaderTotal(), lblTotDesglose = HeaderTotal();
        readonly Label statEntradas = StatValue(), statDesglose = StatValue(), statDif = StatValue();
        readonly StatusPill pill = new StatusPill { Size = Dpi.Sz(200, 42), Margin = Dpi.P(0, 1, 0, 1) };
        readonly Label lblChequesCount = new Label { AutoSize = false, Size = Dpi.Sz(200, 30), Location = Dpi.Pt(RightW - 200, 0), TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.Muted };

        readonly DataGridView grid = new DataGridView();
        readonly SignaturePad firma = new SignaturePad(Dpi.S(RightW), Dpi.S(162));

        public MainForm()
        {
            settings = Storage.LoadSettings();
            // Sizes below are written for 100% display scaling and pass through Dpi once.
            SuspendLayout();
            // Scaling is done by hand through Dpi (see Ui.cs); fonts are in points and follow the display on their own.
            AutoScaleMode = AutoScaleMode.None;
            Text = "Corte de Caja";
            Font = Theme.Body;
            ForeColor = Theme.Text;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Theme.Page;

            var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 3, Padding = Dpi.P(16, 12, 16, 4), Location = Dpi.Pt(0, 0) };
            var header = BuildHeader();
            root.Controls.Add(header, 0, 0);
            root.SetColumnSpan(header, 2);
            root.Controls.Add(BuildLeft(), 0, 1);
            root.Controls.Add(BuildRight(), 1, 1);
            var footer = BuildFooter();
            root.Controls.Add(footer, 0, 2);
            root.SetColumnSpan(footer, 2);
            Controls.Add(root);

            foreach (var box in new[] { mW, mEnvios, mOtroIng, mCash, mMaquina, mDevol, mCompra, mOtros })
                box.TextChanged += (s, e) => Changed();
            foreach (var box in new TextBox[] { txtAgente, nW, nOtroIng, nDevPersona, nDevAgencia, nCompra, nOtros })
                box.TextChanged += (s, e) => Changed();
            dtFecha.ValueChanged += (s, e) => { lblDia.Text = DayText(dtFecha.Value); Changed(); };
            firma.Changed += (s, e) => Changed();

            ShowCorte(new Corte());
            ResumeLayout(false);
            PerformLayout();
            ActiveControl = txtAgente;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.TintTitleBar(this);
        }

        static Label HeaderTotal() => new Label
        {
            AutoSize = false, Size = Dpi.Sz(240, 44), Location = Dpi.Pt(LeftW - 240, 0),
            TextAlign = ContentAlignment.MiddleRight, Font = Theme.Total, ForeColor = Theme.Text,
        };

        static Label StatValue() => new Label { AutoSize = true, Font = Theme.Stat, ForeColor = Theme.Text, Location = Dpi.Pt(0, 18) };

        static string DayText(DateTime d)
        {
            var t = d.ToString("dddd, d 'de' MMMM 'de' yyyy", Es);
            return char.ToUpper(t[0], Es) + t.Substring(1);
        }

        static Field F(TextBox box, int width, int rightMargin = 8)
        {
            var f = new Field(box, width);
            f.Margin = Dpi.P(0, 4, rightMargin, 4);
            return f;
        }

        // ── Header: title and day on the left, fecha and agente lined up with the right column ──
        Control BuildHeader()
        {
            int leftBlock = CardPadX * 2 + LeftW + Gap + CardPadX;
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Dpi.P(0, 0, 0, 12) };

            var title = new Panel { Size = Dpi.Sz(leftBlock, 60), Margin = Padding.Empty };
            title.Controls.Add(new Label { Text = "Corte de caja", AutoSize = true, Font = Theme.PageTitle, ForeColor = Theme.Text, Location = Dpi.Pt(-2, 0) });
            var sub = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Location = Dpi.Pt(0, 38), Margin = Padding.Empty };
            sub.Controls.Add(lblDia);
            sub.Controls.Add(Theme.MakeLabel("  ·  ", Theme.Body, Theme.Hint, Dpi.P(0)));
            sub.Controls.Add(lblGuardado);
            title.Controls.Add(sub);

            var fields = new Panel { Size = Dpi.Sz(RightW + CardPadX, 60), Margin = Padding.Empty };
            fields.Controls.Add(new Label { Text = "FECHA", AutoSize = true, Font = Theme.FieldLabel, ForeColor = Theme.Muted, Location = Dpi.Pt(0, 4) });
            dtFecha.Location = Dpi.Pt(0, 24);
            fields.Controls.Add(dtFecha);
            fields.Controls.Add(new Label { Text = "AGENTE", AutoSize = true, Font = Theme.FieldLabel, ForeColor = Theme.Muted, Location = Dpi.Pt(172, 4) });
            var agente = new Field(txtAgente, RightW - 172) { Location = Dpi.Pt(172, 24) };
            fields.Controls.Add(agente);

            row.Controls.Add(title);
            row.Controls.Add(fields);
            return row;
        }

        // ── Left column: Entradas, Desglose and the Cuadre check ──
        Control BuildLeft()
        {
            var col = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Dpi.P(0, 0, Gap, 0) };

            var entradas = MoneyCard("Entradas", "Dinero que entró", lblTotEntradas, out var t);
            Row(t, "Caja Negra", F(mW, AmountW), F(nW, NoteW, 0));
            Row(t, "Envíos", F(mEnvios, AmountW));
            Row(t, "Otro ingreso", F(mOtroIng, AmountW), F(nOtroIng, NoteW, 0));
            col.Controls.Add(entradas);

            var desglose = MoneyCard("Desglose", "Cómo se entrega", lblTotDesglose, out t);
            Row(t, "Cash", F(mCash, AmountW));
            lblChequesInfo.Anchor = AnchorStyles.Left;
            Row(t, "Cheques", F(tCheques, AmountW), lblChequesInfo);
            Row(t, "Máquina", F(mMaquina, AmountW));
            var dev = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            int half = (NoteW - 8) / 2;
            dev.Controls.Add(F(nDevPersona, half));
            dev.Controls.Add(F(nDevAgencia, NoteW - 8 - half, 0));
            Row(t, "Devolución", F(mDevol, AmountW), dev);
            Row(t, "Compra", F(mCompra, AmountW), F(nCompra, NoteW, 0));
            Row(t, "Otras cosas", F(mOtros, AmountW), F(nOtros, NoteW, 0));
            col.Controls.Add(desglose);

            var cuadre = new Card { Margin = Padding.Empty };
            var line = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            int statW = (LeftW - 200) / 3;
            line.Controls.Add(Stat("Entradas", statEntradas, statW));
            line.Controls.Add(Stat("Desglose", statDesglose, statW));
            line.Controls.Add(Stat("Diferencia", statDif, statW));
            line.Controls.Add(pill);
            cuadre.Add(line);
            col.Controls.Add(cuadre);
            return col;
        }

        static Panel Stat(string caption, Label value, int width)
        {
            var p = new Panel { Size = Dpi.Sz(width, 44), Margin = Padding.Empty };
            p.Controls.Add(new Label { Text = caption, AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Location = Dpi.Pt(0, 0) });
            p.Controls.Add(value);
            return p;
        }

        static Card MoneyCard(string title, string subtitle, Label total, out TableLayoutPanel rows)
        {
            var card = new Card();
            var head = new Panel { Size = Dpi.Sz(LeftW, 44), Margin = Padding.Empty };
            head.Controls.Add(new Label { Text = title, AutoSize = true, Font = Theme.CardTitle, ForeColor = Theme.Text, Location = Dpi.Pt(0, 0) });
            head.Controls.Add(new Label { Text = subtitle, AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Location = Dpi.Pt(0, 24) });
            head.Controls.Add(total);
            card.Add(head);
            card.Add(Theme.Separator(LeftW));
            rows = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Margin = Padding.Empty };
            card.Add(rows);
            return card;
        }

        static void Row(TableLayoutPanel t, string label, Control amount, Control extra = null)
        {
            int r = t.RowCount++;
            t.Controls.Add(new Label
            {
                Text = label, AutoSize = false, Size = Dpi.Sz(LabelW, 30), Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Text, Margin = Dpi.P(0, 4, 8, 4),
            }, 0, r);
            t.Controls.Add(amount, 1, r);
            if (extra != null) t.Controls.Add(extra, 2, r);
        }

        // ── Right column: cheques list and the agent's signature ──
        Control BuildRight()
        {
            var col = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };

            var cheques = new Card();
            var head = new Panel { Size = Dpi.Sz(RightW, 30), Margin = Dpi.P(0, 0, 0, 6) };
            head.Controls.Add(new Label { Text = "Cheques cambiados", AutoSize = true, Font = Theme.CardTitle, ForeColor = Theme.Text, Location = Dpi.Pt(0, 2) });
            head.Controls.Add(lblChequesCount);
            cheques.Add(head);
            SetupGrid();
            cheques.Add(new Frame(grid, RightW, 296));
            cheques.Add(new Label
            {
                Text = "Un cheque por línea: número y monto. Para quitar uno, haga clic en ✕.",
                AutoSize = true, MaximumSize = new Size(Dpi.S(RightW), 0), Font = Theme.Small, ForeColor = Theme.Muted, Margin = Dpi.P(0, 6, 0, 0),
            });
            col.Controls.Add(cheques);

            var sign = new Card { Margin = Padding.Empty };
            var shead = new Panel { Size = Dpi.Sz(RightW, 30), Margin = Dpi.P(0, 0, 0, 6) };
            shead.Controls.Add(new Label { Text = "Firma del agente", AutoSize = true, Font = Theme.CardTitle, ForeColor = Theme.Text, Location = Dpi.Pt(0, 2) });
            var clear = new LinkLabel
            {
                Text = "Borrar firma", AutoSize = false, Size = Dpi.Sz(140, 30), Location = Dpi.Pt(RightW - 140, 0),
                TextAlign = ContentAlignment.MiddleRight, LinkColor = Theme.Accent, ActiveLinkColor = Theme.AccentPressed,
                LinkBehavior = LinkBehavior.HoverUnderline, TabStop = false,
            };
            clear.LinkClicked += (s, e) => firma.Clear();
            shead.Controls.Add(clear);
            sign.Add(shead);
            firma.Margin = Padding.Empty;
            sign.Add(firma);
            col.Controls.Add(sign);
            return col;
        }

        void SetupGrid()
        {
            grid.BorderStyle = BorderStyle.None;
            grid.BackgroundColor = Color.White;
            grid.GridColor = Theme.Border;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.AllowUserToResizeRows = false;
            grid.AllowUserToResizeColumns = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Theme.ReadOnlyFill, ForeColor = Theme.Muted, Font = Theme.FieldLabel,
                SelectionBackColor = Theme.ReadOnlyFill, SelectionForeColor = Theme.Muted, Padding = Dpi.P(6, 6, 6, 6),
            };
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White, ForeColor = Theme.Text, Font = Theme.Body,
                SelectionBackColor = Theme.AccentSoft, SelectionForeColor = Theme.Text, Padding = Dpi.P(6, 4, 6, 4),
            };
            // Size header and rows from the font; fixed pixel heights don't follow display scaling.
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Numero", HeaderText = "NÚMERO DE CHEQUE", FillWeight = 58, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Monto", HeaderText = "MONTO", FillWeight = 32, SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight },
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleRight } },
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Quitar", HeaderText = "", FillWeight = 10, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Theme.Hint,
                    SelectionBackColor = Theme.DangerSoft, SelectionForeColor = Theme.Danger,
                },
            });

            grid.CellFormatting += (s, e) =>
            {
                if (e.ColumnIndex != 2 || e.RowIndex < 0) return;
                e.Value = grid.Rows[e.RowIndex].IsNewRow ? "" : "✕";
                e.FormattingApplied = true;
            };
            grid.CellClick += (s, e) =>
            {
                if (e.ColumnIndex == 2 && e.RowIndex >= 0 && !grid.Rows[e.RowIndex].IsNewRow) RemoveCheque(e.RowIndex);
            };
            grid.KeyDown += (s, e) =>
            {
                var row = grid.CurrentRow;
                if (e.KeyCode == Keys.Delete && row != null && !row.IsNewRow && !grid.IsCurrentCellInEditMode)
                {
                    e.Handled = true;
                    RemoveCheque(row.Index);
                }
            };
            grid.CellMouseEnter += (s, e) => grid.Cursor = e.ColumnIndex == 2 && e.RowIndex >= 0 && !grid.Rows[e.RowIndex].IsNewRow ? Cursors.Hand : Cursors.Default;
            grid.CellValueChanged += (s, e) => Changed();
            grid.RowsRemoved += (s, e) => Changed();
            grid.CellEndEdit += (s, e) =>
            {
                if (e.ColumnIndex != 1) return;
                var cell = grid.Rows[e.RowIndex].Cells[1];
                if (Fmt.TryParseMoney(cell.Value as string, out var v) && v != 0) cell.Value = v.ToString("0.00");
            };
        }

        // Removing a row from inside a grid event can upset the grid, so do it right after.
        void RemoveCheque(int index) => BeginInvoke((Action)(() =>
        {
            if (index < 0 || index >= grid.Rows.Count || grid.Rows[index].IsNewRow) return;
            grid.CancelEdit();
            grid.Rows.RemoveAt(index);
        }));

        // ── Footer: tools on the left, print and save on the right ──
        Control BuildFooter()
        {
            var card = new Card { Padding = Dpi.P(12, 10, 12, 10), Margin = Dpi.P(0, 12, 0, 12) };
            var bar = new Panel { Size = Dpi.Sz(CardPadX * 4 + LeftW + Gap + RightW - 24, 36), Margin = Padding.Empty };

            var right = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Right, Margin = Padding.Empty };
            UiButton B(FlowLayoutPanel p, string text, ButtonKind kind, string glyph, EventHandler click)
            {
                var b = new UiButton(text, kind, glyph);
                b.Margin = p == right ? Dpi.P(8, 0, 0, 0) : Dpi.P(0, 0, 4, 0);
                b.Click += click;
                p.Controls.Add(b);
                return b;
            }
            B(right, "Guardar", ButtonKind.Primary, "", (s, e) => SaveCurrent());
            B(right, "Imprimir térmica", ButtonKind.Secondary, "", (s, e) => PrintCurrent(true));
            B(right, "Imprimir hoja", ButtonKind.Secondary, "", (s, e) => PrintCurrent(false));

            var left = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Left, Margin = Padding.Empty };
            B(left, "Nuevo corte", ButtonKind.Ghost, "", (s, e) => { if (ConfirmDiscard()) ShowCorte(new Corte()); });
            B(left, "Historial", ButtonKind.Ghost, "", (s, e) => OpenHistory());
            B(left, "Configuración", ButtonKind.Ghost, "", (s, e) => { using (var f = new SettingsForm(settings)) f.ShowDialog(this); });

            // Added right-first so Tab goes from the signature to Guardar.
            bar.Controls.Add(right);
            bar.Controls.Add(left);
            card.Add(bar);
            return card;
        }

        void SetSaveStatus(string text, Color color)
        {
            lblGuardado.Text = text;
            lblGuardado.ForeColor = color;
        }

        void Changed()
        {
            if (loading) return;
            dirty = true;
            SetSaveStatus("Sin guardar", Theme.Warning);
            UpdateTotals();
        }

        void UpdateTotals()
        {
            var c = ReadForm(false);
            lblTotEntradas.Text = Fmt.Money(c.TotalEntradas);
            lblTotDesglose.Text = Fmt.Money(c.TotalDesglose);
            statEntradas.Text = Fmt.Money(c.TotalEntradas);
            statDesglose.Text = Fmt.Money(c.TotalDesglose);
            statDif.Text = (c.Diferencia > 0 ? "+" : "") + Fmt.Money(c.Diferencia);
            statDif.ForeColor = c.Diferencia == 0 ? Theme.Text : Theme.Danger;

            tCheques.Text = Fmt.Money(c.TotalCheques);
            int n = c.Cheques.Count;
            lblChequesInfo.Text = n == 0 ? "Agréguelos en la lista de la derecha →" : n + (n == 1 ? " cheque en la lista" : " cheques en la lista");
            lblChequesCount.Text = n == 0 ? "" : n + (n == 1 ? " cheque · " : " cheques · ") + Fmt.Money(c.TotalCheques);

            if (c.TotalEntradas == 0 && c.TotalDesglose == 0) pill.Set(PillKind.Neutral, "Escriba los montos");
            else if (c.Diferencia == 0) pill.Set(PillKind.Success, "Cuadra");
            else pill.Set(PillKind.Danger, (c.Diferencia < 0 ? "Faltan " : "Sobran ") + Fmt.Money(Math.Abs(c.Diferencia)));
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
            lblDia.Text = DayText(c.FechaDate);
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
            grid.CurrentCell = null;
            grid.ClearSelection();
            firma.LoadBase64(c.FirmaPng);
            loading = false;
            dirty = false;
            if (c.GuardadoEn == null) SetSaveStatus("Nuevo", Theme.Muted); else SetSaveStatus("Guardado " + c.GuardadoEn, Theme.Success);
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
            SetSaveStatus("✔ Guardado " + c.GuardadoEn, Theme.Success);
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
