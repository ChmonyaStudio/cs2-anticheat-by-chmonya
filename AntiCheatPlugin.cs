using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System.Reflection;
using System.Text.Json;

namespace CSAntiCheat;

public class AntiCheatPlugin : BasePlugin
{
    public override string ModuleName => "CS2 AntiCheat";
    public override string ModuleVersion => "2.4.2";
    public override string ModuleAuthor => "Chmonya";
    public override string ModuleDescription => "Anti-cheat: webhook, DB, whitelist, DMA/AI/HvH/GridSnap/Prefire detection";

    public static AntiCheatPlugin Instance { get; private set; } = null!;
    public AntiCheatConfig Config { get; private set; } = new();
    public Database Db { get; private set; } = null!;
    public WebhookLogger Webhook { get; private set; } = null!;

    private string _configPath = "";
    private readonly Dictionary<ulong, PlayerStats> _stats = new();
    private readonly Dictionary<ulong, (string reason, DateTime until, int attempts)> _pendingKicks = new();

    // Atomic reference swap — thread-safe without lock
    private volatile HashSet<ulong> _whitelistCache = new();

    private DateTime _lastDebug = DateTime.MinValue;

    // Reflection cache for EyeAngles
    private static PropertyInfo? _eyeAnglesProp;
    private static bool _eyeAnglesSearched;
    private static bool _eyeAnglesWarned;

    // Reflection cache for MovementServices / Buttons
    private static bool _buttonsResolveTried;
    private static PropertyInfo? _msProp;
    private static PropertyInfo? _lastUsercmdProp;
    private static FieldInfo? _buttonsField;

    // Usercmd bit masks (CS2)
    private const ulong IN_LEFT      = 1UL << 7;
    private const ulong IN_RIGHT     = 1UL << 8;
    private const ulong IN_MOVELEFT  = 1UL << 9;
    private const ulong IN_MOVERIGHT = 1UL << 10;

    // ============================================================
    // LOAD / UNLOAD
    // ============================================================
    public override void Load(bool hotReload)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

        Instance = this;
        _configPath = Path.Combine(ModuleDirectory, "anticheat_config.json");
        LoadConfig();
        RebuildWhitelistCache();

        Db = new Database(ModuleDirectory);
        Db.Initialize();
        Webhook = new WebhookLogger(Config.WebhookUrl, Config.WebhookUsername);

