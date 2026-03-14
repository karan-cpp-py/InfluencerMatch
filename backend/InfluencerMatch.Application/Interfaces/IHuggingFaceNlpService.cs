using System.Collections.Generic;
using System.Threading.Tasks;

namespace InfluencerMatch.Application.Interfaces
{
    public record NerEntity(string EntityGroup, string Word, double Score);

    public record EmotionDetectionResult(
        bool Succeeded,
        string DominantEmotion,
        Dictionary<string, double> Scores,
        int EvaluatedCount,
        string? Note = null);

    public record ZeroShotResult(
        bool Succeeded,
        string TopLabel,
        Dictionary<string, double> Scores);

    public interface IHuggingFaceNlpService
    {
        /// <summary>Generates a 384-dim embedding via sentence-transformers/all-MiniLM-L6-v2.</summary>
        Task<float[]?> GetEmbeddingAsync(string text);

        /// <summary>Batch-embed multiple texts in a single API call. Returns one vector per text.</summary>
        Task<List<float[]>> GetEmbeddingsBatchAsync(IList<string> texts);

        /// <summary>NER via Jean-Baptiste/roberta-large-ner-english. Returns entities with score > 0.7.</summary>
        Task<List<NerEntity>> RecognizeEntitiesAsync(string text);

        /// <summary>Emotion detection via j-hartmann/emotion-english-distilroberta-base on up to 20 comment samples.</summary>
        Task<EmotionDetectionResult> DetectEmotionsAsync(IEnumerable<string> texts);

        /// <summary>Zero-shot classification via facebook/bart-large-mnli.</summary>
        Task<ZeroShotResult> ClassifyZeroShotAsync(string text, string[] candidateLabels);

        /// <summary>Cosine similarity between two equal-length float vectors.</summary>
        float CosineSimilarity(float[] a, float[] b);
    }
}
