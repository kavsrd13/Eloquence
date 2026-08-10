using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Eloquence.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace Eloquence.Services
{
    public class BatchEvaluationService
    {
        private readonly EvaluationService _evaluationService;
        public event Action<string>? OnStatusChanged;
        public event Action<Evaluation>? OnEvaluationCompleted;

        public BatchEvaluationService(EvaluationService evaluationService)
        {
            _evaluationService = evaluationService;
        }

        public async Task EvaluatePendingTranscriptsAsync()
        {
            try
            {
                OnStatusChanged?.Invoke("Batch Evaluation: Processing...");

                using var db = new DatabaseContext();
                
                // Get unevaluated records for today
                var pendingRecords = await db.TranscriptRecords
                    .Where(t => !t.IsEvaluated && t.Timestamp.Date == DateTime.Today)
                    .OrderBy(t => t.Timestamp)
                    .ToListAsync();

                if (!pendingRecords.Any())
                {
                    OnStatusChanged?.Invoke("Batch Evaluation: No pending records.");
                    return;
                }

                // Load deployment name
                string deploymentName = string.Empty;
                var settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Eloquence", "settings.json");
                if (File.Exists(settingsPath))
                {
                    try
                    {
                        var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
                        if (doc.RootElement.TryGetProperty("DeploymentName", out var dn))
                        {
                            var val = dn.GetString();
                            if (!string.IsNullOrEmpty(val)) deploymentName = val;
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(deploymentName))
                {
                    OnStatusChanged?.Invoke("Batch Evaluation Error: Deployment Name not configured.");
                    return;
                }

                // Group records into chunks of ~1500 words to avoid token limits
                var batches = new List<List<TranscriptRecord>>();
                var currentBatch = new List<TranscriptRecord>();
                int currentWordCount = 0;

                foreach (var record in pendingRecords)
                {
                    int words = record.Text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    if (currentWordCount + words > 1500 && currentBatch.Any())
                    {
                        batches.Add(currentBatch);
                        currentBatch = new List<TranscriptRecord>();
                        currentWordCount = 0;
                    }
                    currentBatch.Add(record);
                    currentWordCount += words;
                }
                
                if (currentBatch.Any())
                {
                    batches.Add(currentBatch);
                }

                // Evaluate each batch
                for (int i = 0; i < batches.Count; i++)
                {
                    var batch = batches[i];
                    OnStatusChanged?.Invoke($"Batch Evaluation: Sending batch {i + 1} of {batches.Count} to AI...");
                    
                    var combinedText = string.Join("\n\n", batch.Select(b => b.Text));
                    var evalResult = await _evaluationService.EvaluateAsync(deploymentName, combinedText);

                    var session = await db.Sessions.FirstOrDefaultAsync(s => s.SessionDate.Date == DateTime.Today);
                    if (session == null)
                    {
                        session = new Session { SessionDate = DateTime.Today };
                        db.Sessions.Add(session);
                        await db.SaveChangesAsync();
                    }

                    var evaluation = new Evaluation
                    {
                        SessionId = session.Id,
                        Timestamp = DateTime.UtcNow,
                        TranscriptChunk = combinedText,
                        LexicalScore = evalResult.English.LexicalPrecision,
                        DiscourseScore = evalResult.English.DiscourseMarkers,
                        SyntacticScore = evalResult.English.SyntacticVariety,
                        ConcisenessScore = evalResult.English.Conciseness,
                        FluencyScore = evalResult.English.Fluency,
                        AccuracyScore = evalResult.Tech.ConceptualAccuracy,
                        ArchitectureScore = evalResult.Tech.ArchitecturalClarity,
                        PedagogyScore = evalResult.Tech.PedagogicalScaffolding,
                        RealWorldScore = evalResult.Tech.RealWorldApplication,
                        AnalogyScore = evalResult.Tech.AnalogyEffectiveness,
                        LlmFeedbackJson = JsonSerializer.Serialize(new { English = evalResult.English, Tech = evalResult.Tech })
                    };

                    db.Evaluations.Add(evaluation);
                    
                    var englishLog = new LlmLog 
                    {
                        Timestamp = DateTime.UtcNow,
                        Model = deploymentName,
                        PromptTokens = evalResult.EngPromptTokens,
                        CompletionTokens = evalResult.EngCompTokens,
                        TotalTokens = evalResult.EngPromptTokens + evalResult.EngCompTokens,
                        IsSuccess = true
                    };
                    
                    var techLog = new LlmLog 
                    {
                        Timestamp = DateTime.UtcNow,
                        Model = deploymentName,
                        PromptTokens = evalResult.TechPromptTokens,
                        CompletionTokens = evalResult.TechCompTokens,
                        TotalTokens = evalResult.TechPromptTokens + evalResult.TechCompTokens,
                        IsSuccess = true
                    };

                    db.LlmLogs.Add(englishLog);
                    db.LlmLogs.Add(techLog);

                    foreach (var record in batch)
                    {
                        record.IsEvaluated = true;
                    }
                    
                    await db.SaveChangesAsync();
                    OnEvaluationCompleted?.Invoke(evaluation);
                }

                OnStatusChanged?.Invoke($"Batch Evaluation: Finished processing {pendingRecords.Count} chunks.");
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"Batch Evaluation Error: {ex.Message}");
            }
        }
    }
}

