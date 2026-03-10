using ExternalServices;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;
using HallieDomain;

namespace Hallie.Tools
{
    #region Tool SqlQueryTool
    public class SqlQueryTool : ITool
    {
        public string Name => "sql_query";
        public string Description => "Exécute une requête SQL de type SELECT sur la base de données";

        private SqlQueryService _Services;

        public SqlQueryTool(List<TypeConnexionString> connectionsString)
        {
            LoggerService.LogInfo("SqlQueryTool");
            _Services = new SqlQueryService(connectionsString);
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("SqlQueryTool.ExecuteAsync");
            try
            {

                var bddName = parameters["bddname"].ToString();
                var query = parameters["query"].ToString();

                if (bddName == null)
                {
                    LoggerService.LogWarning("Erreur : Base de données non identifiée");
                    return "Erreur : Base de données non identifiée";
                }

                if (query == null)
                {
                    LoggerService.LogWarning("Erreur : Requête non générée");
                    return "Erreur : Requête non générée";
                }

                // Validation de sécurité (TRÈS IMPORTANT)
                if (!IsQuerySafe(query))
                {
                    LoggerService.LogWarning($"SqlQueryTool.ExecuteAsync --> Requête non autorisée : {query}");
                    return "Erreur : Requête non autorisée";
                }

                LoggerService.LogDebug($"SqlQueryTool.ExecuteAsync --> bddName : {bddName}");
                LoggerService.LogDebug($"SqlQueryTool.ExecuteAsync --> query   : {query}");

                var (bOk, reponse) = await _Services.ExecuteQuery(bddName, query);
                if(!bOk)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        rows = "",
                        error = reponse
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    rows = reponse,
                    error = ""
                });
            }
            catch(Exception ex)
            {
                return JsonService.Serialize(new
                {
                    ok = false,
                    rows = "",
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
                    Description = "La requête SQL à exécuter (SELECT uniquement)",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "bddname",
                    Type = "string",
                    Description = "Le nom de la base de données où sera éxécutée la requête SQL",
                    Required = true
                }
            };
        }

