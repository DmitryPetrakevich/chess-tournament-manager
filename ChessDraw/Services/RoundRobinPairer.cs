using ChessDraw.Models;

namespace ChessDraw.Services;

/// <summary>
/// FIDE Berger rotation: last slot is fixed, others rotate with stride n/2 - 1;
/// the pivot game alternates colour, all other boards give White to the lower position.
/// The second cycle mirrors the first with colours reversed.
/// </summary>
public static class RoundRobinPairer
{
    public static List<ChessRound> Generate(Tournament tournament)
    {
        if (tournament.Players.Count < 2)
            throw new InvalidOperationException("Требуется минимум два участника.");

        var players = tournament.Players.OrderBy(p => p.Number).Select(p => (int?)p.Number).ToList();
        if (players.Count % 2 == 1) players.Add(null);
        int n = players.Count;
        int stride = n / 2 - 1;
        var rounds = new List<ChessRound>();
        for (int r = 0; r < n - 1; r++)
        {
            // Rotating slots 0..n-2, pivot slot n-1 remains fixed.
            var position = Enumerable.Range(0, n - 1)
                .Select(j => players[(j - r * stride % (n - 1) + n - 1) % (n - 1)])
                .Append(players[n - 1]).ToArray();

            var round = new ChessRound { Number = r + 1 };
            for (int i = 0; i < n / 2; i++)
            {
                var white = position[i];
                var black = position[n - 1 - i];
                if (i == 0 && r % 2 == 1) (white, black) = (black, white);

                if (!white.HasValue || !black.HasValue)
                {
                    round.Byes.Add(new RoundBye
                    {
                        PlayerId = (white ?? black)!.Value,
                        Kind = ByeKind.RoundRobinRest
                    });
                    continue;
                }
                round.Games.Add(new Game
                {
                    Board = round.Games.Count + 1,
                    WhiteId = white.Value,
                    BlackId = black.Value
                });
            }
            rounds.Add(round);
        }
        if (tournament.DoubleRound)
        {
            int offset = rounds.Count;
            foreach (var round in rounds.ToArray())
            {
                rounds.Add(new ChessRound
                {
                    Number = round.Number + offset,
                    Games = round.Games.Select(g => new Game
                    {
                        Board = g.Board, WhiteId = g.BlackId, BlackId = g.WhiteId
                    }).ToList(),
                    Byes = round.Byes.Select(b => new RoundBye
                    {
                        PlayerId = b.PlayerId, Kind = b.Kind
                    }).ToList()
                });
            }
        }
        return rounds;
    }
}
