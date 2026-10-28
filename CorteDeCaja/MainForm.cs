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

        readonly AppSettings settings;
        // Re-labels every static text in the window; run once at build time and again when the language changes.
        readonly List<Action> relabel = new List<Action>();
        enum SaveState { New, Unsaved, Saved, JustSaved }
        SaveState saveState;
        Corte current;
        bool dirty, loading;

        readonly DateField dtFecha = new DateField(160);
        readonly Label lblDia = Theme.MakeLabel("", Theme.Body, Theme.Muted, Dpi.P(0));
        readonly Label lblGuardado = Theme.MakeLabel("", Theme.Body, Theme.Muted, Dpi.P(0));
        readonly NoteBox txtAgente = new NoteBox("");

        readonly MoneyBox mW = new MoneyBox(), mEnvios = new MoneyBox(), mOtroIng = new MoneyBox();
        readonly NoteBox nW = new NoteBox(""), nOtroIng = new NoteBox("");

        readonly MoneyBox mCash = new MoneyBox(), mMaquina = new MoneyBox(), mDevol = new MoneyBox(), mCompra = new MoneyBox(), mOtros = new MoneyBox();
        readonly TextBox tCheques = new TextBox { ReadOnly = true, TabStop = false, TextAlign = HorizontalAlignment.Right, BackColor = Theme.ReadOnlyFill, ForeColor = Theme.Text };
        readonly Label lblChequesInfo = Theme.MakeLabel("", Theme.Small, Theme.Muted, Dpi.P(0));
        readonly NoteBox nDevPersona = new NoteBox(""), nDevAgencia = new NoteBox("");
        readonly NoteBox nCompra = new NoteBox(""), nOtros = new NoteBox("");

        readonly Label lblTotEntradas = HeaderTotal(), lblTotDesglose = HeaderTotal();
        readonly Label statEntradas = StatValue(), statDesglose = StatValue(), statDif = StatValue();
        readonly StatusPill pill = new StatusPill { Size = Dpi.Sz(200, 42), Margin = Dpi.P(0, 1, 0, 1) };
        readonly Label lblChequesCount = new Label { AutoSize = false, Size = Dpi.Sz(200, 30), Location = Dpi.Pt(RightW - 200, 0), TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.Muted };

        readonly DataGridView grid = new DataGridView();
        readonly SignaturePad firma = new SignaturePad(Dpi.S(RightW), Dpi.S(162));
        readonly LangToggle langToggle = new LangToggle();

        public MainForm()
        {
            settings = Storage.LoadSettings();
            Lang.Init(settings.Idioma);
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
            dtFecha.ValueChanged += (s, e) => { lblDia.Text = Lang.LongDate(dtFecha.Value); Changed(); };

            Cue(txtAgente, "Nombre del agente", "Agent name");
            Cue(nW, "X (iniciales / nota)", "X (initials / note)");
            Cue(nOtroIng, "¿Qué fue? (obligatorio)", "What was it? (required)");
            Cue(nDevPersona, "Persona", "Person");
            Cue(nDevAgencia, "Agencia", "Agency");
            Cue(nCompra, "¿Qué se compró? (ej. papel)", "What was bought? (e.g. paper)");
            Cue(nOtros, "¿Qué fue? (obligatorio)", "What was it? (required)");
            relabel.Add(() =>
            {
                grid.Columns[0].HeaderText = Lang.T("NÚMERO DE CHEQUE", "CHECK NUMBER");
                grid.Columns[1].HeaderText = Lang.T("MONTO", "AMOUNT");
            });
            langToggle.Toggled += (s, e) =>
            {
                Lang.Set(!Lang.En);
                settings.Idioma = Lang.En ? "en" : "es";
                try { Storage.SaveSettings(settings); } catch { /* the switch still works for this session */ }
            };
            Lang.Changed += ApplyLanguage;
            firma.Changed += (s, e) => Changed();

            ShowCorte(new Corte());
            foreach (var a in relabel) a();
            ResumeLayout(false);
            PerformLayout();
            ActiveControl = txtAgente;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Lang.Changed -= ApplyLanguage;
            base.Dispose(disposing);
        }

        /// <summary>Sets a control's text now and whenever the language changes.</summary>
        T L<T>(T c, string es, string en) where T : Control
        {
            relabel.Add(() => c.Text = Lang.T(es, en));
            return c;
        }

        void Cue(NoteBox box, string es, string en) => relabel.Add(() => box.Cue = Lang.T(es, en));

        void ApplyLanguage()
        {
            SuspendLayout();
            // Relabeling must never count as an edit to the corte.
            bool wasLoading = loading;
            loading = true;
            foreach (var a in relabel) a();
            loading = wasLoading;
            lblDia.Text = Lang.LongDate(dtFecha.Value);
            RenderSaveStatus();
            UpdateTotals();
            dtFecha.Invalidate();
            firma.Invalidate();
            langToggle.Invalidate();
            ResumeLayout(true);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // The grid picks a current cell when it first appears; start with nothing highlighted.
            grid.CurrentCell = null;
            grid.ClearSelection();
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
            title.Controls.Add(L(new Label { AutoSize = true, Font = Theme.PageTitle, ForeColor = Theme.Text, Location = Dpi.Pt(-2, 0) }, "Corte de caja", "End-of-day count"));
            var sub = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Location = Dpi.Pt(0, 38), Margin = Padding.Empty };
            sub.Controls.Add(lblDia);
            sub.Controls.Add(Theme.MakeLabel("  ·  ", Theme.Body, Theme.Hint, Dpi.P(0)));
            sub.Controls.Add(lblGuardado);
            title.Controls.Add(sub);
            // Language switch sits at the right end of the title block, lined up with the date field.
            title.Controls.Add(L(new Label { AutoSize = true, Font = Theme.FieldLabel, ForeColor = Theme.Muted, Location = Dpi.Pt(leftBlock - 16 - 92, 4) }, "IDIOMA", "LANGUAGE"));
            langToggle.Location = Dpi.Pt(leftBlock - 16 - 92, 24);
            title.Controls.Add(langToggle);

            var fields = new Panel { Size = Dpi.Sz(RightW + CardPadX, 60), Margin = Padding.Empty };
            fields.Controls.Add(L(new Label { AutoSize = true, Font = Theme.FieldLabel, ForeColor = Theme.Muted, Location = Dpi.Pt(0, 4) }, "FECHA", "DATE"));
            dtFecha.Location = Dpi.Pt(0, 24);
            fields.Controls.Add(dtFecha);
            fields.Controls.Add(L(new Label { AutoSize = true, Font = Theme.FieldLabel, ForeColor = Theme.Muted, Location = Dpi.Pt(172, 4) }, "AGENTE", "AGENT"));
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

            var entradas = MoneyCard(("Entradas", "Money in"), ("Dinero que entró", "Money that came in"), lblTotEntradas, out var t);
            Row(t, ("Caja Negra", "Caja Negra"), F(mW, AmountW), F(nW, NoteW, 0));
            Row(t, ("Envíos", "Envíos"), F(mEnvios, AmountW));
            Row(t, ("Otro ingreso", "Other income"), F(mOtroIng, AmountW), F(nOtroIng, NoteW, 0));
            col.Controls.Add(entradas);

            var desglose = MoneyCard(("Desglose", "Breakdown"), ("Cómo se entrega", "How it is handed over"), lblTotDesglose, out t);
            Row(t, ("Cash", "Cash"), F(mCash, AmountW));
            lblChequesInfo.Anchor = AnchorStyles.Left;
            Row(t, ("Cheques", "Checks"), F(tCheques, AmountW), lblChequesInfo);
            Row(t, ("Máquina", "Máquina"), F(mMaquina, AmountW));
            var dev = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            int half = (NoteW - 8) / 2;
            dev.Controls.Add(F(nDevPersona, half));
            dev.Controls.Add(F(nDevAgencia, NoteW - 8 - half, 0));
            Row(t, ("Devolución", "Refund"), F(mDevol, AmountW), dev);
            Row(t, ("Compra", "Purchase"), F(mCompra, AmountW), F(nCompra, NoteW, 0));
            Row(t, ("Otras cosas", "Other"), F(mOtros, AmountW), F(nOtros, NoteW, 0));
            col.Controls.Add(desglose);

            var cuadre = new Card { Margin = Padding.Empty };
            var line = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            int statW = (LeftW - 200) / 3;
            line.Controls.Add(Stat(("Entradas", "Money in"), statEntradas, statW));
            line.Controls.Add(Stat(("Desglose", "Breakdown"), statDesglose, statW));
            line.Controls.Add(Stat(("Diferencia", "Difference"), statDif, statW));
            line.Controls.Add(pill);
            cuadre.Add(line);
            col.Controls.Add(cuadre);
            return col;
        }

        Panel Stat((string es, string en) caption, Label value, int width)
        {
            var p = new Panel { Size = Dpi.Sz(width, 44), Margin = Padding.Empty };
            p.Controls.Add(L(new Label { AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Location = Dpi.Pt(0, 0) }, caption.es, caption.en));
            p.Controls.Add(value);
            return p;
        }

        Card MoneyCard((string es, string en) title, (string es, string en) subtitle, Label total, out TableLayoutPanel rows)
        {
            var card = new Card();
            var head = new Panel { Size = Dpi.Sz(LeftW, 44), Margin = Padding.Empty };
            head.Controls.Add(L(new Label { AutoSize = true, Font = Theme.CardTitle, ForeColor = Theme.Text, Location = Dpi.Pt(0, 0) }, title.es, title.en));
            head.Controls.Add(L(new Label { AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Location = Dpi.Pt(0, 24) }, subtitle.es, subtitle.en));
            head.Controls.Add(total);
            card.Add(head);
            card.Add(Theme.Separator(LeftW));
            rows = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Margin = Padding.Empty };
            card.Add(rows);
            return card;
        }

        void Row(TableLayoutPanel t, (string es, string en) label, Control amount, Control extra = null)
        {
            int r = t.RowCount++;
            t.Controls.Add(L(new Label
            {
                AutoSize = false, Size = Dpi.Sz(LabelW, 30), Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Text, Margin = Dpi.P(0, 4, 8, 4),
            }, label.es, label.en), 0, r);
            t.Controls.Add(amount, 1, r);
            if (extra != null) t.Controls.Add(extra, 2, r);
        }

        // ── Right column: cheques list and the agent's signature ──
        Control BuildRight()
        {
            var col = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };

            var cheques = new Card();
            var head = new Panel { Size = Dpi.Sz(RightW, 30), Margin = Dpi.P(0, 0, 0, 6) };
            head.Controls.Add(L(new Label { AutoSize = true, Font = Theme.CardTitle, ForeColor = Theme.Text, Location = Dpi.Pt(0, 2) }, "Cheques cambiados", "Cashed checks"));
            head.Controls.Add(lblChequesCount);
            cheques.Add(head);
            SetupGrid();
            cheques.Add(new Frame(grid, RightW, 296));
            cheques.Add(L(new Label
            {
                AutoSize = true, MaximumSize = new Size(Dpi.S(RightW), 0), Font = Theme.Small, ForeColor = Theme.Muted, Margin = Dpi.P(0, 6, 0, 0),
            }, "Un cheque por línea: número y monto. Para quitar uno, haga clic en ✕.", "One check per line: number and amount. To remove one, click ✕."));
            col.Controls.Add(cheques);

            var sign = new Card { Margin = Padding.Empty };
            var shead = new Panel { Size = Dpi.Sz(RightW, 30), Margin = Dpi.P(0, 0, 0, 6) };
            shead.Controls.Add(L(new Label { AutoSize = true, Font = Theme.CardTitle, ForeColor = Theme.Text, Location = Dpi.Pt(0, 2) }, "Firma del agente", "Agent signature"));
            var clear = new LinkLabel
            {
                AutoSize = false, Size = Dpi.Sz(160, 30), Location = Dpi.Pt(RightW - 160, 0),
                TextAlign = ContentAlignment.MiddleRight, LinkColor = Theme.Accent, ActiveLinkColor = Theme.AccentPressed,
                LinkBehavior = LinkBehavior.HoverUnderline, TabStop = false,
            };
            L(clear, "Borrar firma", "Clear signature");
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
            // Renaming a column header also raises this, with row -1; only real cells count as an edit.
            grid.CellValueChanged += (s, e) => { if (e.RowIndex >= 0) Changed(); };
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
            grid.CurrentCell = null;
            grid.ClearSelection();
        }));

        // ── Footer: tools on the left, print and save on the right ──
        Control BuildFooter()
        {
            var card = new Card { Padding = Dpi.P(12, 10, 12, 10), Margin = Dpi.P(0, 12, 0, 12) };
            var bar = new Panel { Size = Dpi.Sz(CardPadX * 4 + LeftW + Gap + RightW - 24, 36), Margin = Padding.Empty };

            var right = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Right, Margin = Padding.Empty };
            UiButton B(FlowLayoutPanel p, (string es, string en) text, ButtonKind kind, string glyph, EventHandler click)
            {
                var b = L(new UiButton(text.es, kind, glyph), text.es, text.en);
                b.Margin = p == right ? Dpi.P(8, 0, 0, 0) : Dpi.P(0, 0, 4, 0);
                b.Click += click;
                p.Controls.Add(b);
                return b;
            }
            B(right, ("Guardar", "Save"), ButtonKind.Primary, "", (s, e) => SaveCurrent());
            B(right, ("Imprimir térmica", "Print thermal"), ButtonKind.Secondary, "", (s, e) => PrintCurrent(true));
            B(right, ("Imprimir hoja", "Print sheet"), ButtonKind.Secondary, "", (s, e) => PrintCurrent(false));

            var left = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Left, Margin = Padding.Empty };
            B(left, ("Nuevo corte", "New count"), ButtonKind.Ghost, "", (s, e) => { if (ConfirmDiscard()) ShowCorte(new Corte()); });
            B(left, ("Historial", "History"), ButtonKind.Ghost, "", (s, e) => OpenHistory());
            B(left, ("Configuración", "Settings"), ButtonKind.Ghost, "", (s, e) => { using (var f = new SettingsForm(settings)) f.ShowDialog(this); });

            // Added right-first so Tab goes from the signature to Guardar.
            bar.Controls.Add(right);
            bar.Controls.Add(left);
            card.Add(bar);
            return card;
        }

        void SetSaveStatus(SaveState state)
        {
            saveState = state;
            RenderSaveStatus();
        }

        void RenderSaveStatus()
        {
            switch (saveState)
            {
                case SaveState.New: lblGuardado.Text = Lang.T("Nuevo", "New"); lblGuardado.ForeColor = Theme.Muted; break;
                case SaveState.Unsaved: lblGuardado.Text = Lang.T("Sin guardar", "Unsaved"); lblGuardado.ForeColor = Theme.Warning; break;
                default:
                    lblGuardado.Text = (saveState == SaveState.JustSaved ? "✔ " : "") + Lang.T("Guardado ", "Saved ") + current.GuardadoEn;
                    lblGuardado.ForeColor = Theme.Success; break;
            }
        }

        void Changed()
        {
            if (loading) return;
            dirty = true;
            SetSaveStatus(SaveState.Unsaved);
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
            lblChequesInfo.Text = n == 0 ? Lang.T("Agréguelos en la lista de la derecha →", "Add them in the list on the right →") : Lang.Checks(n) + Lang.T(" en la lista", " in the list");
            lblChequesCount.Text = n == 0 ? "" : Lang.Checks(n) + " · " + Fmt.Money(c.TotalCheques);

            if (c.TotalEntradas == 0 && c.TotalDesglose == 0) pill.Set(PillKind.Neutral, Lang.T("Escriba los montos", "Enter the amounts"));
            else pill.Set(c.Diferencia == 0 ? PillKind.Success : PillKind.Danger, Lang.Status(c));
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
            lblDia.Text = Lang.LongDate(c.FechaDate);
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
            SetSaveStatus(c.GuardadoEn == null ? SaveState.New : SaveState.Saved);
            UpdateTotals();
        }

        List<string> FindProblems(Corte c)
        {
            var errors = new List<string>();
            foreach (var box in new[] { mW, mEnvios, mOtroIng, mCash, mMaquina, mDevol, mCompra, mOtros })
                if (!box.IsValid) { errors.Add(Lang.T("Hay una cantidad que no es un número (marcada en rojo).", "An amount is not a number (marked in red).")); break; }
            foreach (DataGridViewRow row in grid.Rows)
                if (!row.IsNewRow && !Fmt.TryParseMoney(row.Cells[1].Value as string, out _)) { errors.Add(Lang.T("Hay un cheque con monto que no es un número.", "A check amount is not a number.")); break; }
            if (c.Agente.Length == 0) errors.Add(Lang.T("Escriba el nombre del agente.", "Enter the agent's name."));
            if (c.OtroIngreso != 0 && c.OtroIngresoNota.Length == 0) errors.Add(Lang.T("Otro ingreso: escriba qué fue.", "Other income: say what it was."));
            if (c.Devolucion != 0 && c.DevolucionPersona.Length == 0) errors.Add(Lang.T("Devolución: escriba el nombre de la persona.", "Refund: enter the person's name."));
            if (c.Devolucion != 0 && c.DevolucionAgencia.Length == 0) errors.Add(Lang.T("Devolución: escriba la agencia.", "Refund: enter the agency."));
            if (c.Compra != 0 && c.CompraNota.Length == 0) errors.Add(Lang.T("Compra: escriba qué se compró.", "Purchase: say what was bought."));
            if (c.Otros != 0 && c.OtrosNota.Length == 0) errors.Add(Lang.T("Otras cosas: escriba qué fue.", "Other: say what it was."));
            if (c.Cheques.Exists(ch => ch.Numero.Length == 0)) errors.Add(Lang.T("Cada cheque necesita su número.", "Every check needs its number."));
            if (c.Cheques.Exists(ch => ch.Monto == 0)) errors.Add(Lang.T("Cada cheque necesita su monto.", "Every check needs its amount."));
            if (c.FirmaPng == null) errors.Add(Lang.T("Falta la firma del agente.", "The agent's signature is missing."));
            return errors;
        }

        bool SaveCurrent()
        {
            var c = ReadForm();
            var errors = FindProblems(c);
            if (errors.Count > 0)
            {
                MessageBox.Show(this, Lang.T("No se puede guardar todavía:", "Can't save yet:") + "\n\n• " + string.Join("\n• ", errors), Lang.T("Falta información", "Missing information"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (c.Diferencia != 0 &&
                MessageBox.Show(this, Lang.T("El corte NO cuadra: ", "The count does NOT balance: ") + Lang.Status(c) + ".\n\n" + Lang.T("¿Guardar de todos modos?", "Save anyway?"), Lang.T("No cuadra", "Does not balance"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return false;

            c.GuardadoEn = DateTime.Now.ToString("MM/dd/yyyy h:mm tt", CultureInfo.InvariantCulture);
            try { Storage.SaveCorte(c); }
            catch (Exception ex)
            {
                MessageBox.Show(this, Lang.T("No se pudo guardar:", "Could not save:") + "\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            current = c;
            dirty = false;
            SetSaveStatus(SaveState.JustSaved);
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
            !dirty || MessageBox.Show(this, Lang.T("Hay cambios sin guardar. ¿Descartarlos?", "There are unsaved changes. Discard them?"), "Corte de Caja", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !ConfirmDiscard()) e.Cancel = true;
            base.OnFormClosing(e);
        }

    }
}
