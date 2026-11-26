using ChessDraw.Models;

namespace ChessDraw.UI;

internal sealed class TournamentDialog : Form
{
    private readonly TextBox name = new() { Width = 330 };
    private readonly ComboBox system = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    private readonly NumericUpDown rounds = new() { Minimum = 1, Maximum = 99, Value = 5, Width = 110 };
    private readonly CheckBox twoCycles = new() { Text = "Два круга (с переменой цвета)", AutoSize = true };
    private readonly CheckBox initialWhite = new() { Text = "Первый номер играет белыми в 1-м туре", AutoSize = true, Checked = true };
    public string TournamentName => name.Text.Trim();
    public TournamentSystem System => system.SelectedIndex == 0 ? TournamentSystem.Swiss : TournamentSystem.RoundRobin;
    public int SwissRounds => (int)rounds.Value;
    public bool DoubleRound => twoCycles.Checked;
    public bool InitialWhite => initialWhite.Checked;

    public TournamentDialog(Tournament? t = null)
    {
        Text = t is null ? "Создание турнира" : "Редактирование турнира";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(410, 340); Font = new Font("Segoe UI", 10f);
        BackColor = UiStyle.Light;
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(19), RowCount = 10, ColumnCount = 1 };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Label(string s) => outer.Controls.Add(new Label { Text = s, AutoSize = true, Margin = new Padding(2, 5, 0, 3) });
        Label("Название турнира"); outer.Controls.Add(name);
        Label("Система проведения");
        system.Items.AddRange(new object[] { "Швейцарская (ФИДЕ Dutch)", "Круговая" });
        outer.Controls.Add(system);
        Label("Количество туров (для швейцарской системы)"); outer.Controls.Add(rounds);
        outer.Controls.Add(twoCycles); outer.Controls.Add(initialWhite);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var ok = UiStyle.Button("Сохранить", (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { MessageBox.Show("Введите название турнира."); return; }
            DialogResult = DialogResult.OK;
        }, primary: true, width: 120);
        buttons.Controls.Add(ok);
        buttons.Controls.Add(UiStyle.Button("Отмена", (_, _) => DialogResult = DialogResult.Cancel, width: 110));
        outer.Controls.Add(buttons);
        Controls.Add(outer);
        UiStyle.Apply(this);
        system.SelectedIndexChanged += (_, _) =>
        {
            bool swiss = System == TournamentSystem.Swiss;
            rounds.Enabled = initialWhite.Enabled = swiss && !(t?.Started ?? false);
            twoCycles.Enabled = !swiss && !(t?.Started ?? false);
        };
        name.Text = t?.Name ?? "Турнир ТУСУР";
        rounds.Value = Math.Clamp(t?.SwissRounds ?? 5, 1, 99);
        twoCycles.Checked = t?.DoubleRound ?? false;
        initialWhite.Checked = t?.InitialWhite ?? true;
        system.SelectedIndex = t?.System == TournamentSystem.RoundRobin ? 1 : 0;
        if (t?.Started == true) system.Enabled = false;
    }
}

internal sealed class PlayerDialog : Form
{
    private readonly TextBox name = new() { Width = 330 };
    private readonly NumericUpDown rating = new() { Width = 120, Minimum = 0, Maximum = 3500, Increment = 50 };
    public string FullName => name.Text.Trim();
    public int Rating => string.IsNullOrWhiteSpace(rating.Text) || rating.Value == 0 ? Player.DefaultRating : (int)rating.Value;

    public PlayerDialog(Player? p = null, bool frozenRating = false)
    {
        Text = p is null ? "Добавить игрока" : "Редактировать игрока";
        StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ClientSize = new Size(390, 240);
        Font = new Font("Segoe UI", 10f); BackColor = UiStyle.Light;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(18), WrapContents = false };
        panel.Controls.Add(new Label { Text = "ФИО участника", AutoSize = true }); panel.Controls.Add(name);
        panel.Controls.Add(new Label { Text = "Рейтинг (по умолчанию 1000)", AutoSize = true, Margin = new Padding(3, 14, 3, 3) });
        panel.Controls.Add(rating);
        var ok = UiStyle.Button("Сохранить", (_, _) =>
        {
            if (name.Text.Trim().Length < 3) { MessageBox.Show("Введите полное имя участника."); return; }
            DialogResult = DialogResult.OK;
        }, primary: true);
        ok.Margin = new Padding(3, 16, 3, 3);
        panel.Controls.Add(ok); Controls.Add(panel);
        name.Text = p?.FullName ?? ""; rating.Value = p?.Rating ?? Player.DefaultRating;
        rating.Enabled = !frozenRating;
        UiStyle.Apply(this);
        ActiveControl = name;
    }
}

internal sealed class ByeDialog : Form
{
    private readonly ComboBox choice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    public ByeKind? Kind => choice.SelectedIndex == 2 ? null : choice.SelectedIndex == 1 ? ByeKind.RequestedHalf : ByeKind.RequestedZero;

    public ByeDialog(string player, int round)
    {
        Text = "Пропуск тура"; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(370, 185); Font = new Font("Segoe UI", 10f);
        var p = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        p.Controls.Add(new Label { Text = $"{player} — тур {round}", AutoSize = true });
        choice.Items.AddRange(new object[] { "0 очков (не участвует)", "½ очка (заявленный пропуск)", "Участвует (отменить пропуск)" });
        choice.SelectedIndex = 0; p.Controls.Add(choice);
        var b = UiStyle.Button("Подтвердить", (_, _) => DialogResult = DialogResult.OK, true);
        b.Margin = new Padding(3, 18, 0, 0); p.Controls.Add(b);
        Controls.Add(p);
        UiStyle.Apply(this);
    }
}

internal sealed class PlayerListDialog : Form
{
    private readonly TextBox players = new() { Multiline = true, AcceptsReturn = true,
        ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    public string PlayerText => players.Text;

    public PlayerListDialog()
    {
        Text = "ТУСУР — ввод участников списком";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(590, 490); MinimumSize = new Size(490, 360);
        Font = new Font("Segoe UI", 10f); Padding = new Padding(18);
        var hint = new Label { Dock = DockStyle.Top, Height = 76,
            Text = "Каждый участник с новой строки: ФИО;рейтинг\nБез рейтинга — 1000. Можно вставить два столбца из Excel.\nПример: Иванов Иван Иванович;1800" };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(UiStyle.Button("Добавить", (_, _) => DialogResult = DialogResult.OK, true));
        buttons.Controls.Add(UiStyle.Button("Отмена", (_, _) => DialogResult = DialogResult.Cancel));
        Controls.Add(players); Controls.Add(hint); Controls.Add(buttons);
        UiStyle.Apply(this);
    }
}
