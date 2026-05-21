using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MealPrepApp
{
    public class PageRapoarte : Panel
    {
        public PageRapoarte()
        {
            BackColor = AppColors.Cream;
            Padding = new Padding(20);
            BuildUI();
        }

        private void BuildUI()
        {
            // ── Stat row ──────────────────────────────────────────────────
            var statPanel = new Panel
            {
                Location = new Point(20, 20),
                Size = new Size(940, 80),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            AddStat(statPanel, 0, "15 min", "⚡  Timp minim preparare");
            AddStat(statPanel, 320, "39 lei", "💰  Pret mediu per portie");
            AddStat(statPanel, 640, "440 kcal", "🔥  Calorii medii per reteta");

            // ── Report cards ──────────────────────────────────────────────
            var cardData = new string[,]
            {
                { "📋", "Toate retetele",           "Lista completa, sortabila dupa orice coloana" },
                { "📊", "Top retete planificate",   "Sortate descendent dupa nr. aparitii in plan" },
                { "🛒", "Lista de cumparaturi",     "Ingrediente consolidate din planul saptamanal" },
                { "📤", "Export Excel",             "Exporta orice sectiune in format .xlsx" },
                { "📉", "Alerta stoc",              "Ingrediente care nu sunt disponibile in frigider" },
                { "🏆", "Reteta cu calorii minime", "Cea mai sanatoasa reteta din colectie" },
            };

            var cardGrid = new Panel
            {
                Location = new Point(20, 110),
                Size = new Size(940, 400),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor = Color.Transparent
            };

            for (int i = 0; i < cardData.GetLength(0); i++)
            {
                int col = i % 2, row = i / 2;
                string icon = cardData[i, 0];
                string title = cardData[i, 1];
                string desc = cardData[i, 2];

                // Outer panel: draws rounded background, no children
                Color bg = AppColors.Card1;
                var outer = new Panel
                {
                    Location = new Point(col * 478, row * 132),
                    Size = new Size(460, 118),
                    BackColor = AppColors.Cream, // same as page bg so corners blend
                    Cursor = Cursors.Hand
                };
                outer.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var path = RoundRect(new Rectangle(0, 0, outer.Width, outer.Height), 10))
                    using (var brush = new SolidBrush(outer.Tag is Color tc ? tc : bg))
                        e.Graphics.FillPath(brush, path);
                };
                outer.Tag = bg;

                // Inner panel: contains all labels, sits inside with small margin so never touches rounded edges
                var inner = new Panel
                {
                    Location = new Point(10, 0),
                    Size = new Size(440, 118),
                    BackColor = bg
                };

                var iconLbl = new Label
                {
                    Text = icon,
                    Font = new Font("Segoe UI", 20f),
                    AutoSize = true,
                    Location = new Point(6, 14),
                    BackColor = bg,
                    ForeColor = AppColors.Dark
                };
                var titleLbl = new Label
                {
                    Text = title,
                    Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                    AutoSize = true,
                    Location = new Point(56, 16),
                    ForeColor = AppColors.Dark,
                    BackColor = bg
                };
                var descLbl = new Label
                {
                    Text = desc,
                    Font = AppColors.FontSmall,
                    AutoSize = false,
                    Size = new Size(370, 40),
                    Location = new Point(56, 52),
                    ForeColor = Color.FromArgb(80, 60, 30),
                    BackColor = bg
                };

                inner.Controls.AddRange(new Control[] { iconLbl, titleLbl, descLbl });
                outer.Controls.Add(inner);

                outer.MouseEnter += (s, e) => { outer.Tag = AppColors.MossLight; inner.BackColor = AppColors.MossLight; foreach (Control c in inner.Controls) c.BackColor = AppColors.MossLight; outer.Refresh(); };
                outer.MouseLeave += (s, e) => { outer.Tag = bg; inner.BackColor = bg; foreach (Control c in inner.Controls) c.BackColor = bg; outer.Refresh(); };
                inner.MouseEnter += (s, e) => { outer.Tag = AppColors.MossLight; inner.BackColor = AppColors.MossLight; foreach (Control c in inner.Controls) c.BackColor = AppColors.MossLight; outer.Refresh(); };
                inner.MouseLeave += (s, e) => { outer.Tag = bg; inner.BackColor = bg; foreach (Control c in inner.Controls) c.BackColor = bg; outer.Refresh(); };

                cardGrid.Controls.Add(outer);
            }

            Controls.AddRange(new Control[] { statPanel, cardGrid });
        }

        private static void AddStat(Panel parent, int x, string value, string label)
        {
            Color bg = Color.FromArgb(200, 208, 160);

            var outer = new Panel
            {
                Location = new Point(x, 0),
                Size = new Size(300, 72),
                BackColor = AppColors.Cream
            };
            outer.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(new Rectangle(0, 0, outer.Width, outer.Height), 8))
                using (var brush = new SolidBrush(bg))
                    e.Graphics.FillPath(brush, path);
            };

            // Inner panel with 10px left margin — away from rounded edges
            var inner = new Panel
            {
                Location = new Point(10, 0),
                Size = new Size(280, 72),
                BackColor = bg
            };
            inner.Controls.Add(new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = AppColors.Dark,
                AutoSize = true,
                Location = new Point(4, 8),
                BackColor = bg
            });
            inner.Controls.Add(new Label
            {
                Text = label,
                Font = AppColors.FontSmall,
                ForeColor = Color.FromArgb(90, 80, 40),
                AutoSize = true,
                Location = new Point(4, 44),
                BackColor = bg
            });

            outer.Controls.Add(inner);
            parent.Controls.Add(outer);
        }

        private static GraphicsPath RoundRect(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
            p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}