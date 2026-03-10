using ExternalServices;
using Hallie.Services;
using System.Text;
using HallieDomain;

namespace Hallie.Tools
{
    #region Tool : SearchDocumentTool
    public class SearchDocumentTool : ITool
    {
        public string Name => "search_documents";
        public string Description => "Recherche dans la base de connaissances RAG de l'entreprise (documents, rapports, procédures, etc.)";

        private readonly RAGService _Service;

        public SearchDocumentTool(string ollamaEmbeddingUrl, string ollamaEmbeddingModel, string qdrantUrl, string qdrantCollectionsNames)
        {
            LoggerService.LogInfo("SearchDocumentTool");
            _Service = new RAGService(new OllamaEmbeddingService(ollamaEmbeddingUrl, ollamaEmbeddingModel), qdrantUrl, qdrantCollectionsNames);
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("SearchDocumentTool.ExecuteAsync");


            try
            {
                var query = parameters["query"].ToString() ?? "";

                var domain = parameters.ContainsKey("domain")
                    ? parameters["domain"].ToString()
                    : "";

                var topK = parameters.ContainsKey("top_k")
                    ? Convert.ToInt32(parameters["top_k"])
                    : 20;

                var scoreThreshold = parameters.ContainsKey("score_threshold")
                    ? Convert.ToSingle(parameters["score_threshold"])
                    : 0.4f;

                var deadline_min = parameters.ContainsKey("deadline_min")
                    ? Convert.ToDateTime(parameters["deadline_min"])
                    : DateTime.Today;
                var deadline_max = parameters.ContainsKey("deadline_max")
                    ? Convert.ToDateTime(parameters["deadline_max"])
                    : DateTime.Today;

                if (domain == "" && query.Contains("deadline", StringComparison.InvariantCultureIgnoreCase))
                    domain = "Notes";

                // RAZ du domaine si différent de Emails ou Notes (pour permettre la recherche globale sur tous les domaines autorisés)
                if (domain != "Emails" && domain != "Notes")
                {
                    domain = "";
                }

                // Si recherche deadline dans Notes alors on baisse scoreThreshold pour ramener toutes les notes
                bool isDealine = false;
                if (domain == "Notes" && query.Contains("deadline", StringComparison.InvariantCultureIgnoreCase))
                {
                    isDealine = true;
                    query = "deadline";
                    scoreThreshold = 0.01f;
                }
                else if (domain == "Notes")
                {
                    // Si Notes sans dealine, alors on baisse scoreThreshold (mais moins) car longueur de texte des notes pas assez long
                    scoreThreshold = 0.4f;
                }

                if (string.IsNullOrWhiteSpace(query))
                {
                    LoggerService.LogError("SearchDocumentTool.ExecuteAsync --> La requête ne peut pas être vide");
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "Erreur : La requête ne peut pas être vide."
                    });
                }

                // Rechercher dans Qdrant
                var (bOk, results) = await _Service.SearchAsync(query, domain, topK, scoreThreshold);

                if(isDealine)
                {
                    //results = results.Where(e => e.Deadline != null && e.Deadline.Value.Date >= DateTime.Now.Date).OrderBy(e=>e.Deadline).ToList();
                    results = results.Where(e => e.Deadline != null && e.Deadline.Value >= deadline_min && e.Deadline.Value <= deadline_max).OrderBy(e => e.Deadline).ToList();

                }


