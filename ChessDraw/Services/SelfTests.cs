using System.IO.Compression;
using ChessDraw.Exports;
using ChessDraw.Models;

namespace ChessDraw.Services;

public static class SelfTests
{
    public static void Run(bool quiet = false)
    {
        File.Delete(Path.Combine(AppContext.BaseDirectory, "self-test-failure.txt"));
        var output = new List<string>();
        void Check(bool yes, string description)
        {
            if (!yes) throw new InvalidOperationException("Тест не пройден: " + description);
            output.Add("OK: " + description);
        }
        for (int count = 2; count <= 20; count++)
        {
            foreach (var two in new[] { false, true })
            {
                var t = Make(count); t.System = TournamentSystem.RoundRobin; t.DoubleRound = two;
                t.Rounds = RoundRobinPairer.Generate(t);
                int multiplier = two ? 2 : 1;
                Check(t.Rounds.Count == (count % 2 == 0 ? count - 1 : count) * multiplier,
                    $"круговая: число туров, {count} игроков, double={two}");
                var allPairs = t.Rounds.SelectMany(r => r.Games)
                    .GroupBy(g => (Math.Min(g.WhiteId, g.BlackId), Math.Max(g.WhiteId, g.BlackId)))
                    .ToList();
                Check(allPairs.Count == count * (count - 1) / 2 && allPairs.All(g => g.Count() == multiplier),
                    $"круговая: каждый с каждым, {count} игроков, double={two}");
                Check(t.Rounds.All(r => r.Games.SelectMany(g => new[] { g.WhiteId, g.BlackId })
                    .Concat(r.Byes.Select(b => b.PlayerId)).Distinct().Count() == count),
                    $"круговая: без повторов внутри тура, {count} игроков, double={two}");
                if (!two) Check(t.Players.All(p =>
                    Math.Abs(t.Rounds.SelectMany(r => r.Games).Count(g => g.WhiteId == p.Number) -
                             t.Rounds.SelectMany(r => r.Games).Count(g => g.BlackId == p.Number)) <= 1),
                    $"круговая: баланс цветов, {count} игроков");
                if (count == 6 && !two) Check(t.Rounds[1].Games.Select(g => (g.WhiteId, g.BlackId))
                    .SequenceEqual(new[] { (6, 4), (5, 3), (1, 2) }), "круговая: официальная таблица Бергера для 6 игроков");
                if (two) Check(t.Rounds.Take(t.Rounds.Count / 2).SelectMany(r => r.Games)
                    .All(g => t.Rounds.Skip(t.Rounds.Count / 2).SelectMany(r => r.Games)
                        .Any(other => other.WhiteId == g.BlackId && other.BlackId == g.WhiteId)),
                        $"круговая: смена цветов, {count} игроков");
            }
        }
        var swiss = Make(5); swiss.System = TournamentSystem.Swiss;
        swiss.SwissRounds = 3;
        swiss.Rounds.Add(new ChessRound
        {
            Number = 1,
            Games = new List<Game> { new() { WhiteId = 1, BlackId = 2, Result = GameResult.WhiteWin },
                new() { WhiteId = 3, BlackId = 4, Result = GameResult.Draw } },
            Byes = new List<RoundBye> { new() { PlayerId = 5, Kind = ByeKind.PairingAllocated } }
        });
        var scores = StandingsService.Calculate(swiss);
        Check(scores.Single(x => x.Player.Number == 1).Score == 1, "очки за победу");
        Check(scores.Single(x => x.Player.Number == 5).Score == 1, "очки за нечётный BYE");
        Check(scores.Single(x => x.Player.Number == 3).Score == .5m, "очки за ничью");
        Check(StandingsService.ScoresBeforeRound(swiss, 1).Values.All(v => v == 0m),
            "очки до первого тура равны нулю");
        Check(StandingsService.ScoresBeforeRound(swiss, 2)[5] == 1m,
            "автоматический BYE (+1) переходит в очки перед следующим туром");
        Check(StandingsService.ScoresBeforeRound(swiss, 2)[3] == .5m,
            "половина очка за ничью отображается перед следующим туром");
        Check(swiss.Rounds[0].Completed, "тур с автоматическим BYE завершён без ввода результата за BYE");
        var technical = Make(3);
        technical.Rounds.Add(new ChessRound { Number = 1,
            Games = new() { new Game { WhiteId = 1, BlackId = 2, Result = GameResult.WhiteForfeitWin } },
            Byes = new() { new RoundBye { PlayerId = 3, Kind = ByeKind.PairingAllocated } } });
        Check(StandingsService.ScoresBeforeRound(technical, 2)[1] == 1m &&
              StandingsService.ScoresBeforeRound(technical, 2)[3] == 1m,
            "техническая победа и автоматический BYE оба приносят одно очко");
        var trf = SwissPairer.CreateTrf(swiss, 2);
        var line = trf.Split('\n').Single(s => s.StartsWith("001 0001"));
        Check(line.Substring(91, 8) == "0002 w 1", "TRF: результат белых в нужных колонках");
        Check(trf.Contains("XXC white1"), "TRF: явный начальный цвет");
        var tmpFile = Path.Combine(Path.GetTempPath(), "chessdraw-test-" + Guid.NewGuid() + ".xlsx");
        try
        {
            ExcelExport.Save(swiss, tmpFile);
            using var zip = ZipFile.OpenRead(tmpFile);
            foreach (var entry in zip.Entries)
            {
                using var stream = entry.Open();
                System.Xml.Linq.XDocument.Load(stream);
            }
            Check(zip.GetEntry("xl/workbook.xml") != null && zip.GetEntry("xl/worksheets/sheet2.xml") != null,
                "Excel XLSX содержит обе таблицы");
        }
        finally { if (File.Exists(tmpFile)) File.Delete(tmpFile); }

        var file = Path.Combine(Path.GetTempPath(), "chessdraw-test-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new TournamentStore(file);
            store.Save(new[] { swiss });
            Check(store.Load().Single().Rounds.Single().Games.Count == 2, "сохранение и восстановление турнира");
        }
        finally
        {
            foreach (var f in new[] { file, file + ".bak", file + ".tmp" })
                if (File.Exists(f)) File.Delete(f);
        }
        ExtendedSelfTests.Run(Check);
        var log = Path.Combine(AppContext.BaseDirectory, "self-test-results.txt");
        File.WriteAllLines(log, output);
        if (!quiet) MessageBox.Show($"Пройдено {output.Count} проверок.\n" + log, "Самопроверка ТУСУР");
    }

    private static Tournament Make(int count) => new()
    {
        Name = "Тестовый турнир",
        Players = Enumerable.Range(1, count).Select(i => new Player
        {
            Number = i, FullName = "Тестовый игрок " + i
        }).ToList()
    };
}
