using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EducatorMetrics.Models
{
    public class PhraseImprovement
    {
        [JsonPropertyName("OriginalPhrase")]
        public string OriginalPhrase { get; set; } = string.Empty;

        [JsonPropertyName("Score")]
        public int Score { get; set; }

        [JsonPropertyName("ImprovedPhrase")]
        public string ImprovedPhrase { get; set; } = string.Empty;

        [JsonPropertyName("Reasoning")]
        public string Reasoning { get; set; } = string.Empty;
    }

    public class RepetitiveWordItem
    {
        [JsonPropertyName("Word")]
        public string Word { get; set; } = string.Empty;

        [JsonPropertyName("Count")]
        public int Count { get; set; }

        [JsonPropertyName("SuggestedAlternatives")]
        public List<string> SuggestedAlternatives { get; set; } = new();
    }

    public class BloatedSentence
    {
        [JsonPropertyName("Original")]
        public string Original { get; set; } = string.Empty;

        [JsonPropertyName("Improved")]
        public string Improved { get; set; } = string.Empty;
    }

    public class CriticalMistake
    {
        [JsonPropertyName("WhatYouSaid")]
        public string WhatYouSaid { get; set; } = string.Empty;

        [JsonPropertyName("WhatYouShouldSay")]
        public string WhatYouShouldSay { get; set; } = string.Empty;

        [JsonPropertyName("WhyItMatters")]
        public string WhyItMatters { get; set; } = string.Empty;

        [JsonPropertyName("Severity")]
        public string Severity { get; set; } = "Medium"; // "Critical", "High", "Medium"
    }

    public class EnglishEvaluationResult
    {
        [JsonPropertyName("LexicalPrecision")]
        public int LexicalPrecision { get; set; }
        [JsonPropertyName("DiscourseMarkers")]
        public int DiscourseMarkers { get; set; }
        [JsonPropertyName("SyntacticVariety")]
        public int SyntacticVariety { get; set; }
        [JsonPropertyName("Conciseness")]
        public int Conciseness { get; set; }
        [JsonPropertyName("Fluency")]
        public int Fluency { get; set; }

        [JsonPropertyName("FillerWordsDetected")]
        public List<string> FillerWordsDetected { get; set; } = new();

        [JsonPropertyName("RepetitiveWords")]
        public List<RepetitiveWordItem> RepetitiveWords { get; set; } = new();

        [JsonPropertyName("WeakVocabulary")]
        public List<PhraseImprovement> WeakVocabulary { get; set; } = new();

        [JsonPropertyName("PhraseImprovements")]
        public List<PhraseImprovement> PhraseImprovements { get; set; } = new();

        [JsonPropertyName("BloatedSentences")]
        public List<BloatedSentence> BloatedSentences { get; set; } = new();

        [JsonPropertyName("CriticalMistakes")]
        public List<CriticalMistake> CriticalMistakes { get; set; } = new();
    }

    public class AnalogyImprovement
    {
        [JsonPropertyName("Topic")]
        public string Topic { get; set; } = string.Empty;

        [JsonPropertyName("SuggestedAnalogy")]
        public string SuggestedAnalogy { get; set; } = string.Empty;
    }

    public class TechMistake
    {
        [JsonPropertyName("WhatYouSaid")]
        public string WhatYouSaid { get; set; } = string.Empty;

        [JsonPropertyName("WhatYouShouldSay")]
        public string WhatYouShouldSay { get; set; } = string.Empty;

        [JsonPropertyName("WhyItMatters")]
        public string WhyItMatters { get; set; } = string.Empty;
    }

    public class TechEvaluationResult
    {
        [JsonPropertyName("ConceptualAccuracy")]
        public int ConceptualAccuracy { get; set; }
        [JsonPropertyName("ArchitecturalClarity")]
        public int ArchitecturalClarity { get; set; }
        [JsonPropertyName("PedagogicalScaffolding")]
        public int PedagogicalScaffolding { get; set; }
        [JsonPropertyName("RealWorldApplication")]
        public int RealWorldApplication { get; set; }
        [JsonPropertyName("AnalogyEffectiveness")]
        public int AnalogyEffectiveness { get; set; }

        [JsonPropertyName("TechnicalInaccuracies")]
        public List<string> TechnicalInaccuracies { get; set; } = new();

        [JsonPropertyName("TechnicalMistakes")]
        public List<TechMistake> TechnicalMistakes { get; set; } = new();

        [JsonPropertyName("AnalogyImprovements")]
        public List<AnalogyImprovement> AnalogyImprovements { get; set; } = new();

        [JsonPropertyName("StrongExplanations")]
        public List<string> StrongExplanations { get; set; } = new();
    }
}
