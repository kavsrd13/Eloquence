using System;
using System.ComponentModel.DataAnnotations;

namespace Eloquence.Models
{
    public class LlmLog
    {
        [Key]
        public int Id { get; set; }
        
        public DateTime Timestamp { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public string Model { get; set; } = string.Empty;
        public TimeSpan Latency { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}

