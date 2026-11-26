using ChessDraw.Models;

namespace ChessDraw.Services;

public sealed class Standing
{
    public int Place { get; set; }
    public required Player Player { get; init; }
    public decimal Score { get; set; }
    public decimal Buchholz { get; set; }
    public decimal CutBuchholz { get; set; }
    public decimal Berger { get; set; }
    public int Wins { get; set; }
}

public static class StandingsService
{
    public static decimal Points(ByeKind kind) => kind switch
    {
        ByeKind.PairingAllocated => 1m,
        ByeKind.RequestedHalf => .5m,
        _ => 0m
    };

    public static (decimal White, decimal Black) Points(GameResult result) => result switch
    {
        GameResult.WhiteWin or GameResult.WhiteForfeitWin => (1m, 0m),
        GameResult.BlackWin or GameResult.BlackForfeitWin => (0m, 1m),
        GameResult.Draw => (.5m, .5m),
        _ => (0m, 0m)
    };

    /// <summary>
    /// Scores strictly before the chosen round. Includes automatic pairing-allocated
    /// BYEs (+1) and technical results from earlier rounds, but not pending games.
    /// </summary>
    public static Dictionary<int, decimal> ScoresBeforeRound(Tournament tournament, int roundNumber)
    {
        var totals = tournament.Players.ToDictionary(p => p.Number, _ => 0m);
        foreach (var round in tournament.Rounds.Where(r => r.Number < roundNumber))
        {
            foreach (var game in round.Games)
            {
                var (white, black) = Points(game.Result);
                totals[game.WhiteId] += white;
                totals[game.BlackId] += black;
            }
            foreach (var bye in round.Byes)
                totals[bye.PlayerId] += Points(bye.Kind);
        }
        return totals;
    }

    private sealed record Contribution(decimal OpponentScore, decimal OwnPoints, bool IsVur);

    public static List<Standing> Calculate(Tournament tournament)
    {
        var rows = tournament.Players.ToDictionary(p => p.Number,
            p => new Standing { Player = p });
        var rounds = tournament.Rounds.OrderBy(r => r.Number).ToList();
        var maxRound = 0;

        foreach (var round in rounds)
        {
            foreach (var game in round.Games.Where(g => g.Result != GameResult.Pending))
            {
                maxRound = Math.Max(maxRound, round.Number);
                var (white, black) = Points(game.Result);
                rows[game.WhiteId].Score += white;
                rows[game.BlackId].Score += black;
                if (game.Result == GameResult.WhiteWin) rows[game.WhiteId].Wins++;
                if (game.Result == GameResult.BlackWin) rows[game.BlackId].Wins++;
            }
            foreach (var bye in round.Byes)
            {
                if (bye.Kind != ByeKind.RoundRobinRest) maxRound = Math.Max(maxRound, round.Number);
                rows[bye.PlayerId].Score += Points(bye.Kind);
            }
        }

        // 2026 FIDE regulations, 16.3: adjusted scores for the opponents' tie-breaks.
        var adjusted = rows.ToDictionary(x => x.Key, x => x.Value.Score);
        if (tournament.System == TournamentSystem.Swiss)
        {
            foreach (var player in tournament.Players)
            {
                foreach (var round in rounds)
                {
                    if (round.Number > maxRound) break;
                    var bye = round.Byes.FirstOrDefault(b => b.PlayerId == player.Number);
                    if (bye is null || bye.Kind is not (ByeKind.RequestedHalf or ByeKind.RequestedZero)) continue;
                    bool laterNonVoluntary = rounds.Any(next => next.Number > round.Number && next.Number <= maxRound &&
                        (next.Games.Any(g => (g.WhiteId == player.Number || g.BlackId == player.Number) &&
                            (g.Result is GameResult.WhiteWin or GameResult.BlackWin or GameResult.Draw ||
                             (g.Result == GameResult.WhiteForfeitWin && g.WhiteId == player.Number) ||
                             (g.Result == GameResult.BlackForfeitWin && g.BlackId == player.Number))) ||
                         next.Byes.Any(b => b.PlayerId == player.Number && b.Kind == ByeKind.PairingAllocated)));
                    if (!laterNonVoluntary) adjusted[player.Number] += .5m - Points(bye.Kind);
                }
            }
        }

        foreach (var standing in rows.Values)
        {
            var contrib = new List<Contribution>();
            foreach (var round in rounds)
            {
                if (round.Number > maxRound) break;
                var game = round.Games.FirstOrDefault(g => g.WhiteId == standing.Player.Number || g.BlackId == standing.Player.Number);
                if (game != null && game.Result != GameResult.Pending)
                {
                    bool white = game.WhiteId == standing.Player.Number;
                    int opponent = white ? game.BlackId : game.WhiteId;
                    decimal earned = white ? Points(game.Result).White : Points(game.Result).Black;
                    bool unplayed = game.Result is GameResult.WhiteForfeitWin or GameResult.BlackForfeitWin or GameResult.DoubleForfeit;
                    decimal oppScore = unplayed && tournament.System == TournamentSystem.Swiss
                        ? Math.Min(standing.Score, adjusted[opponent])
                        : adjusted[opponent];
                    bool vur = tournament.System == TournamentSystem.Swiss && unplayed && earned == 0m;
                    contrib.Add(new Contribution(oppScore, earned, vur));
                    continue;
                }
                var bye = round.Byes.FirstOrDefault(b => b.PlayerId == standing.Player.Number);
                if (bye is not null)
                {
                    if (bye.Kind == ByeKind.RoundRobinRest) continue;
                    var oppScore = tournament.System == TournamentSystem.Swiss
                        ? Math.Min(standing.Score, tournament.SwissRounds * .5m)
                        : 0m;
                    contrib.Add(new Contribution(oppScore, Points(bye.Kind),
                        bye.Kind is ByeKind.RequestedHalf or ByeKind.RequestedZero));
                }
            }
            standing.Buchholz = tournament.System == TournamentSystem.Swiss ? contrib.Sum(c => c.OpponentScore) : 0m;
            standing.Berger = contrib.Sum(c => c.OpponentScore * c.OwnPoints);
            var cut = contrib.OrderBy(c => c.OpponentScore).FirstOrDefault();
            // 2026 FIDE article 16.5: for Cut-1 remove the lowest VUR contribution,
            // unless it is lower than the least significant contribution.
            var lowestVur = contrib.Where(c => c.IsVur).OrderBy(c => c.OpponentScore).FirstOrDefault();
            if (lowestVur is not null && cut is not null && lowestVur.OpponentScore >= cut.OpponentScore)
                cut = lowestVur;
            standing.CutBuchholz = tournament.System == TournamentSystem.Swiss ? standing.Buchholz - (cut?.OpponentScore ?? 0) : 0m;
        }

        IOrderedEnumerable<Standing> sorted = tournament.System == TournamentSystem.Swiss
            ? rows.Values.OrderByDescending(s => s.Score)
                .ThenByDescending(s => s.CutBuchholz).ThenByDescending(s => s.Buchholz)
                .ThenByDescending(s => s.Berger).ThenByDescending(s => s.Wins)
            : rows.Values.OrderByDescending(s => s.Score).ThenByDescending(s => s.Berger)
                .ThenByDescending(s => s.Wins);
        var list = sorted.ThenBy(s => s.Player.Number).ToList();
        for (int i = 0; i < list.Count; i++) list[i].Place = i + 1;
        return list;
    }
}
