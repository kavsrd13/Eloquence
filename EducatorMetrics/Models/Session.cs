using System;
using System.Collections.Generic;

namespace EducatorMetrics.Models
{
    public class Session
    {
        public int Id { get; set; }
        public DateTime SessionDate { get; set; }
        public int DurationSeconds { get; set; }
        
        public int OverallEnglishScore { get; set; }
        public int OverallTechScore { get; set; }

        // Navigation property required by EF Core
        public ICollection<Evaluation> Evaluations { get; set; } = new List<Evaluation>();
    }
}
