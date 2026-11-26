using System.Drawing.Drawing2D;
using ChessDraw.Models;
using ChessDraw.Services;

namespace ChessDraw.Exports;

internal sealed record ReportRow(string[] Cells, bool Highlight = false);
internal sealed record ReportSection(string Title, string Subtitle, string[] Headers, float[] Widths, List<ReportRow> Rows);
internal sealed record ReportPage(ReportSection Section, List<(ReportRow Row, float Height)> Rows, bool Continued);

// The same vector layout is used by the PDF printer and the preview checks.
internal sealed class TournamentReport : IDisposable
{
    private readonly Tournament tournament;
    private readonly Font body = new("Segoe UI", 9f);
    private readonly Font bold = new("Segoe UI", 9f, FontStyle.Bold);
    private readonly Font title = new("Segoe UI", 21f, FontStyle.Bold);
    private readonly Font heading = new("Segoe UI", 14f, FontStyle.Bold);
    private readonly Font small = new("Segoe UI", 8f);
    private static readonly Color Navy = Color.FromArgb(28, 43, 68);
    private static readonly Color Ink = Color.FromArgb(36, 49, 68);
    private static readonly Color Muted = Color.FromArgb(104, 117, 135);
    private static readonly Color Accent = Color.FromArgb(0, 133, 129);
    private readonly List<ReportSection> sections;
    private readonly DateTime generated = DateTime.Now;
    private const float TableTop = 178;
    private const float TableHeader = 34;
    private const float BottomReserve = 54;

    public TournamentReport(Tournament tournament)
    {
        this.tournament = tournament;
        bool swiss = tournament.System == TournamentSystem.Swiss;
        var standings = StandingsService.Calculate(tournament);
        var rows = standings.Select(s => new ReportRow(swiss
            ? new[] { s.Place.ToString(), s.Player.Number.ToString(), s.Player.FullName, Rating(s.Player), Score(s.Score), Score(s.CutBuchholz), Score(s.Buchholz), Score(s.Berger), s.Wins.ToString() }
            : new[] { s.Place.ToString(), s.Player.Number.ToString(), s.Player.FullName, Rating(s.Player), Score(s.Score), Score(s.Berger), s.Wins.ToString() }, s.Place <= 3)).ToList();
        sections = new() { new("Турнирная таблица",
            swiss ? "Порядок: очки → БХ−1 → Бухгольц → Бергер → победы за доской → стартовый номер"
                : "Порядок: очки → Бергер → победы за доской → стартовый номер",
            swiss ? new[] { "Место", "№", "Участник", "Рейтинг", "Очки", "БХ−1", "БХ", "Бергер", "Победы*" }
                : new[] { "Место", "№", "Участник", "Рейтинг", "Очки", "Бергер", "Победы*" },
            swiss ? new float[] { 6, 5, 36, 9, 8, 9, 9, 10, 8 } : new float[] { 6, 5, 44, 10, 10, 12, 13 }, rows) };
        foreach (var round in tournament.Rounds.OrderBy(r => r.Number))
        {
            var before = StandingsService.ScoresBeforeRound(tournament, round.Number);
            var games = round.Games.OrderBy(g => g.Board).Select(g => new ReportRow(new[] {
                g.Board.ToString(), Find(g.WhiteId).FullName, Score(before[g.WhiteId]),
                ExcelExport.ResultLabel(g.Result), Score(before[g.BlackId]), Find(g.BlackId).FullName })).ToList();
            games.AddRange(round.Byes.Select(b => new ReportRow(new[] { "—", Find(b.PlayerId).FullName,
                Score(before[b.PlayerId]), ExcelExport.ByeLabel(b.Kind), "—", "Без соперника" })));
            sections.Add(new($"Жеребьёвка и результаты · Тур {round.Number}",
                $"{(round.Completed ? "Тур завершён" : "Результаты внесены не полностью")}   /   Очки участников указаны до начала тура",
                new[] { "Стол", "Белые / участник", "Очки", "Результат", "Очки", "Чёрные" },
                new float[] { 6, 30, 7, 20, 7, 30 }, games));
        }
    }

    public IReadOnlyList<ReportPage> Paginate(Graphics graphics, SizeF size)
    {
        float available = size.Height - TableTop - TableHeader - BottomReserve;
        if (available < 50 || size.Width < 500) throw new InvalidOperationException("Недостаточно места для отчёта. Используйте A4 в альбомной ориентации.");
        var pages = new List<ReportPage>();
        foreach (var section in sections)
        {
            var page = new ReportPage(section, new(), false);
            pages.Add(page);
            float used = 0;
            var widths = Widths(section, size.Width);
            foreach (var row in section.Rows)
            {
                float height = 31;
                for (int c = 0; c < row.Cells.Length; c++)
                    height = Math.Max(height, graphics.MeasureString(row.Cells[c], body, (int)(widths[c] - 18)).Height + 15);
                height = Math.Min(height, available);
                if (used + height > available && page.Rows.Count > 0)
                {
                    page = new(section, new(), true); pages.Add(page); used = 0;
                }
                page.Rows.Add((row, height)); used += height;
            }
        }
        return pages;
    }

