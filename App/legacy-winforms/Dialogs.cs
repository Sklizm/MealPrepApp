using System.Drawing;
using System.Windows.Forms;

namespace MealPrepApp

{
    // ── Shared base for dialogs ───────────────────────────────────────────────
    public class BaseDialog : Form
    {
        protected Panel BodyPanel = new Panel();
        protected Panel FootPanel = new Panel();

        protected BaseDialog(string title, Color headerColor)
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = AppColors.Cream;

            var head = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = headerColor };
            var hLbl = new Label { Text = title, Font = AppColors.FontAction, ForeColor = AppColors.Cream, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 0, 0), BackColor = Color.Transparent };
            head.Controls.Add(hLbl);

            FootPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = AppColors.Cream2 };
            FootPanel.Paint += (s, e) => e.Graphics.DrawLine(new Pen(AppColors.Cream2, 1), 0, 0, FootPanel.Width, 0);

            BodyPanel = new Panel { Dock = DockStyle.Fill, BackColor = AppColors.Cream, Padding = new Padding(20) };

            Controls.Add(BodyPanel);
            Controls.Add(head);
            Controls.Add(FootPanel);

            // close button
            var btnClose = MakeBtn("Anuleaza", AppColors.Cream, AppColors.Dark, false);
            btnClose.Location = new Point(10, 10);
            btnClose.Click   += (s, e) => Close();
            FootPanel.Controls.Add(btnClose);
        }

        protected Button MakeBtn(string text, Color bg, Color fore, bool primary)
        {
            var btn = new Button
            {
                Text      = text,
                Font      = AppColors.FontBody,
                ForeColor = fore,
                BackColor = bg,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(primary ? 130 : 100, 30),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize  = primary ? 0 : 1;
            btn.FlatAppearance.BorderColor = primary ? bg : AppColors.Dark;
            return btn;
        }

        protected Label MakeLabel(string text, int y) =>
            new Label { Text = text, Font = AppColors.FontBody, ForeColor = Color.FromArgb(107, 90, 58), AutoSize = false, Size = new Size(120, 28), Location = new Point(0, y), TextAlign = ContentAlignment.MiddleRight };

        protected TextBox MakeTextBox(int y, string placeholder = "")
        {
            var tb = new TextBox
            {
                Font = AppColors.FontBody,
                BackColor = Color.FromArgb(247, 242, 228),
                BorderStyle = BorderStyle.FixedSingle,
                ForeColor = Color.Gray,
                Size = new Size(220, 28),
                Location = new Point(128, y)
            };

            tb.Text = placeholder;

            tb.GotFocus += (s, e) =>
            {
                if (tb.Text == placeholder)
                {
                    tb.Text = "";
                    tb.ForeColor = AppColors.Dark;
                }
            };

            tb.LostFocus += (s, e) =>
            {
                if (tb.Text == "")
                {
                    tb.Text = placeholder;
                    tb.ForeColor = Color.Gray;
                }
            };

            return tb;
        }

        protected ComboBox MakeCombo(int y, string[] items)
        {
            var c = new ComboBox { Font = AppColors.FontBody, BackColor = Color.FromArgb(247, 242, 228), ForeColor = AppColors.Dark, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Size = new Size(220, 28), Location = new Point(128, y) };
            c.Items.AddRange(items);
            if (c.Items.Count > 0) c.SelectedIndex = 0;
            return c;
        }
    }

    // ── FormRecipeEdit ────────────────────────────────────────────────────────
    public class FormRecipeEdit : BaseDialog
    {
        public FormRecipeEdit(string title, string existingName) : base(title, AppColors.Dark)
        {
            Text          = title;
            ClientSize    = new Size(400, 460);

            int y = 0;
            BodyPanel.Controls.Add(MakeLabel("Denumire *",    y)); BodyPanel.Controls.Add(MakeTextBox(y, existingName ?? "ex: Spaghetti Bolognese")); y += 36;
            BodyPanel.Controls.Add(MakeLabel("Categorie *",   y)); BodyPanel.Controls.Add(MakeCombo(y, new[] { "Fel principal", "Salata", "Supa", "Desert", "Vegan", "Mic dejun" })); y += 36;
            BodyPanel.Controls.Add(MakeLabel("Tip bucatarie", y)); BodyPanel.Controls.Add(MakeCombo(y, new[] { "Italiana", "Asiatica", "Romana", "Americana", "Internationala" })); y += 36;
            BodyPanel.Controls.Add(MakeLabel("Calorii (kcal)",y)); BodyPanel.Controls.Add(MakeTextBox(y, "ex: 520")); y += 36;
            BodyPanel.Controls.Add(MakeLabel("Timp prep. *",  y)); BodyPanel.Controls.Add(MakeTextBox(y, "minute")); y += 36;
            BodyPanel.Controls.Add(MakeLabel("Nr. portii",    y)); BodyPanel.Controls.Add(MakeTextBox(y, "2")); y += 36;
            BodyPanel.Controls.Add(MakeLabel("Dificultate",   y)); BodyPanel.Controls.Add(MakeCombo(y, new[] { "Usor", "Mediu", "Greu" })); y += 36;
            BodyPanel.Controls.Add(MakeLabel("Pret/portie",   y)); BodyPanel.Controls.Add(MakeTextBox(y, "lei")); y += 36;
            BodyPanel.Controls.Add(MakeLabel("Descriere",     y));
            var desc = new TextBox { Multiline = true, Font = AppColors.FontBody, BackColor = Color.FromArgb(247, 242, 228), BorderStyle = BorderStyle.FixedSingle, ForeColor = AppColors.Dark, Size = new Size(220, 50), Location = new Point(128, y) };
            BodyPanel.Controls.Add(desc);

            var btnSave = MakeBtn("Salveaza", AppColors.Moss, AppColors.Cream, true);
            btnSave.Location = new Point(FootPanel.Width - 150, 10);
            btnSave.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnSave.Click   += (s, e) => { /* TODO: save to DB */ Close(); };
            FootPanel.Controls.Add(btnSave);
        }
    }

    // ── FormIngredientEdit ────────────────────────────────────────────────────
    public class FormIngredientEdit : BaseDialog
    {
        public FormIngredientEdit(string title, string existingName) : base(title, AppColors.MossDark)
        {
            Text       = title;
            ClientSize = new Size(400, 260);

            int y = 0;
            BodyPanel.Controls.Add(MakeLabel("Denumire *",   y)); BodyPanel.Controls.Add(MakeTextBox(y, existingName ?? "ex: Mozzarella")); y += 36;
            BodyPanel.Controls.Add(MakeLabel("Unitate *",    y)); BodyPanel.Controls.Add(MakeCombo(y, new[] { "g", "kg", "ml", "l", "buc", "tbsp", "tsp", "catei" })); y += 36;
            BodyPanel.Controls.Add(MakeLabel("Categorie *",  y)); BodyPanel.Controls.Add(MakeCombo(y, new[] { "Lactate", "Carne", "Legume", "Paste", "Condimente", "Sosuri", "Fructe", "Cereale" })); y += 36;
            BodyPanel.Controls.Add(MakeLabel("Calorii/100g", y)); BodyPanel.Controls.Add(MakeTextBox(y, "kcal")); y += 36;
            BodyPanel.Controls.Add(MakeLabel("In frigider",  y)); BodyPanel.Controls.Add(MakeCombo(y, new[] { "Nu", "Da" }));

            var btnSave = MakeBtn("Salveaza", AppColors.Moss, AppColors.Cream, true);
            btnSave.Location = new Point(FootPanel.Width - 150, 10);
            btnSave.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnSave.Click   += (s, e) => { /* TODO: save to DB */ Close(); };
            FootPanel.Controls.Add(btnSave);
        }
    }

    // ── FormConfirmDelete ─────────────────────────────────────────────────────
    public class FormConfirmDelete : BaseDialog
    {
        public FormConfirmDelete(string itemName) : base("Sterge / Arhiveaza", Color.FromArgb(107, 48, 32))
        {
            Text       = "Confirmare stergere";
            ClientSize = new Size(400, 220);

            var warnPanel = new Panel { Location = new Point(0, 0), Size = new Size(340, 60), BackColor = Color.FromArgb(240, 232, 192) };
            warnPanel.Paint += (s, e) => ControlPaint.DrawBorder(e.Graphics, warnPanel.ClientRectangle, Color.FromArgb(192, 160, 64), ButtonBorderStyle.Solid);
            warnPanel.Controls.Add(new Label { Text = "Atentie! Aceasta actiune nu poate fi anulata.\nAlternativ poti arhiva inregistrarea.", Font = AppColors.FontSmall, ForeColor = Color.FromArgb(107, 90, 58), AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8) });

            var nameLbl = new Label { Text = "Inregistrare selectata:  " + itemName, Font = AppColors.FontBody, ForeColor = AppColors.Dark, AutoSize = true, Location = new Point(0, 70) };

            BodyPanel.Controls.AddRange(new Control[] { warnPanel, nameLbl });

            var btnArh = MakeBtn("Arhiveaza", Color.FromArgb(138, 112, 64), AppColors.Cream, true);
            btnArh.Location = new Point(FootPanel.Width - 280, 10);
            btnArh.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnArh.Click   += (s, e) => { /* TODO: archive */ Close(); };

            var btnDel = MakeBtn("Sterge definitiv", Color.FromArgb(138, 32, 16), AppColors.Cream, true);
            btnDel.Location = new Point(FootPanel.Width - 150, 10);
            btnDel.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnDel.Click   += (s, e) => { /* TODO: hard delete */ Close(); };

            FootPanel.Controls.AddRange(new Control[] { btnArh, btnDel });
        }
    }

    // ── FormAddMeal ───────────────────────────────────────────────────────────
    public class FormAddMeal : BaseDialog
    {
        public FormAddMeal() : base("Adauga masa in plan", AppColors.Dark)
        {
            Text       = "Adauga masa";
            ClientSize = new Size(380, 240);

            int y = 0;
            BodyPanel.Controls.Add(MakeLabel("Data *",     y)); BodyPanel.Controls.Add(MakeTextBox(y, "dd.MM.yyyy")); y += 36;
            BodyPanel.Controls.Add(MakeLabel("Tip masa *", y)); BodyPanel.Controls.Add(MakeCombo(y, new[] { "Breakfast", "Lunch", "Dinner", "Snack" })); y += 36;
            BodyPanel.Controls.Add(MakeLabel("Reteta *",   y)); BodyPanel.Controls.Add(MakeCombo(y, new[] { "— selecteaza o reteta —" })); // populated from DB

            var btnSave = MakeBtn("Adauga", AppColors.Moss, AppColors.Cream, true);
            btnSave.Location = new Point(FootPanel.Width - 150, 10);
            btnSave.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnSave.Click   += (s, e) => { /* TODO: add to plan */ Close(); };
            FootPanel.Controls.Add(btnSave);
        }
    }
}
