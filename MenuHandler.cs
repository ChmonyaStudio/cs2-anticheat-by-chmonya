using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;

namespace CSAntiCheat;

public static class MenuHandler
{
    // ============================================================
    // MAIN MENU
    // ============================================================
    public static void OpenMainMenu(CCSPlayerController player)
    {
        var cfg = AntiCheatPlugin.Instance.Config;
        var menu = new ChatMenu("🛡️ AntiCheat");

        menu.AddMenuOption(
            $"Autoban: {(cfg.AutoBanEnabled ? "ON" : "OFF")}",
            (p, o) =>
            {
                cfg.AutoBanEnabled = !cfg.AutoBanEnabled;
                AntiCheatPlugin.Instance.SaveConfigPublic();
                p.PrintToChat($" {ChatColors.Red}[AntiCheat]{ChatColors.Default} Autoban: {(cfg.AutoBanEnabled ? "ON" : "OFF")}");
                OpenMainMenu(p);
            });

        menu.AddMenuOption(
            $"Webhook: {(cfg.WebhookEnabled ? "ON" : "OFF")}",
            (p, o) =>
            {
                cfg.WebhookEnabled = !cfg.WebhookEnabled;
                AntiCheatPlugin.Instance.SaveConfigPublic();
                p.PrintToChat($" {ChatColors.Red}[AntiCheat]{ChatColors.Default} Webhook: {(cfg.WebhookEnabled ? "ON" : "OFF")}");
                OpenMainMenu(p);
            });

        menu.AddMenuOption(
            $"Debug: {(cfg.DebugLog ? "ON" : "OFF")}",
            (p, o) =>
            {
                cfg.DebugLog = !cfg.DebugLog;
                AntiCheatPlugin.Instance.SaveConfigPublic();
                p.PrintToChat($" {ChatColors.Red}[AntiCheat]{ChatColors.Default} Debug: {(cfg.DebugLog ? "ON" : "OFF")}");
                OpenMainMenu(p);
            });

        menu.AddMenuOption("⚙️ Adjust thresholds", (p, o) => OpenThresholds(p));
        menu.AddMenuOption("✅ Manage whitelist", (p, o) => OpenWhitelist(p));
        menu.AddMenuOption("👥 Players (check/actions)", (p, o) => OpenPlayers(p));
        menu.AddMenuOption("📋 Recent suspicions", (p, o) => ShowRecentSuspicions(p));
        menu.AddMenuOption("🔨 Recent bans", (p, o) => ShowRecentBans(p));
        menu.AddMenuOption("📜 Verification rules", (p, o) =>
        {
            MenuManager.CloseActiveMenu(p);
            var rules = cfg.VerificationRules;
            p.PrintToChat($" {ChatColors.Gold}=== {rules.Title} ===");
            foreach (var r in rules.Rules)
                p.PrintToChat($" {ChatColors.Yellow}{r.Num}.{ChatColors.Default} {r.Text}");
        });

        menu.AddMenuOption("❌ Close", (p, o) => MenuManager.CloseActiveMenu(p));

        MenuManager.OpenChatMenu(player, menu);
    }

