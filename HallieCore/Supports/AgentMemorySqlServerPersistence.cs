using ExternalServices;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace Hallie.Services
{
    public interface IAgentMemoryPersistence
    {
        Task<IReadOnlyList<AgentMemoryEntry>> GetRecentAsync(string agentName, int take = 4, CancellationToken ct = default);
        Task SaveAsync(AgentMemoryEntry entry, CancellationToken ct = default);
    }

    public sealed class SqlServerAgentMemoryPersistence : IAgentMemoryPersistence
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);
        private bool _schemaReady;

        public SqlServerAgentMemoryPersistence(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<IReadOnlyList<AgentMemoryEntry>> GetRecentAsync(string agentName, int take = 4, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(agentName))
                return Array.Empty<AgentMemoryEntry>();

            await EnsureSchemaAsync(ct);

            const string sql = @"
                SELECT TOP (@Take)
                    AgentName,
                    UserPrompt,
                    ParametersJson,
                    Result,
                    IsSuccess,
                    Why,
                    CreatedAtUtc,
                    ISNULL(TechnicalSuccess, IsSuccess) AS TechnicalSuccess,
                    ISNULL(TaskSuccess, IsSuccess) AS TaskSuccess,
                    ISNULL(OutcomeKind, N'') AS OutcomeKind,
                    CorrelationId,
                    StepIndex
                FROM dbo.AgentMemoryLog
                WHERE AgentName = @AgentName
                ORDER BY CreatedAtUtc DESC;";

            var rows = new List<AgentMemoryEntry>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Take", Math.Max(1, take));
            cmd.Parameters.AddWithValue("@AgentName", agentName);
            cmd.CommandTimeout = 30;

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                rows.Add(new AgentMemoryEntry
                {
                    AgentName = reader.GetString(0),
                    UserPrompt = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Parameters = DeserializeParameters(reader.IsDBNull(2) ? "{}" : reader.GetString(2)),
                    Result = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    IsSuccess = !reader.IsDBNull(4) && reader.GetBoolean(4),
                    Why = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Timestamp = reader.IsDBNull(6) ? DateTime.UtcNow : reader.GetDateTime(6),
                    TechnicalSuccess = !reader.IsDBNull(7) && reader.GetBoolean(7),
                    TaskSuccess = !reader.IsDBNull(8) && reader.GetBoolean(8),
                    OutcomeKind = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    CorrelationId = reader.IsDBNull(10) ? null : reader.GetGuid(10).ToString("N"),
                    StepIndex = reader.IsDBNull(11) ? null : reader.GetInt32(11)
                });
            }

            rows.Reverse();
            return rows;
        }

        public async Task SaveAsync(AgentMemoryEntry entry, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(entry);
            await EnsureSchemaAsync(ct);

            const string sql = @"
                INSERT INTO dbo.AgentMemoryLog
                    (Id, CreatedAtUtc, AgentName, UserPrompt, ParametersJson, Result, IsSuccess, Why, TechnicalSuccess, TaskSuccess, OutcomeKind, CorrelationId, StepIndex)
                VALUES
                    (@Id, @CreatedAtUtc, @AgentName, @UserPrompt, @ParametersJson, @Result, @IsSuccess, @Why, @TechnicalSuccess, @TaskSuccess, @OutcomeKind, @CorrelationId, @StepIndex);";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@CreatedAtUtc", entry.Timestamp == default ? DateTime.UtcNow : entry.Timestamp.ToUniversalTime());
            cmd.Parameters.AddWithValue("@AgentName", entry.AgentName ?? "unknown");
            cmd.Parameters.AddWithValue("@UserPrompt", (object?)entry.UserPrompt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ParametersJson", JsonSerializer.Serialize(entry.Parameters ?? new Dictionary<string, object>()));
            cmd.Parameters.AddWithValue("@Result", (object?)entry.Result ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsSuccess", entry.IsSuccess);
            cmd.Parameters.AddWithValue("@Why", string.IsNullOrWhiteSpace(entry.Why) ? DBNull.Value : entry.Why);
            cmd.Parameters.AddWithValue("@TechnicalSuccess", entry.TechnicalSuccess);
            cmd.Parameters.AddWithValue("@TaskSuccess", entry.TaskSuccess);
            cmd.Parameters.AddWithValue("@OutcomeKind", string.IsNullOrWhiteSpace(entry.OutcomeKind) ? DBNull.Value : entry.OutcomeKind);
            cmd.Parameters.AddWithValue("@CorrelationId", Guid.TryParse(entry.CorrelationId, out var cid) ? cid : DBNull.Value);
            cmd.Parameters.AddWithValue("@StepIndex", entry.StepIndex.HasValue ? entry.StepIndex.Value : DBNull.Value);
            cmd.CommandTimeout = 30;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task EnsureSchemaAsync(CancellationToken ct)
        {
            if (_schemaReady)
                return;

            await _schemaLock.WaitAsync(ct);
            try
            {
                if (_schemaReady)
                    return;

                const string sql = @"
IF OBJECT_ID('dbo.AgentMemoryLog','U') IS NULL
BEGIN
    CREATE TABLE dbo.AgentMemoryLog (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AgentMemoryLog PRIMARY KEY,
        CreatedAtUtc DATETIME2(3) NOT NULL,
        AgentName NVARCHAR(128) NOT NULL,
        UserPrompt NVARCHAR(MAX) NULL,
        ParametersJson NVARCHAR(MAX) NOT NULL,
        Result NVARCHAR(MAX) NULL,
        IsSuccess BIT NOT NULL,
        Why NVARCHAR(512) NULL,
        TechnicalSuccess BIT NULL,
        TaskSuccess BIT NULL,
        OutcomeKind NVARCHAR(64) NULL,
        CorrelationId UNIQUEIDENTIFIER NULL,
        StepIndex INT NULL
    );

    CREATE INDEX IX_AgentMemoryLog_AgentName_CreatedAtUtc
        ON dbo.AgentMemoryLog (AgentName, CreatedAtUtc DESC);
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.AgentMemoryLog', 'TechnicalSuccess') IS NULL
        ALTER TABLE dbo.AgentMemoryLog ADD TechnicalSuccess BIT NULL;

    IF COL_LENGTH('dbo.AgentMemoryLog', 'TaskSuccess') IS NULL
        ALTER TABLE dbo.AgentMemoryLog ADD TaskSuccess BIT NULL;

    IF COL_LENGTH('dbo.AgentMemoryLog', 'OutcomeKind') IS NULL
        ALTER TABLE dbo.AgentMemoryLog ADD OutcomeKind NVARCHAR(64) NULL;

    IF COL_LENGTH('dbo.AgentMemoryLog', 'CorrelationId') IS NULL
        ALTER TABLE dbo.AgentMemoryLog ADD CorrelationId UNIQUEIDENTIFIER NULL;

    IF COL_LENGTH('dbo.AgentMemoryLog', 'StepIndex') IS NULL
        ALTER TABLE dbo.AgentMemoryLog ADD StepIndex INT NULL;
END

UPDATE dbo.AgentMemoryLog
SET TechnicalSuccess = ISNULL(TechnicalSuccess, IsSuccess),
    TaskSuccess = ISNULL(TaskSuccess, IsSuccess)
WHERE TechnicalSuccess IS NULL OR TaskSuccess IS NULL;";

                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(ct);
                await using var cmd = new SqlCommand(sql, conn);
                cmd.CommandTimeout = 30;
                await cmd.ExecuteNonQueryAsync(ct);
                _schemaReady = true;
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"SqlServerAgentMemoryPersistence.EnsureSchemaAsync : {ex.Message}");
                throw;
            }
            finally
            {
                _schemaLock.Release();
            }
        }

        private static Dictionary<string, object> DeserializeParameters(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in doc.RootElement.EnumerateObject())
                    result[prop.Name] = ConvertJsonElement(prop.Value);
                return result;
            }
            catch
            {
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static object? ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)!, StringComparer.OrdinalIgnoreCase),
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.TryGetDouble(out var d) ? d : element.GetRawText(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.GetRawText()
            };
        }
    }
}
