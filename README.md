# CS2 AntiCheat for CounterStrikeSharp

**🌍 Language / Язык:** **[🇬🇧 English](#-english)** · **[🇷🇺 Русский](#-русский)**

![CS2](https://img.shields.io/badge/CS2-CounterStrikeSharp-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Release](https://img.shields.io/github/v/release/ChmonyaStudio/cs2-anticheat-by-chmonya)
![License](https://img.shields.io/badge/license-MIT-green)

---

## 🇬🇧 English

A modern, feature-rich anti-cheat plugin for CS2 servers running CounterStrikeSharp. Catches aimbot, wallhack, HvH, DMA, triggerbot, bhop scripts, and more — with zero performance impact.

### 📦 Download

**[⬇️ Download the latest release](https://github.com/ChmonyaStudio/cs2-anticheat-by-chmonya/releases/latest)**

No compilation required — the release contains the compiled `.dll`, ready to drop into your plugins folder.

### ✨ Features

#### 🎯 Detection Modules

| Module | What it catches |
|---|---|
| **Speed / NoClip** | Speedhack, noclip, ground-speed abuse |
| **Aim Snap** | Instant flick > 55°/tick |
| **Aim Spin** | Sustained > 20°/tick avg |
| **Jerk Analysis** | AI aimbot (Bezier smoothing, stable acceleration) |
| **DMA Subpixel** | Hardware mouse emulators (Arduino, KMBox) |
| **Grid Snap Timing** | Triggerbot firing in server tick grid |
| **Robotic Click Timing** | Perfect shot rhythm (σ < 15 ms) |
| **Prefire** | Shooting before enemy enters FOV cone |
| **Wallbang Proxy** | >30% hits with no visible target |
| **Auto-Strafe Sync** | A/D synced to mouse with 0ms reaction |
| **HS-Ratio** | >90% headshots after 12+ kills |

#### 🧨 HvH Modules (instant ban)

| Module | Detects |
|---|---|
| **Pitch Anti-Aim** | Pitch outside `[-89°; 89°]` — impossible without cheat |
| **Spinbot / Jitter** | Δyaw > 140°/tick × 3+ ticks |
| **Tickbase / DoubleTap** | >140 uscmd/sec |

#### ⚙️ Infrastructure

- ✅ **JSON ban-list** — survives server restart (unlike native `banid`)
- ✅ **Discord webhook** — every detection logged with SteamID, details, timestamp
- ✅ **In-game admin menu** (`!ac`) — thresholds, whitelist, kick/ban players
- ✅ **Whitelist + SuperAdmins** — from config, no recompile
- ✅ **Zero-alloc tick loop** — no GC pressure even with 64 players
- ✅ **Auto-ban with suspicion score** — decay over time, no false positives

### 🚀 Quick Start

#### 1. Install CounterStrikeSharp
Follow the official guide: https://docs.cssharp.dev/

#### 2. Download the plugin

Go to the [**Releases**](https://github.com/ChmonyaStudio/cs2-anticheat-by-chmonya/releases/latest) page and download:

- `CSAntiCheat-vX.Y.Z.zip` — compiled plugin, ready to install
- `anticheat_config.example.json` — config template

#### 3. Install

Extract the `.zip` into:
```
game/csgo/addons/counterstrikesharp/plugins/CSAntiCheat/
```

Expected structure:
```
plugins/
└── CSAntiCheat/
    ├── CSAntiCheat.dll
    └── (dependencies, if any)
```

Restart the server.

#### 4. Configure

On first launch, `anticheat_config.json` and `anticheat_bans.json` are created automatically. Edit:

```json
{
  "WebhookUrl": "https://discord.com/api/webhooks/YOUR_WEBHOOK",
  "ServerName": "My CS2 Server",
  "Whitelist": ["76561198737716015"],
  "SuperAdmins": ["76561198737716015"]
}
```

#### 5. Test

Join the server and type `!ac` in chat. The admin menu should appear.

### 🎮 Admin Commands

| Command | What it does |
|---|---|
| `!ac` / `!anticheat` | Open anti-cheat menu |
| `!rules` | Show verification checklist |

### 🔧 Configuration Reference

Key thresholds (full list in the config file):

| Parameter | Default | Description |
|---|---|---|
| `MaxSpeed` | 6.0 | Ground-speed threshold (m/s) |
| `SuspicionToBan` | 25 | Points needed for auto-ban |
| `SuspicionDecaySeconds` | 60 | Points decay interval |
| `MaxAngleDeltaPerTick` | 55 | Aim-snap threshold (°/tick) |
| `JerkConsecutiveTicks` | 4 | AI aimbot detection threshold |
| `PrefireWindowMs` | 150 | Prefire detection window |
| `PitchBanConsecutiveTicks` | 2 | HvH pitch ban threshold |
| `SpinbotMinYawDelta` | 140 | Spinbot threshold (°/tick) |

### 🧠 How it works

The plugin runs on every server tick (~64 Hz) and analyzes:

1. **Player velocity** — squared sums (no sqrt in hot path)
2. **Eye angles** — via reflection, works across CSS versions
3. **Usercmd buttons** — for strafe-sync detection
4. **Shot intervals** — for triggerbot detection
5. **Memory fingerprint** — for DMA subpixel patterns

Each violation adds **suspicion points**. At threshold → auto-ban + webhook + JSON log.

To avoid false positives:
- **Consecutive-tick filters** — ignore single-frame spikes
- **Warmup / freeze skip** — no detection during warmup
- **Whitelist** — admins, VIPs, developers never banned
- **Decay** — honest players don't accumulate points

### 🛠️ Requirements

- **CounterStrikeSharp** 1.0.300+
- **.NET 8.0** runtime
- Windows or Linux server

### 📋 Changelog

See [CHANGELOG.md](CHANGELOG.md) for the full version history.

### 🐛 Known Limitations

- Pattern scan for stream-proof may differ between CS2 builds
- Wallbang proxy uses **angle-based heuristic**, not raycast (for performance)
- Voice / sound cheats are not covered

### 🤝 Contributing

PRs are welcome. For new detection modules — please open an issue first.

### 📜 License

MIT — see [LICENSE](LICENSE).

### 💬 Contact

- **GitHub Issues:** [report a bug](https://github.com/ChmonyaStudio/cs2-anticheat-by-chmonya/issues)
- **Telegram:** [@p1zdabol4ik](https://t.me/p1zdabol4ik)
- **Site:** [chmonya.ct.ws](https://chmonya.ct.ws)

---

## 🇷🇺 Русский

Современный античит-плагин для CS2 серверов на CounterStrikeSharp. Ловит aimbot, wallhack, HvH, DMA, triggerbot, bhop-скрипты и другое — без влияния на производительность.

### 📦 Скачать

**[⬇️ Скачать последний релиз](https://github.com/ChmonyaStudiocs2-anticheat-by-chmonya/releases/latest)**

Сборка не нужна — в релизе готовый `.dll`, который просто кладётся в папку плагинов.

### ✨ Возможности

#### 🎯 Модули детекта

| Модуль | Что ловит |
|---|---|
| **Speed / NoClip** | Speedhack, noclip, ускорение на земле |
| **Aim Snap** | Мгновенный рывок > 55°/тик |
| **Aim Spin** | Плавное вращение > 20°/тик (среднее) |
| **Jerk Analysis** | AI-aimbot (сглаживание по Безье, стабильное ускорение) |
| **DMA Subpixel** | Аппаратные эмуляторы мыши (Arduino, KMBox) |
| **Grid Snap Timing** | Triggerbot, стреляющий в сетку тика сервера |
| **Robotic Click Timing** | Идеальный ритм стрельбы (σ < 15 мс) |
| **Prefire** | Выстрел до входа врага в конус обзора |
| **Wallbang Proxy** | >30% попаданий без видимой цели |
| **Auto-Strafe Sync** | A/D синхронизированы с мышью с 0 мс реакции |
| **HS-Ratio** | >90% хедшотов после 12+ киллов |

#### 🧨 HvH модули (мгновенный бан)

| Модуль | Что детектит |
|---|---|
| **Pitch Anti-Aim** | Pitch вне `[-89°; 89°]` — невозможно без чита |
| **Spinbot / Jitter** | Δyaw > 140°/тик × 3+ тика |
| **Tickbase / DoubleTap** | >140 uscmd/сек |

#### ⚙️ Инфраструктура

- ✅ **JSON бан-лист** — переживает рестарт сервера (в отличие от нативного `banid`)
- ✅ **Discord-вебхук** — каждый детект логируется с SteamID, деталями, временем
- ✅ **Внутриигровое меню админа** (`!ac`) — пороги, вайтлист, кик/бан игроков
- ✅ **Вайтлист + SuperAdmins** — из конфига, без перекомпиляции
- ✅ **Zero-alloc в тике** — никакого давления на GC даже при 64 игроках
- ✅ **Автобан с очками подозрения** — спад со временем, без ложных срабатываний

### 🚀 Быстрый старт

#### 1. Установи CounterStrikeSharp
Официальный гайд: https://docs.cssharp.dev/

#### 2. Скачай плагин

Открой страницу [**Releases**](https://github.com/ChmonyaStudio/cs2-anticheat-by-chmonya/releases/latest) и скачай:

- `CSAntiCheat-vX.Y.Z.zip` — скомпилированный плагин, готов к установке
- `anticheat_config.example.json` — шаблон конфига

#### 3. Установи

Распакуй `.zip` в:
```
game/csgo/addons/counterstrikesharp/plugins/CSAntiCheat/
```

Должна получиться структура:
```
plugins/
└── CSAntiCheat/
    ├── CSAntiCheat.dll
    └── (зависимости, если есть)
```

Перезапусти сервер.

#### 4. Настрой

При первом запуске автоматически создаются `anticheat_config.json` и `anticheat_bans.json`. Открой конфиг:

```json
{
  "WebhookUrl": "https://discord.com/api/webhooks/ТВОЙ_ВЕБХУК",
  "ServerName": "Мой CS2 сервер",
  "Whitelist": ["76561198737716015"],
  "SuperAdmins": ["76561198737716015"]
}
```

#### 5. Проверь

Зайди на сервер и напиши `!ac` в чат. Должно открыться меню.

### 🎮 Команды админа

| Команда | Что делает |
|---|---|
| `!ac` / `!anticheat` | Открыть меню античита |
| `!rules` | Показать чек-лист проверки |

### 🔧 Справочник настроек

Ключевые пороги (полный список — в конфиге):

| Параметр | Дефолт | Описание |
|---|---|---|
| `MaxSpeed` | 6.0 | Порог скорости на земле (м/с) |
| `SuspicionToBan` | 25 | Очков для автобана |
| `SuspicionDecaySeconds` | 60 | Интервал спада очков |
| `MaxAngleDeltaPerTick` | 55 | Порог aim-snap (°/тик) |
| `JerkConsecutiveTicks` | 4 | Порог детекта AI-aimbot |
| `PrefireWindowMs` | 150 | Окно детекта prefire |
| `PitchBanConsecutiveTicks` | 2 | Порог бана по HvH Pitch |
| `SpinbotMinYawDelta` | 140 | Порог spinbot (°/тик) |

### 🧠 Как это работает

Плагин работает **каждый тик сервера** (~64 раза/сек) и анализирует:

1. **Скорость игрока** — суммы квадратов (без sqrt в горячем пути)
2. **Углы обзора** — через рефлексию, работает на всех версиях CSS
3. **Кнопки usercmd** — для детекта синхронизации стрейфов
4. **Интервалы выстрелов** — для детекта triggerbot
5. **Отпечаток памяти** — для DMA-субпиксельных паттернов

Каждое нарушение даёт **очки подозрения**. При достижении порога → автобан + вебхук + запись в JSON.

Чтобы избежать ложных срабатываний:
- **Фильтр "N тиков подряд"** — игнорирует одиночные всплески
- **Пропуск разминки / фриза** — нет детекта в warmup
- **Вайтлист** — админы, VIP, разработчики не банятся
- **Спад** — честные игроки не накапливают очки

### 🛠️ Требования

- **CounterStrikeSharp** 1.0.300+
- **.NET 8.0** runtime
- Windows или Linux сервер

### 📋 Changelog

Полная история версий: [CHANGELOG.md](CHANGELOG.md)

### 🐛 Известные ограничения

- Паттерн-скан для stream-proof может отличаться между билдами CS2
- Wallbang proxy использует **эвристику по углам**, а не raycast (для производительности)
- Voice / sound читы не покрываются

### 🤝 Вклад

PR приветствуются. Для новых модулей детекта — сначала открывай issue.

### 📜 Лицензия

MIT — см. [LICENSE](LICENSE).

### 💬 Контакты

- **GitHub Issues:** [сообщить о баге](https://github.com/ChmonyaStudio/cs2-anticheat-by-chmonya/issues)
- **Telegram:** [@p1zdabol4ik](https://t.me/p1zdabol4ik)
- **Сайт:** [chmonya.ct.ws](https://chmonya.ct.ws)

---

**Made with ❤️ for the CS2 community. / Сделано с ❤️ для CS2-сообщества.**
