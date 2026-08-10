using System;

namespace EducatorMetrics.Models
{
    public class Evaluation
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public Session Session { get; set; } = null!;
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string TranscriptChunk { get; set; } = string.Empty;
        
        // English Agent Scores (kept permanently)
        public int LexicalScore { get; set; }
        public int DiscourseScore { get; set; }
        public int SyntacticScore { get; set; }
        public int ConcisenessScore { get; set; }
        public int FluencyScore { get; set; }
        
        // Tech Agent Scores (kept permanently)
        public int AccuracyScore { get; set; }
        public int ArchitectureScore { get; set; }
        public int PedagogyScore { get; set; }
        public int RealWorldScore { get; set; }
        public int AnalogyScore { get; set; }
        
        // Raw JSON output (kept permanently for report generation)
        public string LlmFeedbackJson { get; set; } = string.Empty;
    }
}
