using HallieDomain;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hallie.Services;
using ExternalServices;

namespace Hallie.Services
{
    /// <summary>
    /// Stocke le feedback de routage dans SQL Server.
    ///
    /// Tables : dbo.FeedbackLog, dbo.ToolRoutingRules.
    /// </summary>
    public sealed class FeedbackDbService
    {
        private readonly string _connectionString;
        public string ConnectionString => _connectionString;

        #region Constructeur
        public FeedbackDbService(string connectionString)
        {
            _connectionString = !string.IsNullOrWhiteSpace(connectionString)
                ? connectionString!
                : ResolveDefaultConnectionString();
        }
        #endregion

        #region Méthodes publiques
        public async Task<bool> IsBddActif(string connectionString="")
        {
            try
            {
                if (connectionString == "")
                    connectionString = _connectionString;
                LoggerService.LogDebug($"FeedbackDbService.IsBddActif : {connectionString}");

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
                await using var conn = new SqlConnection(connectionString);
                await using var cmd = new SqlCommand(sql, conn);
                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"FeedbackDbService.IsBddActif : {ex.Message}");
                return false;
            }
        }
        public async Task EnsureSchemaAsync(CancellationToken ct = default)
        {
            const string sql = @"
                IF OBJECT_ID('dbo.FeedbackLog','U') IS NULL
                BEGIN
                    CREATE TABLE dbo.FeedbackLog (
                        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_FeedbackLog PRIMARY KEY,
                        CreatedAtUtc DATETIME2(3) NOT NULL,
                        ConversationId NVARCHAR(128) NOT NULL,
                        TurnId INT NOT NULL,

                        UserRating INT NOT NULL,
                        Outcome NVARCHAR(16) NOT NULL,
                        ErrorClass NVARCHAR(32) NULL,

                        ToolUsed NVARCHAR(64) NOT NULL,
                        ToolParamsHash CHAR(64) NOT NULL,
                        ToolParamsJson NVARCHAR(MAX) NOT NULL,

                        ExpectedTool NVARCHAR(64) NULL,
                        Comment NVARCHAR(MAX) NULL,

                        PromptText NVARCHAR(MAX) NOT NULL,
                        StepIndex int NULL
                    );

                    CREATE INDEX IX_FeedbackLog_Conversation_Turn
                    ON dbo.FeedbackLog (ConversationId, TurnId);

                    CREATE INDEX IX_FeedbackLog_ToolUsed_CreatedAt
                    ON dbo.FeedbackLog (ToolUsed, CreatedAtUtc DESC);

                    CREATE INDEX IX_FeedbackLog_Outcome_ErrorClass
                    ON dbo.FeedbackLog (Outcome, ErrorClass);
                END

                IF OBJECT_ID('dbo.ToolRoutingRules','U') IS NULL
                BEGIN
                    CREATE TABLE dbo.ToolRoutingRules (
                        RuleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ToolRoutingRules PRIMARY KEY,
                        IsEnabled BIT NOT NULL,
                        CreatedAtUtc DATETIME2(3) NOT NULL,

                        Name NVARCHAR(128) NOT NULL,
                        ConditionJson NVARCHAR(MAX) NOT NULL,
                        Tool NVARCHAR(64) NOT NULL,
                        ScoreDelta INT NOT NULL,
                        Source NVARCHAR(16) NOT NULL,
                        Confidence DECIMAL(5,4) NOT NULL
                    );

                    CREATE INDEX IX_ToolRoutingRules_Enabled_Tool
                    ON dbo.ToolRoutingRules (IsEnabled, Tool);

                    CREATE INDEX IX_ToolRoutingRules_Source
                    ON dbo.ToolRoutingRules (Source);
                END
                ";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = 30;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        public async Task<Guid> InsertFeedbackAsync(FeedbackLogInsertRequest req, CancellationToken ct = default)
        {
            try
            {
                await EnsureSchemaAsync(ct);

                var canonicalParams = CanonicalizeJson(req.ToolParamsJson);
                var hash = Sha256Hex(canonicalParams);

                var id = Guid.NewGuid();
                const string sql = @"
                INSERT INTO dbo.FeedbackLog
                (Id, CreatedAtUtc, ConversationId, TurnId, UserRating, Outcome, ErrorClass, ToolUsed, ToolParamsHash, ToolParamsJson, ExpectedTool, Comment, PromptText, StepIndex)
                VALUES
                (@Id, @CreatedAtUtc, @ConversationId, @TurnId, @UserRating, @Outcome, @ErrorClass, @ToolUsed, @ToolParamsHash, @ToolParamsJson, @ExpectedTool, @Comment, @PromptText, @StepIndex);
                ";

                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(ct);
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@CreatedAtUtc", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("@ConversationId", req.ConversationId);
                cmd.Parameters.AddWithValue("@TurnId", req.TurnId);
                cmd.Parameters.AddWithValue("@UserRating", req.UserRating);
                cmd.Parameters.AddWithValue("@Outcome", req.Outcome);
                cmd.Parameters.AddWithValue("@ErrorClass", (object?)req.ErrorClass ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ToolUsed", req.ToolUsed);
                cmd.Parameters.AddWithValue("@ToolParamsHash", hash);
                cmd.Parameters.AddWithValue("@ToolParamsJson", canonicalParams);
                cmd.Parameters.AddWithValue("@ExpectedTool", (object?)req.ExpectedTool ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Comment", (object?)req.Comment ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PromptText", req.PromptText);
                cmd.Parameters.AddWithValue("@StepIndex", (object?)req.StepIndex ?? DBNull.Value);

                cmd.CommandTimeout = 30;
                await cmd.ExecuteNonQueryAsync(ct);
                return id;
            }
            catch(Exception ex)
            {
                LoggerService.LogError($"FeedbackDbService.InsertFeedbackAsync : {ex.Message}");
                return Guid.Empty;
            }
        }
        public async Task<List<FeedbackLogRow>> GetFeedbackByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return [];

            var sb = new StringBuilder();
            sb.Append("SELECT Id, CreatedAtUtc, ConversationId, TurnId, UserRating, Outcome, ErrorClass, ToolUsed, ToolParamsHash, ToolParamsJson, ExpectedTool, Comment, PromptText FROM dbo.FeedbackLog WHERE Id IN (");
            for (int i = 0; i < idList.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("@p" + i);
            }
            sb.Append(");");

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sb.ToString(), conn);
            for (int i = 0; i < idList.Count; i++)
                cmd.Parameters.AddWithValue("@p" + i, idList[i]);

            cmd.CommandTimeout = 30;
            var rows = new List<FeedbackLogRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                rows.Add(new FeedbackLogRow
                {
                    Id = reader.GetGuid(0),
                    CreatedAtUtc = reader.GetDateTime(1),
                    ConversationId = reader.GetString(2),
                    TurnId = reader.GetInt32(3),
                    UserRating = reader.GetInt32(4),
                    Outcome = reader.GetString(5),
                    ErrorClass = reader.IsDBNull(6) ? null : reader.GetString(6),
                    ToolUsed = reader.GetString(7),
                    ToolParamsHash = reader.GetString(8),
                    ToolParamsJson = reader.GetString(9),
                    ExpectedTool = reader.IsDBNull(10) ? null : reader.GetString(10),
                    Comment = reader.IsDBNull(11) ? null : reader.GetString(11),
                    PromptText = reader.GetString(12)
                });
            }
            return rows;
        }
        public async Task<List<ToolRoutingRuleRow>> GetEnabledRulesAsync(CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);

            const string sql = @"SELECT RuleId, IsEnabled, CreatedAtUtc, Name, ConditionJson, Tool, ScoreDelta, Source, Confidence FROM dbo.ToolRoutingRules WHERE IsEnabled = 1";
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = 30;
            var rows = new List<ToolRoutingRuleRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                rows.Add(new ToolRoutingRuleRow
                {
                    RuleId = reader.GetGuid(0),
                    IsEnabled = reader.GetBoolean(1),
                    CreatedAtUtc = reader.GetDateTime(2),
                    Name = reader.GetString(3),
                    ConditionJson = reader.GetString(4),
                    Tool = reader.GetString(5),
                    ScoreDelta = reader.GetInt32(6),
                    Source = reader.GetString(7),
                    Confidence = reader.GetDecimal(8)
                });
            }
            return rows;
        }
        #endregion

        #region Méthodes privées
        private static string ResolveDefaultConnectionString()
        {
            if (Params.BddConnexionsString is null || Params.BddConnexionsString.Count == 0)
                throw new InvalidOperationException("Aucune connexion SQL configurée (Params.BddConnexionsString vide). Impossible d'activer FeedbackLog/ToolRoutingRules.");

            // Tentative : une base nommée "Hallie" ou "Agent"; sinon première.
            var preferred = Params.BddConnexionsString.FirstOrDefault(c =>
                c.BddName.Equals("hallie", StringComparison.OrdinalIgnoreCase)
                || c.BddName.Contains("hallie", StringComparison.OrdinalIgnoreCase));

            return (preferred ?? Params.BddConnexionsString[0]).ConnexionString;
        }
        private static string CanonicalizeJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                using var ms = new MemoryStream();
                using (var writer = new Utf8JsonWriter(ms))
                {
                    WriteCanonical(writer, doc.RootElement);
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
            catch
            {
                return json;
            }
        }
        private static void WriteCanonical(Utf8JsonWriter writer, JsonElement el)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var prop in el.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(prop.Name);
                        WriteCanonical(writer, prop.Value);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in el.EnumerateArray())
                        WriteCanonical(writer, item);
                    writer.WriteEndArray();
                    break;
                default:
                    el.WriteTo(writer);
                    break;
            }
        }
        private static string Sha256Hex(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var hash = SHA256.HashData(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
        #endregion
    }

    #region Classes support
    public sealed class FeedbackLogInsertRequest
    {
        public required string ConversationId { get; init; }
        public required int TurnId { get; init; }
        public required int UserRating { get; init; }
        public required string Outcome { get; init; }           // solved|partial|failed
        public string? ErrorClass { get; init; }                // timeout|bad_route|wrong_data|hallucination...

        public required string ToolUsed { get; init; }
        public required string ToolParamsJson { get; init; }    // JSON string

        public string? ExpectedTool { get; init; }
        public string? Comment { get; init; }
        public required string PromptText { get; init; }
        public int? StepIndex { get; init; }
    }

    public sealed class FeedbackLogRow
    {
        public Guid Id { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string ConversationId { get; set; } = "";
        public int TurnId { get; set; }
        public int UserRating { get; set; }
        public string Outcome { get; set; } = "partial";
        public string? ErrorClass { get; set; }
        public string ToolUsed { get; set; } = "";
        public string ToolParamsHash { get; set; } = "";
        public string ToolParamsJson { get; set; } = "";
        public string? ExpectedTool { get; set; }
        public string? Comment { get; set; }
        public string PromptText { get; set; } = "";
    }

    public sealed class ToolRoutingRuleRow
    {
        public Guid RuleId { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string Name { get; set; } = "";
        public string ConditionJson { get; set; } = "";
        public string Tool { get; set; } = "";
        public int ScoreDelta { get; set; }
        public string Source { get; set; } = "manual";
        public decimal Confidence { get; set; }
    }
    #endregion
}
