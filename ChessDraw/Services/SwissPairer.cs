using System.Diagnostics;
using System.Globalization;
using System.Text;
using ChessDraw.Models;

namespace ChessDraw.Services;

public static class SwissPairer
{
    public static string EnginePath => Path.Combine(AppContext.BaseDirectory, "engines", "bbpPairings.exe");

    public static ChessRound Pair(Tournament tournament)
    {
        if (tournament.System != TournamentSystem.Swiss) throw new ArgumentException("Неверная система турнира.");
        if (tournament.Players.Count < 2) throw new InvalidOperationException("Требуется минимум два участника.");
        if (tournament.Rounds.Count >= tournament.SwissRounds) throw new InvalidOperationException("Все туры уже сформированы.");
        if (tournament.Rounds.Count > 0 && !tournament.Rounds[^1].Completed)
            throw new InvalidOperationException("Сначала внесите результаты всех партий предыдущего тура.");

        int number = tournament.Rounds.Count + 1;
        var scheduled = tournament.PlannedByes.Where(b => b.RoundNumber == number).ToDictionary(b => b.PlayerId);
        var absent = tournament.Players.Where(p => p.Withdrawn || scheduled.ContainsKey(p.Number)).ToList();
        var active = tournament.Players.Where(p => !p.Withdrawn && !scheduled.ContainsKey(p.Number)).ToList();
        var round = new ChessRound { Number = number };
        foreach (var p in absent)
            round.Byes.Add(new RoundBye { PlayerId = p.Number, Kind =
                !p.Withdrawn && scheduled.TryGetValue(p.Number, out var entry) ? entry.Kind : ByeKind.RequestedZero });

        if (active.Count == 0) return round;
        if (!File.Exists(EnginePath))
            throw new FileNotFoundException("Движок официальной жеребьёвки bbpPairings 6.0 не найден.\n" +
                "Запустите Install-BBPPairings.ps1 или положите bbpPairings.exe в папку engines рядом с ChessDraw.exe.", EnginePath);

        string temp = Path.Combine(Path.GetTempPath(), "ChessDraw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        string input = Path.Combine(temp, "input.trf");
        string output = Path.Combine(temp, "pairs.txt");
        try
        {
            File.WriteAllText(input, CreateTrf(tournament, number), Encoding.ASCII);
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = EnginePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                WorkingDirectory = temp
            };
            process.StartInfo.ArgumentList.Add("--dutch");
            process.StartInfo.ArgumentList.Add(input);
            process.StartInfo.ArgumentList.Add("-p");
            process.StartInfo.ArgumentList.Add(output);
            if (!process.Start()) throw new InvalidOperationException("Не удалось запустить движок жеребьёвки.");
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(45000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Превышено время расчёта жеребьёвки (45 секунд).");
            }
            string error = errorTask.GetAwaiter().GetResult().Trim();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Движок bbpPairings вернул код {process.ExitCode}. " +
                    (error.Length == 0 ? "Допустимая жеребьёвка не найдена." : error));
            if (!File.Exists(output)) throw new InvalidDataException("Движок не создал файл жеребьёвки.");
            var lines = File.ReadAllLines(output).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            if (lines.Length == 0 || !int.TryParse(lines[0].Trim(), out int count) || count < 0)
                throw new InvalidDataException("Неверный формат ответа движка (ожидалось число пар).");
            if (lines.Length != count + 1) throw new InvalidDataException("Неверное число строк в ответе движка.");

            var seen = new HashSet<int>();
            var allowed = active.Select(p => p.Number).ToHashSet();
            int pabCount = 0;
            for (int i = 1; i <= count; i++)
            {
                var fields = lines[i].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length != 2 || !int.TryParse(fields[0], out int white) || !int.TryParse(fields[1], out int black))
                    throw new InvalidDataException("Недопустимая пара в строке " + i);
                if (white == 0 || black == 0)
                {
                    int byeId = white == 0 ? black : white;
                    if (byeId == 0 || !allowed.Contains(byeId) || !seen.Add(byeId) || ++pabCount > 1)
                        throw new InvalidDataException("Ошибочный пропуск тура от движка.");
                    round.Byes.Add(new RoundBye { PlayerId = byeId, Kind = ByeKind.PairingAllocated });
                }
                else
                {
                    if (white == black || !allowed.Contains(white) || !allowed.Contains(black) ||
                        !seen.Add(white) || !seen.Add(black))
                        throw new InvalidDataException("Движок вернул повторяющегося или неизвестного участника.");
                    round.Games.Add(new Game { Board = round.Games.Count + 1, WhiteId = white, BlackId = black });
                }
            }
            if (!seen.SetEquals(allowed)) throw new InvalidDataException("Жеребьёвка охватила не всех заявленных участников.");
            // One automatic pairing-allocated bye (+1 point) is mandatory for an
            // odd number of active Swiss players. A voluntarily requested bye is
            // not a pairing-allocated bye and does not count toward this check.
            if (pabCount != active.Count % 2)
                throw new InvalidDataException("Движок вернул неверное число автоматических BYE.");
            return round;
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { /* temporary files can be locked */ }
        }
    }

    // Fixed-width TRF26. Name is informational; IDs and prior results control pairings.
    public static string CreateTrf(Tournament tournament, int upcomingRound)
    {
        if (upcomingRound < 1 || upcomingRound > tournament.SwissRounds) throw new ArgumentOutOfRangeException(nameof(upcomingRound));
        var sb = new StringBuilder();
        sb.AppendLine("012 " + ToAscii(tournament.Name));
        sb.AppendLine("062 " + tournament.Players.Count);
        sb.AppendLine("092 Individual: Swiss-System");
        sb.AppendLine("142 " + tournament.SwissRounds);
        sb.AppendLine("152 " + (tournament.InitialWhite ? "W" : "B"));
        sb.AppendLine("XXR " + tournament.SwissRounds);
        sb.AppendLine("XXC " + (tournament.InitialWhite ? "white1" : "black1"));

        var scores = StandingsService.Calculate(tournament).ToDictionary(s => s.Player.Number, s => s.Score);
        foreach (var p in tournament.Players.OrderBy(p => p.Number))
        {
            var content = Enumerable.Repeat(' ', 91 + upcomingRound * 10).ToArray();
            static void Put(char[] buffer, int column, string value, int max)
            {
                var target = value.Length <= max ? value : value[..max];
                for (int x = 0; x < target.Length; x++) buffer[column - 1 + x] = target[x];
            }
            Put(content, 1, "001", 3);
            Put(content, 5, p.Number.ToString("D4"), 4);
            Put(content, 15, ToAscii(p.FullName), 33);
            Put(content, 49, p.Rating > 0 ? p.Rating.ToString("D4") : "    ", 4);
            Put(content, 54, "RUS", 3);
            var planned = tournament.PlannedByes.FirstOrDefault(b => b.PlayerId == p.Number && b.RoundNumber == upcomingRound);
            decimal score = scores[p.Number];
            if (planned is not null && !p.Withdrawn) score += StandingsService.Points(planned.Kind);
            Put(content, 81, score.ToString("0.0", CultureInfo.InvariantCulture).PadLeft(4), 4);
            Put(content, 86, p.Number.ToString().PadLeft(4), 4);

            for (int r = 1; r <= upcomingRound; r++)
            {
                var round = tournament.Rounds.FirstOrDefault(x => x.Number == r);
                var game = round?.Games.FirstOrDefault(x => x.WhiteId == p.Number || x.BlackId == p.Number);
                int opponent = 0;
                char color = '-';
                char result = ' ';
                if (game is not null)
                {
                    bool white = game.WhiteId == p.Number;
                    opponent = white ? game.BlackId : game.WhiteId;
                    color = white ? 'w' : 'b';
                    result = game.Result switch
                    {
                        GameResult.WhiteWin => white ? '1' : '0',
                        GameResult.BlackWin => white ? '0' : '1',
                        GameResult.Draw => '=',
                        GameResult.WhiteForfeitWin => white ? '+' : '-',
                        GameResult.BlackForfeitWin => white ? '-' : '+',
                        GameResult.DoubleForfeit => '-',
                        _ => ' '
                    };
                }
                else
                {
                    var bye = round?.Byes.FirstOrDefault(x => x.PlayerId == p.Number);
                    if (r == upcomingRound && round is null)
                    {
                        if (p.Withdrawn) bye = new RoundBye { PlayerId = p.Number, Kind = ByeKind.RequestedZero };
                        else if (planned is not null) bye = new RoundBye { PlayerId = p.Number, Kind = planned.Kind };
                    }
                    if (bye is not null)
                        result = bye.Kind switch
                        {
                            ByeKind.PairingAllocated => 'U',
                            ByeKind.RequestedHalf => 'H',
                            _ => 'Z'
                        };
                }
                var pos = 92 + (r - 1) * 10;
                if (r < upcomingRound || result != ' ')
                {
                    Put(content, pos, opponent.ToString("D4"), 4);
                    Put(content, pos + 5, color.ToString(), 1);
                    Put(content, pos + 7, result.ToString(), 1);
                }
            }
            sb.AppendLine(new string(content).TrimEnd());
        }
        return sb.ToString();
    }

    private static string ToAscii(string input)
    {
        const string ru = "абвгдежзийклмнопрстуфхцчшщъыьэюя";
        string[] latin = { "a","b","v","g","d","e","zh","z","i","y","k","l","m","n","o","p","r","s","t","u","f","kh","ts","ch","sh","shch","","y","","e","yu","ya" };
        var sb = new StringBuilder();
        foreach (char ch in input)
        {
            int ix = ru.IndexOf(char.ToLowerInvariant(ch));
            string letter = ix >= 0 ? latin[ix] : ch is >= ' ' and <= '~' ? ch.ToString() : " ";
            if (char.IsUpper(ch) && letter.Length > 0) letter = char.ToUpperInvariant(letter[0]) + letter[1..];
            sb.Append(letter);
        }
        return sb.ToString();
    }
}