    public void DrawPage(Graphics g, RectangleF bounds, ReportPage page, int number, int total)
    {
        var state = g.Save();
        try
        {
            g.TranslateTransform(bounds.Left, bounds.Top);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float width = bounds.Width;
            Fill(g, Navy, new(0, 0, width, 102));
            Fill(g, Accent, new(0, 0, 6, 102));
            Text(g, "ТУСУР  /  ШАХМАТНАЯ ЖЕРЕБЬЁВКА", bold, Color.FromArgb(149, 223, 216), new(22, 13, width - 44, 20));
            Text(g, tournament.Name, title, Color.White, new(20, 35, width - 40, 37));
            string system = tournament.System == TournamentSystem.Swiss ? "Швейцарская система" : "Круговая система · " + (tournament.DoubleRound ? "два круга" : "один круг");
            int completed = tournament.Rounds.Count(r => r.Completed);
            Text(g, $"{system}     /     Участников: {tournament.Players.Count}     /     Завершено туров: {completed} из {tournament.TotalRounds}",
                small, Color.FromArgb(212, 221, 235), new(22, 77, width - 44, 19));
            Text(g, page.Section.Title + (page.Continued ? " · продолжение" : ""), heading, Ink, new(0, 118, width, 30));
            Text(g, page.Section.Subtitle, small, Muted, new(0, 151, width, 23));

            var widths = Widths(page.Section, width);
            Fill(g, Navy, new(0, TableTop, width, TableHeader));
            float x = 0;
            for (int c = 0; c < widths.Length; c++)
            {
                Text(g, page.Section.Headers[c], bold, Color.White, new(x + 8, TableTop, widths[c] - 16, TableHeader), IsName(page.Section, c));
                x += widths[c];
            }
            float y = TableTop + TableHeader;
            int index = 0;
            foreach (var (row, height) in page.Rows)
            {
                Fill(g, row.Highlight ? Color.FromArgb(233, 246, 243) : index % 2 == 0 ? Color.FromArgb(244, 247, 251) : Color.White, new(0, y, width, height));
                x = 0;
                for (int c = 0; c < widths.Length; c++)
                {
                    bool emphasis = page.Section.Headers[c] is "Очки" or "Место" or "Результат";
                    Text(g, row.Cells[c], emphasis ? bold : body, emphasis ? Accent : Ink,
                        new(x + 8, y + 3, widths[c] - 16, height - 6), IsName(page.Section, c));
                    x += widths[c];
                }
                using var line = new Pen(Color.FromArgb(223, 229, 237), .6f);
                g.DrawLine(line, 0, y + height, width, y + height);
                y += height; index++;
            }
            if (page.Rows.Count == 0) Text(g, "Участники пока не добавлены", body, Muted, new(8, y + 12, width - 16, 28));
            float bottom = bounds.Height - 36;
            using var rule = new Pen(Color.FromArgb(218, 225, 234));
            g.DrawLine(rule, 0, bottom - 12, width, bottom - 12);
            Text(g, "* Победы за доской: без технических побед и BYE.", small, Muted, new(0, bottom - 7, width * .64f, 21));
            Text(g, $"{generated:dd.MM.yyyy HH:mm}   ·   {number} / {total}", small, Muted, new(width * .65f, bottom - 7, width * .35f, 21), false);
        }
        finally { g.Restore(state); }
    }

    private static bool IsName(ReportSection section, int column) => section.Headers[column] is "Участник" or "Белые / участник" or "Чёрные";
    private static float[] Widths(ReportSection section, float width) => section.Widths.Select(w => width * w / section.Widths.Sum()).ToArray();
    private Player Find(int id) => tournament.Players.First(p => p.Number == id);
    private static string Score(decimal score) => score.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
    private static string Rating(Player player) => player.Rating > 0 ? player.Rating.ToString() : "—";
    private static void Fill(Graphics g, Color color, RectangleF rect) { using var brush = new SolidBrush(color); g.FillRectangle(brush, rect); }
    private static void Text(Graphics g, string text, Font font, Color color, RectangleF rect, bool left = true)
    {
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = left ? StringAlignment.Near : StringAlignment.Center,
            LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord };
        g.DrawString(text, font, brush, rect, format);
    }
    public void Dispose() { body.Dispose(); bold.Dispose(); title.Dispose(); heading.Dispose(); small.Dispose(); }
}
