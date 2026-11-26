using ChessDraw.Exports;
using ChessDraw.Models;
using ChessDraw.Services;

namespace ChessDraw.UI;

public sealed class MainForm : Form
{
    private readonly TournamentStore store;
    private bool storeAvailable = true;
    private readonly List<Tournament> tournaments;
    private Tournament? current;
    private readonly ListBox tournamentList = new();
    private readonly Label tournamentTitle = new();
    private readonly Label tournamentDescription = new();
    private readonly Label footer = new();
    private readonly TabControl tabs = new WorkspaceTabs();
    private readonly DataGridView playerGrid = UiStyle.Grid();
    private readonly DataGridView roundGrid = UiStyle.Grid();
    private readonly DataGridView standingsGrid = UiStyle.Grid();
    private readonly DataGridView progressGrid = UiStyle.Grid();
    private readonly Label overview = new();
    private Button btnImport = null!;
    private readonly ComboBox roundPicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 135 };
    private readonly Label hintLabel = new() { AutoSize = false, Width = 660, Height = 34, TextAlign = ContentAlignment.MiddleLeft };
    private Button btnAddPlayer = null!, btnEditPlayer = null!, btnDeletePlayer = null!;
    private Button btnPair = null!, btnBye = null!, btnWithdraw = null!, btnSaveResults = null!, btnUndo = null!;
    private static readonly (string Label, GameResult Value)[] Results =
    {
        ("Не сыграно", GameResult.Pending), ("1:0", GameResult.WhiteWin),
        ("0:1", GameResult.BlackWin), ("½:½", GameResult.Draw),
        ("Тех. 1:0", GameResult.WhiteForfeitWin),
        ("Тех. 0:1", GameResult.BlackForfeitWin),
        ("Тех. 0:0", GameResult.DoubleForfeit)
    };

    public MainForm(TournamentStore? tournamentStore = null)
    {
        store = tournamentStore ?? new TournamentStore();
        try { tournaments = store.Load(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message + "\nДанные не изменены. Закройте программу и восстановите копию перед созданием новых турниров.",
                "Ошибка базы турниров", MessageBoxButtons.OK, MessageBoxIcon.Error);
            tournaments = new();
            storeAvailable = false;
        }
        Text = "ТУСУР — шахматная жеребьёвка";
        Icon = SystemIcons.Application;
        Width = 1480; Height = 900; MinimumSize = new Size(1200, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.8f); BackColor = UiStyle.Light; ForeColor = UiStyle.Text;
        CreateLayout();
        RefreshTournamentList();
        if (tournaments.Count > 0) tournamentList.SelectedIndex = 0;
        else RefreshCurrent();
        FormClosing += (_, e) =>
        {
            try { SaveState(); }
            catch (Exception ex) { e.Cancel = true; MessageBox.Show(this, ex.Message, "Не удалось сохранить турнир"); }
        };
    }

    private void CreateLayout()
    {
        var heading = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = UiStyle.Navy };
        heading.Controls.Add(new Label
        {
            Text = "♞   ТУСУР   /   Шахматная жеребьёвка",
            ForeColor = UiStyle.Text, Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(22, 0, 0, 0)
        });
        Controls.Add(heading);
        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = UiStyle.Navy };
        footer.Dock = DockStyle.Fill; footer.TextAlign = ContentAlignment.MiddleLeft;
        footer.ForeColor = UiStyle.Text; footer.Padding = new Padding(14, 0, 0, 0);
        statusPanel.Controls.Add(footer); Controls.Add(statusPanel);

        var sidebar = new Panel { Dock = DockStyle.Left, Width = 284, BackColor = UiStyle.Surface, Padding = new Padding(10) };
        var label = new Label { Dock = DockStyle.Top, Height = 48, Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            Text = "МОИ ТУРНИРЫ", TextAlign = ContentAlignment.MiddleLeft, ForeColor = UiStyle.Muted };
        var panelButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 86, WrapContents = true };
        panelButtons.Controls.Add(UiStyle.Button("+ Создать", (_, _) => Act(CreateTournament), true, 120));
        panelButtons.Controls.Add(UiStyle.Button("Изменить", (_, _) => Act(EditTournament), width: 116));
        panelButtons.Controls.Add(UiStyle.Button("Удалить", (_, _) => Act(DeleteTournament), width: 120));
        tournamentList.Dock = DockStyle.Fill;
        tournamentList.BorderStyle = BorderStyle.None;
        tournamentList.BackColor = UiStyle.Surface; tournamentList.ForeColor = UiStyle.Text;
        tournamentList.DrawMode = DrawMode.OwnerDrawFixed;
        tournamentList.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using var bg = new SolidBrush(selected ? UiStyle.Navy : UiStyle.Surface);
            e.Graphics.FillRectangle(bg, e.Bounds);
            TextRenderer.DrawText(e.Graphics, ((Tournament)tournamentList.Items[e.Index]).Name, tournamentList.Font,
                Rectangle.Inflate(e.Bounds, -10, 0), selected ? UiStyle.Accent : UiStyle.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        };
        tournamentList.Font = new Font("Segoe UI", 11f);
        tournamentList.ItemHeight = 36;
        tournamentList.DisplayMember = nameof(Tournament.Name);
        tournamentList.SelectedIndexChanged += (_, _) =>
        {
            current = tournamentList.SelectedItem as Tournament;
            RefreshCurrent();
        };
        var sideBottom = new Label
        {
            Dock = DockStyle.Bottom, Height = 64, ForeColor = UiStyle.Muted,
            Text = "Все изменения сохраняются автоматически.\nДанные хранятся локально на этом ПК.",
            TextAlign = ContentAlignment.MiddleLeft
        };
        sidebar.Controls.Add(tournamentList);
        sidebar.Controls.Add(sideBottom);
        sidebar.Controls.Add(panelButtons);
        sidebar.Controls.Add(label);
        Controls.Add(sidebar);

        var content = new Panel { Dock = DockStyle.Fill, BackColor = UiStyle.Light, Padding = new Padding(13) };
        Controls.Add(content);
        content.BringToFront();

        var detailHeader = new Panel { Dock = DockStyle.Top, Height = 184, BackColor = UiStyle.Surface, Padding = new Padding(10) };
        tournamentTitle.Dock = DockStyle.Top; tournamentTitle.Height = 35;
        tournamentTitle.ForeColor = UiStyle.Text; tournamentTitle.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
        tournamentDescription.Dock = DockStyle.Top; tournamentDescription.Height = 26;
        tournamentDescription.ForeColor = UiStyle.Muted;
        var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 2, 0, 0) };
        btnImport = UiStyle.Button("Ввод списком", (_, _) => Act(ImportPlayers), width: 142);
        btnAddPlayer = UiStyle.Button("+ Участник", (_, _) => Act(AddPlayer), true, 118);
        btnEditPlayer = UiStyle.Button("Изменить ФИО", (_, _) => Act(EditPlayer), width: 145);
        btnDeletePlayer = UiStyle.Button("Удалить игрока", (_, _) => Act(DeletePlayer), width: 146);
        btnBye = UiStyle.Button("Пропуск тура", (_, _) => Act(ScheduleBye), width: 140);
        btnWithdraw = UiStyle.Button("Снять / вернуть", (_, _) => Act(ToggleWithdrawn), width: 145);
        btnPair = UiStyle.Button("Жеребьёвка", (_, _) => Act(PairNext), true, 148);
        tools.Controls.AddRange(new Control[] { btnAddPlayer, btnImport, btnEditPlayer, btnDeletePlayer, btnBye, btnWithdraw, btnPair });
        overview.Dock = DockStyle.Bottom; overview.Height = 38; overview.ForeColor = UiStyle.Accent;
        overview.TextAlign = ContentAlignment.MiddleLeft;
        detailHeader.Controls.Add(tools);
        detailHeader.Controls.Add(overview);
        detailHeader.Controls.Add(tournamentDescription);
        detailHeader.Controls.Add(tournamentTitle);
        content.Controls.Add(detailHeader);

        tabs.Dock = DockStyle.Fill;
        tabs.Font = new Font("Segoe UI", 10f);
        tabs.TabPages.Add(new TabPage("Участники") { BackColor = UiStyle.Surface });
        tabs.TabPages.Add(new TabPage("Жеребьёвка и результаты") { BackColor = UiStyle.Surface });
        tabs.TabPages.Add(new TabPage("Турнирная таблица") { BackColor = UiStyle.Surface });
        tabs.TabPages.Add(new TabPage("Результаты по турам") { BackColor = UiStyle.Surface });
        tabs.TabPages[3].Controls.Add(progressGrid);
        tabs.TabPages[3].Controls.Add(new Label { Dock = DockStyle.Top, Height = 40, ForeColor = UiStyle.Muted,
            Text = "  В ячейке: номер соперника · б/ч — цвет · результат. т — технический результат, ? — не сыграно.",
            TextAlign = ContentAlignment.MiddleLeft });
        var participants = tabs.TabPages[0];
        playerGrid.Columns.Add("num", "№");
        playerGrid.Columns.Add("name", "ФИО участника");
        playerGrid.Columns.Add("rating", "Рейтинг");
        playerGrid.Columns.Add("state", "Статус");
        playerGrid.Columns[0].FillWeight = 12; playerGrid.Columns[1].FillWeight = 62;
        playerGrid.Columns[2].FillWeight = 18; playerGrid.Columns[3].FillWeight = 20;
        participants.Controls.Add(playerGrid);

        var pairingTab = tabs.TabPages[1];
        var pairingControls = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = UiStyle.Surface, Padding = new Padding(8, 4, 0, 0) };
        pairingControls.Controls.Add(new Label { Text = "Тур:", Width = 48, Height = 30, TextAlign = ContentAlignment.MiddleLeft });
        pairingControls.Controls.Add(roundPicker);
        btnSaveResults = UiStyle.Button("Сохранить результаты", (_, _) => Act(SaveResults), true, 202);
        btnUndo = UiStyle.Button("Отменить последний тур", (_, _) => Act(UndoLastRound), width: 209);
        pairingControls.Controls.Add(btnSaveResults); pairingControls.Controls.Add(btnUndo);
        roundPicker.BackColor = UiStyle.Navy; roundPicker.ForeColor = UiStyle.Text;
        roundPicker.FlatStyle = FlatStyle.Flat;
        roundPicker.SelectedIndexChanged += (_, _) => RefreshRoundGrid();
        roundGrid.Columns.Add("board", "Стол");
        roundGrid.Columns.Add("white", "Белые");
        roundGrid.Columns.Add("whitePoints", "Очки белых");
        roundGrid.Columns.Add("black", "Чёрные");
        roundGrid.Columns.Add("blackPoints", "Очки чёрных");
        roundGrid.Columns.Add("info", "Тип");
        var resultCol = new DataGridViewComboBoxColumn { Name = "result", HeaderText = "Результат",
            FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton };
        resultCol.Items.AddRange(Results.Select(x => (object)x.Label).ToArray());
        roundGrid.Columns.Add(resultCol);
        roundGrid.Columns[0].FillWeight = 10;
        roundGrid.Columns[1].FillWeight = 38;
        roundGrid.Columns[2].FillWeight = 14;
        roundGrid.Columns[3].FillWeight = 38;
        roundGrid.Columns[4].FillWeight = 14;
        roundGrid.Columns[5].FillWeight = 21;
        roundGrid.Columns[6].FillWeight = 26;
        roundGrid.ReadOnly = false;
        for (int i = 0; i < 6; i++) roundGrid.Columns[i].ReadOnly = true;
        roundGrid.DataError += (_, e) => { e.ThrowException = false; if (e.RowIndex >= 0) roundGrid.Rows[e.RowIndex].ErrorText = "Выберите результат из списка."; };
        roundGrid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 6 || roundGrid.Rows[e.RowIndex].Tag is not Game game) return;
            var value = roundGrid.Rows[e.RowIndex].Cells[6].Value?.ToString();
            var result = Results.FirstOrDefault(x => x.Label == value);
            if (result.Label is null || game.Result == result.Value) return;
            var previous = game.Result;
            game.Result = result.Value;
            try
            {
                SaveState(); RefreshStandings(); UpdateOverview();
                roundGrid.Rows[e.RowIndex].Cells[5].Value = game.Result is GameResult.WhiteForfeitWin or GameResult.BlackForfeitWin or GameResult.DoubleForfeit ? "Технический" : "Партия";
            }
            catch (Exception ex)
            {
                game.Result = previous;
                roundGrid.Rows[e.RowIndex].Cells[6].Value = Results.First(x => x.Value == previous).Label;
                MessageBox.Show(this, ex.Message, "Не удалось сохранить результат");
            }
        };
        roundGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (roundGrid.IsCurrentCellDirty) roundGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        hintLabel.ForeColor = UiStyle.Accent;
        var hintPanel = new Panel { Dock = DockStyle.Bottom, Height = 37, Padding = new Padding(9, 0, 0, 0) };
        hintLabel.Dock = DockStyle.Fill; hintPanel.Controls.Add(hintLabel);
        pairingTab.Controls.Add(roundGrid);
        pairingTab.Controls.Add(hintPanel);
        pairingTab.Controls.Add(pairingControls);

        var standingsTab = tabs.TabPages[2];
        var exportPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8, 4, 0, 0) };
        exportPanel.Controls.Add(UiStyle.Button("Экспорт в Excel (.xlsx)", (_, _) => Act(ExportExcel), true, 210));
        exportPanel.Controls.Add(UiStyle.Button("Экспорт в PDF", (_, _) => Act(ExportPdf), width: 180));
        var cols = new (string Id, string Caption, float Weight)[]
        {
            ("place","Место",10), ("number","№",8), ("player","Участник",46),
            ("points","Очки",13), ("buchholz","Бухгольц",15), ("cut","Усеч. БХ",15),
            ("berger","Бергер",15), ("wins","Победы за доской",20)
        };
        foreach (var (id, caption, weight) in cols)
        {
            standingsGrid.Columns.Add(id, caption);
            standingsGrid.Columns[^1].FillWeight = weight;
        }
        standingsTab.Controls.Add(standingsGrid);
        standingsTab.Controls.Add(exportPanel);
        content.Controls.Add(UiStyle.TabBar(tabs));
        content.Controls.Add(tabs);
        tabs.BringToFront();
    }

    private void Act(Action action)
    {
        try
        {
            if (!storeAvailable) throw new InvalidOperationException("Файл турниров повреждён. Работа заблокирована, чтобы не затереть данные. Восстановите копию .bak или переименуйте повреждённый файл.");
            action();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void SaveState()
    {
        if (storeAvailable) store.Save(tournaments);
    }

    private void RefreshTournamentList(Guid? prefer = null)
    {
        var key = prefer ?? current?.Id;
        tournamentList.BeginUpdate();
        tournamentList.Items.Clear();
        foreach (var t in tournaments.OrderByDescending(x => x.CreatedAt)) tournamentList.Items.Add(t);
        tournamentList.EndUpdate();
        var chosen = tournaments.FirstOrDefault(x => x.Id == key);
        if (chosen is not null) tournamentList.SelectedItem = chosen;
        else if (tournamentList.Items.Count > 0) tournamentList.SelectedIndex = 0;
        else { current = null; RefreshCurrent(); }
    }

    private void RefreshCurrent()
    {
        var t = current;
        bool selected = t is not null, started = t?.Started ?? false;
        tournamentTitle.Text = t?.Name ?? "Выберите турнир или создайте новый";
        tournamentDescription.Text = t is null ? "Для начала создайте турнир в левой панели." :
            (t.System == TournamentSystem.Swiss ? "Швейцарская система • ФИДЕ Dutch • " :
                "Круговая система • " + (t.DoubleRound ? "два круга • " : "один круг • ")) +
            $"{t.Players.Count} участников • {t.Rounds.Count}/{t.TotalRounds} туров";
        btnAddPlayer.Enabled = selected && !started;
        btnImport.Enabled = selected && !started;
        UpdateOverview();
        btnEditPlayer.Enabled = selected;
        btnDeletePlayer.Enabled = selected && !started;
        btnBye.Enabled = selected && t!.System == TournamentSystem.Swiss && t.Rounds.Count < t.SwissRounds;
        btnWithdraw.Enabled = selected && t!.System == TournamentSystem.Swiss && started;
        btnPair.Enabled = selected && (t!.System == TournamentSystem.Swiss ?
            t.Rounds.Count < t.SwissRounds : !started);
        btnPair.Text = t?.System == TournamentSystem.RoundRobin ? "Создать расписание" : "Следующий тур";
        footer.Text = t is null ? "Данные: " + store.DataPath :
            "Сохранено автоматически  •  " + store.DataPath;
        playerGrid.Rows.Clear();
        if (t is not null)
        {
            foreach (var p in t.Players.OrderBy(x => x.Number))
            {
                int row = playerGrid.Rows.Add(p.Number, p.FullName, p.Rating == 0 ? "—" : p.Rating,
                    p.Withdrawn ? "Снят" : t.PlannedByes.Any(b => b.PlayerId == p.Number && b.RoundNumber == t.Rounds.Count + 1) ? "Пропуск следующего тура" : "Участвует");
                playerGrid.Rows[row].Tag = p;
            }
        }
        var selectedRound = roundPicker.SelectedIndex;
        roundPicker.Items.Clear();
        if (t is not null)
            foreach (var r in t.Rounds.OrderBy(r => r.Number)) roundPicker.Items.Add($"Тур {r.Number}");
        if (roundPicker.Items.Count > 0) roundPicker.SelectedIndex = Math.Clamp(selectedRound, 0, roundPicker.Items.Count - 1);
        else RefreshRoundGrid();
        RefreshStandings();
    }

    private void RefreshRoundGrid()
    {
        roundGrid.Rows.Clear();
        var t = current;
        int chosen = roundPicker.SelectedIndex;
        var round = t?.Rounds.OrderBy(r => r.Number).ElementAtOrDefault(chosen >= 0 ? chosen : int.MaxValue);
        bool editAllowed = t is not null && round is not null &&
            (t.System == TournamentSystem.RoundRobin || round.Number == t.Rounds.Count);
        btnSaveResults.Enabled = editAllowed;
        btnUndo.Enabled = t is not null && t.System == TournamentSystem.Swiss && round is not null && round.Number == t.Rounds.Count;
        hintLabel.Text = t is null || round is null ? "Сначала сформируйте жеребьёвку." :
            editAllowed ? "Результаты сохраняются сразу после выбора. BYE начисляется автоматически." :
            "Результаты прошедшего тура заблокированы после жеребьёвки следующего.";
        if (t is null || round is null) return;
        // Display the standings at the START of the selected round, not the live
        // totals after results entered in this round (or later round-robin rounds).
        var before = StandingsService.ScoresBeforeRound(t, round.Number);
        foreach (var game in round.Games)
        {
            int index = roundGrid.Rows.Add(game.Board,
                t.Players.First(p => p.Number == game.WhiteId).FullName,
                Format(before[game.WhiteId]),
                t.Players.First(p => p.Number == game.BlackId).FullName,
                Format(before[game.BlackId]),
                game.Result is GameResult.WhiteForfeitWin or GameResult.BlackForfeitWin or GameResult.DoubleForfeit ? "Технический" : "Партия",
                Results.First(x => x.Value == game.Result).Label);
            roundGrid.Rows[index].Tag = game;
            roundGrid.Rows[index].Cells[6].ReadOnly = !editAllowed;
        }
        foreach (var bye in round.Byes)
        {
            var player = t.Players.First(p => p.Number == bye.PlayerId);
            string kind = bye.Kind == ByeKind.PairingAllocated ? "Автоматический BYE (+1)" :
                bye.Kind == ByeKind.RoundRobinRest ? "Свободен (0)" : "Пропуск тура";
            int index = roundGrid.Rows.Add("—", player.FullName, Format(before[player.Number]), "—", "—", kind, null);
            // DataGridViewCell.ReadOnly can be changed only after the cell is
            // attached to a row that belongs to the DataGridView.
            // Setting it in an object initializer crashes startup when a saved
            // tournament contains a BYE (or a round-robin rest).
            var byeCell = new DataGridViewTextBoxCell();
            roundGrid.Rows[index].Cells[6] = byeCell;
            byeCell.Value = ExcelExport.ByeLabel(bye.Kind);
            byeCell.ReadOnly = true;
        }
    }

    private void RefreshStandings()
    {
        standingsGrid.Rows.Clear();
        standingsGrid.Columns["buchholz"].Visible = standingsGrid.Columns["cut"].Visible = current?.System != TournamentSystem.RoundRobin;
        RefreshProgress();
        if (current is null) return;
        foreach (var s in StandingsService.Calculate(current))
            standingsGrid.Rows.Add(s.Place, s.Player.Number, s.Player.FullName,
                Format(s.Score), Format(s.Buchholz), Format(s.CutBuchholz), Format(s.Berger), s.Wins);
    }
    private static string Format(decimal n) => n.ToString("0.0");

    private void CreateTournament()
    {
        using var dialog = new TournamentDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var t = new Tournament
        {
            Name = dialog.TournamentName, System = dialog.System,
            SwissRounds = dialog.SwissRounds, DoubleRound = dialog.DoubleRound,
            InitialWhite = dialog.InitialWhite
        };
        tournaments.Add(t); current = t; SaveState(); RefreshTournamentList(t.Id);
    }

    private void EditTournament()
    {
        if (current is null) return;
        using var dialog = new TournamentDialog(current);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        current.Name = dialog.TournamentName;
        if (!current.Started)
        {
            current.System = dialog.System;
            current.DoubleRound = dialog.DoubleRound;
            current.SwissRounds = dialog.SwissRounds;
            current.InitialWhite = dialog.InitialWhite;
        }
        SaveState(); RefreshTournamentList(current.Id); RefreshCurrent();
    }
    private void DeleteTournament()
    {
        if (current is null) return;
        if (MessageBox.Show(this, $"Удалить «{current.Name}» вместе со всеми результатами?", "Подтверждение",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        tournaments.Remove(current); current = null; SaveState(); RefreshTournamentList();
    }

    private Player? SelectedPlayer => playerGrid.CurrentRow?.Tag as Player;

    private void AddPlayer()
    {
        if (current is null || current.Started) return;
        using var dialog = new PlayerDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (current.Players.Any(p => string.Equals(p.FullName, dialog.FullName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Игрок с таким ФИО уже добавлен.");
        if (current.Players.Count > 0 && current.Players.Max(p => p.Number) >= 9999) throw new InvalidOperationException("Превышено число игроков в формате TRF (9999).");
        current.Players.Add(new Player { Number = current.Players.Count == 0 ? 1 : current.Players.Max(p => p.Number) + 1,
            FullName = dialog.FullName, Rating = dialog.Rating });
        SaveState(); RefreshCurrent();
    }

    private void EditPlayer()
    {
        var p = SelectedPlayer;
        if (p is null || current is null) return;
        using var dialog = new PlayerDialog(p, frozenRating: current.Started);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (current.Players.Any(x => x.Number != p.Number && string.Equals(x.FullName, dialog.FullName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Игрок с таким ФИО уже добавлен.");
        p.FullName = dialog.FullName;
        if (!current.Started) p.Rating = dialog.Rating;
        SaveState(); RefreshCurrent();
    }
    private void DeletePlayer()
    {
        var p = SelectedPlayer;
        if (current is null || current.Started || p is null) return;
        if (MessageBox.Show(this, $"Удалить игрока {p.FullName}?", "Подтверждение",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        current.Players.Remove(p);
        current.PlannedByes.RemoveAll(b => b.PlayerId == p.Number);
        SaveState(); RefreshCurrent();
    }

    private void ScheduleBye()
    {
        if (current is null || current.System != TournamentSystem.Swiss) return;
        var p = SelectedPlayer;
        if (p is null) throw new InvalidOperationException("Выберите участника на вкладке «Участники».");
        if (p.Withdrawn) throw new InvalidOperationException("Игрок снят с турнира.");
        int next = current.Rounds.Count + 1;
        if (next > current.SwissRounds) throw new InvalidOperationException("Турнир завершён.");
        using var dialog = new ByeDialog(p.FullName, next);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        current.PlannedByes.RemoveAll(b => b.PlayerId == p.Number && b.RoundNumber == next);
        if (dialog.Kind is { } kind)
            current.PlannedByes.Add(new PlannedBye { RoundNumber = next, PlayerId = p.Number, Kind = kind });
        SaveState();
        MessageBox.Show(this, $"{p.FullName}, тур {next}: {(dialog.Kind is { } selectedKind ? ExcelExport.ByeLabel(selectedKind) : "участвует")}.",
            "Пропуск назначен", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ToggleWithdrawn()
    {
        if (current is null || current.System != TournamentSystem.Swiss || !current.Started) return;
        var p = SelectedPlayer;
        if (p is null) throw new InvalidOperationException("Выберите игрока на вкладке «Участники».");
        p.Withdrawn = !p.Withdrawn;
        if (p.Withdrawn) current.PlannedByes.RemoveAll(b => b.PlayerId == p.Number && b.RoundNumber > current.Rounds.Count);
        SaveState(); RefreshCurrent();
    }

    private void PairNext()
    {
        var t = current;
        if (t is null) return;
        if (t.Players.Count < 2) throw new InvalidOperationException("Добавьте минимум двух участников.");
        if (t.System == TournamentSystem.RoundRobin)
        {
            if (t.Started) return;
            t.Rounds = RoundRobinPairer.Generate(t);
        }
        else
        {
            if (t.Rounds.Count >= t.SwissRounds) throw new InvalidOperationException("Все туры уже сформированы.");
            var paired = TournamentOperations.PairNextSwiss(t);
            t.Rounds.Add(paired);
        }
        SaveState(); RefreshCurrent();
        roundPicker.SelectedIndex = t.System == TournamentSystem.Swiss ? t.Rounds.Count - 1 : 0;
        tabs.SelectedIndex = 1;
    }

    private void SaveResults()
    {
        if (current is null || roundPicker.SelectedIndex < 0) return;
        var round = current.Rounds.OrderBy(x => x.Number).ElementAt(roundPicker.SelectedIndex);
        if (current.System == TournamentSystem.Swiss && round.Number != current.Rounds.Count)
            throw new InvalidOperationException("Редактирование прошлых туров невозможно: жеребьёвка следующих туров уже зависит от их результатов.");
        roundGrid.EndEdit();
        foreach (DataGridViewRow row in roundGrid.Rows)
        {
            if (row.Tag is not Game game) continue;
            string value = row.Cells[6].Value?.ToString() ?? Results[0].Label;
            game.Result = Results.First(x => x.Label == value).Value;
        }
        SaveState(); RefreshStandings(); RefreshRoundGrid(); UpdateOverview();
        MessageBox.Show(this, round.Completed ? "Все результаты тура сохранены. Можно переходить к следующему туру." :
            "Результаты сохранены. Остались незавершённые партии.", "Сохранено", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UndoLastRound()
    {
        if (current is null || current.System != TournamentSystem.Swiss || current.Rounds.Count == 0) return;
        if (MessageBox.Show(this, "Удалить жеребьёвку и все результаты последнего тура? Это действие нельзя отменить.",
            "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        current.Rounds.RemoveAt(current.Rounds.Count - 1);
        SaveState(); RefreshCurrent();
    }

    private void ExportExcel()
    {
        if (current is null) return;
        using var d = new SaveFileDialog { Filter = "Книга Excel (*.xlsx)|*.xlsx", DefaultExt = "xlsx",
            FileName = SafeName(current.Name) + "-таблицы.xlsx" };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        ExcelExport.Save(current, d.FileName);
        MessageBox.Show(this, "Файл Excel сохранён:\n" + d.FileName);
    }
    private void ExportPdf()
    {
        if (current is null) return;
        using var d = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", DefaultExt = "pdf",
            FileName = SafeName(current.Name) + "-отчёт.pdf" };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        PdfExport.Save(current, d.FileName);
        MessageBox.Show(this, "Печать в PDF запущена.\nПроверьте файл:\n" + d.FileName);
    }
    private void UpdateOverview()
    {
        if (current is null) { overview.Text = "Участники → жеребьёвка → результаты → итоги"; return; }
        var t = current;
        int complete = t.Rounds.Count(r => r.Completed);
        int pending = t.Rounds.Sum(r => r.Games.Count(g => g.Result == GameResult.Pending));
        string state = t.Started && complete == t.TotalRounds ? "Турнир завершён" : !t.Started ? "Регистрация участников" : "Турнир идёт";
        overview.Text = $"{state}     •     Завершено туров: {complete} / {t.TotalRounds}     •     Без результата: {pending}";
    }

    private void RefreshProgress()
    {
        progressGrid.Rows.Clear(); progressGrid.Columns.Clear();
        progressGrid.Columns.Add("number", "№"); progressGrid.Columns.Add("name", "Участник");
        progressGrid.Columns[0].FillWeight = 30; progressGrid.Columns[1].FillWeight = 180;
        if (current is null) return;
        var rounds = current.Rounds.OrderBy(r => r.Number).ToList();
        foreach (var r in rounds) progressGrid.Columns.Add("r" + r.Number, "Тур " + r.Number);
        progressGrid.Columns.Add("score", "Очки");
        progressGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        progressGrid.Columns[0].Width = 55; progressGrid.Columns[1].Width = 240;
        progressGrid.Columns[1].Frozen = true;
        foreach (var s in StandingsService.Calculate(current))
        {
            var cells = new List<object> { s.Player.Number, s.Player.FullName };
            cells.AddRange(rounds.Select(r => (object)TournamentOperations.RoundCell(r, s.Player.Number)));
            cells.Add(Format(s.Score));
            progressGrid.Rows.Add(cells.ToArray());
        }
    }

    private void ImportPlayers()
    {
        if (current is null || current.Started) return;
        using var dialog = new PlayerListDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var players = TournamentOperations.ParsePlayers(dialog.PlayerText, current.Players);
        current.Players.AddRange(players);
        SaveState(); RefreshCurrent();
    }

    private static string SafeName(string s) => string.Concat(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