        private static bool IsQuerySafe(string query)
        {
            var lowerQuery = query.ToLower();

            // Autoriser uniquement SELECT
            if (!lowerQuery.TrimStart().StartsWith("select"))
                return false;

            // Bloquer les mots-clés dangereux
            var forbidden = new[] { "drop", "delete", "insert", "update", "exec", "execute" };
            return !forbidden.Any(kw => lowerQuery.Contains(kw));
        }

    }
    #endregion

    #region Tool SqlActionTool
    public class SqlActionTool : ITool
    {
        public string Name => "sql_action";
        public string Description => "Exécute une requête SQL de type INSERT / UPDATE / DELETE sur la base de données";

        private SqlQueryService _Services;

        public SqlActionTool(List<TypeConnexionString> connectionsString)
        {
            LoggerService.LogInfo("SqlActionTool");
            _Services = new SqlQueryService(connectionsString);
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            LoggerService.LogInfo("SqlActionTool.ExecuteAsync");
            try
            {

                var bddName = parameters["bddname"].ToString();
                var query = parameters["query"].ToString();

                if (bddName == null)
                {
                    LoggerService.LogWarning("Erreur : Base de données non identifiée");
                    return "Erreur : Base de données non identifiée";
                }

                if (query == null)
                {
                    LoggerService.LogWarning("Erreur : Requête non générée");
                    return "Erreur : Requête non générée";
                }

                // Validation de sécurité (TRÈS IMPORTANT)
                
                if (!IsQuerySafe(query))
                {
                    LoggerService.LogWarning($"SqlActionTool.ExecuteAsync --> Requête non autorisée : {query}");
                    return "Erreur : Requête non autorisée";
                }
                
                LoggerService.LogDebug($"SqlActionTool.ExecuteAsync --> bddName : {bddName}");
                LoggerService.LogDebug($"SqlActionTool.ExecuteAsync --> query   : {query}");

                var (bOk, nbRows, reponse) = await _Services.ExecuteNonQueryAsync(bddName, query);
                if (!bOk)
                {
                    return JsonService.Serialize(new
                    {
                        ok = false,
                        rows = "",
                        error = reponse
                    });
                }

                return JsonService.Serialize(new
                {
                    ok = true,
                    rows = reponse,
                    error = ""
                });
            }
            catch (Exception ex)
            {
                return JsonService.Serialize(new
                {
                    ok = false,
                    rows = "",
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
                    Description = "La requête SQL à exécuter (INSERT ou UPDATE ou DELETE)",
                    Required = true
                },
                new ToolParameter
                {
                    Name = "bddname",
                    Type = "string",
                    Description = "Le nom de la base de données où sera éxécutée la requête SQL",
                    Required = true
                }
            };
        }

        private static bool IsQuerySafe(string query)
        {
            var lowerQuery = query.ToLower();

            // Pas l'outil pour les SELECT
            if (lowerQuery.TrimStart().StartsWith("select"))
                return false;

            // Bloquer les mots-clés dangereux
            var forbidden = new[] {"exec", "execute", "truncate", };
            return !forbidden.Any(kw => lowerQuery.Contains(kw));
        }

    }
    #endregion

    #region Service
    public class SqlQueryService
    {
        List<TypeConnexionString> _ConnectionsString = new();

        public SqlQueryService(List<TypeConnexionString> connectionsString) 
        {
            _ConnectionsString = connectionsString;

        }

        public string FindStructureBdd()
        {
            LoggerService.LogDebug($"SqlQueryService.FindStructureBdd");
            try
            {
                StringBuilder sbReturn = new();
                string sql = """
                    SELECT (
                        SELECT 
                            (
                                SELECT 
                                    s.name AS table_schema,
                                    t.name AS table_name,
                                    (
                                        SELECT 
                                            c.name AS column_name,
                                            ty.name AS data_type,
                                            c.is_nullable,
                                            CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS is_primary_key
                                        FROM sys.columns c
                                        INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
                                        LEFT JOIN (
                                            SELECT ic.object_id, ic.column_id
                                            FROM sys.indexes i
                                            INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                                            WHERE i.is_primary_key = 1
                                        ) pk ON c.object_id = pk.object_id AND c.column_id = pk.column_id
                                        WHERE c.object_id = t.object_id
                                        ORDER BY c.column_id
                                        FOR JSON PATH
                                    ) AS columns
                                FROM sys.tables t
                                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                                FOR JSON PATH
                            ) AS tables,
                            (
                                SELECT 
                                    fk.name AS constraint_name,
                                    OBJECT_SCHEMA_NAME(fk.parent_object_id) AS from_schema,
                                    OBJECT_NAME(fk.parent_object_id) AS from_table,
                                    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS from_column,
                                    OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS to_schema,
                                    OBJECT_NAME(fk.referenced_object_id) AS to_table,
                                    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS to_column
                                FROM sys.foreign_keys fk
                                INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                                FOR JSON PATH
                            ) AS foreign_keys
                        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                    ) AS database_structure;
                    """;
                foreach (var cnx in _ConnectionsString)
                {
                    var bddName = cnx.BddName.Split('(')[0].Trim();
                    var sql1 = sql.Replace("@@BDD", bddName);
                    string json;
                    string connectionString = GetConnectionString(cnx.BddName);

                    using var conn = new SqlConnection(connectionString);
                    using var cmd = new SqlCommand(sql1, conn)
                    {
                        CommandTimeout = 120
                    };

                    conn.Open();
                    using var reader = cmd.ExecuteReader(
                        CommandBehavior.SequentialAccess | CommandBehavior.SingleRow);
                    var sb = new StringBuilder();
                    sb.AppendLine($"Base de données : {bddName}");
                    if (reader.Read())
                    {
                        using var textReader = reader.GetTextReader(0);

                        char[] buffer = new char[8192];
                        int read;

                        while ((read = textReader.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            sb.Append(buffer, 0, read);
                        }
                    }

                    json = sb.ToString();
                    sbReturn.AppendLine(json);
                }
                return sbReturn.ToString();
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"FindStructureBdd : {ex.Message}");
                return "";
            }
        }

        public async Task<(bool, string)> ExecuteQuery(string bddName, string query)
        {
            try
            {
                string connectionString = GetConnectionString(bddName);
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(query, connection);
                await connection.OpenAsync();

                //var result = await command.ExecuteScalarAsync();
                using var reader = await command.ExecuteReaderAsync();

                var results = new List<Dictionary<string, object>>();
                var returnS = "";
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var value = await reader.IsDBNullAsync(i)
                            ? null
                            : reader.GetValue(i);

                        // Ajout d'un opérateur de null-coalescence pour garantir que la clé n'est jamais null.
                        var key = reader.GetName(i) ?? string.Empty;
                        row[key] = value!;

                        returnS += $"{reader.GetName(i)}: {value} ; ";
                    }

                    returnS += "\n";
                    results.Add(row);
                }
                var rep = JsonService.Serialize(results);
                rep = rep.Replace("\"[", "[");
                rep = rep.Replace("]\"", "]");
                rep = rep.Replace("\u0022", "\"");


                return (true, rep);
            }
            catch(Exception ex)
            {
                LoggerService.LogError($"SqlQueryService.ExecuteQuery --> Erreur lors de l'exécution de la requête SQL : {ex.Message}");
                return (false, ex.Message);
            }

        }

        public async Task<(bool Success, int RowsAffected, string Message)> ExecuteNonQueryAsync(string bddName, string query)
        {
            try
            {
                // Sécurité minimale : refuse SELECT
                var trimmed = query.TrimStart().ToUpperInvariant();
                if (trimmed.StartsWith("SELECT"))
                    return (false, 0, "Cette méthode ne supporte pas SELECT.");

                string connectionString = GetConnectionString(bddName);

                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(query, connection);

                await connection.OpenAsync();

                int rows = await command.ExecuteNonQueryAsync();

                return (true, rows, $"{rows} ligne(s) affectée(s).");
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"SqlQueryService.ExecuteNonQueryAsync --> {ex.Message}");
                return (false, 0, ex.Message);
            }
        }
        private string GetConnectionString(string bddName)
        {
            var typeConnexion = _ConnectionsString.FirstOrDefault(c => c.BddName == bddName);

            if(typeConnexion == null)
                typeConnexion = _ConnectionsString.Where(c => c.BddName.StartsWith(bddName)).FirstOrDefault();

            if (typeConnexion == null)
                typeConnexion = _ConnectionsString.Where(c => c.BddName.Contains(bddName)).FirstOrDefault();

            return typeConnexion?.ConnexionString ?? "";
        }

    }
    #endregion
}
