using System.Text.Json;
using ChessDraw.Models;

namespace ChessDraw.Services;

public sealed class TournamentStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string path;
    public string DataPath => path;

    public TournamentStore(string? path = null)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChessDraw");
        this.path = path ?? Path.Combine(folder, "tournaments.json");
    }

    public List<Tournament> Load()
    {
        if (!File.Exists(path)) return new();
        try { return Read(path); }
        catch (Exception first)
        {
            if (File.Exists(path + ".bak"))
            {
                try
                {
                    var recovered = Read(path + ".bak");
                    // Restore the healthy snapshot before the next atomic save replaces .bak.
                    File.Copy(path + ".bak", path, overwrite: true);
                    return recovered;
                }
                catch { /* report original failure below */ }
            }
            throw new InvalidDataException("Не удалось открыть базу турниров. Архивный файл также повреждён.\n" + first.Message);
        }
    }

    private static List<Tournament> Read(string file)
    {
        var tournaments = JsonSerializer.Deserialize<List<Tournament>>(File.ReadAllText(file), Options)
            ?? throw new InvalidDataException("Пустой файл базы турниров.");
        Validate(tournaments);
        return tournaments;
    }

    private static void Validate(IReadOnlyList<Tournament> tournaments)
    {
        var ids = new HashSet<Guid>();
        foreach (var tournament in tournaments)
        {
            if (tournament is null || !ids.Add(tournament.Id)) throw new InvalidDataException("Повреждён список турниров.");
            TournamentValidator.Validate(tournament);
        }
    }

    public void Save(IReadOnlyList<Tournament> tournaments)
    {
        Validate(tournaments);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(tournaments, Options));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
