using ChessDraw.Models;

namespace ChessDraw.Services;

public static class TournamentValidator
{
    public static void Validate(Tournament t)
    {
        void Require(bool valid) { if (!valid) throw new InvalidDataException("Нарушена структура турнира «" + t.Name + "»."); }
        Require(t.Id != Guid.Empty && !string.IsNullOrWhiteSpace(t.Name) && Enum.IsDefined(t.System));
        Require(t.SwissRounds is >= 1 and <= 99 && t.Players is not null && t.Rounds is not null && t.PlannedByes is not null);
        var ids = new HashSet<int>();
        foreach (var p in t.Players!)
            Require(p is not null && p.Number is >= 1 and <= 9999 && ids.Add(p.Number) &&
                !string.IsNullOrWhiteSpace(p.FullName) && p.Rating is >= 0 and <= 3500);
        Require(t.Rounds!.Count <= t.TotalRounds);
        int next = 1;
        foreach (var r in t.Rounds.OrderBy(r => r.Number))
        {
            Require(r.Number == next++ && r.Games is not null && r.Byes is not null);
            var seen = new HashSet<int>();
            foreach (var g in r.Games!)
                Require(ids.Contains(g.WhiteId) && ids.Contains(g.BlackId) && seen.Add(g.WhiteId) && seen.Add(g.BlackId) && Enum.IsDefined(g.Result));
            foreach (var b in r.Byes!)
                Require(ids.Contains(b.PlayerId) && seen.Add(b.PlayerId) && Enum.IsDefined(b.Kind) &&
                    (t.System == TournamentSystem.Swiss ? b.Kind != ByeKind.RoundRobinRest : b.Kind == ByeKind.RoundRobinRest));
            Require(seen.SetEquals(ids));
            if (t.System == TournamentSystem.Swiss && r.Number < t.Rounds.Count) Require(r.Completed);
        }
        var planned = new HashSet<(int, int)>();
        foreach (var b in t.PlannedByes!)
            Require(ids.Contains(b.PlayerId) && b.RoundNumber >= 1 && b.RoundNumber <= t.SwissRounds &&
                b.Kind is ByeKind.RequestedHalf or ByeKind.RequestedZero && planned.Add((b.RoundNumber, b.PlayerId)));
    }
}
