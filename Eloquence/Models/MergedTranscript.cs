using System;

namespace Eloquence.Models
{
    public class MergedTranscript
    {
        public DateTime Date { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsEvaluated { get; set; }
    }
}

