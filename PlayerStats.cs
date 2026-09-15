using CounterStrikeSharp.API.Modules.Utils;

namespace CSAntiCheat;

public class PlayerStats
{
    // ============================================================
    // IDENTITY
    // ============================================================
    public ulong SteamId { get; set; }
    public string Name { get; set; } = "";

    // ============================================================
    // SUSPICION / DECAY
    // ============================================================
    public int SuspicionScore { get; set; }
    public DateTime LastDecay { get; set; } = DateTime.UtcNow;
    public DateTime LastFlag { get; set; } = DateTime.MinValue;

    // ============================================================
    // ANGLE TRACKING
    // ============================================================
    public QAngle? LastAngles { get; set; }

    // ============================================================
    // VIOLATION COUNTERS
    // ============================================================
    public int SpeedViolations { get; set; }
    public int SnapViolations { get; set; }
    public int BhopViolations { get; set; }
    public int PacketViolations { get; set; }

    // ============================================================
    // KILL TRACKING (HS-ratio)
    // ============================================================
    public int TotalKills { get; set; }
    public int Headshots { get; set; }

    // ============================================================
    // CONSECUTIVE TICK FILTERS
    // ============================================================
    public int ConsecutiveSpeedTicks { get; set; }
    public int ConsecutiveSnapTicks { get; set; }
    public int ConsecutiveBhopTicks { get; set; }
    public int ConsecutiveHugeSpeed { get; set; }

    // ============================================================
    // DMA / SUBPIXEL
    // ============================================================
    public int ConsecutiveIntegerAimTicks { get; set; }
    public float LastAimDelta { get; set; }

    // ============================================================
    // JERK / BEZIER SMOOTHING
    // ============================================================
    public float LastAcceleration { get; set; }
    public int ConsecutiveSameAccelerationTicks { get; set; }
    public float LastJerk { get; set; }
    public int ConsecutiveSmoothJerkTicks { get; set; }

    // ============================================================
    // AIM WINDOW
    // ============================================================
    public float AimWindowDelta { get; set; }
    public int AimWindowTicks { get; set; }

    // ============================================================
    // SHOT TIMING
    // ============================================================
    public DateTime LastShotTime { get; set; } = DateTime.MinValue;
    public List<double> ShotIntervals { get; set; } = new();

    // ============================================================
    // AUTO-STRAFE SYNC
    // ============================================================
    public float LastYawDelta { get; set; }
    public ulong LastButtons { get; set; }
    public int ConsecutiveStrafeSyncTicks { get; set; }

    // ============================================================
    // PREFIRE
    // ============================================================
    public QAngle? LastShotAngles { get; set; }
    public HashSet<ulong> EnemiesInConeAtShot { get; set; } = new();
    public int PrefireEvents { get; set; }
    public DateTime LastPrefireFlagTime { get; set; } = DateTime.MinValue;

    // ============================================================
    // WALLBANG PROXY
    // ============================================================
    public int HitsTotal { get; set; }
    public int HitsBlind { get; set; }

    // ============================================================
    // HvH
    // ============================================================
    public int ConsecutiveInvalidPitchTicks { get; set; }
    public int ConsecutiveSpinbotTicks { get; set; }

    // ============================================================
    // PACKET / TICKBASE
    // ============================================================
    public DateTime LastPacketWindow { get; set; } = DateTime.UtcNow;
    public int CmdCountInWindow { get; set; }
    public int JumpCountInWindow { get; set; }

    // ============================================================
    // AI SCORING (diversity bonus)
    // ============================================================
    public Dictionary<string, int> ReasonCounts { get; set; } = new();
}
