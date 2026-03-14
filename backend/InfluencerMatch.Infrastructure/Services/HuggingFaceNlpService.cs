using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
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
        private readonly string _apiBaseUrl;
        private readonly string _embedModel;
        private readonly string _nerModel;
        private readonly string _emotionModel;
        private readonly string _zeroShotModel;

        private const string DefaultBaseUrl = "https://api-inference.huggingface.co/models/";
        private const string DefaultEmbedModel = "sentence-transformers/all-MiniLM-L6-v2";
        private const string DefaultNerModel = "Jean-Baptiste/roberta-large-ner-english";
        private const string DefaultEmotionModel = "j-hartmann/emotion-english-distilroberta-base";
        private const string DefaultZeroShotModel = "facebook/bart-large-mnli";

        public HuggingFaceNlpService(
            IHttpClientFactory http,
            IConfiguration config,
            ILogger<HuggingFaceNlpService> logger)
        {
            _http     = http;
            _logger   = logger;
            _apiToken = config["HuggingFace:ApiToken"]
                     ?? config["HuggingFace__ApiToken"]
                     ?? config["HUGGINGFACE_API_TOKEN"]
                     ?? config["HF_TOKEN"]
                     ?? config["HF_API_TOKEN"]
                     ?? config["HUGGING_FACE_HUB_TOKEN"];

            _apiBaseUrl = NormalizeBaseUrl(config["HuggingFace:InferenceBaseUrl"]
                ?? config["HuggingFace__InferenceBaseUrl"]
                ?? config["HUGGINGFACE_INFERENCE_BASE_URL"]
                ?? DefaultBaseUrl);

            _embedModel = config["HuggingFace:EmbeddingModel"]
                ?? config["HuggingFace__EmbeddingModel"]
                ?? DefaultEmbedModel;

            _nerModel = config["HuggingFace:NerModel"]
                ?? config["HuggingFace__NerModel"]
                ?? DefaultNerModel;

            _emotionModel = config["HuggingFace:EmotionModel"]
                ?? config["HuggingFace__EmotionModel"]
                ?? DefaultEmotionModel;

            _zeroShotModel = config["HuggingFace:ZeroShotModel"]
                ?? config["HuggingFace__ZeroShotModel"]
                ?? DefaultZeroShotModel;
        }

        // ── Embeddings ────────────────────────────────────────────────────────

        public async Task<float[]?> GetEmbeddingAsync(string text)
        {
            var batch = await GetEmbeddingsBatchAsync(new[] { text });
            return batch.Count > 0 ? batch[0] : null;
        }

        public async Task<List<float[]>> GetEmbeddingsBatchAsync(IList<string> texts)
        {
            if (texts.Count == 0) return new();
            try
            {
                var inputs = texts.Select(t => t.Length > 512 ? t[..512] : t).ToList();
                var payload = new { inputs };
                var response = await SendModelRequestAsync(BuildClient(), _embedModel, payload);
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
            if (string.IsNullOrWhiteSpace(text)) return new();
            try
            {
                var input = NormalizeForNer(text);
                if (string.IsNullOrWhiteSpace(input)) return new();
                var payload = new
                {
                    inputs = input,
                    parameters = new { aggregation_strategy = "simple" }
                };
                var response = await SendModelRequestAsync(BuildClient(), _nerModel, payload);
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
            var textList = texts
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Length > 256 ? t[..256] : t)
                .Take(40)
                .ToList();

            if (textList.Count < 3)
                return new EmotionDetectionResult(false, "neutral", new(), textList.Count, "insufficient_sample");

            var scores     = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var evaluated  = 0;
            var client     = BuildClient();

            try
            {
                var payload = new
                {
                    inputs = textList,
                    options = new { wait_for_model = true, use_cache = true }
                };
                var response = await SendModelRequestAsync(client, _emotionModel, payload);
                if (!response.IsSuccessStatusCode)
                {
                    var status = (int)response.StatusCode;
                    return new EmotionDetectionResult(false, "neutral", new(), 0,
                        string.IsNullOrWhiteSpace(_apiToken)
                            ? $"all_requests_failed_http_{status}_token_missing"
                            : $"all_requests_failed_http_{status}");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    // Batched output: [[{label,score}], [{label,score}], ...]
                    if (root[0].ValueKind == JsonValueKind.Array)
                    {
                        foreach (var perText in root.EnumerateArray())
                        {
                            var sawLabel = false;
                            foreach (var item in perText.EnumerateArray())
                            {
                                if (!item.TryGetProperty("label", out var lbl) || !item.TryGetProperty("score", out var sc))
                                    continue;

                                var label = lbl.GetString() ?? string.Empty;
                                if (!scores.ContainsKey(label)) scores[label] = 0;
                                scores[label] += sc.GetDouble();
                                sawLabel = true;
                            }
                            if (sawLabel) evaluated++;
                        }
                    }
                    // Single-text output: [{label,score}, ...]
                    else
                    {
                        foreach (var item in root.EnumerateArray())
                        {
                            if (!item.TryGetProperty("label", out var lbl) || !item.TryGetProperty("score", out var sc))
                                continue;

                            var label = lbl.GetString() ?? string.Empty;
                            if (!scores.ContainsKey(label)) scores[label] = 0;
                            scores[label] += sc.GetDouble();
                        }
                        evaluated = 1;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HuggingFace emotion detection failed");
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
            if (string.IsNullOrWhiteSpace(text) || candidateLabels.Length == 0)
                return new ZeroShotResult(false, "", new());
            try
            {
                var input   = text.Length > 512 ? text[..512] : text;
                var payload = new
                {
                    inputs     = input,
                    parameters = new { candidate_labels = candidateLabels, multi_label = false }
                };
                var response = await SendModelRequestAsync(BuildClient(), _zeroShotModel, payload);
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

        private static string NormalizeBaseUrl(string value)
            => value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";

        private static string NormalizeForNer(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var cleaned = Regex.Replace(text, @"https?://\S+", " ", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"[\r\n\t]+", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned.Length > 700 ? cleaned[..700] : cleaned;
        }

        private async Task<HttpResponseMessage> SendModelRequestAsync(HttpClient client, string model, object payload)
        {
            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _apiBaseUrl + Uri.EscapeDataString(model))
                {
                    Content = JsonContent.Create(payload)
                };

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                if ((response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.ServiceUnavailable)
                    && attempt < maxAttempts)
                {
                    await Task.Delay(500 * attempt);
                    response.Dispose();
                    continue;
                }

                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }

        private HttpClient BuildClient()
        {
            var client = _http.CreateClient("HuggingFaceNlp");
            if (!string.IsNullOrWhiteSpace(_apiToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(45);
            return client;
        }
    }
}
