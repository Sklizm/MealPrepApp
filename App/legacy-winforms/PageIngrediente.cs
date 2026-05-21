using System.Drawing;
using System.Windows.Forms;

namespace MealPrepApp
{
    public class PageIngrediente : Panel
    {
        private DataGridView _dgv = new DataGridView();

        public PageIngrediente()
        {
            BackColor = AppColors.Cream;
            BuildUI();
        }

        private void BuildUI()
        {
            // ── Sidebar ───────────────────────────────────────────────────
            var sidebar = new Panel { Dock = DockStyle.Left, Width = 160, BackColor = AppColors.Sidebar };
            var items = new[] { "Toate", "Categorii", "Frigider", "Lista de\ncumparaturi" };
            int sy = 8;
            foreach (var item in items)
            {
                var lbl = new Label
                {
                    Text = item,
                    Font = new Font("Segoe UI", 12f),
                    ForeColor = AppColors.Dark,
                    AutoSize = false,
                    Size = new Size(160, item.Contains("\n") ? 52 : 40),
                    Location = new Point(0, sy),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(18, 0, 0, 0),
                    Cursor = Cursors.Hand,
                    Tag = item
                };
                lbl.Click += (s, e) => HighlightSide(sidebar, (s as Label) != null && (s as Label).Tag != null ? (s as Label).Tag.ToString() : "");
                sidebar.Controls.Add(lbl);
                sy += item.Contains("\n") ? 54 : 42;
            }
            HighlightSide(sidebar, "Toate");

            // ── Divider line (separate panel so labels never cover it) ────
            var divider = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Color.FromArgb(160, 160, 120) };

            // ── Right ─────────────────────────────────────────────────────
            var right = new Panel { Dock = DockStyle.Fill, BackColor = AppColors.Cream, Padding = new Padding(12) };

            var filterPanel = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = AppColors.Cream };
            var txtSearch = new TextBox
            {
                Font = AppColors.FontBody,
                BackColor = AppColors.SearchBg,
                ForeColor = Color.FromArgb(200, AppColors.Cream),
                BorderStyle = BorderStyle.None,
                Size = new Size(360, 26),
                Location = new Point(0, 10),
                Text = "Cauta dupa denumire..."
            };
            txtSearch.GotFocus += (s, e) => { if (txtSearch.Text == "Cauta dupa denumire...") { txtSearch.Text = ""; txtSearch.ForeColor = AppColors.Cream; } };
            txtSearch.LostFocus += (s, e) => { if (txtSearch.Text == "") { txtSearch.Text = "Cauta dupa denumire..."; txtSearch.ForeColor = Color.FromArgb(200, AppColors.Cream); } };
            filterPanel.Controls.Add(txtSearch);

            _dgv = PageAcasa.BuildStyledDGV();
            _dgv.Dock = DockStyle.Fill;
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ingredient", FillWeight = 25 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Categorie", FillWeight = 18 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Unitate masura", FillWeight = 15 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Calorii/100g", FillWeight = 15 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "In frigider", FillWeight = 12 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Categorie alim.", FillWeight = 15 });

            right.Controls.Add(_dgv);
            right.Controls.Add(filterPanel);
            Controls.Add(right);
            Controls.Add(divider);
            Controls.Add(sidebar);
        }

        public void OpenAdd() { new FormIngredientEdit("Adauga ingredient nou", null).ShowDialog(this); }
        public void OpenMod()
        {
            if (_dgv.SelectedRows.Count == 0) { MessageBox.Show("Selectati un ingredient.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var name = _dgv.SelectedRows[0].Cells[0].Value != null ? _dgv.SelectedRows[0].Cells[0].Value.ToString() : "";
            new FormIngredientEdit("Modifica ingredient", name).ShowDialog(this);
        }
        public void OpenDel()
        {
            if (_dgv.SelectedRows.Count == 0) { MessageBox.Show("Selectati un ingredient.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var name = _dgv.SelectedRows[0].Cells[0].Value != null ? _dgv.SelectedRows[0].Cells[0].Value.ToString() : "—";
            new FormConfirmDelete(name).ShowDialog(this);
        }

        private static void HighlightSide(Control sidebar, string active)
        {
            foreach (Control c in sidebar.Controls)
            {
                Label l = c as Label;
                if (l != null)
                    l.BackColor = (l.Tag != null && l.Tag.ToString() == active) ? Color.FromArgb(200, 200, 160) : AppColors.Sidebar;
            }
            sidebar.Refresh(); // force full repaint of border
        }
    }
}