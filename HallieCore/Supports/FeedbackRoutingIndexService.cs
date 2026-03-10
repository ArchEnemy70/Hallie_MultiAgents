using Qdrant.Client.Grpc;
using System.Text.Json;

namespace Hallie.Services
{
    /// <summary>
    /// Index vectoriel (Qdrant) pour retrouver des prompts similaires à partir du feedback.
    /// Collection dédiée : FeedbackRouting
    ///
    /// </summary>
    public sealed class FeedbackRoutingIndexService
    {
        private const int EmbeddingDim = 768;
        private const Distance DefaultDistance = Distance.Cosine;

        private readonly Qdrant.Client.QdrantClient _client;
        private readonly IEmbeddingService _embedding;
        private readonly string _collectionName;

        public FeedbackRoutingIndexService(IEmbeddingService embeddingService, string qdrantUrl)
        {
            _embedding = embeddingService;
            _collectionName = "FeedbackRouting";
            _client = new Qdrant.Client.QdrantClient(new Uri(qdrantUrl));
        }

        public async Task EnsureCollectionAsync(CancellationToken ct = default)
        {
            var existing = await _client.ListCollectionsAsync();
            if (!existing.Contains(_collectionName))
            {
                await _client.CreateCollectionAsync(_collectionName, new VectorParams
                {
                    Size = (ulong)EmbeddingDim,
                    Distance = DefaultDistance
                });
            }
        }

        public async Task UpsertAsync(Guid feedbackId, string promptText, object payload, CancellationToken ct = default)
        {
            await EnsureCollectionAsync(ct);
            var vector = await _embedding.GenerateEmbeddingAsync(promptText);

            var vectorObj = new Qdrant.Client.Grpc.Vector();
            var dense = new Qdrant.Client.Grpc.DenseVector();
            dense.Data.AddRange(vector);
            vectorObj.Dense = dense;

            var json = JsonSerializer.Serialize(payload);
            using var doc = JsonDocument.Parse(json);

            static Value ToValue(JsonElement el)
            {
                return el.ValueKind switch
                {
                    JsonValueKind.String => new Value { StringValue = el.GetString() ?? "" },
                    JsonValueKind.Number => el.TryGetInt64(out var l)
                        ? new Value { IntegerValue = l }
                        : new Value { DoubleValue = el.GetDouble() },
                    JsonValueKind.True => new Value { BoolValue = true },
                    JsonValueKind.False => new Value { BoolValue = false },
                    JsonValueKind.Object => new Value { StringValue = el.GetRawText() },
                    JsonValueKind.Array => new Value { StringValue = el.GetRawText() },
                    _ => new Value { StringValue = el.ToString() }
                };
            }

            var point = new PointStruct
            {
                Id = new PointId { Uuid = feedbackId.ToString() },
                Vectors = new Vectors { Vector = vectorObj },
                Payload = { doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => ToValue(p.Value)) }
            };

            // API QdrantClient est déjà utilisée ailleurs dans le projet sans options avancées.
            await _client.UpsertAsync(_collectionName, new[] { point });
        }

        public async Task<List<(Guid Id, float Score)>> SearchSimilarAsync(string promptText, int topK, float minScore, CancellationToken ct = default)
        {
            await EnsureCollectionAsync(ct);
            var vector = await _embedding.GenerateEmbeddingAsync(promptText);

            // Pas de filtre : on veut la similarité brute sur la collection.
            var results = await _client.SearchAsync(_collectionName, vector, filter: null, limit: (ulong)topK);

            var hits = new List<(Guid, float)>();
            foreach (var r in results)
            {
                if (r.Score < minScore) continue;
                if (r.Id?.Uuid is null) continue;
                if (!Guid.TryParse(r.Id.Uuid, out var id)) continue;
                hits.Add((id, r.Score));
            }

            return hits;
        }
    }
}
