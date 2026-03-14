using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using InfluencerMatch.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InfluencerMatch.Infrastructure.Services
{
    /// <summary>
    /// LLM text generation backed by the Groq Inference API (free tier).
    ///
    /// Model: llama-3.1-8b-instant
    /// Free tier: 14 400 req/day, 30 RPM, 131 072 tokens/min
    /// Endpoint: https://api.groq.com/openai/v1/chat/completions
    ///
    /// Configure: Groq:ApiKey (or GROQ_API_KEY environment variable).
    /// All methods return null / empty list gracefully when the key is absent.
    /// </summary>
    public class GroqLlmService : IGroqLlmService
    {
        private readonly IHttpClientFactory _http;
        private readonly ILogger<GroqLlmService> _logger;
        private readonly string? _apiKey;

        private const string GroqUrl = "https://api.groq.com/openai/v1/chat/completions";
        private const string Model   = "llama-3.1-8b-instant";

        public GroqLlmService(
            IHttpClientFactory http,
            IConfiguration config,
            ILogger<GroqLlmService> logger)
        {
            _http   = http;
            _logger = logger;
            _apiKey = config["Groq:ApiKey"]
                   ?? config["Groq__ApiKey"]
                   ?? config["GROQ_API_KEY"];
        }

        // ── Core call ─────────────────────────────────────────────────────────

        public async Task<string?> GenerateTextAsync(string systemPrompt, string userPrompt, int maxTokens = 400)
        {
            if (string.IsNullOrWhiteSpace(_apiKey)) return null;
            try
            {
                var client = _http.CreateClient("Groq");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                client.Timeout = TimeSpan.FromSeconds(30);

                var body = new
                {
                    model    = Model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user",   content = userPrompt   }
                    },
                    max_tokens  = maxTokens,
                    temperature = 0.35
                };

                var response = await client.PostAsJsonAsync(GroqUrl, body);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Groq API returned {Status}", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()?.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Groq LLM request failed");
                return null;
            }
        }

        // ── Creator coaching tips ─────────────────────────────────────────────

        public async Task<List<string>> GenerateCreatorCoachingTipsAsync(
            string channelName, string category,
            double avgViews, double engagementRate, string bestPostingWindow)
        {
            const string system =
                "You are a YouTube growth strategist. " +
                "Reply with ONLY a JSON array of exactly 4 short, actionable coaching tip strings. " +
                "No preamble, no markdown, no keys — just the array.";

            var user =
                $"Channel: \"{channelName}\", Category: \"{category}\", " +
                $"Avg Views: {avgViews:N0}, Engagement Rate: {engagementRate * 100:F1}%, " +
                $"Best Posting Window: {bestPostingWindow}. " +
                "Give 4 specific weekly actions to grow this channel.";

            var raw = await GenerateTextAsync(system, user, 320);
            return ParseJsonArrayOrLines(raw);
        }

        // ── Campaign–creator match explanation ────────────────────────────────

        public async Task<string?> ExplainCreatorBrandMatchAsync(
            string campaignCategory, string campaignLocation,
            string creatorCategory, string creatorCountry, double matchScore)
        {
            const string system =
                "You are an influencer marketing analyst. " +
                "Write exactly one sentence (≤ 25 words) explaining why this creator-brand pairing is or isn't a strong fit.";

            var user =
                $"Campaign: {campaignCategory} brand targeting {campaignLocation}. " +
                $"Creator: {creatorCategory} content, {creatorCountry} audience. " +
                $"Match score: {matchScore:F0}/100.";

            return await GenerateTextAsync(system, user, 80);
        }

        // ── Creator marketplace summary ───────────────────────────────────────

        public async Task<string?> SummarizeCreatorForBrandAsync(
            string channelName, string category,
            long subscribers, double engagementRate, string? topBrands)
        {
            const string system =
                "You are a creator marketplace copywriter. " +
                "Write exactly 2 punchy professional sentences describing this creator for a brand sponsor.";

            var brandsNote = string.IsNullOrWhiteSpace(topBrands) ? "" : $" Known brand associations: {topBrands}.";
            var user =
                $"Channel: \"{channelName}\", Category: {category}, " +
                $"{subscribers:N0} subscribers, {engagementRate * 100:F1}% engagement rate.{brandsNote}";

            return await GenerateTextAsync(system, user, 120);
        }

        // ── Video summary ─────────────────────────────────────────────────────

        public async Task<string?> SummarizeVideoAsync(string title, string description)
        {
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description)) return null;

            const string system =
                "You are a YouTube content analyst. " +
                "Write one clear, informative sentence summarising what the video is about.";

            var descSnippet = string.IsNullOrWhiteSpace(description)
                ? ""
                : " Description excerpt: " + description[..Math.Min(description.Length, 400)];

            var user = $"Video title: \"{title}\".{descSnippet}";
            return await GenerateTextAsync(system, user, 80);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static List<string> ParseJsonArrayOrLines(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new();
            try
            {
                var start = raw.IndexOf('[');
                var end   = raw.LastIndexOf(']');
                if (start >= 0 && end > start)
                {
                    var json = raw[start..(end + 1)];
                    var parsed = JsonSerializer.Deserialize<List<string>>(json);
                    if (parsed is { Count: > 0 }) return parsed;
                }
            }
            catch { /* fall through to line splitting */ }

            return raw
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim().TrimStart('-', '*', '1', '2', '3', '4', '.', ' '))
                .Where(l => l.Length > 10)
                .Take(4)
                .ToList();
        }
    }
}
