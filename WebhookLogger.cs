using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace CSAntiCheat;

public class WebhookLogger
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly string _url;
    private readonly string _username;

    public WebhookLogger(string url, string username)
    {
        _url = url?.Trim() ?? "";
        _username = string.IsNullOrWhiteSpace(username) ? "AntiCheat" : username;
    }

    /// <summary>
    /// Sends an embed to the configured Discord webhook.
    /// Silently returns if URL is empty or placeholder.
    /// </summary>
    public void Send(string title, string description, int color = 0xFFAA00, List<(string, string)>? fields = null)
    {
        // Guard: config toggle
        var cfg = AntiCheatPlugin.Instance?.Config;
        if (cfg == null || !cfg.WebhookEnabled) return;

        // Guard: URL not set or still placeholder
        if (string.IsNullOrWhiteSpace(_url)) return;
        if (_url.Contains("YOUR_WEBHOOK_HERE")) return;
        if (!_url.StartsWith("https://discord.com/api/webhooks/") &&
            !_url.StartsWith("https://discordapp.com/api/webhooks/"))
            return;

        // Build embed
        var embed = new Dictionary<string, object>
        {
            ["title"] = title,
            ["description"] = description,
            ["color"] = color,
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["footer"] = new { text = "AntiCheat" }
        };

        if (fields != null && fields.Count > 0)
        {
            embed["fields"] = fields
                .Select(f => new
                {
                    name = Truncate(f.Item1, 256),
                    value = Truncate(f.Item2, 1024),
                    inline = true
                })
                .ToArray();
        }

        var payload = new
        {
            username = _username,
            embeds = new[] { embed }
        };

        string json;
        try
        {
            json = JsonSerializer.Serialize(payload);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AntiCheat Webhook] Serialize error: {ex.Message}");
            return;
        }

        // Fire and forget
        _ = Task.Run(async () =>
        {
            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var resp = await _http.PostAsync(_url, content);

                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[AntiCheat Webhook] HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}");
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("[AntiCheat Webhook] Timeout");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AntiCheat Webhook] {ex.Message}");
            }
        });
    }

    // ============================================================
    // HELPERS
    // ============================================================
    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s.Substring(0, max - 3) + "...";
    }
}
