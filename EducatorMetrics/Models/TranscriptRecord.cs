using System;

namespace EducatorMetrics.Models
{
    public class TranscriptRecord
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public Session Session { get; set; } = null!;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Text { get; set; } = string.Empty;
        public bool IsEvaluated { get; set; } = false;
    }
}
