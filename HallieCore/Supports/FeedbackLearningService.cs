using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Text;

namespace HallieCore.Services
{
    public class FeedbackLearningService
    {
        private readonly string _connectionString;

        public FeedbackLearningService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<RouterLearningRule>> GetRouterHintsAsync(string userPrompt)
        {
            var rules = new List<RouterLearningRule>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT TOP 5 ToolUsed, ExpectedTool, ErrorClass
                FROM FeedbackLog
                WHERE PromptText LIKE '%' + @prompt + '%'
                AND Outcome = 'failed'
                ORDER BY CreatedAtUtc DESC", conn);

            cmd.Parameters.AddWithValue("@prompt", userPrompt);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                rules.Add(new RouterLearningRule
                {
                    ToolUsed = reader["ToolUsed"]?.ToString(),
                    ExpectedTool = reader["ExpectedTool"]?.ToString(),
                    Reason = reader["ErrorClass"]?.ToString()
                });
            }

            return rules;
        }
    }

    public class RouterLearningRule
    {
        public string ToolUsed { get; set; } = "";
        public string ExpectedTool { get; set; } = "";
        public string Reason { get; set; } = "";
    }
}
