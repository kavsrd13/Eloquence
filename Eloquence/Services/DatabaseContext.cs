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
            
            // If the database already existed (created == false) from an older version of the app,
            // EnsureCreated() won't add new tables. We manually create TranscriptRecords here.
            if (!created)
            {
                try
                {
                    Database.ExecuteSqlRaw(@"
                        CREATE TABLE IF NOT EXISTS ""TranscriptRecords"" (
                            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_TranscriptRecords"" PRIMARY KEY AUTOINCREMENT,
                            ""SessionId"" INTEGER NOT NULL,
                            ""Timestamp"" TEXT NOT NULL,
                            ""Text"" TEXT NOT NULL,
                            ""IsEvaluated"" INTEGER NOT NULL,
                            CONSTRAINT ""FK_TranscriptRecords_Sessions_SessionId"" FOREIGN KEY (""SessionId"") REFERENCES ""Sessions"" (""Id"") ON DELETE CASCADE
                        );
                    ");
                }
                catch { }
            }
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

