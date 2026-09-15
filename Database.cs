using System.Text.Json;

namespace CSAntiCheat;

// ============================================================
// RECORDS
// ============================================================
public class SuspicionRecord
{
    public ulong SteamId { get; set; }
    public string PlayerName { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Details { get; set; } = "";
    public string ServerName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class BanRecord
{
    public ulong SteamId { get; set; }
    public string PlayerName { get; set; } = "";
    public string Reason { get; set; } = "";
    public string AdminName { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string ServerName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

// ============================================================
// DATABASE
// ============================================================
/// <summary>
/// Simple JSON storage. Thread-safe, with auto-save.
/// Files: anticheat_suspicions.json and anticheat_bans.json in plugin folder.
/// </summary>
public class Database
{
    private readonly string _suspPath;
    private readonly string _bansPath;
    private readonly object _lock = new();

    private List<SuspicionRecord> _suspicions = new();
    private List<BanRecord> _bans = new();

    private const int MAX_RECORDS = 50_000;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public Database(string pluginFolder)
    {
        _suspPath = Path.Combine(pluginFolder, "anticheat_suspicions.json");
        _bansPath = Path.Combine(pluginFolder, "anticheat_bans.json");
    }

    public void Initialize()
    {
        lock (_lock)
        {
            _suspicions = LoadList<SuspicionRecord>(_suspPath);
            _bans = LoadList<BanRecord>(_bansPath);
            Save();
        }
    }

    // ============================================================
    // SUSPICIONS
    // ============================================================
    public void AddSuspicion(ulong steamId, string name, string reason, string details, string server)
    {
        lock (_lock)
        {
            _suspicions.Add(new SuspicionRecord
            {
                SteamId = steamId,
                PlayerName = name,
                Reason = reason,
                Details = details,
                ServerName = server,
                CreatedAt = DateTime.UtcNow
            });

            // Keep only latest MAX_RECORDS entries
            if (_suspicions.Count > MAX_RECORDS)
                _suspicions.RemoveRange(0, _suspicions.Count - MAX_RECORDS);

            Save();
        }
    }

    public List<SuspicionRecord> GetRecentSuspicions(int limit)
    {
        lock (_lock)
        {
            return _suspicions
                .OrderByDescending(s => s.CreatedAt)
                .Take(limit)
                .Select(s => new SuspicionRecord
                {
                    SteamId = s.SteamId,
                    PlayerName = s.PlayerName,
                    Reason = s.Reason,
                    Details = s.Details,
                    ServerName = s.ServerName,
                    CreatedAt = s.CreatedAt.ToLocalTime()
                })
                .ToList();
        }
    }

    // ============================================================
    // BANS
    // ============================================================
    public void AddBan(ulong steamId, string name, string reason, string admin, int duration, string server)
    {
        lock (_lock)
        {
            _bans.Add(new BanRecord
            {
                SteamId = steamId,
                PlayerName = name,
                Reason = reason,
                AdminName = admin,
                DurationMinutes = duration,
                ServerName = server,
                CreatedAt = DateTime.UtcNow
            });

            if (_bans.Count > MAX_RECORDS)
                _bans.RemoveRange(0, _bans.Count - MAX_RECORDS);

            Save();
        }
    }

    public List<BanRecord> GetRecentBans(int limit)
    {
        lock (_lock)
        {
            return _bans
                .OrderByDescending(b => b.CreatedAt)
                .Take(limit)
                .Select(b => new BanRecord
                {
                    SteamId = b.SteamId,
                    PlayerName = b.PlayerName,
                    Reason = b.Reason,
                    AdminName = b.AdminName,
                    DurationMinutes = b.DurationMinutes,
                    ServerName = b.ServerName,
                    CreatedAt = b.CreatedAt.ToLocalTime()
                })
                .ToList();
        }
    }

    // ============================================================
    // INTERNALS
    // ============================================================
    private void Save()
    {
        try
        {
            File.WriteAllText(_suspPath, JsonSerializer.Serialize(_suspicions, JsonOpts));
            File.WriteAllText(_bansPath, JsonSerializer.Serialize(_bans, JsonOpts));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AntiCheat DB] Save error: {ex.Message}");
        }
    }

    private static List<T> LoadList<T>(string path)
    {
        if (!File.Exists(path)) return new List<T>();
        try
        {
            var txt = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<T>>(txt) ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }
}
