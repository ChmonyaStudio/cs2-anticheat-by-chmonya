# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Planned
- Voice / sound cheat detection
- Improved wallbang raycast detection (performance permitting)
- Cross-server ban synchronization
- Web panel for ban management

---

## [2.4.1] — 2026-XX-XX

### Added
- **HvH Pitch Anti-Aim** — instant ban when pitch is outside `[-89°; 89°]`
- **Spinbot / Jitter detection** — instant ban when Δyaw > 140°/tick for 3+ ticks
- **Tickbase / DoubleTap** — uscmd rate abuse detection (>140 uscmd/sec)
- Reflection-based EyeAngles resolver — works across all CounterStrikeSharp versions
- Buttons resolver fallback chain (`MovementServices` → `LastUsercmd` → `Buttons`)

### Changed
- Reduced `PacketMaxCmdsPerSecond` from 200 → 140 (better HvH detection on 64-tick)
- Fully wrapped `OnTick` in try/catch — no listener removal on error
- `AddSuspicion` moved AI-diversity boost out of `OnTick` to avoid double counting

### Fixed
- HANDLE leak in `ScanModulesInProcess` (RAII wrapper)
- `npos` crash in SteamID parser on malformed input (`STEAM_0:1` without second colon)
- Binary-safe Aho-Corasick for null bytes in process memory scans
- Whitelist cache race condition (atomic reference swap instead of mutation)
- `IN_LEFT` / `IN_RIGHT` bit masks in strafe-sync detection

---

## [2.3.1] — 2026-XX-XX

### Added
- **Prefire detection** — 150 ms window, direction match, 15 sec throttle
- **Wallbang proxy** — >30% blind hits after 30+ hits trigger
- **Jerk analysis** (3rd derivative) — catches AI aimbot with Bezier smoothing
- **DMA subpixel pattern** — detects hardware mouse emulators (Arduino, KMBox)

### Changed
- `AddSuspicion` now takes `CCSPlayerController` directly (no separate `_stats` lookup)

### Fixed
- Zero-alloc `ShotIntervals` — no more `new HashSet` on every shot
- `Math.Sign(acc)` zero-edge case in Jerk analysis
- `ContainsRaw` for binary data (memory, file content)

---

## [2.3.0] — 2026-XX-XX

### Added
- **Prefire detection** module
- **Wallbang proxy** module
- **Jerk / Bezier smoothing** detector
- **DMA subpixel pattern** detector
- `PitchAntiAimCheckEnabled` and `SpinbotCheckEnabled` config toggles
- `JerkSmallTolerance` and `SmoothJerkConsecutiveTicks` config

### Removed
- DWM topmost toggle from AntiOBS Lua (caused CS2 freezes)

---

## [2.2.0] — 2026-XX-XX

### Added
- **Auto-Strafe Sync** detection (A/D + mouse in same tick)
- **Grid Snap timing** detection (triggerbot firing in server tick grid)
- **AI Diversity Boost** (×1.6 for 3+ different violation types)

### Changed
- `SuspicionToBan` raised from 12 → 25 (fewer false positives)
- `SuspicionDecaySeconds` raised from 60 → 90

---

## [2.1.0] — 2026-XX-XX

### Added
- **Robotic Click Timing** detection (σ < 15 ms across 5+ shots)
- Reflection-based EyeAngles (fallback for CSS versions without direct access)
- `_pendingKicks` queue with retry every tick (5 sec window)
- Ban message de-duplication via `_pendingKicks.ContainsKey`

### Fixed
- `banid` malformed command — switched from `steamid64` to `userid`
- Double ban message (removed duplicate call from `OnTick`)

---

## [2.0.0] — 2026-XX-XX

### Added
- **JSON ban-list** — survives server restart (unlike native `banid`)
- **Discord webhook** integration — every detection logged
- **In-game admin menu** (`!ac`) — thresholds, whitelist, kick/ban
- **Whitelist** + **SuperAdmins** from config
- **Speed / NoClip** detection (ground-only, squared sums, no sqrt in hot path)
- **Aim Snap** detection (>55°/tick × 2+)
- **Aim Spin** detection (avg >20°/tick over 8 tick window)
- **HS-Ratio** detection (>90% HS after 12+ kills)
- Config file with all thresholds
- `Database.cs` — JSON persistence for suspicions and bans
- `WebhookLogger.cs` — Discord webhook with embeds
- `MenuHandler.cs` — ChatMenu UI for admins

### Removed
- SQLite dependency (caused load errors on CSS — switched to JSON)

---

## [1.0.0] — 2026-XX-XX

### Added
- Initial release
- Basic Speed / Aim detection
- Simple ban system (in-memory only, resets on restart)
- Basic admin command `!ac`

---

## Version format

- **MAJOR** — breaking changes (config format, API changes)
- **MINOR** — new detection modules, new features
- **PATCH** — bug fixes, threshold tweaks, refactoring

---

## Links

- **Releases:** https://github.com/ChmonyaStudio/cs2-anticheat/releases
- **Issues:** https://github.com/ChmonyaStudio/cs2-anticheat/issues
- **Repository:** https://github.com/ChmonyaStudio/cs2-anticheat
