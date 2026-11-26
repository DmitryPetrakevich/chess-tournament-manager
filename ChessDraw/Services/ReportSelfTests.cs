using ChessDraw.Exports;
using ChessDraw.Models;
using ChessDraw.UI;

namespace ChessDraw.Services;

internal static class ReportSelfTests
{
    public static void Run(Action<bool, string> check)
    {
        var players = TournamentOperations.ParsePlayers("Иванов Иван\nПетров Пётр;\nСидоров Семён;0\nСмирнов Алексей;1650", Array.Empty<Player>());
        check(players.Take(3).All(p => p.Rating == 1000) && players[3].Rating == 1650,
            "рейтинг 1000: отсутствует, пустое поле, ноль; явный рейтинг сохраняется");
        check(new Player().Rating == 1000, "рейтинг новой модели — 1000");
        using (var dialog = new PlayerDialog()) check(dialog.Rating == 1000, "рейтинг в форме добавления — 1000");
        var demo = new Tournament { Name = "Кубок ТУСУР · Студенческий турнир", System = TournamentSystem.RoundRobin,
            Players = TournamentOperations.ParsePlayers("Иванов Александр Сергеевич;1850\nПетрова Анна Дмитриевна;1720\nСмирнов Михаил Андреевич\nКузнецова Мария Алексеевна;1640\nПопов Дмитрий Олегович;1530\nСоколова Екатерина Игоревна\nВасильев Артём Николаевич;1480", Array.Empty<Player>()) };
        demo.Rounds = RoundRobinPairer.Generate(demo);
        foreach (var round in demo.Rounds.Take(3))
            foreach (var game in round.Games) game.Result = (game.Board + round.Number) % 3 == 0 ? GameResult.Draw : GameResult.WhiteWin;
        using var bitmap = new Bitmap(1169, 827);
        bitmap.SetResolution(100, 100);
        using var graphics = Graphics.FromImage(bitmap);
        var bounds = new Rectangle(46, 40, 1077, 747);
        using (var report = new TournamentReport(demo))
        {
            var pages = report.Paginate(graphics, bounds.Size);
            check(pages.Count == 8 && pages[0].Rows.Count == 7, "PDF: таблица и семь туров на отдельных страницах");
            check(pages[0].Section.Headers.All(h => !h.StartsWith("БХ")), "PDF: без Бухгольца в круговом отчёте");
            check(pages[0].Rows.Any(r => r.Row.Cells[5] == "2,25"), "PDF: коэффициенты с четвертью очка сохраняют точность");
            foreach (int index in new[] { 0, 1 })
            {
                graphics.Clear(Color.White);
                report.DrawPage(graphics, bounds, pages[index], index + 1, pages.Count);
                bitmap.Save(Path.Combine(AppContext.BaseDirectory, index == 0 ? "pdf-preview-standings.png" : "pdf-preview-round.png"));
            }
        }
        var large = new Tournament { Name = "Проверка многостраничного отчёта", Players = Enumerable.Range(1, 100).Select(i => new Player {
            Number = i, FullName = i == 1 ? "Очень длинная фамилия и составное имя участника студенческого шахматного турнира ТУСУР" : "Участник " + i }).ToList() };
        using (var report = new TournamentReport(large))
        {
            var pages = report.Paginate(graphics, bounds.Size);
            check(pages.Count > 1 && pages.Sum(p => p.Rows.Count) == 100, "PDF: все 100 участников сохранены при переносе страниц");
            check(pages.Skip(1).All(p => p.Continued), "PDF: продолжения таблицы отмечены");
            check(pages.All(p => p.Rows.Sum(r => r.Height) <= 481), "PDF: строки не заходят в нижний колонтитул");
            check(pages[0].Rows[0].Height > 31, "PDF: длинное ФИО переносится на несколько строк");
            graphics.Clear(Color.White);
            report.DrawPage(graphics, bounds, pages[0], 1, pages.Count);
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, "pdf-preview-large.png"));
        }
        using (var report = new TournamentReport(new Tournament()))
            check(report.Paginate(graphics, bounds.Size).Count == 1, "PDF: пустой турнир создаёт одну страницу");
        if (Environment.GetCommandLineArgs().Contains("--pdf-demo"))
        {
            string file = Path.Combine(AppContext.BaseDirectory, "tusur-report-demo.pdf");
            File.Delete(file);
            PdfExport.Save(demo, file);
            bool valid = false;
            for (int i = 0; i < 50; i++)
            {
                try
                {
                    using var stream = File.OpenRead(file);
                    var marker = new byte[5];
                    valid = stream.Read(marker) == 5 && System.Text.Encoding.ASCII.GetString(marker) == "%PDF-";
                }
                catch (IOException) { }
                if (valid) break;
                Thread.Sleep(100);
            }
            check(valid, "PDF: реальная печать в Microsoft Print to PDF создаёт PDF-файл");
        }
    }
}