                if (!bOk)
                {
                    LoggerService.LogError("SearchDocumentTool.ExecuteAsync --> Une erreur s'est produite dans QdrantService.SearchAsync");
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "Une erreur s'est produite dans QdrantService.SearchAsync"
                    });
                }

                if (results.Count == 0)
                {
                    LoggerService.LogWarning("SearchDocumentTool.ExecuteAsync --> Aucun document pertinent trouvé dans la base de connaissances");
                    return JsonService.Serialize(new
                    {
                        ok = true,
                        reponse = "Aucun document pertinent trouvé dans la base de connaissances",
                        error = ""
                    });
                }

                // Formater les résultats pour le LLM
                var context = new StringBuilder();
                context.AppendLine($"Voici les {results.Count} documents les plus pertinents :");
                context.AppendLine();
                LoggerService.LogDebug($"SearchDocumentTool.ExecuteAsync --> résultats trouvés : {results.Count}");
                for (int i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    context.AppendLine($"--- Document {i + 1} (Score: {result.Score:F2}) ---");
                    context.AppendLine($"Titre: {result.Title}");
                    context.AppendLine($"Source: {result.Source}");
                    context.AppendLine($"Contenu: {result.Content}");

                    if (result.Metadata.Count > 0)
                    {
                        context.AppendLine("Métadonnées:");
                        foreach (var meta in result.Metadata)
                        {
                            context.AppendLine($"  - {meta.Key}: {meta.Value}");
                        }
                    }

                    context.AppendLine();
                }

                LoggerService.LogDebug($"SearchDocumentTool.ExecuteAsync --> résultats : {context.ToString()}");
                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = context.ToString(),
                    error = ""
                });

            }
            catch (Exception ex)
            {
                LoggerService.LogError($"SearchDocumentTool.ExecuteAsync --> {ex.Message}");
                return JsonService.Serialize(new
                {
                    ok = false,
                    reponse = "",
                    error = ex.Message
                });
            }
        }

        public ToolParameter[] GetParameters()
        {
            return new[]
            {
                new ToolParameter
                {
                    Name = "query",
                    Type = "string",
                    Description = "La question ou recherche à effectuer dans la base de connaissances",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "domain",
                    Type = "string",
                    Description = "Domaine de la base de connaissance où chercher l'information recherchée",
                    Required = false
                },
                new ToolParameter
                {
                    Name = "top_k",
                    Type = "number",
                    Description = "Nombre de documents à retourner (défaut: 20)",
                    Required = false
                },
                new ToolParameter
                {
                    Name = "score_threshold",
                    Type = "number",
                    Description = "Score minimum de pertinence (0-1, défaut: 0.4)",
                    Required = false
                },
                new ToolParameter
                {
                    Name = "deadline_min",
                    Type = "string",
                    Description = "date et heure de la fourchette basse des deadlines recherchées",
                    Required = false
                },
                new ToolParameter
                {
                    Name = "deadline_max",
                    Type = "string",
                    Description = "date et heure de la fourchette haute des deadlines recherchées",
                    Required = false
                }
            };
        }
    }
    #endregion

    #region Tool : IngestDocumentTool
    public class IngestDocumentTool : ITool
    {
        public string Name => "ingest_documents";
        public string Description => "Enregistre dans la base de connaissances RAG de l'entreprise (documents, rapports, procédures, etc.). Les éléments enregistrés sont classés par DOMAINE.";

        private readonly QdrantService _Service;

        public IngestDocumentTool(string ollamaEmbeddingUrl, string ollamaEmbeddingModel, string qdrantUrl, string qdrantCollectionsNames)
        {
            LoggerService.LogInfo("IngestDocumentTool");

            var qdrantService = new QdrantService(
                new OllamaEmbeddingService(ollamaEmbeddingUrl, ollamaEmbeddingModel),
                qdrantUrl,
                qdrantCollectionsNames);
            _Service = qdrantService;
        }

        public IngestDocumentTool(QdrantService qdrantService)
        {
            LoggerService.LogInfo("IngestDocumentTool");

            _Service = qdrantService;
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("IngestDocumentTool.ExecuteAsync");


            try
            {
                var domain = parameters["domain"].ToString() ?? "";
                var deadline = parameters["deadline"].ToString() ?? "";
                var content = parameters["content"].ToString() ?? "";
                var objet = parameters.ContainsKey("objet")
                    ? Convert.ToString(parameters["objet"])
                    : "";

                var Domain = DomainExtensions.GetDomainByCollectionName(domain);

                // Si note avec Deadline, on ajoute la deadline dans le texte si elle n'y est pas
                if(Domain == eDomain.Notes && deadline != "" && !content.Contains("deadline", StringComparison.InvariantCultureIgnoreCase))
                {
                    content += $". Deadline: {deadline}";
                }

                // Rechercher dans Qdrant
                var bOk = await _Service.IngestDocumentAsync(Domain, false, content, "",Params.SmtpUser!,objet!, deadline);

                if (!bOk)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        reponse = "",
                        error = "Une erreur s'est produite dans IngestDocumentTool.SearchAsync"
                    });
                }

                // Formater les résultats pour le LLM
                return JsonService.Serialize(new
                {
                    ok = true,
                    reponse = "Element enregistré dans la base de connaissance",
                    error = ""
                });

            }
            catch (Exception ex)
            {
                return JsonService.Serialize(new
                {
                    ok = false,
                    reponse = "",
                    error = ex.Message
                });
            }
        }

        public ToolParameter[] GetParameters()
        {
            return new[]
            {
                new ToolParameter
                {
                    Name = "domain",
                    Type = "string",
                    Description = "Le domaine dans lequel sera enregistré le document dans la base de connaissance",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "content",
                    Type = "string",
                    Description = "Le contenu du ocument qui sera enregistré dans la base de connaissance",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "objet",
                    Type = "string",
                    Description = "Le titre du document qui sera enregistré dans la base de connaissance (obligatoire pour le domaine EMAILS ou NOTES)",
                    Required = false
                },
                new ToolParameter
                {
                    Name = "deadline",
                    Type = "string",
                    Description = "Date et heure de dealine explicitement indiquée dans le contenu. Si pas indiquée, mettre null. Format : yyy-MM-dd HH:mm",
                    Required = false
                }
            };
        }
    }
    #endregion

    #region RAGService
    /// <summary>
    /// Service pour interagir avec Qdrant (base de données vectorielle)
    /// </summary>
    public class RAGService
    {
        #region Variables 
        private readonly QdrantService _qdrantService;
        #endregion

        #region Constructeur
        public RAGService(IEmbeddingService embeddingService, string url, string collectionsName)
        {
            _qdrantService = new QdrantService(embeddingService, url, collectionsName);
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
        public async Task<(bool,List<SearchResult>)> SearchAsync(string query, string domain, int topK = 20, float scoreThreshold = 0.7f)
        {
            try
            {
                return await _qdrantService.SearchAsync(query, domain, topK, scoreThreshold);
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"QdrantService.SearchAsync : Erreur lors de la recherche: {ex.Message}");
                return (false,[]);
            }
        }
        public async Task<bool> IngestDocumentAsync(eDomain domain, bool isRAZCollections, string content, string filename, string proprietaire = "", string objet = "", string deadline="", string niveauAcces = "")
        {
            try
            {
                return await _qdrantService.IngestDocumentAsync(domain, isRAZCollections, content, filename, proprietaire, objet, deadline, niveauAcces);

            }
            catch (Exception ex)
            {
                LoggerService.LogError($"QdrantService.IngestDocument : {ex.Message}");
                return false;
            }
        }
        #endregion

    }
    #endregion

}

