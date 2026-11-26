using ChessDraw.Models;
using ChessDraw.UI;
using System.Reflection;

namespace ChessDraw.Services;

internal static class ExtendedSelfTests
{
    private static Tournament Make(int count) => new() { Name = "ТУСУР — тест",
        Players = Enumerable.Range(1, count).Select(i => new Player { Number = i, FullName = "Игрок " + i, Rating = 2000 - i * 10 }).ToList() };
    private static Game Game(int w, int b, GameResult result) => new() { WhiteId = w, BlackId = b, Result = result };

    public static void Run(Action<bool, string> check)
    {
        ApplicationConfiguration.Initialize();
        ReportSelfTests.Run(check);
        void Reject(Action action, string description)
        {
            bool rejected = false;
            try { action(); } catch (Exception e) when (e is InvalidOperationException or InvalidDataException) { rejected = true; }
            check(rejected, description);
        }
        var normal = Make(4);
        normal.Rounds.Add(new() { Number = 1, Games = new() { Game(1, 2, GameResult.WhiteWin), Game(3, 4, GameResult.Draw) } });
        normal.Rounds.Add(new() { Number = 2, Games = new() { Game(1, 3, GameResult.Draw), Game(2, 4, GameResult.WhiteWin) } });
        var table = StandingsService.Calculate(normal).ToDictionary(s => s.Player.Number);
        check(table[1].Score == 1.5m && table[1].Buchholz == 2 && table[1].Berger == 1.5m && table[1].CutBuchholz == 1,
            "контрольный пример: очки, Бухгольц, Cut-1, Бергер");
        check(table[3].Place < table[2].Place, "разрешение равенства по Бергеру");
        var bye = Make(3); bye.SwissRounds = 9;
        bye.Rounds.Add(new() { Number = 1, Games = new() { Game(2, 3, GameResult.Draw) },
            Byes = new() { new() { PlayerId = 1, Kind = ByeKind.PairingAllocated } } });
        check(StandingsService.Calculate(bye).Single(s => s.Player.Number == 1).Buchholz == 1,
            "предел BYE использует число туров турнира, а не сыгранных туров");
        var rr = Make(4); rr.System = TournamentSystem.RoundRobin;
        rr.Rounds = new() {
            new() { Number = 1, Games = new() { Game(1, 2, GameResult.WhiteForfeitWin), Game(3, 4, GameResult.WhiteWin) } },
            new() { Number = 2, Games = new() { Game(1, 3, GameResult.BlackWin), Game(2, 4, GameResult.WhiteWin) } },
            new() { Number = 3, Games = new() { Game(1, 4, GameResult.BlackWin), Game(2, 3, GameResult.WhiteWin) } } };
        check(StandingsService.Calculate(rr).Single(s => s.Player.Number == 1).Berger == 2,
            "круговой: техническая победа учитывает реальные очки соперника");
        check(StandingsService.Calculate(rr).All(s => s.Buchholz == 0 && s.CutBuchholz == 0), "Бухгольц не применяется в круговой системе");
        check(TournamentOperations.ParsePlayers("Иванов Иван;1800\nПетров Пётр\t1500\nСидоров Семён", Array.Empty<Player>()).Count == 3,
            "массовый ввод: рейтинг, табуляция, игрок без рейтинга");
        Reject(() => TournamentOperations.ParsePlayers("Иванов Иван;9999", Array.Empty<Player>()), "отклонение неверного рейтинга");
        Reject(() => TournamentOperations.ParsePlayers("Иванов Иван\nиванов иван", Array.Empty<Player>()), "отклонение повторов ФИО");
        var invalid = Make(2); invalid.Players[1].Number = 1;
        Reject(() => TournamentValidator.Validate(invalid), "проверка дублирующихся номеров в базе");
        var failed = Make(2); failed.SwissRounds = 0;
        failed.Players[0].Number = 20; failed.Players[1].Number = 10;
        Reject(() => TournamentOperations.PairNextSwiss(failed), "ошибка первой жеребьёвки отклонена");
        check(failed.Players[0].Number == 20 && failed.Players[1].Number == 10, "ошибка жеребьёвки не меняет стартовые номера");

        var trailing = Make(3); trailing.SwissRounds = 3;
        trailing.Rounds.Add(new() { Number = 1, Games = new() { Game(1, 2, GameResult.WhiteWin) },
            Byes = new() { new() { PlayerId = 3, Kind = ByeKind.PairingAllocated } } });
        trailing.Rounds.Add(new() { Number = 2, Games = new() { Game(1, 3, GameResult.Draw) },
            Byes = new() { new() { PlayerId = 2, Kind = ByeKind.RequestedZero } } });
        var adjusted = StandingsService.Calculate(trailing).Single(s => s.Player.Number == 1);
        check(adjusted.Buchholz == 2m && adjusted.Berger == 1.25m, "завершающий добровольный пропуск соперника учитывается как ничья для коэффициентов");
        var cutExample = Make(5); cutExample.SwissRounds = 3;
        cutExample.Rounds = new() {
            new() { Number = 1, Games = new() { Game(1, 2, GameResult.WhiteWin), Game(3, 4, GameResult.Draw) }, Byes = new() { new() { PlayerId = 5, Kind = ByeKind.PairingAllocated } } },
            new() { Number = 2, Games = new() { Game(1, 3, GameResult.WhiteWin), Game(2, 4, GameResult.BlackWin) }, Byes = new() { new() { PlayerId = 5, Kind = ByeKind.RequestedZero } } },
            new() { Number = 3, Games = new() { Game(2, 3, GameResult.Draw), Game(4, 5, GameResult.BlackWin) }, Byes = new() { new() { PlayerId = 1, Kind = ByeKind.RequestedZero } } } };
        var cutRow = StandingsService.Calculate(cutExample).Single(s => s.Player.Number == 1);
        check(cutRow.Buchholz == 3m && cutRow.CutBuchholz == 1.5m, "Cut-1 исключает вклад добровольного пропуска вместо меньшего вклада сыгранной партии");

        check(File.Exists(SwissPairer.EnginePath), "движок установлен для интеграционных тестов");
        foreach (int count in new[] { 5, 6, 9, 12 })
        {
            var t = Make(count); t.SwissRounds = count <= 6 ? 3 : 5;
            var played = new HashSet<(int, int)>();
            var byePlayers = new HashSet<int>();
            for (int r = 1; r <= t.SwissRounds; r++)
            {
                if (r == 2 && count > 6) t.PlannedByes.Add(new() { RoundNumber = r, PlayerId = 2, Kind = ByeKind.RequestedHalf });
                if (r == 3 && count > 6) { t.Players.Single(p => p.Number == 2).Withdrawn = true; }
                if (r == 4 && count > 6) { t.Players.Single(p => p.Number == 2).Withdrawn = false; }
                var round = TournamentOperations.PairNextSwiss(t);
                t.Rounds.Add(round);
                check(round.Games.SelectMany(g => new[] { g.WhiteId, g.BlackId }).Concat(round.Byes.Select(b => b.PlayerId)).Distinct().Count() == count,
                    $"Swiss {count}, тур {r}: все участники учтены");
                foreach (var g in round.Games)
                {
                    var pair = (Math.Min(g.WhiteId, g.BlackId), Math.Max(g.WhiteId, g.BlackId));
                    check(played.Add(pair), $"Swiss {count}, тур {r}: без повторных сыгранных пар");
                    g.Result = (r + g.Board) % 3 == 0 ? GameResult.Draw : (r + g.Board) % 2 == 0 ? GameResult.WhiteWin : GameResult.BlackWin;
                }
                foreach (var b in round.Byes.Where(b => b.Kind == ByeKind.PairingAllocated))
                    check(byePlayers.Add(b.PlayerId), $"Swiss {count}: автоматический BYE не повторяется");
                check(count <= 6 || r != 2 || round.Byes.Any(b => b.PlayerId == 2 && b.Kind == ByeKind.RequestedHalf), "заявленный пропуск ½");
                check(count <= 6 || r != 3 || round.Byes.Any(b => b.PlayerId == 2 && b.Kind == ByeKind.RequestedZero), "снятие игрока даёт 0");
                TournamentValidator.Validate(t);
            }
            check(t.Rounds.All(r => r.Completed), $"Swiss {count}: завершён полный турнир");
            Reject(() => SwissPairer.Pair(t), "не допускается тур сверх лимита");
        }
        var pending = Make(4); pending.Rounds.Add(SwissPairer.Pair(pending));
        Reject(() => SwissPairer.Pair(pending), "нельзя жеребить при незавершённом туре");
        var technical = Make(6);
        technical.Rounds.Add(SwissPairer.Pair(technical));
        technical.Rounds[0].Games[0].Result = GameResult.WhiteForfeitWin;
        technical.Rounds[0].Games[1].Result = GameResult.BlackForfeitWin;
        technical.Rounds[0].Games[2].Result = GameResult.DoubleForfeit;
        check(SwissPairer.Pair(technical).Games.Count == 3, "движок принимает все виды технических результатов");

        var folder = Path.Combine(Path.GetTempPath(), "tusur-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string file = Path.Combine(folder, "tournaments.json");
            var store = new TournamentStore(file);
            store.Save(new[] { bye }); store.Save(new[] { normal });
            File.WriteAllText(file, "broken");
            check(store.Load().Single().Players.Count == 3, "восстановление из резервной копии при повреждении JSON");
            store.Save(new[] { normal });
            File.WriteAllText(file, "[{\"Name\":\"Invalid\",\"Players\":[{\"Number\":0}]}]");
            check(store.Load().Single().Players.Count == 3, "восстановление при семантически повреждённых данных");
            store.Save(new[] { bye });
            using var form = new MainForm(store);
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
            form.ShowInTaskbar = false; form.Show(); Application.DoEvents(); form.PerformLayout();
            T Field<T>(string name) => (T)typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            var grid = Field<DataGridView>("roundGrid");
            check(grid.Rows.Count == 2 && grid.Rows[1].Cells[6].ReadOnly, "интерфейс: загрузка BYE без сбоя, результат BYE заблокирован");
            grid.Rows[0].Cells[6].Value = "1:0";
            check(store.Load().Single().Rounds[0].Games[0].Result == GameResult.WhiteWin, "интерфейс: результат сразу сохраняется на диск");
            Field<TabControl>("tabs").SelectedIndex = 1;
            Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, "ui-preview.png"));
            check(form.Text.Contains("ТУСУР"), "интерфейс: название ТУСУР");
            store.Save(new[] { normal });
            using var history = new MainForm(store);
            var historyGrid = (DataGridView)typeof(MainForm).GetField("roundGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(history)!;
            check(historyGrid.Rows.Cast<DataGridViewRow>().All(row => row.Cells[6].ReadOnly), "интерфейс: прошлый швейцарский тур заблокирован");
            var picker = (ComboBox)typeof(MainForm).GetField("roundPicker", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(history)!;
            picker.SelectedIndex = 1;
            check(historyGrid.Rows.Cast<DataGridViewRow>().All(row => !row.Cells[6].ReadOnly), "интерфейс: последний швейцарский тур редактируется");
            var rest = Make(3); rest.System = TournamentSystem.RoundRobin; rest.Rounds = RoundRobinPairer.Generate(rest);
            store.Save(new[] { rest });
            using var roundRobinForm = new MainForm(store);
            var rrGrid = (DataGridView)typeof(MainForm).GetField("roundGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(roundRobinForm)!;
            check(rrGrid.Rows.Count == 2 && rrGrid.Rows[1].Cells[6].ReadOnly, "интерфейс: круговой свободный тур загружается и не редактируется");
            File.WriteAllText(file, "broken"); File.WriteAllText(file + ".bak", "broken");
            Reject(() => store.Load(), "повреждение обеих копий не открывает пустую базу");
        }
        finally { Directory.Delete(folder, true); }
    }
}
