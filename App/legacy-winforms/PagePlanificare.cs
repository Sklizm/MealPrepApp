using System;
using System.Drawing;
using System.Windows.Forms;

namespace MealPrepApp
{
    public class PagePlanificare : Panel
    {
        private Panel _weekPanel = new Panel();
        private Panel _calPanel = new Panel();
        private Button _btnSap = new Button();
        private Button _btnCal = new Button();
        private Label _statusLbl = new Label();

        private static readonly string[] WeekDays = { "Luni", "Marti", "Miercuri", "Joi", "Vineri", "Sambata", "Duminica" };

        public PagePlanificare()
        {
            BackColor = AppColors.Cream;
            BuildUI();
        }

        private void BuildUI()
        {
            var bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 42, BackColor = AppColors.Dark };
            _statusLbl = new Label
            {
                Text = "Sectiunea - Planificare - Saptamanal   11.05.2026 – 17.05.2026",
                Font = AppColors.FontSmall,
                ForeColor = AppColors.Cream,
                AutoSize = false,
                Dock = DockStyle.Left,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            _btnCal = MakeToggleBtn("Calendar", false);
            _btnSap = MakeToggleBtn("Saptamanal", true);
            _btnCal.Location = new Point(700, 6); _btnCal.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSap.Location = new Point(820, 6); _btnSap.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnCal.Click += (s, e) => SetView(false);
            _btnSap.Click += (s, e) => SetView(true);
            bottomBar.Controls.AddRange(new Control[] { _statusLbl, _btnCal, _btnSap });

            _weekPanel = BuildWeekPanel();
            _weekPanel.Dock = DockStyle.Fill;

            _calPanel = BuildCalPanel();
            _calPanel.Dock = DockStyle.Fill;
            _calPanel.Visible = false;

            Controls.Add(_weekPanel);
            Controls.Add(_calPanel);
            Controls.Add(bottomBar);
        }

