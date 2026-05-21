using System.Drawing;
using System.Windows.Forms;

namespace MealPrepApp
{
    public class PageAcasa : Panel
    {
        public PageAcasa()
        {
            BackColor = AppColors.Cream;
            BuildUI();
        }

        private void BuildUI()
        {
            // ── 3 stat cards ──────────────────────────────────────────────
            var cardPanel = new Panel { Location = new Point(20, 20), Size = new Size(940, 110), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            AddStatCard(cardPanel, 0, "5",  "Retete active", AppColors.Card1, AppColors.Dark);
            AddStatCard(cardPanel, 320, "21",  "Ingrediente", AppColors.Card2, AppColors.Cream);
            AddStatCard(cardPanel, 640, "7", "Mese planificate", AppColors.Card3, AppColors.Cream);

            // ── recent recipes label ──────────────────────────────────────
            var recentLbl = new Label
            {
                Text = "Retete Recente",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = AppColors.Dark,
                AutoSize = true,
                Location = new Point(20, 140)
            };

            // ── DataGridView placeholder (empty — data wired later) ────────
            var dgv = BuildStyledDGV();
            dgv.Location = new Point(20, 170);
            dgv.Size = new Size(940, 300);
            dgv.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reteta", FillWeight = 30 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Categorie", FillWeight = 18 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Calorii (kcal)", FillWeight = 15 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Timp prep.", FillWeight = 15 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Dificultate", FillWeight = 12 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nr. portii", FillWeight = 10 });

            Controls.AddRange(new Control[] { cardPanel, recentLbl, dgv });
        }

        private static void AddStatCard(Panel parent, int x, string number, string label, Color bg, Color fore)
        {
            var card = new Panel
            {
                Location = new Point(x, 0),
                Size = new Size(295, 100),
                BackColor = bg,
                Cursor = Cursors.Hand
            };

            var numLbl = new Label { Text = number, Font = new Font("Segoe UI", 36f, FontStyle.Bold), ForeColor = fore, AutoSize = true, Location = new Point(22, 12), BackColor = bg };
            var txtLbl = new Label { Text = label, Font = AppColors.FontBody, ForeColor = Color.FromArgb(200, fore), AutoSize = true, Location = new Point(24, 62), BackColor = bg };
            card.Controls.AddRange(new Control[] { numLbl, txtLbl });
            parent.Controls.Add(card);
        }

        // ── shared DGV style ──────────────────────────────────────────────
        public static DataGridView BuildStyledDGV()
        {
            var dgv = new DataGridView
            {
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = AppColors.Cream,
                BorderStyle = BorderStyle.None,
                GridColor = AppColors.Cream2,
                RowHeadersVisible = false,
                Font = AppColors.FontBody,
                EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            dgv.ColumnHeadersDefaultCellStyle.BackColor = AppColors.Dark;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = AppColors.Cream;
            dgv.ColumnHeadersDefaultCellStyle.Font = AppColors.FontHeader;
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
            dgv.ColumnHeadersHeight = 32;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            dgv.DefaultCellStyle.BackColor = AppColors.Cream;
            dgv.DefaultCellStyle.ForeColor = AppColors.Dark;
            dgv.DefaultCellStyle.SelectionBackColor = AppColors.TableSel;
            dgv.DefaultCellStyle.SelectionForeColor = AppColors.Dark;
            dgv.DefaultCellStyle.Padding = new Padding(4, 0, 0, 0);
            dgv.RowTemplate.Height = 32;

            dgv.AlternatingRowsDefaultCellStyle.BackColor = AppColors.TableAlt;

            return dgv;
        }

        // helpers
        private static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(r.Right - radius * 2, r.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
        private static Region RoundRegion(Rectangle r, int radius) => new Region(RoundRect(r, radius));
    }
}