    // ============================================================
    // THRESHOLDS
    // ============================================================
    private static void OpenThresholds(CCSPlayerController player)
    {
        var cfg = AntiCheatPlugin.Instance.Config;
        var menu = new ChatMenu("⚙️ Thresholds");

        menu.AddMenuOption($"Max speed: {cfg.MaxSpeed:F1} m/s", (p, o) =>
        {
            cfg.MaxSpeed += 0.5f;
            if (cfg.MaxSpeed > 15f) cfg.MaxSpeed = 5f;
            AntiCheatPlugin.Instance.SaveConfigPublic();
            OpenThresholds(p);
        });

        menu.AddMenuOption($"Ban threshold: {cfg.SuspicionToBan}", (p, o) =>
        {
            cfg.SuspicionToBan += 5;
            if (cfg.SuspicionToBan > 100) cfg.SuspicionToBan = 5;
            AntiCheatPlugin.Instance.SaveConfigPublic();
            OpenThresholds(p);
        });

        menu.AddMenuOption($"Aim snap: {cfg.MaxAngleDeltaPerTick:F0}°/tick", (p, o) =>
        {
            cfg.MaxAngleDeltaPerTick += 5;
            if (cfg.MaxAngleDeltaPerTick > 180) cfg.MaxAngleDeltaPerTick = 30;
            AntiCheatPlugin.Instance.SaveConfigPublic();
            OpenThresholds(p);
        });

        menu.AddMenuOption($"Prefire window: {cfg.PrefireWindowMs} ms", (p, o) =>
        {
            cfg.PrefireWindowMs += 50;
            if (cfg.PrefireWindowMs > 500) cfg.PrefireWindowMs = 100;
            AntiCheatPlugin.Instance.SaveConfigPublic();
            OpenThresholds(p);
        });

        menu.AddMenuOption($"Ban duration: {(cfg.BanDurationMinutes == 0 ? "perm" : cfg.BanDurationMinutes + " min")}", (p, o) =>
        {
            cfg.BanDurationMinutes = cfg.BanDurationMinutes switch
            {
                0 => 60,
                60 => 360,
                360 => 1440,
                1440 => 10080,
                10080 => 0,
                _ => 0
            };
            AntiCheatPlugin.Instance.SaveConfigPublic();
            OpenThresholds(p);
        });

        menu.AddMenuOption($"Decay: {cfg.SuspicionDecaySeconds} sec", (p, o) =>
        {
            cfg.SuspicionDecaySeconds += 30;
            if (cfg.SuspicionDecaySeconds > 300) cfg.SuspicionDecaySeconds = 30;
            AntiCheatPlugin.Instance.SaveConfigPublic();
            OpenThresholds(p);
        });

        menu.AddMenuOption("⬅ Back", (p, o) => OpenMainMenu(p));
        MenuManager.OpenChatMenu(player, menu);
    }

    // ============================================================
    // WHITELIST
    // ============================================================
    private static void OpenWhitelist(CCSPlayerController player)
    {
        var cfg = AntiCheatPlugin.Instance.Config;
        var menu = new ChatMenu("✅ Whitelist");

        foreach (var sid in cfg.Whitelist.ToList())
        {
            var captured = sid;
            menu.AddMenuOption($"❌ Remove {captured}", (p, o) =>
            {
                cfg.Whitelist.Remove(captured);
                AntiCheatPlugin.Instance.SaveConfigPublic();
                p.PrintToChat($" {ChatColors.Red}[AntiCheat]{ChatColors.Default} {captured} removed from whitelist.");
                OpenWhitelist(p);
            });
        }

        menu.AddMenuOption("➕ Add myself", (p, o) =>
        {
            var sid = p.SteamID.ToString();
            if (!cfg.Whitelist.Contains(sid))
            {
                cfg.Whitelist.Add(sid);
                AntiCheatPlugin.Instance.SaveConfigPublic();
                p.PrintToChat($" {ChatColors.Red}[AntiCheat]{ChatColors.Default} You've been added to whitelist.");
            }
            OpenWhitelist(p);
        });

        menu.AddMenuOption("⬅ Back", (p, o) => OpenMainMenu(p));
        MenuManager.OpenChatMenu(player, menu);
    }

    // ============================================================
    // PLAYERS
    // ============================================================
    private static void OpenPlayers(CCSPlayerController player)
    {
        var menu = new ChatMenu("👥 Players");

        foreach (var target in Utilities.GetPlayers())
        {
            if (target == null || !target.IsValid || target.IsBot) continue;
            var t = target;
            menu.AddMenuOption($"{t.PlayerName} | {t.SteamID}", (p, o) => OpenPlayerActions(p, t));
        }

        menu.AddMenuOption("⬅ Back", (p, o) => OpenMainMenu(p));
        MenuManager.OpenChatMenu(player, menu);
    }

