using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace Eloquence.Services
{
    public class DatabaseContext : DbContext
    {
        public DbSet<Models.Session> Sessions { get; set; } = null!;
        public DbSet<Models.Evaluation> Evaluations { get; set; } = null!;
        public DbSet<Models.TranscriptRecord> TranscriptRecords { get; set; } = null!;
        public DbSet<Models.LlmLog> LlmLogs { get; set; } = null!;

        public DatabaseContext()
        {
            var created = Database.EnsureCreated();
            
            // If the database already existed from an older version,
            // EnsureCreated() won't add new tables or columns. We handle migrations manually.
            if (!created)
            {
                RunMigrations();
            }
        }

        private void RunMigrations()
        {
            // Each migration is wrapped in try/catch so it's safe to run multiple times
            // (ALTER TABLE will fail if the column already exists, which is fine)

            // Migration 1: TranscriptRecords table
            TryExecuteSql(@"
                CREATE TABLE IF NOT EXISTS ""TranscriptRecords"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_TranscriptRecords"" PRIMARY KEY AUTOINCREMENT,
                    ""SessionId"" INTEGER NOT NULL,
                    ""Timestamp"" TEXT NOT NULL,
                    ""Text"" TEXT NOT NULL,
                    ""IsEvaluated"" INTEGER NOT NULL,
                    CONSTRAINT ""FK_TranscriptRecords_Sessions_SessionId"" FOREIGN KEY (""SessionId"") REFERENCES ""Sessions"" (""Id"") ON DELETE CASCADE
                );
            ");

            // Migration 2: LlmLogs table
            TryExecuteSql(@"
                CREATE TABLE IF NOT EXISTS ""LlmLogs"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_LlmLogs"" PRIMARY KEY AUTOINCREMENT,
                    ""Timestamp"" TEXT NOT NULL,
                    ""PromptTokens"" INTEGER NOT NULL,
                    ""CompletionTokens"" INTEGER NOT NULL,
                    ""TotalTokens"" INTEGER NOT NULL,
                    ""Model"" TEXT NOT NULL DEFAULT '',
                    ""Latency"" TEXT NOT NULL DEFAULT '00:00:00',
                    ""IsSuccess"" INTEGER NOT NULL DEFAULT 1,
                    ""AgentName"" TEXT NOT NULL DEFAULT '',
                    ""ErrorMessage"" TEXT NOT NULL DEFAULT ''
                );
            ");

            // Migration 3: v2 English metrics (Coherence, Grammar, Confidence)
            TryExecuteSql(@"ALTER TABLE ""Evaluations"" ADD COLUMN ""CoherenceScore"" INTEGER NOT NULL DEFAULT 0;");
            TryExecuteSql(@"ALTER TABLE ""Evaluations"" ADD COLUMN ""GrammarScore"" INTEGER NOT NULL DEFAULT 0;");
            TryExecuteSql(@"ALTER TABLE ""Evaluations"" ADD COLUMN ""ConfidenceScore"" INTEGER NOT NULL DEFAULT 0;");

            // Migration 4: v2 Tech metrics (Depth, Tradeoff)
            TryExecuteSql(@"ALTER TABLE ""Evaluations"" ADD COLUMN ""DepthScore"" INTEGER NOT NULL DEFAULT 0;");
            TryExecuteSql(@"ALTER TABLE ""Evaluations"" ADD COLUMN ""TradeoffScore"" INTEGER NOT NULL DEFAULT 0;");

            // Migration 5: AgentName column on LlmLogs (for existing DBs that already had the table)
            TryExecuteSql(@"ALTER TABLE ""LlmLogs"" ADD COLUMN ""AgentName"" TEXT NOT NULL DEFAULT '';");
        }

        private void TryExecuteSql(string sql)
        {
            try { Database.ExecuteSqlRaw(sql); } catch { }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Eloquence");
            Directory.CreateDirectory(folder);
            var dbPath = Path.Combine(folder, "Eloquence.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}