        private static Panel BuildWeekPanel()
        {
            var outer = new Panel { BackColor = AppColors.Cream, AutoScroll = true };

            var hdrPanel = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = AppColors.Cream2 };
            hdrPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(192, 176, 144), 2), 0, hdrPanel.Height - 1, hdrPanel.Width, hdrPanel.Height - 1);

            var cols = new[] { "Breakfast", "Lunch", "Dinner", "Snack" };
            for (int i = 0; i < 4; i++)
            {
                var ci = i;
                var lbl = new Label
                {
                    Text = cols[i],
                    Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                    ForeColor = AppColors.Dark,
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 0, 0),
                    Dock = DockStyle.None,
                    Height = 38
                };
                hdrPanel.Resize += (s, e) =>
                {
                    int w = hdrPanel.Width / 4;
                    lbl.SetBounds(ci * w, 0, w, 38);
                };
                hdrPanel.Controls.Add(lbl);
            }

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            for (int d = 0; d < 7; d++)
            {
                var di = d;
                var dayRow = BuildDayRow(di);
                scroll.Controls.Add(dayRow);
                scroll.Resize += (s, e) =>
                {
                    dayRow.SetBounds(0, di * 90, scroll.Width, 90);
                };
            }

            outer.Controls.Add(scroll);
            outer.Controls.Add(hdrPanel);
            return outer;
        }

        private static Panel BuildDayRow(int dayIndex)
        {
            var dateStr = new DateTime(2026, 5, 11 + dayIndex).ToString("dd.MM.yyyy") + " — " + WeekDays[dayIndex];

            var row = new Panel { BackColor = AppColors.Cream };
            row.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(AppColors.Cream2, 1), 0, row.Height - 1, row.Width, row.Height - 1);

            var banner = new Panel { Height = 28, BackColor = dayIndex == 0 ? AppColors.Moss : AppColors.MossDark, Dock = DockStyle.Top };
            var bannerLbl = new Label
            {
                Text = dateStr,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = AppColors.Cream,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            banner.Controls.Add(bannerLbl);

            var cellsPanel = new Panel { Dock = DockStyle.Fill };
            for (int c = 0; c < 4; c++)
            {
                var ci = c;
                var cell = new Panel { BackColor = AppColors.Cream, BorderStyle = BorderStyle.None };
                cell.Paint += (s, e) =>
                {
                    if (ci < 3)
                        e.Graphics.DrawLine(new Pen(AppColors.Cream2, 1), cell.Width - 1, 0, cell.Width - 1, cell.Height);
                };
                var addBtn = new Button
                {
                    Text = "+",
                    Font = new Font("Segoe UI", 14f),
                    ForeColor = AppColors.Moss,
                    BackColor = Color.Transparent,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(28, 28),
                    Location = new Point(4, 4),
                    Cursor = Cursors.Hand
                };
                addBtn.FlatAppearance.BorderSize = 0;
                addBtn.Click += (s, e) => new FormAddMeal().ShowDialog();
                cell.Controls.Add(addBtn);
                cellsPanel.Controls.Add(cell);
                cellsPanel.Resize += (s, e) =>
                {
                    int w = cellsPanel.Width / 4;
                    cell.SetBounds(ci * w, 0, w, cellsPanel.Height);
                };
            }

            row.Controls.Add(cellsPanel);
            row.Controls.Add(banner);
            return row;
        }

        private static Panel BuildCalPanel()
        {
            var outer = new Panel { BackColor = AppColors.Cream };

            var dowPanel = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = AppColors.Cream2 };
            var dows = new[] { "Dum", "Lun", "Mar", "Mie", "Joi", "Vin", "Sam" };
            for (int i = 0; i < 7; i++)
            {
                var ii = i;
                var lbl = new Label
                {
                    Text = dows[i],
                    Font = AppColors.FontHeader,
                    ForeColor = AppColors.Dark,
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoSize = false,
                    Height = 32
                };
                dowPanel.Controls.Add(lbl);
                dowPanel.Resize += (s, e) =>
                {
                    int w = dowPanel.Width / 7;
                    lbl.SetBounds(ii * w, 0, w, 32);
                };
            }

            var grid = new Panel { Dock = DockStyle.Fill };
            var year = 2026; var month = 5;
            var firstDay = (int)new DateTime(year, month, 1).DayOfWeek;
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var daysInPrev = DateTime.DaysInMonth(year, month - 1);
            int total = (int)Math.Ceiling((firstDay + daysInMonth) / 7.0) * 7;

            var cells = new Panel[total];
            for (int i = 0; i < total; i++)
            {
                var ii = i;
                int day; bool otherMonth = false;
                DateTime cellDate;
                if (i < firstDay)
                { day = daysInPrev - (firstDay - 1 - i); cellDate = new DateTime(year, month - 1, day); otherMonth = true; }
                else if (i >= firstDay + daysInMonth)
                { day = i - firstDay - daysInMonth + 1; cellDate = new DateTime(year, month + 1, day); otherMonth = true; }
                else
                { day = i - firstDay + 1; cellDate = new DateTime(year, month, day); }

                bool today = cellDate.Date == DateTime.Today;

                var cell = new Panel { BackColor = today ? Color.FromArgb(240, 232, 192) : AppColors.Cream };
                if (otherMonth) cell.BackColor = Color.FromArgb(230, 226, 208);

                var numLbl = new Label
                {
                    Text = day.ToString(),
                    Font = today ? new Font("Segoe UI", 9f, FontStyle.Bold) : AppColors.FontSmall,
                    ForeColor = today ? AppColors.Cream : AppColors.Dark,
                    AutoSize = false,
                    Size = new Size(22, 22),
                    Location = new Point(4, 4),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = today ? AppColors.Moss : Color.Transparent
                };

                cell.Controls.Add(numLbl);
                cell.Cursor = Cursors.Hand;
                cell.Click += (s, e) => new FormAddMeal().ShowDialog();
                cells[ii] = cell;

                grid.Controls.Add(cell);
                grid.Resize += (s, e) =>
                {
                    int rows = total / 7;
                    int cw = grid.Width / 7;
                    int ch = grid.Height / rows;
                    int col = ii % 7;
                    int row = ii / 7;
                    cell.SetBounds(col * cw, row * ch, cw, ch);
                    cell.Paint -= CellBorderPaint;
                    cell.Paint += CellBorderPaint;
                };
            }

            outer.Controls.Add(grid);
            outer.Controls.Add(dowPanel);
            return outer;
        }

        private static void CellBorderPaint(object s, PaintEventArgs e)
        {
            Panel p = s as Panel;
            if (p != null)
                ControlPaint.DrawBorder(e.Graphics, p.ClientRectangle,
                    Color.FromArgb(216, 208, 176), ButtonBorderStyle.Solid);
        }

        private void SetView(bool weekly)
        {
            _weekPanel.Visible = weekly;
            _calPanel.Visible = !weekly;
            _btnSap.BackColor = weekly ? AppColors.Moss : Color.FromArgb(138, 128, 96);
            _btnCal.BackColor = !weekly ? AppColors.Moss : Color.FromArgb(138, 128, 96);
            _statusLbl.Text = weekly
                ? "Sectiunea - Planificare - Saptamanal   11.05.2026 – 17.05.2026"
                : "Sectiunea - Planificare - Calendar   Mai 2026";
        }

        private static Button MakeToggleBtn(string text, bool active)
        {
            var btn = new Button
            {
                Text = text,
                Font = AppColors.FontBody,
                ForeColor = AppColors.Dark,
                BackColor = active ? AppColors.Moss : Color.FromArgb(138, 128, 96),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(112, 30),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }
    }
}