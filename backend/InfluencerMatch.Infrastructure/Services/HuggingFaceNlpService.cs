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
    /// Multi-task NLP service backed by the HuggingFace Inference API (free tier).
    ///
    /// Models used:
    ///   • sentence-transformers/all-MiniLM-L6-v2   — 384-dim embeddings
    ///   • Jean-Baptiste/roberta-large-ner-english   — Named Entity Recognition
    ///   • j-hartmann/emotion-english-distilroberta-base — 7-class emotion detection
    ///   • facebook/bart-large-mnli                  — Zero-shot classification
    ///
    /// All calls degrade gracefully: if the API token is absent or a request fails,
    /// the method returns an empty/default result without throwing.
    /// </summary>
    public class HuggingFaceNlpService : IHuggingFaceNlpService
    {
        private readonly IHttpClientFactory _http;
        private readonly ILogger<HuggingFaceNlpService> _logger;
        private readonly string? _apiToken;

        private const string BaseUrl     = "https://router.huggingface.co/hf-inference/models/";
        private const string EmbedModel  = "sentence-transformers/all-MiniLM-L6-v2";
        private const string NerModel    = "Jean-Baptiste/roberta-large-ner-english";
        private const string EmotionModel = "j-hartmann/emotion-english-distilroberta-base";
        private const string ZeroShotModel = "facebook/bart-large-mnli";

        public HuggingFaceNlpService(
            IHttpClientFactory http,
            IConfiguration config,
            ILogger<HuggingFaceNlpService> logger)
        {
            _http     = http;
            _logger   = logger;
            _apiToken = config["HuggingFace:ApiToken"]
                     ?? config["HuggingFace__ApiToken"]
                     ?? config["HUGGINGFACE_API_TOKEN"];
        }

        // ── Embeddings ────────────────────────────────────────────────────────

        public async Task<float[]?> GetEmbeddingAsync(string text)
        {
            var batch = await GetEmbeddingsBatchAsync(new[] { text });
            return batch.Count > 0 ? batch[0] : null;
        }

        public async Task<List<float[]>> GetEmbeddingsBatchAsync(IList<string> texts)
        {
            if (!IsConfigured() || texts.Count == 0) return new();
            try
            {
                var inputs = texts.Select(t => t.Length > 512 ? t[..512] : t).ToList();
                var payload = new { inputs };
                var response = await BuildClient().PostAsJsonAsync(BaseUrl + EmbedModel, payload);
                if (!response.IsSuccessStatusCode) return new();

                var json = await response.Content.ReadAsStringAsync();
                var outer = JsonSerializer.Deserialize<List<List<float>>>(json);
                return outer?.Select(v => v.ToArray()).ToList<float[]>() ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HuggingFace batch embedding failed");
                return new();
            }
        }

        // ── Named Entity Recognition ─────────────────────────────────────────

        public async Task<List<NerEntity>> RecognizeEntitiesAsync(string text)
        {
            if (!IsConfigured() || string.IsNullOrWhiteSpace(text)) return new();
            try
            {
                var input = text.Length > 512 ? text[..512] : text;
                var payload = new
                {
                    inputs = input,
                    parameters = new { aggregation_strategy = "simple" }
                };
                var response = await BuildClient().PostAsJsonAsync(BaseUrl + NerModel, payload);
                if (!response.IsSuccessStatusCode) return new();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return new();

                var result = new List<NerEntity>();
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var entityGroup = item.TryGetProperty("entity_group", out var eg) ? eg.GetString() ?? "" : "";
                    var word        = item.TryGetProperty("word",         out var w)  ? w.GetString()  ?? "" : "";
                    var score       = item.TryGetProperty("score",        out var s)  ? s.GetDouble()       : 0.0;
                    if (!string.IsNullOrWhiteSpace(entityGroup) && !string.IsNullOrWhiteSpace(word))
                        result.Add(new NerEntity(entityGroup, word.Trim(), Math.Round(score, 3)));
                }
                // Only return high-confidence entities
                return result.Where(e => e.Score >= 0.70).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HuggingFace NER failed");
                return new();
            }
        }

        // ── Emotion Detection ─────────────────────────────────────────────────

        public async Task<EmotionDetectionResult> DetectEmotionsAsync(IEnumerable<string> texts)
        {
            if (!IsConfigured())
                return new EmotionDetectionResult(false, "neutral", new(), 0, "model_not_configured");

            var textList = texts
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Take(20)
                .ToList();

            if (textList.Count < 3)
                return new EmotionDetectionResult(false, "neutral", new(), textList.Count, "insufficient_sample");

            var scores     = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var evaluated  = 0;
            var client     = BuildClient();

            foreach (var text in textList)
            {
                try
                {
                    var input   = text.Length > 256 ? text[..256] : text;
                    var payload = new { inputs = input };
                    var response = await client.PostAsJsonAsync(BaseUrl + EmotionModel, payload);
                    if (!response.IsSuccessStatusCode) continue;

                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    // Response shape is either [[{label,score},...]] or [{label,score},...]
                    var root  = doc.RootElement;
                    JsonElement items;
                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        var first = root[0];
                        items = first.ValueKind == JsonValueKind.Array ? first : root;
                    }
                    else continue;

                    foreach (var item in items.EnumerateArray())
                    {
                        if (item.TryGetProperty("label", out var lbl) && item.TryGetProperty("score", out var sc))
                        {
                            var label = lbl.GetString() ?? "";
                            if (!scores.ContainsKey(label)) scores[label] = 0;
                            scores[label] += sc.GetDouble();
                        }
                    }
                    evaluated++;
                }
                catch { /* skip individual failures silently */ }
            }

            if (evaluated == 0)
                return new EmotionDetectionResult(false, "neutral", scores, 0, "all_requests_failed");

            foreach (var key in scores.Keys.ToList())
                scores[key] = Math.Round(scores[key] / evaluated, 4);

            var dominant = scores.OrderByDescending(kv => kv.Value).FirstOrDefault().Key ?? "neutral";
            return new EmotionDetectionResult(true, dominant, scores, evaluated);
        }

        // ── Zero-Shot Classification ──────────────────────────────────────────

        public async Task<ZeroShotResult> ClassifyZeroShotAsync(string text, string[] candidateLabels)
        {
            if (!IsConfigured() || string.IsNullOrWhiteSpace(text) || candidateLabels.Length == 0)
                return new ZeroShotResult(false, "", new());
            try
            {
                var input   = text.Length > 512 ? text[..512] : text;
                var payload = new
                {
                    inputs     = input,
                    parameters = new { candidate_labels = candidateLabels, multi_label = false }
                };
                var response = await BuildClient().PostAsJsonAsync(BaseUrl + ZeroShotModel, payload);
                if (!response.IsSuccessStatusCode) return new ZeroShotResult(false, "", new());

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var labels    = doc.RootElement.TryGetProperty("labels", out var l) ?
                    l.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : new List<string>();
                var rawScores = doc.RootElement.TryGetProperty("scores", out var s) ?
                    s.EnumerateArray().Select(x => x.GetDouble()).ToList() : new List<double>();

                var resultScores  = labels.Zip(rawScores).ToDictionary(x => x.First, x => Math.Round(x.Second, 4));
                var topLabel      = resultScores.OrderByDescending(kv => kv.Value).FirstOrDefault().Key ?? "";
                return new ZeroShotResult(!string.IsNullOrEmpty(topLabel), topLabel, resultScores);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HuggingFace zero-shot classification failed");
                return new ZeroShotResult(false, "", new());
            }
        }

        // ── Cosine Similarity ─────────────────────────────────────────────────

        public float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length || a.Length == 0) return 0f;
            double dot = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot  += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            return normA == 0 || normB == 0 ? 0f : (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private bool IsConfigured() => !string.IsNullOrWhiteSpace(_apiToken);

        private HttpClient BuildClient()
        {
            var client = _http.CreateClient("HuggingFaceNlp");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
            client.Timeout = TimeSpan.FromSeconds(45);
            return client;
        }
    }
}
