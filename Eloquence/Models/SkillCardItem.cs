namespace Eloquence.Models
{
    /// <summary>
    /// Represents a single skill metric for display in the dashboard skill cards grid.
    /// </summary>
    public class SkillCardItem
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "English"; // "English" or "Tech"
        public int Score { get; set; }
        public int Delta { get; set; } // Change from previous session
        public string Level { get; set; } = "Novice";
        public string LevelColor { get; set; } = "#EF4444";
        public string DeltaText { get; set; } = string.Empty;

        public static (string Level, string Color) GetLevel(int score)
        {
            return score switch
            {
                >= 86 => ("Expert", "#3B82F6"),
                >= 71 => ("Proficient", "#10B981"),
                >= 51 => ("Competent", "#84CC16"),
                >= 31 => ("Developing", "#F59E0B"),
                _ => ("Novice", "#EF4444")
            };
        }

        public static string GetDeltaText(int delta)
        {
            if (delta > 0) return $"↑ +{delta}";
            if (delta < 0) return $"↓ {delta}";
            return "→ Steady";
        }
    }
}