        // Event handlers
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnect);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Post);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Post);
        RegisterEventHandler<EventPlayerJump>(OnPlayerJump, HookMode.Post);
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire, HookMode.Post);

        // Commands
        AddCommand("css_ac", "Anti-cheat menu", OnAcCommand);
        AddCommand("css_anticheat", "Anti-cheat menu", OnAcCommand);
        AddCommand("css_rules", "Verification rules", OnRulesCommand);

        // Main loop
        RegisterListener<Listeners.OnTick>(OnTick);

        Console.WriteLine($"[AntiCheat v{ModuleVersion}] Loaded. Server={Config.ServerName}, Webhook={Config.WebhookEnabled}, AutoBan={Config.AutoBanEnabled}");
    }

    public override void Unload(bool hotReload)
    {
        Instance = null!;
        Console.WriteLine("[AntiCheat] Unloaded.");
    }

    public void SaveConfigPublic()
    {
        try
        {
            File.WriteAllText(_configPath, JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true }));
            RebuildWhitelistCache();
        }
        catch (Exception ex) { Console.WriteLine($"[AntiCheat] SaveConfig error: {ex.Message}"); }
    }

    private void LoadConfig()
    {
        if (File.Exists(_configPath))
        {
            try { Config = JsonSerializer.Deserialize<AntiCheatConfig>(File.ReadAllText(_configPath)) ?? new(); }
            catch (Exception ex) { Console.WriteLine($"[AntiCheat] LoadConfig error: {ex.Message}"); Config = new(); }
        }
        else Config = new();
        SaveConfigPublic();
    }

    public void RebuildWhitelistCache()
    {
        var newCache = new HashSet<ulong>();
        foreach (var s in Config.Whitelist)
            if (ulong.TryParse(s, out var v) && v != 0UL) newCache.Add(v);
        _whitelistCache = newCache;
        Console.WriteLine($"[AntiCheat] Whitelist cache rebuilt: {newCache.Count} entries");
    }

    // ============================================================
    // CONNECT / DISCONNECT
    // ============================================================
    private HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        try
        {
            if (!IsWhitelisted(player.SteamID))
            {
                var bans = Db.GetRecentBans(500);
                var hit = bans.FirstOrDefault(b => b.SteamId == player.SteamID);
                if (hit != null)
                {
                    bool stillBanned = hit.DurationMinutes == 0
                        || (DateTime.UtcNow - hit.CreatedAt.ToUniversalTime()).TotalMinutes < hit.DurationMinutes;
                    if (stillBanned)
                    {
                        Console.WriteLine($"[AntiCheat] {player.PlayerName} is banned. Kicking.");
                        _pendingKicks[player.SteamID] = ("Banned", DateTime.UtcNow.AddSeconds(5), 0);
                        return HookResult.Continue;
                    }
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[AntiCheat] connect error: {ex.Message}"); }

        _stats[player.SteamID] = new PlayerStats
        {
            SteamId = player.SteamID,
            Name = player.PlayerName,
            LastDecay = DateTime.UtcNow
        };
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null)
        {
            _stats.Remove(player.SteamID);
            _pendingKicks.Remove(player.SteamID);
        }
        return HookResult.Continue;
    }

    // ============================================================
    // WEAPON FIRE — timing + prefire snapshot
    // ============================================================
    private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        try
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;
            if (IsWhitelisted(player.SteamID)) return HookResult.Continue;

            if (!_stats.TryGetValue(player.SteamID, out var st)) return HookResult.Continue;

            var now = DateTime.UtcNow;
            var pawn = player.PlayerPawn.Value;

            // === Shot intervals ===
            if (st.LastShotTime != DateTime.MinValue)
            {
                double interval = (now - st.LastShotTime).TotalMilliseconds;
                st.ShotIntervals.Add(interval);
                if (st.ShotIntervals.Count > 6) st.ShotIntervals.RemoveAt(0);

                if (st.ShotIntervals.Count >= 5)
                {
                    int n = st.ShotIntervals.Count;
                    double sum = 0;
                    for (int i = 0; i < n; i++) sum += st.ShotIntervals[i];
                    double mean = sum / n;

                    double varSum = 0;
                    for (int i = 0; i < n; i++) { double d = st.ShotIntervals[i] - mean; varSum += d * d; }
                    double stdDev = Math.Sqrt(varSum / n);

                    if (stdDev < Config.StdDevTrigger && mean > 10.0)
                    {
                        AddSuspicion(player, "Robotic Click Timing", $"StdDev: {stdDev:F2}ms, Mean: {mean:F2}ms", 4);
                        st.ShotIntervals.Clear();
                    }
                    else if (Config.GridSnapEnabled)
                    {
                        double tickMs = (double)Server.TickInterval * 1000.0;
                        if (tickMs > 0.5)
                        {
                            int snapped = 0;
                            for (int i = 0; i < n; i++)
                            {
                                double ticks = st.ShotIntervals[i] / tickMs;
                                double remMs = Math.Abs(ticks - Math.Round(ticks)) * tickMs;
                                if (remMs < Config.GridSnapTolerance) snapped++;
                            }
                            if (snapped >= Config.GridSnapMinShots)
                            {
                                AddSuspicion(player, "Grid Snapped Timing",
                                    $"{snapped}/{n} shots in tick grid ({tickMs:F2}ms)", 5);
                                st.ShotIntervals.Clear();
                            }
                        }
                    }
                }
            }

            // === PREFIRE: cone snapshot (zero-alloc via Clear) ===
            if (Config.PrefireCheckEnabled && pawn != null && pawn.IsValid)
            {
                var angles = GetEyeAnglesSafe(pawn) ?? GetEyeAnglesFromController(player);
                if (angles != null)
                {
                    st.LastShotAngles = angles;
                    st.EnemiesInConeAtShot.Clear();

                    var origin = pawn.AbsOrigin;
                    if (origin != null)
                    {
                        foreach (var other in Utilities.GetPlayers())
                        {
                            if (other == null || !other.IsValid || other.IsBot || !other.PawnIsAlive) continue;
                            if (other.SteamID == player.SteamID) continue;
                            if (other.Team == player.Team) continue;
                            var op = other.PlayerPawn.Value;
                            if (op == null || !op.IsValid || op.AbsOrigin == null) continue;

                            float dist = Distance(origin, op.AbsOrigin);
                            if (dist > Config.PrefireMaxDistance) continue;
                            if (AngleToTarget(angles, origin, op.AbsOrigin) <= Config.PrefireConeDegrees)
                                st.EnemiesInConeAtShot.Add(other.SteamID);
                        }
                    }
                }
            }

            st.LastShotTime = now;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AntiCheat] OnWeaponFire error: {ex.Message}");
        }
        return HookResult.Continue;
    }

    // ============================================================
    // HURT — wallbang proxy
    // ============================================================
    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        try
        {
            var attacker = @event.Attacker;
            var victim = @event.Userid;
            if (attacker == null || !attacker.IsValid || attacker.IsBot) return HookResult.Continue;
            if (victim == null || !victim.IsValid || victim.IsBot) return HookResult.Continue;
            if (attacker.SteamID == victim.SteamID) return HookResult.Continue;
            if (IsWhitelisted(attacker.SteamID)) return HookResult.Continue;

            if (!_stats.TryGetValue(attacker.SteamID, out var stats)) return HookResult.Continue;

            if (Config.WallbangProxyCheckEnabled)
            {
                stats.HitsTotal++;

                var pawn = attacker.PlayerPawn.Value;
                var angles = pawn != null && pawn.IsValid
                    ? (GetEyeAnglesSafe(pawn) ?? GetEyeAnglesFromController(attacker))
                    : null;
                var origin = pawn?.AbsOrigin;

                if (angles == null || origin == null)
                {
                    stats.HitsBlind++;
                }
                else
                {
                    bool anyVisible = false;
                    foreach (var other in Utilities.GetPlayers())
                    {
                        if (other == null || !other.IsValid || other.IsBot || !other.PawnIsAlive) continue;
                        if (other.SteamID == attacker.SteamID) continue;
                        if (other.Team == attacker.Team) continue;
                        var op = other.PlayerPawn.Value;
                        if (op == null || !op.IsValid || op.AbsOrigin == null) continue;

                        if (AngleToTarget(angles, origin, op.AbsOrigin) <= Config.WallbangProxyConeDegrees)
                        {
                            anyVisible = true;
                            break;
                        }
                    }
                    if (!anyVisible) stats.HitsBlind++;
                }

                if (stats.HitsTotal >= Config.WallbangProxyMinHits)
                {
                    float ratio = (float)stats.HitsBlind / stats.HitsTotal;
                    if (ratio >= Config.WallbangProxyRatioTrigger)
                    {
                        AddSuspicion(attacker, "Wallbang Proxy",
                            $"{stats.HitsBlind}/{stats.HitsTotal} ({ratio:P0}) blind hits", 2);
                        stats.HitsBlind = 0;
                        stats.HitsTotal = 0;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AntiCheat] OnPlayerHurt error: {ex.Message}");
        }
        return HookResult.Continue;
    }

    // ============================================================
    // TICK — heart of the anti-cheat
    // ============================================================
    private void OnTick()
    {
        try
        {
            // Process pending kicks first (retries)
            ProcessPendingKicks();

            var players = Utilities.GetPlayers();
            if (players.Count < Config.MinPlayersForChecks) return;

            // Skip warmup and freeze-time
            var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
            if (gameRules != null)
            {
                if (gameRules.WarmupPeriod) return;
                if (gameRules.FreezePeriod) return;
            }

            // ===== DEBUG (sqrt only inside throttle) =====
            if (Config.DebugLog && (DateTime.UtcNow - _lastDebug).TotalSeconds >= Config.DebugIntervalSeconds)
            {
                _lastDebug = DateTime.UtcNow;
                foreach (var dbg in players)
                {
                    if (dbg == null || !dbg.IsValid || dbg.IsBot || dbg.IsHLTV || !dbg.PawnIsAlive) continue;
                    var p = dbg.PlayerPawn.Value;
                    if (p == null || !p.IsValid) continue;

                    var v = p.AbsVelocity;
                    float spd = v == null ? -1f : (float)Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z) / 52.49f;
                    var ang = GetEyeAnglesSafe(p) ?? GetEyeAnglesFromController(dbg);
                    string angStr = ang == null ? "NULL" : $"X={ang.X:F1} Y={ang.Y:F1}";
                    if (_stats.TryGetValue(dbg.SteamID, out var st))
                        Console.WriteLine($"[AC DBG] {dbg.PlayerName} | spd={spd:F2} | score={st.SuspicionScore} | ang={angStr}");
                }
            }

            float maxSpeedSq = (Config.MaxSpeed * 52.49f) * (Config.MaxSpeed * 52.49f);
            float hugeSpeedSq = (Config.HugeSpeedThreshold * 52.49f) * (Config.HugeSpeedThreshold * 52.49f);

            // ===== MAIN LOOP =====
            foreach (var player in players)
            {
                if (player == null || !player.IsValid || player.IsBot || player.IsHLTV || !player.PawnIsAlive) continue;
                if (!_stats.TryGetValue(player.SteamID, out var stats)) continue;
                if (IsWhitelisted(player.SteamID)) continue;

                var pawn = player.PlayerPawn.Value;
                if (pawn == null || !pawn.IsValid) continue;

                // ============ HvH: PITCH ANTI-AIM ============
                if (Config.PitchAntiAimCheckEnabled)
                {
                    var ang = GetEyeAnglesSafe(pawn) ?? GetEyeAnglesFromController(player);
                    if (ang != null)
                    {
                        float pitch = ang.X;
                        if (pitch < Config.PitchMinLimit || pitch > Config.PitchMaxLimit)
                            stats.ConsecutiveInvalidPitchTicks++;
                        else
                            stats.ConsecutiveInvalidPitchTicks = 0;

                        if (stats.ConsecutiveInvalidPitchTicks >= Config.PitchBanConsecutiveTicks)
                        {
                            Console.WriteLine($"[AntiCheat] HvH Pitch AA: {player.PlayerName} pitch={pitch:F1}");
                            BanPlayer(player,
                                $"[HvH] Invalid Pitch angle {pitch:F1}° (limit [{Config.PitchMinLimit};{Config.PitchMaxLimit}])",
                                null, 0);
                            stats.ConsecutiveInvalidPitchTicks = 0;
                            continue;
                        }
                    }
                }

                // ============ HvH: SPINBOT / JITTER ============
                if (Config.SpinbotCheckEnabled)
                {
                    var ang = GetEyeAnglesSafe(pawn) ?? GetEyeAnglesFromController(player);
                    if (ang != null && stats.LastAngles != null)
                    {
                        float dyaw = Math.Abs(NormalizeAngle(ang.Y - stats.LastAngles.Y));

                        if (dyaw > Config.SpinbotMinYawDelta)
                            stats.ConsecutiveSpinbotTicks++;
                        else
                            stats.ConsecutiveSpinbotTicks = 0;

                        if (stats.ConsecutiveSpinbotTicks >= Config.SpinbotConsecutiveTicks)
                        {
                            Console.WriteLine($"[AntiCheat] HvH Spinbot: {player.PlayerName} dyaw={dyaw:F1}");
                            BanPlayer(player,
                                $"[HvH] Spinbot/Jitter AA: Δyaw={dyaw:F1}°/tick × {Config.SpinbotConsecutiveTicks}+",
                                null, 0);
                            stats.ConsecutiveSpinbotTicks = 0;
                            continue;
                        }
                    }
                }

                // ============ PREFIRE ============
                if (Config.PrefireCheckEnabled && stats.LastShotTime != DateTime.MinValue)
                {
                    double msSinceShot = (DateTime.UtcNow - stats.LastShotTime).TotalMilliseconds;
                    if (msSinceShot >= 0 && msSinceShot <= Config.PrefireWindowMs)
                    {
                        var angles = GetEyeAnglesSafe(pawn) ?? GetEyeAnglesFromController(player);
                        var origin = pawn.AbsOrigin;
                        if (angles != null && origin != null)
                        {
                            foreach (var other in players)
                            {
                                if (other == null || !other.IsValid || other.IsBot || !other.PawnIsAlive) continue;
                                if (other.SteamID == player.SteamID) continue;
                                if (other.Team == player.Team) continue;
                                if (stats.EnemiesInConeAtShot.Contains(other.SteamID)) continue;

                                var op = other.PlayerPawn.Value;
                                if (op == null || !op.IsValid || op.AbsOrigin == null) continue;

                                float dist = Distance(origin, op.AbsOrigin);
                                if (dist > Config.PrefireMaxDistance) continue;

                                float angToTarget = AngleToTarget(angles, origin, op.AbsOrigin);
                                if (angToTarget > Config.PrefireConeDegrees) continue;

                                if (stats.LastShotAngles != null)
                                {
                                    float shotAng = AngleToTarget(stats.LastShotAngles, origin, op.AbsOrigin);
                                    if (shotAng > Config.PrefireShotDirTolerance) continue;
                                }

                                if ((DateTime.UtcNow - stats.LastPrefireFlagTime).TotalSeconds < Config.PrefireThrottleSeconds) continue;

                                stats.PrefireEvents++;
                                if (stats.PrefireEvents >= Config.PrefireMinEvents)
                                {
                                    AddSuspicion(player, "Prefire",
                                        $"Shot {msSinceShot:F0}ms before target enters cone (dist={dist:F0})", 2);
                                    stats.PrefireEvents = 0;
                                    stats.LastPrefireFlagTime = DateTime.UtcNow;
                                }
                                break;
                            }
                        }
                    }
                    else if (msSinceShot > Config.PrefireWindowMs + 1000)
                    {
                        if (stats.PrefireEvents > 0) stats.PrefireEvents--;
                        if (msSinceShot > 10000) stats.LastShotTime = DateTime.MinValue;
                    }
                }

                // ============ SPEED ============
                if (Config.SpeedCheckEnabled)
                {
                    var vel = pawn.AbsVelocity;
                    bool onGround = (pawn.Flags & 1) != 0;
                    if (vel != null)
                    {
                        float hSpeedSq = vel.X * vel.X + vel.Y * vel.Y;
                        float totalSpeedSq = hSpeedSq + vel.Z * vel.Z;

                        if (totalSpeedSq > hugeSpeedSq) stats.ConsecutiveHugeSpeed++;
                        else stats.ConsecutiveHugeSpeed = 0;

                        if (stats.ConsecutiveHugeSpeed >= Config.HugeSpeedConsecutiveTicks)
                        {
                            float totalSpeed = (float)Math.Sqrt(totalSpeedSq) / 52.49f;
                            stats.SpeedViolations++;
                            stats.SuspicionScore += 5;
                            stats.ConsecutiveHugeSpeed = 0;
                            AddSuspicion(player, "Speed/NoClip", $"{totalSpeed:F1} m/s × {Config.HugeSpeedConsecutiveTicks}+", 5);
                        }

                        if (onGround && hSpeedSq > maxSpeedSq) stats.ConsecutiveSpeedTicks++;
                        else stats.ConsecutiveSpeedTicks = 0;

                        if (stats.ConsecutiveSpeedTicks >= Config.SpeedConsecutiveTicks)
                        {
                            float hSpeed = (float)Math.Sqrt(hSpeedSq) / 52.49f;
                            stats.SpeedViolations++;
                            stats.SuspicionScore += 3;
                            stats.ConsecutiveSpeedTicks = 0;
                            if (stats.SpeedViolations % 2 == 0)
                                AddSuspicion(player, "Speed (ground)", $"{hSpeed:F1} m/s × {Config.SpeedConsecutiveTicks}+", 3);
                        }
                    }
                }

                // ============ AIM + JERK ============
                if (Config.AimCheckEnabled)
                {
                    var angles = GetEyeAnglesSafe(pawn) ?? GetEyeAnglesFromController(player);
                    if (angles != null && stats.LastAngles != null)
                    {
                        float signedDy = NormalizeAngle(angles.Y - stats.LastAngles.Y);
                        float dx = Math.Abs(NormalizeAngle(angles.X - stats.LastAngles.X));
                        float dy = Math.Abs(signedDy);
                        float delta = (float)Math.Sqrt(dx * dx + dy * dy);
                        stats.LastYawDelta = signedDy;

                        // AIM snap
                        if (delta > Config.MaxAngleDeltaPerTick) stats.ConsecutiveSnapTicks++;
                        else stats.ConsecutiveSnapTicks = 0;

                        if (stats.ConsecutiveSnapTicks >= Config.SnapConsecutiveTicks)
                        {
                            stats.SnapViolations++;
                            stats.SuspicionScore += 4;
                            stats.ConsecutiveSnapTicks = 0;
                            AddSuspicion(player, "Aim snap (instant)", $"Δ={delta:F0}°/tick × {Config.SnapConsecutiveTicks}+", 4);
                        }

                        // AIM window
                        stats.AimWindowDelta += delta;
                        stats.AimWindowTicks++;
                        if (stats.AimWindowTicks >= Config.AimWindowSize)
                        {
                            float avg = stats.AimWindowDelta / stats.AimWindowTicks;
                            if (avg > Config.AimWindowMaxAvg)
                            {
                                stats.SnapViolations++;
                                stats.SuspicionScore += 3;
                                AddSuspicion(player, "Aim spin", $"Avg {avg:F1}°/tick × {Config.AimWindowSize}", 3);
                            }
                            stats.AimWindowDelta = 0;
                            stats.AimWindowTicks = 0;
                        }
                        else if (delta < 1.0f)
                        {
                            stats.AimWindowDelta *= 0.8f;
                            stats.AimWindowTicks = Math.Max(0, stats.AimWindowTicks - 1);
                        }

                        // JERK (2 signals)
                        if (Config.JerkAnalysisEnabled)
                        {
                            float acc = delta - stats.LastAimDelta;
                            float jerk = acc - stats.LastAcceleration;

                            bool accSignificant = Math.Abs(acc) > Config.JerkMinMagnitude;
                            bool lastAccSignificant = Math.Abs(stats.LastAcceleration) > 0.001f;
                            bool sameSign = accSignificant && lastAccSignificant
                                            && ((acc > 0f) == (stats.LastAcceleration > 0f));

                            if (sameSign) stats.ConsecutiveSameAccelerationTicks++;
                            else stats.ConsecutiveSameAccelerationTicks = 0;

                            if (stats.ConsecutiveSameAccelerationTicks >= Config.JerkConsecutiveTicks)
                            {
                                stats.SnapViolations++;
                                stats.SuspicionScore += 4;
                                AddSuspicion(player, "Algorithmic Smoothing (Acc)",
                                    $"Stable acceleration {acc:F2} × {Config.JerkConsecutiveTicks}+", 4);
                                stats.ConsecutiveSameAccelerationTicks = 0;
                            }

                            bool smallJerk = Math.Abs(jerk) < Config.JerkSmallTolerance && Math.Abs(acc) > 0.5f;
                            if (smallJerk) stats.ConsecutiveSmoothJerkTicks++;
                            else stats.ConsecutiveSmoothJerkTicks = 0;

                            if (stats.ConsecutiveSmoothJerkTicks >= Config.SmoothJerkConsecutiveTicks)
                            {
                                stats.SnapViolations++;
                                stats.SuspicionScore += 5;
                                AddSuspicion(player, "Bezier Smoothing (Jerk≈0)",
                                    $"|jerk|<{Config.JerkSmallTolerance} × {Config.SmoothJerkConsecutiveTicks}+", 5);
                                stats.ConsecutiveSmoothJerkTicks = 0;
                            }

                            stats.LastAcceleration = acc;
                            stats.LastJerk = jerk;
                        }

                        // Subpixel / DMA
                        bool isIntX = Math.Abs(dx - Math.Round(dx)) < 0.001f;
                        bool isIntY = Math.Abs(dy - Math.Round(dy)) < 0.001f;
                        if (isIntX && isIntY && delta > Config.SubpixelMinDelta) stats.ConsecutiveIntegerAimTicks++;
                        else stats.ConsecutiveIntegerAimTicks = 0;

                        if (stats.ConsecutiveIntegerAimTicks >= Config.SubpixelConsecutiveTicks)
                        {
                            stats.SnapViolations++;
                            stats.SuspicionScore += 5;
                            AddSuspicion(player, "DMA Subpixel Pattern",
                                $"Integer steps × {Config.SubpixelConsecutiveTicks}+", 5);
                            stats.ConsecutiveIntegerAimTicks = 0;
                        }

                        stats.LastAimDelta = delta;
                    }
                    if (angles != null) stats.LastAngles = angles;
                }

                // ============ AUTO-BHOP STRAFE SYNC ============
                if (Config.StrafeSyncCheckEnabled)
                {
                    var vel = pawn.AbsVelocity;
                    if (vel != null)
                    {
                        float speedMs = (float)Math.Sqrt(vel.X * vel.X + vel.Y * vel.Y) / 52.49f;
                        ulong btns = GetButtonsSafe(pawn);

                        bool moveLeft  = (btns & IN_MOVELEFT)  != 0 || (btns & IN_LEFT)  != 0;
                        bool moveRight = (btns & IN_MOVERIGHT) != 0 || (btns & IN_RIGHT) != 0;

                        float yawDelta = stats.LastYawDelta;
                        bool mouseRight = yawDelta > Config.StrafeSyncMinYawDelta;
                        bool mouseLeft  = yawDelta < -Config.StrafeSyncMinYawDelta;

                        bool sync = speedMs > Config.StrafeSyncMinSpeed &&
                                    ((moveLeft && mouseLeft) || (moveRight && mouseRight));

                        if (sync) stats.ConsecutiveStrafeSyncTicks++;
                        else stats.ConsecutiveStrafeSyncTicks = 0;

                        if (stats.ConsecutiveStrafeSyncTicks >= Config.StrafeSyncConsecutiveTicks)
                        {
                            stats.BhopViolations++;
                            stats.SuspicionScore += 4;
                            AddSuspicion(player, "Auto-Strafe Sync",
                                $"A/D + mouse synced × {Config.StrafeSyncConsecutiveTicks}+", 4);
                            stats.ConsecutiveStrafeSyncTicks = 0;
                        }

                        stats.LastButtons = btns;
                    }
                }

                // ============ BHOP ============
                if (Config.BhopCheckEnabled)
                {
                    var vel = pawn.AbsVelocity;
                    bool onGround = (pawn.Flags & 1) != 0;
                    if (vel != null)
                    {
                        float speedMs = (float)Math.Sqrt(vel.X * vel.X + vel.Y * vel.Y) / 52.49f;
                        if (onGround && speedMs > Config.BhopMinSpeed) stats.ConsecutiveBhopTicks++;
                        else stats.ConsecutiveBhopTicks = 0;

                        if (stats.ConsecutiveBhopTicks >= Config.BhopConsecutiveTicks)
                        {
                            stats.BhopViolations++;
                            stats.SuspicionScore += 2;
                            stats.ConsecutiveBhopTicks = 0;
                            if (stats.BhopViolations % 3 == 0)
                                AddSuspicion(player, "Bhop abuse",
                                    $"{speedMs:F1} m/s on ground × {Config.BhopConsecutiveTicks}+", 2);
                        }
                    }
                }

                // ============ PACKET ============
                if (Config.PacketCheckEnabled)
                {
                    stats.CmdCountInWindow++;
                    var elapsed = (DateTime.UtcNow - stats.LastPacketWindow).TotalSeconds;
                    if (elapsed >= 1.0)
                    {
                        if (stats.CmdCountInWindow > Config.PacketMaxCmdsPerSecond)
                        {
                            stats.PacketViolations++;
                            stats.SuspicionScore += 5;
                            AddSuspicion(player, "Tickbase / DoubleTap",
                                $"USCMD/sec: {stats.CmdCountInWindow} (limit {Config.PacketMaxCmdsPerSecond})", 5);
                        }
                        if (stats.JumpCountInWindow > Config.PacketMaxJumpPerSecond)
                        {
                            stats.PacketViolations++;
                            stats.SuspicionScore += 2;
                            AddSuspicion(player, "Jump rate abuse", $"{stats.JumpCountInWindow} jumps/sec", 2);
                        }
                        stats.CmdCountInWindow = 0;
                        stats.JumpCountInWindow = 0;
                        stats.LastPacketWindow = DateTime.UtcNow;
                    }
                }

                // ============ DECAY ============
                if ((DateTime.UtcNow - stats.LastDecay).TotalSeconds >= Config.SuspicionDecaySeconds)
                {
                    if (stats.SuspicionScore > 0) stats.SuspicionScore--;
                    stats.LastDecay = DateTime.UtcNow;
                }

                // ============ AUTO-BAN ============
                if (Config.AutoBanEnabled && stats.SuspicionScore >= Config.SuspicionToBan)
                {
                    if (!IsWhitelisted(player.SteamID) && !_pendingKicks.ContainsKey(player.SteamID))
                    {
                        BanPlayer(player,
                            $"[AC] score={stats.SuspicionScore} (spd:{stats.SpeedViolations} snap:{stats.SnapViolations} bhop:{stats.BhopViolations} pkt:{stats.PacketViolations})",
                            null, Config.BanDurationMinutes);
                    }
                    stats.SuspicionScore = 0;
                }
            }
        }
        catch (Exception ex)
        {
            if (!_eyeAnglesWarned)
            {
                Console.WriteLine($"[AntiCheat] OnTick error: {ex.Message}");
                _eyeAnglesWarned = true;
            }
        }
    }

    // ============================================================
    // EYE ANGLES — SAFE (reflection, cross-version)
    // ============================================================
    private static QAngle? GetEyeAnglesSafe(CCSPlayerPawn pawn)
    {
        if (!_eyeAnglesSearched)
        {
            _eyeAnglesSearched = true;

            string[] names = { "EyeAngles", "V_angle", "ViewAngle", "AimAngles", "Angles" };

            Type[] types =
            {
                pawn.GetType(),
                typeof(CCSPlayerPawnBase),
                typeof(CBasePlayerPawn),
                typeof(CBaseEntity),
            };

            foreach (var name in names)
            {
                foreach (var t in types)
                {
                    try
                    {
                        var p = t.GetProperty(name,
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                        if (p == null) continue;

                        var pt = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                        if (pt != typeof(QAngle)) continue;

                        _eyeAnglesProp = p;
                        Console.WriteLine($"[AntiCheat] EyeAngles resolved: {t.Name}.{p.Name} ({p.PropertyType.Name})");
                        break;
                    }
                    catch { }
                }
                if (_eyeAnglesProp != null) break;
            }

            if (_eyeAnglesProp == null)
                Console.WriteLine("[AntiCheat] WARNING: EyeAngles/V_angle not found. Aim detection disabled.");
        }

        if (_eyeAnglesProp == null) return null;

        try
        {
            var value = _eyeAnglesProp.GetValue(pawn);
            if (value is QAngle q) return q;
        }
        catch { }
        return null;
    }
    private static QAngle? GetEyeAnglesFromController(CCSPlayerController player)
    {
        try
        {
            var t = player.GetType();
            foreach (var name in new[] { "EyeAngles", "V_angle", "ViewAngle" })
            {
                var p = t.GetProperty(name,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (p == null) continue;

                var pt = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                if (pt != typeof(QAngle)) continue;

                var v = p.GetValue(player);
                if (v is QAngle q) return q;
            }
        }
        catch { }
        return null;
    }

    // ============================================================
    // HELPERS
    // ============================================================
    private static float Distance(Vector a, Vector b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static float AngleToTarget(QAngle angles, Vector fromOrigin, Vector toOrigin)
    {
        float dx = toOrigin.X - fromOrigin.X;
        float dy = toOrigin.Y - fromOrigin.Y;
        float dz = toOrigin.Z - fromOrigin.Z;

        float horizDist = (float)Math.Sqrt(dx * dx + dy * dy);
        if (horizDist < 1f) return 0f;

        float targetYaw = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
        float targetPitch = (float)(-Math.Atan2(dz, horizDist) * 180.0 / Math.PI);

        float dyaw = Math.Abs(NormalizeAngle(angles.Y - targetYaw));
        float dpitch = Math.Abs(NormalizeAngle(angles.X - targetPitch));

        return (float)Math.Sqrt(dyaw * dyaw + dpitch * dpitch);
    }

    private static ulong GetButtonsSafe(CCSPlayerPawn pawn)
    {
        try
        {
            if (!_buttonsResolveTried)
            {
                _buttonsResolveTried = true;
                var pawnType = pawn.GetType();
                _msProp = pawnType.GetProperty("MovementServices");
                if (_msProp != null)
                {
                    var msType = _msProp.PropertyType;
                    _lastUsercmdProp = msType.GetProperty("LastUsercmd");
                }
                Console.WriteLine($"[AntiCheat] Buttons resolver: ms={_msProp != null}, cmd={_lastUsercmdProp != null}");
            }

            if (_msProp == null || _lastUsercmdProp == null) return 0;
            var ms = _msProp.GetValue(pawn);
            if (ms == null) return 0;
            var cmd = _lastUsercmdProp.GetValue(ms);
            if (cmd == null) return 0;

            var cmdType = cmd.GetType();
            _buttonsField ??= cmdType.GetField("Buttons");
            if (_buttonsField != null)
            {
                var v = _buttonsField.GetValue(cmd);
                if (v is ulong u) return u;
                if (v is int i) return (ulong)i;
                if (v is uint ui) return ui;
            }
            return 0;
        }
        catch { return 0; }
    }

    // ============================================================
    // SUSPICION
    // ============================================================
    private void AddSuspicion(CCSPlayerController player, string reason, string details, int weight)
    {
        if (!_stats.TryGetValue(player.SteamID, out var stats)) return;

        stats.SuspicionScore += weight;
        stats.ReasonCounts[reason] = stats.ReasonCounts.GetValueOrDefault(reason) + 1;
        stats.LastFlag = DateTime.UtcNow;

        Db.AddSuspicion(player.SteamID, player.PlayerName, reason, details, Config.ServerName);

        if (Config.AiScoringEnabled && stats.ReasonCounts.Count >= 3)
        {
            int extra = (int)(stats.SuspicionScore * (Config.AiDiversityBonus - 1f));
            if (extra > 0) { stats.SuspicionScore += extra; details += $" [AI Boost: +{extra}]"; }
        }

        Webhook.Send(
            "⚠️ Suspicion detected",
            $"**Player:** {player.PlayerName}\n" +
            $"**SteamID:** `{player.SteamID}`\n" +
            $"**Reason:** {reason}\n" +
            $"**Details:** {details}\n" +
            $"**Server:** {Config.ServerName}",
            0xFFAA00,
            new List<(string, string)>
            {
                ("Suspicion score", stats.SuspicionScore.ToString()),
                ("Violations", string.Join(", ", stats.ReasonCounts.Select(kv => $"{kv.Key}:{kv.Value}")))
            });
    }

    // ============================================================
    // PENDING KICKS / JUMP / DEATH
    // ============================================================
    private void ProcessPendingKicks()
    {
        if (_pendingKicks.Count == 0) return;
        var now = DateTime.UtcNow;
        foreach (var kv in _pendingKicks.ToList())
        {
            if (now > kv.Value.until) { _pendingKicks.Remove(kv.Key); continue; }

            var target = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && p.SteamID == kv.Key);
            if (target == null) { _pendingKicks.Remove(kv.Key); continue; }

            int uid = target.UserId ?? -1;
            if (uid > 0)
            {
                try { Server.ExecuteCommand($"kickid {uid}"); } catch { }
            }
        }
    }

    private HookResult OnPlayerJump(EventPlayerJump @event, GameEventInfo info)
    {
        try
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;
            if (_stats.TryGetValue(player.SteamID, out var st)) st.JumpCountInWindow++;
        }
        catch { }
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        try
        {
            var attacker = @event.Attacker;
            var victim = @event.Userid;
            if (attacker == null || !attacker.IsValid || attacker.IsBot) return HookResult.Continue;
            if (victim == null || !victim.IsValid) return HookResult.Continue;
            if (attacker.SteamID == victim.SteamID) return HookResult.Continue;
            if (IsWhitelisted(attacker.SteamID)) return HookResult.Continue;

            if (!_stats.TryGetValue(attacker.SteamID, out var stats)) return HookResult.Continue;

            stats.TotalKills++;
            if (@event.Headshot) stats.Headshots++;

            if (stats.TotalKills >= Config.MinKillsToCheckHS)
            {
                var ratio = (float)stats.Headshots / stats.TotalKills;
                if (ratio >= Config.HeadshotRatioToFlag)
                {
                    AddSuspicion(attacker, "High HS-ratio",
                        $"HS: {stats.Headshots}/{stats.TotalKills} ({ratio:P0})", 2);
                    stats.Headshots = 0;
                    stats.TotalKills = 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AntiCheat] OnPlayerDeath error: {ex.Message}");
        }
        return HookResult.Continue;
    }

    // ============================================================
    // BAN / KICK
    // ============================================================
    public void BanPlayer(CCSPlayerController target, string reason, CCSPlayerController? admin, int durationMinutes)
    {
        if (target == null || !target.IsValid) return;
        if (IsWhitelisted(target.SteamID)) return;
        if (_pendingKicks.ContainsKey(target.SteamID)) return;

        string adminName = admin?.PlayerName ?? "AntiCheat (auto)";
        var durationStr = durationMinutes == 0 ? "permanent" : $"{durationMinutes} min";

        Db.AddBan(target.SteamID, target.PlayerName, reason, adminName, durationMinutes, Config.ServerName);

        Webhook.Send(
            "🔨 Player banned",
            $"**Player:** {target.PlayerName}\n" +
            $"**SteamID:** `{target.SteamID}`\n" +
            $"**Reason:** {reason}\n" +
            $"**Admin:** {adminName}\n" +
            $"**Duration:** {durationStr}\n" +
            $"**Server:** {Config.ServerName}",
            0xFF0000);

        Server.PrintToChatAll($" {ChatColors.Red}[AntiCheat]{ChatColors.Default} " +
                              $"Player {ChatColors.Red}{target.PlayerName}{ChatColors.Default} " +
                              $"was banned. Reason: {reason}");

        Console.WriteLine($"[AntiCheat] BAN: {target.PlayerName} | sid={target.SteamID} | reason={reason}");

        _pendingKicks[target.SteamID] = (reason, DateTime.UtcNow.AddSeconds(5), 0);

        int uid = target.UserId ?? -1;
        if (uid > 0)
        {
            try { Server.ExecuteCommand($"banid {durationMinutes} {uid}"); } catch { }
            try { Server.ExecuteCommand($"kickid {uid}"); } catch { }
        }
    }

    public void KickPlayer(CCSPlayerController target, string reason, CCSPlayerController? admin)
    {
        if (target == null || !target.IsValid) return;
        var adminName = admin?.PlayerName ?? "AntiCheat";

        Webhook.Send("👢 Player kicked",
            $"**Player:** {target.PlayerName}\n**SteamID:** `{target.SteamID}`\n**Reason:** {reason}\n**Admin:** {adminName}\n**Server:** {Config.ServerName}",
            0xFF6600);
        Server.PrintToChatAll($" {ChatColors.Red}[AntiCheat]{ChatColors.Default} Player {target.PlayerName} kicked. Reason: {reason}");

        int uid = target.UserId ?? -1;
        if (uid > 0)
        {
            _pendingKicks[target.SteamID] = (reason, DateTime.UtcNow.AddSeconds(3), 0);
            try { Server.ExecuteCommand($"kickid {uid}"); } catch { }
        }
    }

    // ============================================================
    // PUBLIC HELPERS (used by MenuHandler)
    // ============================================================
    public bool IsWhitelisted(ulong steamId) => _whitelistCache.Contains(steamId);
    public bool IsSuperAdmin(ulong steamId) => Config.SuperAdmins.Contains(steamId.ToString());

    public int GetSuspicionScore(ulong steamId)
    {
        if (_stats.TryGetValue(steamId, out var stats))
            return stats.SuspicionScore;
        return 0;
    }

    public bool IsAcAdmin(CCSPlayerController player)
    {
        if (IsSuperAdmin(player.SteamID)) return true;
        try { return AdminManager.PlayerHasPermissions(player, "@css/generic"); }
        catch { return false; }
    }

    // ============================================================
    // COMMANDS
    // ============================================================
    private void OnRulesCommand(CCSPlayerController? player, CommandInfo cmd)
    {
        if (player == null) return;
        var rules = Config.VerificationRules;
        player.PrintToChat($" {ChatColors.Gold}=== {rules.Title} ===");
        foreach (var r in rules.Rules)
            player.PrintToChat($" {ChatColors.Yellow}{r.Num}.{ChatColors.Default} {r.Text}");
    }

    private void OnAcCommand(CCSPlayerController? player, CommandInfo cmd)
    {
        if (player == null || !player.IsValid) return;
        if (!IsAcAdmin(player))
        {
            player.PrintToChat($" {ChatColors.Red}[AntiCheat]{ChatColors.Default} Access denied.");
            return;
        }
        MenuHandler.OpenMainMenu(player);
    }

    private static float NormalizeAngle(float a)
    {
        while (a > 180) a -= 360;
        while (a < -180) a += 360;
        return a;
    }
}
