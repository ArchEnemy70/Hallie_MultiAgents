using ExternalServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Qdrant.Client.Grpc;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Match = Qdrant.Client.Grpc.Match;
using HallieDomain;

namespace Hallie.Services
{
    #region Interface IEmbeddingService
    public interface IEmbeddingService
    {
        Task<float[]> GenerateEmbeddingAsync(string text);
    }
    #endregion

    #region OllamaEmbeddingService qui implémente IEmbeddingService
    public class OllamaEmbeddingService : IEmbeddingService
    {
        #region Variables
        private readonly HttpClient _http;
        private readonly string _model;
        #endregion

        #region Constructeur
        public OllamaEmbeddingService(string ollamaUrl, string model)
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(ollamaUrl)
            };
            _model = model;
        }
        #endregion

        #region Méthodes publiques
        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            string? content = "";
            try
            {
                var request = new
                {
                    model = _model,
                    prompt = text
                };

                var response = await _http.PostAsJsonAsync("/api/embeddings", request);
                content = await response.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(content);

                var embedding = json.RootElement
                    .GetProperty("embedding")
                    .EnumerateArray()
                    .Select(x => (float)x.GetDouble())
                    .ToArray();

                return embedding;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"OllamaEmbeddingService.GenerateEmbeddingAsync --> Erreur: {ex.Message}  /  {content}");
                throw;
            }
        }
        #endregion
    }
    #endregion

    #region Domain et gestion des domaines
    public enum eDomain
    {
        RH,
        Juridique,
        Global,
        Emails,
        CV,
        Technique,
        Compta,
        Notes,
        FeedbackRouting
    }
    public static class DomainExtensions
    {
        private static readonly string _CollectionRH = "RH";
        private static readonly string _CollectionJuridique = "Juridique";
        private static readonly string _CollectionGlobal = "Global";
        private static readonly string _CollectionEmails = "Emails";
        private static readonly string _CollectionCV = "CV";
        private static readonly string _CollectionTechnique = "Technique";
        private static readonly string _CollectionCompta = "Compta";
        private static readonly string _CollectionNotes = "Notes";
        private static readonly string _CollectionFeedbackRouting = "FeedbackRouting";

        public static List<string> CollectionsName = GetCollectionsName();
        public static List<string> CollectionsNameRestricted = GetCollectionsNameRestricted(false);

        private static List<string> GetCollectionsName()
        {
            List<string> strings = new()
            {
                _CollectionRH,
                _CollectionJuridique,
                _CollectionGlobal,
                _CollectionEmails,
                _CollectionCV,
                _CollectionTechnique,
                _CollectionCompta,
                _CollectionFeedbackRouting,
                _CollectionNotes
            };
            return strings;
        }

        private static List<string> GetCollectionsNameRestricted(bool isVeryRestricted)
        {
            List<string> strings = new();
            strings.Add(_CollectionGlobal);
            strings.Add(_CollectionTechnique);
            strings.Add(_CollectionCompta);

            if (!isVeryRestricted)
            {
                strings.Add(_CollectionJuridique);
                strings.Add(_CollectionRH);
            }
            return strings;
        }

        /// <summary>
        /// Retourne le nom de collection Qdrant associé à un domaine.
        /// </summary>
        public static string ToCollectionName(this eDomain domain) => domain switch
        {
            eDomain.RH => _CollectionRH,
            eDomain.Juridique => _CollectionJuridique,
            eDomain.Global => _CollectionGlobal,
            eDomain.Emails => _CollectionEmails,
            eDomain.CV => _CollectionCV,
            eDomain.Technique => _CollectionTechnique,
            eDomain.Compta => _CollectionCompta,
            //eDomain.Conversation => _CollectionConversation,
            eDomain.Notes => _CollectionNotes,
            _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null)
        };

        /// <summary>
        /// Retourne le nom de collection Qdrant associé à un domaine.
        /// </summary>
        public static eDomain GetDomainByCollectionName(string CollectionName)
        {
            eDomain domain = eDomain.Global;

            if (string.IsNullOrWhiteSpace(CollectionName))
                return domain;

            CollectionName = CollectionName.Trim().ToLowerInvariant();
            switch (CollectionName)
            {
                case "rh":
                    domain = eDomain.RH;
                    break;
                case "juridique":
                    domain = eDomain.Juridique;
                    break;
                case "global":
                    domain = eDomain.Global;
                    break;
                case "emails":
                    domain = eDomain.Emails;
                    break;
                case "cv":
                    domain = eDomain.CV;
                    break;
                case "technique":
                    domain = eDomain.Technique;
                    break;
                case "compta":
                    domain = eDomain.Compta;
                    break;
                case "feedbackrouting":
                    domain = eDomain.FeedbackRouting;
                    break;
                //case "conversation":
                //    domain = eDomain.Conversation;
                //    break;
                case "notes":
                    domain = eDomain.Notes;
                    break;
            }
            return domain;
        }

        public static eDomain GetDomainByPath(string filePath)
        {
            var dirs = Path.GetDirectoryName(filePath)?
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar,
                       StringSplitOptions.RemoveEmptyEntries);

            if (dirs == null)
                return eDomain.Global;

            var collections = GetCollectionsName();

            // On cherche la première correspondance
            var collectionName = dirs.FirstOrDefault(d =>
                collections.Any(c => string.Equals(c, d, StringComparison.OrdinalIgnoreCase)));

            return GetDomainByCollectionName(collectionName ?? "Global");
        }


    }
    #endregion

    #region QdrantService
    /// <summary>
    /// Service pour interagir avec Qdrant (base de données vectorielle)
    /// </summary>
    public class QdrantService
    {
        private const int EmbeddingDim = 768;
        private const Distance DefaultDistance = Distance.Cosine;

        #region Variables 
        private readonly Qdrant.Client.QdrantClient _client;
        private readonly string _Url;
        private readonly List<string> _collectionsName;
        private readonly IEmbeddingService _embeddingService;
        #endregion

        #region Constructeur
        public QdrantService(IEmbeddingService embeddingService, string url, string collectionsName)
        {
            _Url = url;
            _collectionsName = collectionsName.Split(',').ToList();
            _embeddingService = embeddingService;
            _client = new Qdrant.Client.QdrantClient(new Uri($"{_Url}"));
        }
        #endregion

        #region Méthodes publiques
        /// <summary>
        /// Recherche des documents similaires par similarité vectorielle
        /// </summary>
        /// <param name="query">Texte de la requête</param>
        /// <param name="topK">Nombre de résultats à retourner</param>
        /// <param name="scoreThreshold">Score minimum (0-1)</param>
        /// <returns>Liste des résultats ordonnés par pertinence</returns>
        public async Task<(bool, List<SearchResult>)> SearchAsync(string query, string domain, int topK = 20, float scoreThreshold = 0.7f)
        {
            try
            {
                var resultList = new List<SearchResult>();
                // Si le domaine est précisé, on cherche d'abord dans ce domaine
                if (domain != "")
                {
                    var (b, r) = await SearchRagAsync(query, domain, topK, scoreThreshold);
                    if (!b)
                    {
                        return (false, []);
                    }
                    resultList.AddRange(r);
                }
                // Reranker avec BGE
                var ranked = BgeReranker.Rerank(query, resultList, topK: 5);

                // Si le domaine n'est pas précisé ou pas de résultat avec le domaine précisé, on cherche dans tous les domaines
                if (ranked.Count == 0)
                {
                    resultList.Clear();
                    foreach (var collectionName in _collectionsName)
                    {
                        var (b, r) = await SearchRagAsync(query, collectionName, topK, scoreThreshold);
                        if (!b)
                        {
                            return (false, []);
                        }
                        resultList.AddRange(r);
                    }

                    // Reranker avec BGE
                    ranked = BgeReranker.Rerank(query, resultList, topK: 5);
                }

                // Si scoreThreshold très bas, alors c'est que l'on veut la liste brute
                if (scoreThreshold <= 0.01f)
                    return (true, resultList);

                return (true, ranked);
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"QdrantService.SearchAsync : Erreur lors de la recherche: {ex.Message}");
                return (false, []);
            }
        }
        public async Task<(bool, List<SearchResult>)> SearchAsync(string query, eDomain domain, int topK = 20, float scoreThreshold = 0.7f)
        {
            try
            {
                var resultList = new List<SearchResult>();
                var collectionName = domain.ToCollectionName();

                var (b, r) = await SearchRagAsync(query, collectionName, topK, scoreThreshold);
                if (!b)
                {
                    return (false, []);
                }
                resultList.AddRange(r);


                // Reranker avec BGE
                var ranked = BgeReranker.Rerank(query, resultList, topK: 5);

                return (true, ranked);
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"[Qdrant] ✗ Erreur lors de la recherche: {ex.Message}");
                return (false, []);
            }
        }
        public async Task<bool> IngestDocumentAsync(eDomain domain, bool isRAZCollections, string content, string filename, string proprietaire = "", string objet = "", string deadline = "", string niveauAcces = "")
        {
            LoggerService.LogInfo("QdrantService.IngestDocument");

            try
            {
                if (System.IO.File.Exists(content))
                {
                    filename = System.IO.Path.GetFileName(content);
                    content = FilesService.ExtractText(filename);
                }
                else
                {
                    var fullName = System.IO.Path.Combine(Params.DocumentsPathToVectorize!, content);
                    if (System.IO.File.Exists(fullName))
                    {
                        filename = System.IO.Path.GetFileName(fullName);
                        content = FilesService.ExtractText(fullName);
                    }

                }

                if (filename.Trim() == "")
                    filename = $"{domain}_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}";

                var chunks = ChunkText(content);
                LoggerService.LogDebug("QdrantService.IngestDocument - Après ChunkText()");

                await InitializeCollectionsAsync(isRAZCollections);
                LoggerService.LogDebug("QdrantService.IngestDocument - Après InitializeCollectionsAsync()");

                for (int i = 0; i < chunks.Length; i++)
                {
                    var chunk = chunks[i];
                    LoggerService.LogDebug("QdrantService.IngestDocument - Avant GetEmbeddingAsync()");
                    (bool b, float[]? embedding) = await GetEmbeddingAsync(chunk);
                    LoggerService.LogDebug("QdrantService.IngestDocument - Après GetEmbeddingAsync()");

                    if (b && embedding != null)
                    {
                        var id = $"{System.IO.Path.GetFileName(filename)}_chunk_{i}";
                        await IngestChunck(domain, embedding, filename, id, chunk, objet, proprietaire, deadline, niveauAcces);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"QdrantService.IngestDocument : {ex.Message}");
                return false;
            }
        }
        /*
        public async Task<bool> IngestConversationAsync(string convId, string question, string reponse, string tool, string proprietaire)
        {
            LoggerService.LogInfo("QdrantService.IngestConversation");
            eDomain domain = eDomain.Conversation;

            try
            {
                var chunks = ChunkText(question);
                LoggerService.LogDebug("QdrantService.IngestConversation - Après ChunkText()");

                await InitializeCollectionsAsync(false);
                LoggerService.LogDebug("QdrantService.IngestConversation - Après InitializeCollectionsAsync()");
                var dt = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                for (int i = 0; i < chunks.Length; i++)
                {
                    var chunk = chunks[i];
                    LoggerService.LogDebug("QdrantService.IngestConversation - Avant GetEmbeddingAsync()");
                    (bool b, float[]? embedding) = await GetEmbeddingAsync(chunk);
                    LoggerService.LogDebug("QdrantService.IngestConversation - Après GetEmbeddingAsync()");

                    if (b && embedding != null)
                    {
                        var idChunck = $"{dt}_chunk_{i}";
                        await IngestChunck(domain, convId, idChunck, embedding, question, reponse, tool, proprietaire);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"QdrantService.IngestConversation : {ex.Message}");
                return false;
            }
        }
        */
        /*
        public async Task<(bool, List<ConversationHistorique>?, List<ConversationHistorique>?)> FindHistoriqueSimilarRequest(string query)
        {
            var allJson = ConversationsService.LoadHistoriques();
            var (b, all) = await SearchAsync(query, eDomain.Conversation);

            List<ConversationHistorique> HistoGoodTool = new();
            List<ConversationHistorique> HistoBadTool = new();

            if (b)
            {
                var alls = all.ToList();
                foreach (var one in alls)
                {
                    var id = one.Id.Split(':')[1];
                    id = id.Replace("\"", "").Replace("}", "").Trim();
                    var convSimilGoodTool = allJson.Where(c => c.Id == id && c.IsGoodTool == true).ToList();
                    var convSimilBadTool = allJson.Where(c => c.Id == id && c.IsGoodTool == false).ToList();

                    if (convSimilGoodTool != null && convSimilGoodTool.Count() > 0)
                        HistoGoodTool.AddRange(convSimilGoodTool);

                    if (convSimilBadTool != null && convSimilBadTool.Count() > 0)
                        HistoBadTool.AddRange(convSimilBadTool);

                }

                return (b, HistoGoodTool, HistoBadTool);
            }
            return (false, null, null);
        }
        public async Task<(bool, string, string)> FindHistoriqueSimilarRequestJson(string query)
        {
            var (b, HistoGoodTool, HistoBadTool) = await FindHistoriqueSimilarRequest(query);

            if (b)
            {
                string jsonGoodTool = "";
                string jsonBadTool = "";
                if (HistoGoodTool != null && HistoGoodTool.Count > 0)
                    jsonGoodTool = JsonService.Serialize(HistoGoodTool!);
                if (HistoBadTool != null && HistoBadTool.Count > 0)
                    jsonBadTool = JsonService.Serialize(HistoBadTool!);
                return (b, jsonGoodTool, jsonBadTool);
            }
            return (false, "", "");
        }
        */
        #endregion

        #region Méthodes privées
        private async Task InitializeCollectionsAsync(bool isRAZCollections)
        {
            if (isRAZCollections)
            {
                LoggerService.LogDebug("QdrantService.InitializeCollectionsAsync - Suppression des collections existantes.");
                await DeleteCollectionAsync();
            }

            foreach (eDomain domain in Enum.GetValues(typeof(eDomain)))
            {
                LoggerService.LogDebug($"QdrantService.InitializeCollectionsAsync - EnsureCollectionAsync pour {domain.ToCollectionName()}");
                await EnsureCollectionAsync(domain);
            }
        }
        private async Task EnsureCollectionAsync(eDomain domain)
        {
            try
            {
                var collectionName = domain.ToCollectionName();
                var existing = await _client.ListCollectionsAsync();

                if (!existing.Contains(collectionName))
                {
                    LoggerService.LogDebug($"QdrantService.EnsureCollectionAsync - Avant Création de la collection {collectionName}");
                    await _client.CreateCollectionAsync(collectionName, new VectorParams
                    {
                        Size = (ulong)EmbeddingDim,
                        Distance = DefaultDistance
                    });
                    LoggerService.LogDebug($"QdrantService.EnsureCollectionAsync - Après Création de la collection {collectionName}");

                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"QdrantService.EnsureCollectionAsync - Erreur lors de la création de la collection : {ex.Message}");
            }
        }
        private async Task DeleteCollectionAsync()
        {
            var collections = await _client.ListCollectionsAsync();
            foreach (var col in collections)
            {
                await DeleteCollectionAsync(col);
            }
        }
        private async Task DeleteCollectionAsync(string collectionName)
        {
            await _client.DeleteCollectionAsync(collectionName);
        }
        private async Task<(bool, float[]?)> GetEmbeddingAsync(string text)
        {
            LoggerService.LogInfo($"QdrantService.GetEmbeddingAsync");

            try
            {
                var url = $"{Params.OllamaEmbeddingUrl}/api/embeddings";
                var model = Params.OllamaEmbeddingModel;//"nomic-embed-text:v1.5";

                var request = new
                {
                    model = model,
                    prompt = text,
                    stream = false,
                    keep_alive = "0m" // ne garde pas le modèle en mémoire
                };

                LoggerService.LogDebug($"embeddings Ollama : Model : {model} - URL : {url}");

                HttpClient httpClient = new();
                var json = JsonSerializer.Serialize(request);
                var response = await httpClient.PostAsync(url,
                    new StringContent(json, Encoding.UTF8, "application/json"));

                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(result);
                var retour = doc.RootElement
                    .GetProperty("embedding")
                    .EnumerateArray()
                    .Select(e => e.GetSingle())
                    .ToArray();
                return (true, retour);
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"QdrantService.GetEmbeddingAsync --> Erreur lors de la récupération de l'embedding : {ex.Message}");
                return (false, null);
            }

        }
        private async Task IngestChunck(eDomain domain, string poinId, string nomChunck, float[] vector, string question, string reponse, string tool, string proprietaire)
        {
            var pointId = new PointId { Uuid = poinId };
            var collectionName = domain.ToCollectionName();
            var vectorObj = new Qdrant.Client.Grpc.Vector();

            // Remplacement de l'utilisation obsolète de Vector.Data :
            // On crée et assigne une DenseVector, puis on ajoute les valeurs via AddRange.
            var dense = new Qdrant.Client.Grpc.DenseVector();
            dense.Data.AddRange(vector);
            vectorObj.Dense = dense;

            await _client.UpsertAsync(collectionName, new[]
            {
                new PointStruct
                {
                    Id = pointId,
                    Vectors = new Vectors { Vector = vectorObj },
                    Payload =
                    {
                        ["categorie"] = collectionName,
                        ["nom_chunck"] = nomChunck,
                        ["question"] = question,
                        ["reponse"] = reponse,
                        ["tool"] = tool,
                        ["proprietaire"] = proprietaire
                    }
                }
            });
        }
        private async Task IngestChunck(eDomain domain, float[] vector, string nomFichier, string nomChunck, string content, string infoCompl = "", string proprietaire = "", string deadline = "", string niveauAcces = "")
        {
            var objet = "";
            var clientId = "";

            if (domain == eDomain.Compta)
            {
                clientId = infoCompl;
            }
            else
            {
                objet = infoCompl;
            }

            var pointId = new PointId { Uuid = Guid.NewGuid().ToString() };
            var collectionName = domain.ToCollectionName();
            var vectorObj = new Qdrant.Client.Grpc.Vector();

            // On crée et assigne une DenseVector, puis on ajoute les valeurs via AddRange.
            var dense = new Qdrant.Client.Grpc.DenseVector();
            dense.Data.AddRange(vector);
            vectorObj.Dense = dense;

            await _client.UpsertAsync(collectionName, new[]
            {
                new PointStruct
                {
                    Id = pointId,
                    Vectors = new Vectors { Vector = vectorObj },
                    Payload =
                    {
                        ["categorie"] = collectionName,
                        ["nom_fichier"] = nomFichier,
                        ["client_id"] = clientId,
                        ["object"] = objet,
                        ["nom_chunck"] = nomChunck,
                        ["niveau_acces"] = niveauAcces,
                        ["content"] = content,
                        ["proprietaire"] = proprietaire,
                        ["deadline"] = deadline
                    }
                }
            });
        }
        private string[] ChunkText(string text, int chunkSize = 250, int overlap = 50)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var chunks = new List<string>();
            int start = 0;

            while (start < words.Length)
            {
                int length = Math.Min(chunkSize, words.Length - start);
                var chunk = string.Join(' ', words.Skip(start).Take(length));
                chunks.Add(chunk);
                start += chunkSize - overlap;
            }

            return chunks.ToArray();
        }
        private async Task<(bool, List<SearchResult>)> SearchRagAsync(string query, string collectionName, int topK = 20, float scoreThreshold = 0.7f)
        {
            try
            {
                LoggerService.LogInfo($"QdrantService.SearchRagAsync : '{query}' (top {topK}, seuil {scoreThreshold})");

                // 1. Générer l'embedding de la requête
                var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
                LoggerService.LogDebug($"QdrantService.SearchRagAsync : Embedding généré ({queryEmbedding.Length} dimensions)");

                Filter filter = new();
                if (DomainExtensions.GetDomainByCollectionName(collectionName) == eDomain.Emails)
                {
                    filter = new Qdrant.Client.Grpc.Filter
                    {
                        Must =
                        {
                            new Condition
                            {
                                Field = new FieldCondition
                                {
                                    Key = "categorie",
                                    Match = new Match { Text = collectionName }
                                }
                            },
                            new Condition
                            {
                                Field = new FieldCondition
                                {
                                    Key = "proprietaire",
                                    Match = new Match { Text = Params.SmtpUser }
                                }
                            }
                        }
                    };
                }
                else
                {
                    filter = new Qdrant.Client.Grpc.Filter
                    {
                        Must =
                        {
                            new Condition
                            {
                                Field = new FieldCondition
                                {
                                    Key = "categorie",
                                    Match = new Match { Text = collectionName }
                                }
                            }
                        }
                    };
                }

                var rep = await _client.SearchAsync(collectionName, queryEmbedding, filter, null, (ulong)topK);
                var filteredChunks = rep
                    .Where(r => r.Score >= scoreThreshold)
                    .ToList();

                if (filteredChunks.Count > 0)
                {
                    LoggerService.LogDebug($"QdrantService.SearchRagAsync --> {collectionName} : {filteredChunks.Count} éléments trouvés");
                }

                var resultList = new List<SearchResult>();

                if (collectionName == DomainExtensions.ToCollectionName(eDomain.Notes))
                {
                    resultList = filteredChunks.Select(r => new SearchResult
                    {
                        Id = r.Id.ToString(),
                        Score = r.Score,
                        CollectionName = collectionName,
                        Content = r.Payload["content"].StringValue,
                        Title = r.Payload["nom_fichier"].StringValue,
                        Source = r.Payload["nom_fichier"].StringValue,
                        Deadline = (r.Payload["deadline"] != null && r.Payload["deadline"].StringValue != "") ? DateTime.Parse(r.Payload["deadline"].StringValue) : null
                    }).ToList();
                }
                /*
                else if (collectionName == DomainExtensions.ToCollectionName(eDomain.Conversation))
                {
                    resultList = filteredChunks.Select(r => new SearchResult
                    {
                        Id = r.Id.ToString(),
                        Score = r.Score,
                        CollectionName = collectionName,
                        Content = r.Payload["reponse"].StringValue,
                        Title = r.Payload["question"].StringValue,
                        Source = r.Payload["tool"].StringValue,
                    }).ToList();
                }
                */
                else
                {
                    resultList = filteredChunks.Select(r => new SearchResult
                    {
                        Id = r.Id.ToString(),
                        Score = r.Score,
                        CollectionName = collectionName,
                        Content = r.Payload["content"].StringValue,
                        Title = r.Payload["nom_fichier"].StringValue,
                        Source = r.Payload["nom_fichier"].StringValue
                    }).ToList();
                }

                return (true, resultList);
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"QdrantService.SearchRagAsync : Erreur lors de la recherche: {ex.Message}");
                return (false, []);
            }
        }

        // Méthode utilitaire pour extraire une propriété string en toute sécurité
        private static string GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                return prop.ValueKind == JsonValueKind.String ? prop.GetString() ?? "" : prop.ToString();
            }
            return "";
        }
        #endregion
    }
    #endregion

    #region BgeReranker
    public static class BgeReranker
    {
        private static InferenceSession? _session;
        private static Tokenizer? _tokenizer;

        public static List<SearchResult> Rerank(string query, List<SearchResult> docs, int topK = 5)
        {
            //LoggerService.LogDebug("BgeReranker.Rerank");
            if (_tokenizer == null || _session == null)
            {
                var b = LoadModeles();
                if (!b)
                {
                    return [];
                }
            }
            var scored = new List<SearchResult>();

            foreach (var doc in docs)
            {
                float score = ScorePair(query, doc.Content);
                scored.Add(new SearchResult
                {
                    Content = doc.Content,
                    Source = doc.Source,
                    CollectionName = doc.CollectionName,
                    Title = doc.Title,
                    Id = doc.Id,
                    Metadata = doc.Metadata,
                    Score = score,
                    Deadline = doc.Deadline
                });
            }

            return scored
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .ToList();
        }

        private static bool LoadModeles()
        {
            //LoggerService.LogDebug("BgeReranker.LoadModeles");
            try
            {
                //LoggerService.LogDebug(typeof(InferenceSession).Assembly.Location);
                var pathModele = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var modeleFullPath = Path.Combine(pathModele!, "Models");
                _tokenizer = new Tokenizer(Path.Combine(modeleFullPath, "tokenizer.json"));
                /*
                var opts = new SessionOptions();
                opts.AppendExecutionProvider_CPU(); // forcé
                var session = new InferenceSession("onnx/model.onnx", opts);
                */
                _session = new InferenceSession(Path.Combine(modeleFullPath, "model.onnx"));
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"BgeReranker.LoadModeles : {ex.Message}");
                throw;

            }
        }

        private static float ScorePair(string query, string document)
        {
            //LoggerService.LogDebug("BgeReranker.ScorePair");
            var encoded = _tokenizer!.EncodePair(query, document, 100);

            var inputIds = new DenseTensor<long>(new[] { 1, encoded.inputIds.Length });
            var attentionMask = new DenseTensor<long>(new[] { 1, encoded.attentionMask.Length });

            for (int i = 0; i < encoded.inputIds.Length; i++)
            {
                inputIds[0, i] = encoded.inputIds[i];
                attentionMask[0, i] = encoded.attentionMask[i];
            }

            var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
        };

            using var result = _session!.Run(inputs);
            var output = result.First().AsEnumerable<float>().First();

            return output;
        }
    }
    #endregion

    #region Tokenizer
    public class Tokenizer
    {
        private readonly List<VocabEntry>? _vocab2;
        private readonly List<List<object>>? _vocab;
        private readonly Dictionary<string, int> _tokenToId;

        //private readonly Dictionary<(string, string), int>? _merges;
        private readonly string _cls = "[CLS]";
        private readonly string _sep = "[SEP]";
        private readonly string _pad = "[PAD]";
        private readonly string _unk = "[UNK]";

        public Tokenizer(string tokenizerJsonPath)
        {
            var json = File.ReadAllText(tokenizerJsonPath);
            var root = JsonSerializer.Deserialize<TokenizerJson>(json);
            if (root?.Model?.Vocab == null)
                throw new Exception("Le JSON ne contient pas le vocabulaire attendu");

            var model = root.Model;
            _vocab2 = model.Vocab;
            _vocab = model.VocabRaw;

            // Crée un dictionnaire token -> index
            _tokenToId = _vocab2
                .Select((entry, idx) => new { Token = entry.Token ?? string.Empty, Id = idx })
                .ToDictionary(x => x.Token, x => x.Id);

            // Gestion des tokens spéciaux
            _cls = "<s>";
            _sep = "</s>";
            _pad = "<pad>";
            _unk = "<unk>";
        }

        private static Dictionary<(string, string), int> LoadMerges(List<string> merges)
        {
            var dict = new Dictionary<(string, string), int>();
            for (int i = 0; i < merges.Count; i++)
            {
                var parts = merges[i].Split(' ');
                dict[(parts[0], parts[1])] = i;
            }
            return dict;
        }

        // ---- PUBLIC ------------------------------------------------------
        public (long[] inputIds, long[] attentionMask) EncodePair(string query, string document, int maxLen)
        {
            var tokens = new List<string>
            {
                _cls
            };
            tokens.AddRange(EncodeText(query));
            tokens.Add(_sep);
            tokens.AddRange(EncodeText(document));
            tokens.Add(_sep);

            // Convert tokens → ids
            var ids = tokens
                .Select(t => _tokenToId.ContainsKey(t) ? _tokenToId[t] : _tokenToId[_unk])
                .ToList();

            // Troncature
            if (ids.Count > maxLen)
                ids = ids.Take(maxLen).ToList();

            // Padding
            while (ids.Count < maxLen)
                ids.Add(_tokenToId[_pad]);

            long[] inputIds = ids.Select(i => (long)i).ToArray();
            long[] attentionMask = inputIds.Select(x => x == _tokenToId[_pad] ? 0L : 1L).ToArray();

            return (inputIds, attentionMask);
        }

        // ---- TOKENIZATION BPE -------------------------------------------

        private List<string> EncodeText(string text)
        {
            text = text.ToLower().Trim();
            var words = Regex.Split(text, @"\s+");

            var tokens = new List<string>();
            foreach (var word in words)
                tokens.AddRange(TokenizeWordBpe(word));

            return tokens;
        }


        private List<string> TokenizeWordBpe(string word)
        {
            var tokens = new List<string>();
            int i = 0;

            while (i < word.Length)
            {
                string? match = null;

                // On cherche le token le plus long qui correspond
                for (int j = word.Length; j > i; j--)
                {
                    string sub = word[i..j];
                    if (_tokenToId.ContainsKey(sub))
                    {
                        match = sub;
                        break;
                    }
                }

                if (match != null)
                {
                    tokens.Add(match);
                    i += match.Length;
                }
                else
                {
                    tokens.Add(_unk);
                    i += 1;
                }
            }

            return tokens;
        }


        private static List<(string, string)> GetPairs(List<string> symbols)
        {
            var pairs = new List<(string, string)>();

            for (int i = 0; i < symbols.Count - 1; i++)
                pairs.Add((symbols[i], symbols[i + 1]));

            return pairs;
        }

        private static List<string> Merge(List<string> symbols, (string, string) pair)
        {
            var merged = new List<string>();

            int i = 0;
            while (i < symbols.Count)
            {
                if (i < symbols.Count - 1 &&
                    symbols[i] == pair.Item1 &&
                    symbols[i + 1] == pair.Item2)
                {
                    merged.Add(pair.Item1 + pair.Item2);
                    i += 2;
                }
                else
                {
                    merged.Add(symbols[i]);
                    i++;
                }
            }

            return merged;
        }

        // ---- JSON STRUCTS -----------------------------------------------

        private class TokenizerJson
        {
            [JsonPropertyName("model")]
            public TokenizerModel? Model { get; set; }

            [JsonPropertyName("added_tokens")]
            public List<AddedToken>? AddedTokens { get; set; }

            [JsonPropertyName("decoder")]
            public Decoder? Decoder { get; set; }

            [JsonPropertyName("normalizer")]
            public Normalizer? Normalizer { get; set; }
        }

        private class TokenizerModel
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("unk_id")]
            public int UnkId { get; set; }

            // Vocabulaire : chaque élément est une paire [string, double]
            [JsonPropertyName("vocab")]
            public List<List<object>>? VocabRaw { get; set; }

            // Transformation en liste typée
            [JsonIgnore]
            public List<VocabEntry>? Vocab => VocabRaw?.Select(x => new VocabEntry
            {
                Token = x[0]?.ToString() ?? string.Empty, // on force string.Empty si null
                Score = x[1] is JsonElement je ? je.GetDouble() : Convert.ToDouble(x[1])
            }).ToList();
        }

        public class VocabEntry
        {
            public string? Token { get; set; }
            public double Score { get; set; }
        }
        // Exemple pour added_tokens
        public class AddedToken
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("content")]
            public string? Content { get; set; }

            [JsonPropertyName("single_word")]
            public bool SingleWord { get; set; }

            [JsonPropertyName("lstrip")]
            public bool LStrip { get; set; }

            [JsonPropertyName("rstrip")]
            public bool RStrip { get; set; }

            [JsonPropertyName("normalized")]
            public bool Normalized { get; set; }

            [JsonPropertyName("special")]
            public bool Special { get; set; }
        }

        // Decoder minimal
        public class Decoder
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("replacement")]
            public string? Replacement { get; set; }

            [JsonPropertyName("add_prefix_space")]
            public bool AddPrefixSpace { get; set; }
        }

        // Normalizer minimal
        public class Normalizer
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }
        }
    }
    #endregion

    #region Classe SearchResult : pour la réception des résultat 
    /// <summary>
    /// Résultat de recherche Qdrant
    /// </summary>
    public class SearchResult
    {
        public string Id { get; set; } = "";
        public float Score { get; set; }
        public string CollectionName { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Source { get; set; } = "";
        public DateTime? Deadline { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = [];
    }
    #endregion
}