    private static void OpenPlayerActions(CCSPlayerController admin, CCSPlayerController target)
    {
        var cfg = AntiCheatPlugin.Instance.Config;
        var plugin = AntiCheatPlugin.Instance;
        var menu = new ChatMenu($"Player: {target.PlayerName}");

        menu.AddMenuOption("🔍 Info", (p, o) =>
        {
            MenuManager.CloseActiveMenu(p);
            p.PrintToChat($" {ChatColors.Gold}=== {target.PlayerName} ===");
            p.PrintToChat($" SteamID: {target.SteamID}");
            p.PrintToChat($" Whitelisted: {(plugin.IsWhitelisted(target.SteamID) ? "yes" : "no")}");
            p.PrintToChat($" SuperAdmin: {(plugin.IsSuperAdmin(target.SteamID) ? "yes" : "no")}");
            p.PrintToChat($" Suspicion score: {(plugin.GetSuspicionScore(target.SteamID))}");
        });

        menu.AddMenuOption("➕ Add to whitelist", (p, o) =>
        {
            var sid = target.SteamID.ToString();
            if (!cfg.Whitelist.Contains(sid))
            {
                cfg.Whitelist.Add(sid);
                plugin.SaveConfigPublic();
                p.PrintToChat($" {ChatColors.Red}[AntiCheat]{ChatColors.Default} {target.PlayerName} added to whitelist.");
            }
        });

        menu.AddMenuOption("➖ Remove from whitelist", (p, o) =>
        {
            var sid = target.SteamID.ToString();
            cfg.Whitelist.Remove(sid);
            plugin.SaveConfigPublic();
            p.PrintToChat($" {ChatColors.Red}[AntiCheat]{ChatColors.Default} {target.PlayerName} removed from whitelist.");
        });

        menu.AddMenuOption("👢 Kick", (p, o) => plugin.KickPlayer(target, "Admin decision", p));
        menu.AddMenuOption("🔨 Ban (PERM)", (p, o) => plugin.BanPlayer(target, "Admin decision", p, 0));
        menu.AddMenuOption("🔨 Ban (60 min)", (p, o) => plugin.BanPlayer(target, "Admin decision", p, 60));
        menu.AddMenuOption("🔨 Ban (1440 min)", (p, o) => plugin.BanPlayer(target, "Admin decision", p, 1440));

        menu.AddMenuOption("⬅ Back", (p, o) => OpenPlayers(p));
        MenuManager.OpenChatMenu(admin, menu);
    }

    // ============================================================
    // RECENT LISTS
    // ============================================================
    private static void ShowRecentSuspicions(CCSPlayerController player)
    {
        MenuManager.CloseActiveMenu(player);
        var recent = AntiCheatPlugin.Instance.Db.GetRecentSuspicions(10);
        player.PrintToChat($" {ChatColors.Gold}=== Recent suspicions ===");
        if (recent.Count == 0) { player.PrintToChat(" Empty."); return; }
        foreach (var s in recent)
            player.PrintToChat($" {ChatColors.Yellow}[{s.CreatedAt:HH:mm}] {s.PlayerName} ({s.SteamId}): {s.Reason}");
    }

    private static void ShowRecentBans(CCSPlayerController player)
    {
        MenuManager.CloseActiveMenu(player);
        var recent = AntiCheatPlugin.Instance.Db.GetRecentBans(10);
        player.PrintToChat($" {ChatColors.Gold}=== Recent bans ===");
        if (recent.Count == 0) { player.PrintToChat(" Empty."); return; }
        foreach (var b in recent)
            player.PrintToChat($" {ChatColors.Red}[{b.CreatedAt:HH:mm}] {b.PlayerName} ({b.SteamId}): {b.Reason}");
    }
}
