using System;
using System.Drawing;
using System.Windows.Forms;

namespace MealPrepApp
{
    public class FormMain : Form
    {
        // ── menu buttons ──────────────────────────────────────────────────
        private readonly Button[] _menuBtns = new Button[5];
        private readonly string[] _menuNames = { "Acasa", "Retete", "Ingrediente", "Planificare", "Rapoarte" };

        // ── action bar labels ─────────────────────────────────────────────
        private Panel _actionBar = new Panel();
        private Label _lblAdd = new Label();
        private Label _lblMod = new Label();
        private Label _lblDel = new Label();
        private Label _lblExp = new Label();

        // ── pages ─────────────────────────────────────────────────────────
        private PageAcasa _pgAcasa = new PageAcasa();
        private PageRetete _pgRetete = new PageRetete();
        private PageIngrediente _pgIngrediente = new PageIngrediente();
        private PagePlanificare _pgPlanificare = new PagePlanificare();
        private PageRapoarte _pgRapoarte = new PageRapoarte();
        private Control[] _pages = new Control[0];

        // ── active page index ─────────────────────────────────────────────
        private int _activeIdx = 0;

        // ── status bar ────────────────────────────────────────────────────
        private Label _statusLbl = new Label();

        public FormMain()
        {
            Text = "Planificator si Retele de Mese";
            Size = new Size(1000, 680);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = AppColors.Cream;
            FormBorderStyle = FormBorderStyle.Sizable;

            BuildUI();
            SwitchPage(0);
        }

        // ─────────────────────────────────────────────────────────────────
        private void BuildUI()
        {
            // title bar panel
            var titleBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = AppColors.Dark };
            int dx = 14;
            foreach (var col in new[] { AppColors.Moss, AppColors.MossLight, AppColors.Cream })
            {
                var dot = new Panel { Size = new Size(13, 13), Location = new Point(dx, 12), BackColor = col };
                var gp = new System.Drawing.Drawing2D.GraphicsPath();
                gp.AddEllipse(0, 0, 13, 13);
                dot.Region = new Region(gp);
                titleBar.Controls.Add(dot);
                dx += 20;
            }
            var titleLbl = new Label
            {
                Text = "Planificator si retele de mese",
                Font = AppColors.FontTitle,
                ForeColor = AppColors.Cream,
                AutoSize = true,
                Location = new Point(70, 8)
            };
            titleBar.Controls.Add(titleLbl);

            // menu bar
            var menuBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = AppColors.Dark };
            int mx = 10;
            for (int i = 0; i < _menuNames.Length; i++)
            {
                var idx = i;
                var btn = new Button
                {
                    Text = _menuNames[i],
                    Font = AppColors.FontMenu,
                    ForeColor = Color.FromArgb(190, AppColors.Cream),
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(110, 30),
                    Location = new Point(mx, 7),
                    BackColor = AppColors.Dark,
                    Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 255, 255, 255);
                btn.Click += (s, e) => SwitchPage(idx);
                _menuBtns[i] = btn;
                menuBar.Controls.Add(btn);
                mx += 115;
            }
            var exitLbl = new Label
            {
                Text = "Iesire ->",
                Font = AppColors.FontMenu,
                ForeColor = Color.FromArgb(140, AppColors.Cream),
                AutoSize = true,
                Location = new Point(menuBar.Width - 90, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            exitLbl.Click += (s, e) => Application.Exit();
            menuBar.Controls.Add(exitLbl);

            // action bar
            _actionBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = AppColors.Cream2 };
            _actionBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(180, 160, 140), 2),
                    0, _actionBar.Height - 1, _actionBar.Width, _actionBar.Height - 1);

            _lblAdd = MakeActionLabel("+ Adauga", 14, Color.FromArgb(50, 60, 20));
            _lblMod = MakeActionLabel("+ Modifica", 160, Color.FromArgb(50, 60, 20));
            _lblDel = MakeActionLabel("- Archiveaza/Sterge", 310, Color.FromArgb(100, 50, 20));
            _lblExp = MakeActionLabel("Export Excel", 0, Color.FromArgb(30, 50, 10));
            _lblExp.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _lblExp.Left = 900;

            _actionBar.Controls.AddRange(new Control[] { _lblAdd, _lblMod, _lblDel, _lblExp });
            _actionBar.Resize += (s, e) => _lblExp.Left = _actionBar.Width - 130;

            _lblAdd.Click += (s, e) =>
            {
                if (_activeIdx == 1) _pgRetete.OpenAdd();
                else if (_activeIdx == 2) _pgIngrediente.OpenAdd();
            };
            _lblMod.Click += (s, e) =>
            {
                if (_activeIdx == 1) _pgRetete.OpenMod();
                else if (_activeIdx == 2) _pgIngrediente.OpenMod();
            };
            _lblDel.Click += (s, e) =>
            {
                if (_activeIdx == 1) _pgRetete.OpenDel();
                else if (_activeIdx == 2) _pgIngrediente.OpenDel();
            };

            // pages container
            var pageHost = new Panel { Dock = DockStyle.Fill, BackColor = AppColors.Cream };
            _pages = new Control[] { _pgAcasa, _pgRetete, _pgIngrediente, _pgPlanificare, _pgRapoarte };
            foreach (var pg in _pages)
            {
                pg.Dock = DockStyle.Fill;
                pg.Visible = false;
                pageHost.Controls.Add(pg);
            }

            // status bar
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = AppColors.Dark };
            _statusLbl = new Label
            {
                Font = AppColors.FontSmall,
                ForeColor = AppColors.Cream,
                AutoSize = false,
                Dock = DockStyle.Left,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            var authorLbl = new Label
            {
                Text = "Railean Margarita · P-2331",
                Font = AppColors.FontSmall,
                ForeColor = AppColors.Cream,
                AutoSize = false,
                Dock = DockStyle.Right,
                Width = 220,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 10, 0)
            };
            statusBar.Controls.AddRange(new Control[] { _statusLbl, authorLbl });

            // assemble — bottom-up order matters for DockStyle
            Controls.Add(pageHost);
            Controls.Add(_actionBar);
            Controls.Add(menuBar);
            Controls.Add(titleBar);
            Controls.Add(statusBar);
        }

        private static Label MakeActionLabel(string text, int x, Color fore)
        {
            return new Label
            {
                Text = text,
                Font = AppColors.FontAction,
                ForeColor = fore,
                AutoSize = true,
                Location = new Point(x, 12),
                Cursor = Cursors.Hand
            };
        }

        public void SwitchPage(int idx)
        {
            foreach (var pg in _pages) pg.Visible = false;
            _pages[idx].Visible = true;
            _activeIdx = idx;

            for (int i = 0; i < _menuBtns.Length; i++)
            {
                _menuBtns[i].BackColor = i == idx ? AppColors.Moss : AppColors.Dark;
                _menuBtns[i].ForeColor = i == idx ? AppColors.Cream : Color.FromArgb(190, AppColors.Cream);
            }

            bool showAB = idx == 1 || idx == 2 || idx == 3;
            _actionBar.Visible = showAB;

            var statuses = new[]
            {
                "Acasa",
                "Sectiunea Retete",
                "Sectiunea Ingrediente",
                "Sectiunea Planificare",
                "Rapoarte si statistici"
            };
            _statusLbl.Text = statuses[idx];
        }
    }
}