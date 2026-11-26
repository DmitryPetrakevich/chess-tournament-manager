namespace ChessDraw.Models;

public enum TournamentSystem { Swiss, RoundRobin }
public enum GameResult { Pending, WhiteWin, BlackWin, Draw, WhiteForfeitWin, BlackForfeitWin, DoubleForfeit }
public enum ByeKind { PairingAllocated, RequestedZero, RequestedHalf, RoundRobinRest }

public sealed class Tournament
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Новый турнир";
    public TournamentSystem System { get; set; } = TournamentSystem.Swiss;
    public int SwissRounds { get; set; } = 5;
    public bool DoubleRound { get; set; }
    public bool InitialWhite { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<Player> Players { get; set; } = new();
    public List<ChessRound> Rounds { get; set; } = new();
    public List<PlannedBye> PlannedByes { get; set; } = new();
    public int TotalRounds => System == TournamentSystem.Swiss ? SwissRounds :
        (Players.Count % 2 == 0 ? Math.Max(0, Players.Count - 1) : Players.Count) * (DoubleRound ? 2 : 1);
    public bool Started => Rounds.Count > 0;
}

public sealed class Player
{
    public int Number { get; set; }
    public string FullName { get; set; } = "";
    public const int DefaultRating = 1000;
    public int Rating { get; set; } = DefaultRating;
    public bool Withdrawn { get; set; }
    public override string ToString() => $"{Number}. {FullName}";
}

public sealed class ChessRound
{
    public int Number { get; set; }
    public List<Game> Games { get; set; } = new();
    public List<RoundBye> Byes { get; set; } = new();
    public bool Completed => Games.All(g => g.Result != GameResult.Pending);
}

public sealed class Game
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Board { get; set; }
    public int WhiteId { get; set; }
    public int BlackId { get; set; }
    public GameResult Result { get; set; }
}

public sealed class RoundBye
{
    public int PlayerId { get; set; }
    public ByeKind Kind { get; set; }
}

public sealed class PlannedBye
{
    public int RoundNumber { get; set; }
    public int PlayerId { get; set; }
    public ByeKind Kind { get; set; }
}
