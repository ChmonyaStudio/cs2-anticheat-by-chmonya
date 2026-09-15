namespace CSAntiCheat;

public class AntiCheatConfig
{
    // ============================================================
    // GENERAL
    // ============================================================
    public string WebhookUrl { get; set; } = "https://discord.com/api/webhooks/YOUR_WEBHOOK_HERE";
    public string WebhookUsername { get; set; } = "Server | AntiCheat";
    public string ServerName { get; set; } = "My CS2 Server";
    public bool WebhookEnabled { get; set; } = true;

    // ============================================================
    // WHITELIST / ADMINS
    // ============================================================
    public List<string> Whitelist { get; set; } = new() { "76561198737716015" };
    public List<string> SuperAdmins { get; set; } = new() { "76561198737716015" };

    // ============================================================
    // MODULE TOGGLES
    // ============================================================
    public bool SpeedCheckEnabled { get; set; } = true;
    public bool AimCheckEnabled { get; set; } = true;
    public bool BhopCheckEnabled { get; set; } = false;
    public bool PacketCheckEnabled { get; set; } = true;
    public bool GridSnapEnabled { get; set; } = true;
    public bool JerkAnalysisEnabled { get; set; } = true;
    public bool StrafeSyncCheckEnabled { get; set; } = true;
    public bool PrefireCheckEnabled { get; set; } = true;
    public bool WallbangProxyCheckEnabled { get; set; } = true;
    public bool PitchAntiAimCheckEnabled { get; set; } = true;
    public bool SpinbotCheckEnabled { get; set; } = true;

    // ============================================================
    // SPEED
    // ============================================================
    public float MaxSpeed { get; set; } = 6.0f;
    public int SpeedConsecutiveTicks { get; set; } = 8;
    public float HugeSpeedThreshold { get; set; } = 14.0f;
    public int HugeSpeedConsecutiveTicks { get; set; } = 3;

    // ============================================================
    // AIM
    // ============================================================
    public float MaxAngleDeltaPerTick { get; set; } = 55f;
    public int SnapConsecutiveTicks { get; set; } = 2;
    public float AimWindowMaxAvg { get; set; } = 20f;
    public int AimWindowSize { get; set; } = 8;

    // ============================================================
    // JERK / AI AIMBOT
    // ============================================================
    public int JerkConsecutiveTicks { get; set; } = 4;
    public float JerkMinMagnitude { get; set; } = 0.3f;
    public float JerkSmallTolerance { get; set; } = 0.4f;
    public int SmoothJerkConsecutiveTicks { get; set; } = 6;

    // ============================================================
    // SUBPIXEL / DMA
    // ============================================================
    public int SubpixelConsecutiveTicks { get; set; } = 15;
    public float SubpixelMinDelta { get; set; } = 1.5f;

    // ============================================================
    // GRID SNAP / TRIGGERBOT
    // ============================================================
    public float GridSnapTolerance { get; set; } = 0.5f;
    public int GridSnapMinShots { get; set; } = 4;
    public double StdDevTrigger { get; set; } = 15.0;

    // ============================================================
    // PREFIRE
    // ============================================================
    public int PrefireWindowMs { get; set; } = 150;
    public float PrefireConeDegrees { get; set; } = 20f;
    public float PrefireMaxDistance { get; set; } = 3000f;
    public float PrefireShotDirTolerance { get; set; } = 30f;
    public int PrefireMinEvents { get; set; } = 2;
    public int PrefireThrottleSeconds { get; set; } = 15;
    public int PrefireSingleTapWindowMs { get; set; } = 500;

    // ============================================================
    // WALLBANG PROXY
    // ============================================================
    public int WallbangProxyMinHits { get; set; } = 30;
    public float WallbangProxyRatioTrigger { get; set; } = 0.30f;
    public float WallbangProxyConeDegrees { get; set; } = 25f;

    // ============================================================
    // AUTO-STRAFE SYNC
    // ============================================================
    public int StrafeSyncConsecutiveTicks { get; set; } = 12;
    public float StrafeSyncMinYawDelta { get; set; } = 1.0f;
    public float StrafeSyncMinSpeed { get; set; } = 3.0f;

    // ============================================================
    // HvH — PITCH ANTI-AIM
    // ============================================================
    public float PitchMinLimit { get; set; } = -89f;
    public float PitchMaxLimit { get; set; } = 89f;
    public int PitchBanConsecutiveTicks { get; set; } = 2;

    // ============================================================
    // HvH — SPINBOT / JITTER
    // ============================================================
    public float SpinbotMinYawDelta { get; set; } = 140f;
    public int SpinbotConsecutiveTicks { get; set; } = 3;

    // ============================================================
    // BHOP
    // ============================================================
    public float BhopMinSpeed { get; set; } = 8.0f;
    public int BhopConsecutiveTicks { get; set; } = 6;

    // ============================================================
    // PACKET / TICKBASE
    // ============================================================
    public int PacketMaxCmdsPerSecond { get; set; } = 140;
    public int PacketMaxJumpPerSecond { get; set; } = 6;

    // ============================================================
    // AI SCORING
    // ============================================================
    public bool AiScoringEnabled { get; set; } = true;
    public float AiDiversityBonus { get; set; } = 1.6f;

    // ============================================================
    // GENERAL BAN LOGIC
    // ============================================================
    public int SuspicionToBan { get; set; } = 25;
    public int SuspicionDecaySeconds { get; set; } = 90;
    public bool AutoBanEnabled { get; set; } = true;
    public int BanDurationMinutes { get; set; } = 0;
    public int MinPlayersForChecks { get; set; } = 1;
    public float HeadshotRatioToFlag { get; set; } = 0.90f;
    public int MinKillsToCheckHS { get; set; } = 12;

    // ============================================================
    // DEBUG
    // ============================================================
    public bool DebugLog { get; set; } = true;
    public int DebugIntervalSeconds { get; set; } = 2;

    // ============================================================
    // VERIFICATION RULES (shown to admins)
    // ============================================================
    public VerificationRules VerificationRules { get; set; } = new();
}

public class VerificationRules
{
    public string Title { get; set; } = "🔍 Verification rules";
    public List<RuleItem> Rules { get; set; } = new()
    {
        new() { Num = "1",  Text = "Reports are submitted via !report." },
        new() { Num = "2",  Text = "Admin collects match logs and Prefetch logs." },
        new() { Num = "3",  Text = "Checklist: match duration >5 min, at least one lost round." },
        new() { Num = "4",  Text = "Anomalies: speed >7.2 m/s, rotation >360° per frame." },
        new() { Num = "5",  Text = "Allowed software: Steam Input, NVIDIA/AMD drivers, MSI Afterburner." },
        new() { Num = "6",  Text = "Forbidden: Aim, Wallhack, ESP, Bhop, Recoil Control, Anti-Aim." },
        new() { Num = "7",  Text = "Deleting Prefetch logs — ban up to 72 hours." },
        new() { Num = "8",  Text = "Punishments: tier 1 — 2-12 h; tier 2 — 24-72 h; tier 3 — permanent." },
        new() { Num = "9",  Text = "Appeal within 1 hour after verdict with evidence." },
        new() { Num = "10", Text = "Final decision made by anti-cheat committee." }
    };
}

public class RuleItem
{
    public string Num { get; set; } = "";
    public string Text { get; set; } = "";
}
