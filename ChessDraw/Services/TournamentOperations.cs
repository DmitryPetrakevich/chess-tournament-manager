using ChessDraw.Exports;
using ChessDraw.Models;
using System.Text.Json;

namespace ChessDraw.Services;

public static class TournamentOperations
{
    // Work on a snapshot: a missing engine or an impossible pairing must not change IDs.
    public static ChessRound PairNextSwiss(Tournament tournament)
    {
        var snapshot = JsonSerializer.Deserialize<Tournament>(JsonSerializer.Serialize(tournament))!;
        if (!snapshot.Started)
        {
            var names = StringComparer.Create(new System.Globalization.CultureInfo("ru-RU"), true);
            var ordered = snapshot.Players.OrderByDescending(p => p.Rating).ThenBy(p => p.FullName, names).ToList();
            var map = ordered.Select((p, i) => (p.Number, New: i + 1)).ToDictionary(p => p.Number, p => p.New);
            foreach (var bye in snapshot.PlannedByes) bye.PlayerId = map[bye.PlayerId];
            foreach (var player in snapshot.Players) player.Number = map[player.Number];
        }
        var round = SwissPairer.Pair(snapshot);
        foreach (var player in tournament.Players)
            player.Number = snapshot.Players[tournament.Players.IndexOf(player)].Number;
        tournament.PlannedByes = snapshot.PlannedByes;
        return round;
    }

    public static List<Player> ParsePlayers(string text, IReadOnlyList<Player> existing)
    {
        var names = existing.Select(p => p.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int number = existing.Count == 0 ? 0 : existing.Max(p => p.Number);
        var added = new List<Player>();
        foreach (string line in text.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            var fields = line.Trim().Split(new[] { ';', '\t' });
            string name = fields[0].Trim();
            int rating = Player.DefaultRating;
            if (fields.Length > 2 || name.Length < 3 || (fields.Length == 2 && fields[1].Trim().Length > 0 &&
                (!int.TryParse(fields[1].Trim(), out rating) || rating < 0 || rating > 3500)))
                throw new InvalidOperationException("Неверная строка: " + line + "\nФормат: ФИО;рейтинг (0–3500).");
            if (!names.Add(name)) throw new InvalidOperationException("Повтор ФИО: " + name);
            if (++number > 9999) throw new InvalidOperationException("Максимальный стартовый номер — 9999.");
            added.Add(new Player { Number = number, FullName = name, Rating = rating == 0 ? Player.DefaultRating : rating });
        }
        if (added.Count == 0) throw new InvalidOperationException("Введите хотя бы одного участника.");
        return added;
    }

    public static string RoundCell(ChessRound round, int playerId)
    {
        var game = round.Games.FirstOrDefault(g => g.WhiteId == playerId || g.BlackId == playerId);
        if (game is null)
        {
            var bye = round.Byes.FirstOrDefault(b => b.PlayerId == playerId);
            return bye is null ? "—" : ExcelExport.ByeLabel(bye.Kind);
        }
        bool white = game.WhiteId == playerId;
        var score = white ? StandingsService.Points(game.Result).White : StandingsService.Points(game.Result).Black;
        string result = game.Result == GameResult.Pending ? "?" : score == .5m ? "½" : score.ToString("0");
        string technical = game.Result is GameResult.WhiteForfeitWin or GameResult.BlackForfeitWin or GameResult.DoubleForfeit ? "т" : "";
        return $"{(white ? game.BlackId : game.WhiteId)}{(white ? "б" : "ч")} / {result}{technical}";
    }
}
