using System;
using System.Drawing;
using System.Windows.Forms;

namespace MealPrepApp
{
    public class PageRetete : Panel
    {
        private DataGridView _dgv = new DataGridView();

        public PageRetete()
        {
            BackColor = AppColors.Cream;
            BuildUI();
        }

        private void BuildUI()
        {
            // ── Left sidebar ──────────────────────────────────────────────
            var sidebar = new Panel { Dock = DockStyle.Left, Width = 160, BackColor = AppColors.Sidebar };
            var sideItems = new[] { "Toate", "Favorite", "Recente" };
            int sy = 8;
            foreach (var item in sideItems)
            {
                var lbl = new Label
                {
                    Text = item,
                    Font = new Font("Segoe UI", 12f, FontStyle.Regular),
                    ForeColor = AppColors.Dark,
                    AutoSize = false,
                    Size = new Size(160, 40),
                    Location = new Point(0, sy),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(18, 0, 0, 0),
                    Cursor = Cursors.Hand,
                    Tag = item
                };
                lbl.Click += SideItem_Click;
                sidebar.Controls.Add(lbl);
                sy += 42;
            }
            HighlightSide(sidebar, "Toate");

            // ── Divider line (separate panel so labels never cover it) ────
            var divider = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Color.FromArgb(160, 160, 120) };

            // ── Right content ─────────────────────────────────────────────
            var right = new Panel { Dock = DockStyle.Fill, BackColor = AppColors.Cream, Padding = new Padding(12) };

            var filterPanel = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = AppColors.Cream };

            var cmbCat = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = AppColors.FontBody,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppColors.Dark,
                ForeColor = AppColors.Cream,
                Size = new Size(170, 30),
                Location = new Point(0, 8)
            };
            cmbCat.Items.AddRange(new object[] { "Toate categoriile", "Fel principal", "Salata", "Supa", "Desert", "Vegan" });
            cmbCat.SelectedIndex = 0;

            var txtSearch = new TextBox
            {
                Font = AppColors.FontBody,
                BackColor = AppColors.SearchBg,
                ForeColor = Color.FromArgb(200, AppColors.Cream),
                BorderStyle = BorderStyle.None,
                Size = new Size(300, 26),
                Location = new Point(185, 10),
                Text = "Cauta dupa denumire..."
            };
            txtSearch.GotFocus += (s, e) => { if (txtSearch.Text == "Cauta dupa denumire...") { txtSearch.Text = ""; txtSearch.ForeColor = AppColors.Cream; } };
            txtSearch.LostFocus += (s, e) => { if (txtSearch.Text == "") { txtSearch.Text = "Cauta dupa denumire..."; txtSearch.ForeColor = Color.FromArgb(200, AppColors.Cream); } };

            filterPanel.Controls.AddRange(new Control[] { cmbCat, txtSearch });

            _dgv = PageAcasa.BuildStyledDGV();
            _dgv.Dock = DockStyle.Fill;
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reteta", FillWeight = 28 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Categorie", FillWeight = 16 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Calorii (kcal)", FillWeight = 14 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Timp prep.", FillWeight = 13 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Dificultate", FillWeight = 13 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nr. portii", FillWeight = 10 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Pret/portie", FillWeight = 11 });

            right.Controls.Add(_dgv);
            right.Controls.Add(filterPanel);
            Controls.Add(right);
            Controls.Add(divider);
            Controls.Add(sidebar);
        }

        public void OpenAdd()
        {
            new FormRecipeEdit("Adauga reteta noua", null).ShowDialog(this);
        }

        public void OpenMod()
        {
            if (_dgv.SelectedRows.Count == 0) { MessageBox.Show("Selectati o reteta din lista.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var name = _dgv.SelectedRows[0].Cells[0].Value != null ? _dgv.SelectedRows[0].Cells[0].Value.ToString() : "";
            new FormRecipeEdit("Modifica reteta", name).ShowDialog(this);
        }

        public void OpenDel()
        {
            if (_dgv.SelectedRows.Count == 0) { MessageBox.Show("Selectati o reteta din lista.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var name = _dgv.SelectedRows[0].Cells[0].Value != null ? _dgv.SelectedRows[0].Cells[0].Value.ToString() : "—";
            new FormConfirmDelete(name).ShowDialog(this);
        }

        private static void SideItem_Click(object s, EventArgs e)
        {
            Label lbl = s as Label;
            if (lbl != null && lbl.Parent != null)
                HighlightSide(lbl.Parent, lbl.Tag != null ? lbl.Tag.ToString() : "");
        }

        private static void HighlightSide(Control sidebar, string active)
        {
            foreach (Control c in sidebar.Controls)
            {
                Label l = c as Label;
                if (l != null)
                {
                    bool sel = l.Tag != null && l.Tag.ToString() == active;
                    l.BackColor = sel ? Color.FromArgb(200, 200, 160) : AppColors.Sidebar;
                    l.ForeColor = AppColors.Dark;
                    l.Paint -= LblPaint;
                    if (sel) l.Paint += LblPaint;
                }
            }
            sidebar.Refresh(); // force full repaint of border
        }

        private static void LblPaint(object s, PaintEventArgs e)
        {
            Label l = s as Label;
            if (l != null)
                e.Graphics.FillRectangle(new SolidBrush(AppColors.Moss), 0, 0, 3, l.Height);
        }
    }
}