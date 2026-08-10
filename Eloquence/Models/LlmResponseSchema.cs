using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Eloquence.Models
{
    // ==========================================
    // ENGLISH EVALUATION MODELS
    // ==========================================

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
        public string Severity { get; set; } = "Medium";
    }

    public class HedgingPhrase
    {
        [JsonPropertyName("Original")]
        public string Original { get; set; } = string.Empty;

        [JsonPropertyName("Assertive")]
        public string Assertive { get; set; } = string.Empty;
    }

    // ==========================================
    // AGENT 1: English Scorer (scores only)
    // ==========================================
    public class EnglishScoreResult
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
        [JsonPropertyName("CoherenceStructure")]
        public int CoherenceStructure { get; set; }
        [JsonPropertyName("GrammaticalAccuracy")]
        public int GrammaticalAccuracy { get; set; }
        [JsonPropertyName("ConfidenceLanguage")]
        public int ConfidenceLanguage { get; set; }
    }

    // ==========================================
    // AGENT 2: English Coach (qualitative feedback)
    // ==========================================
    public class EnglishCoachResult
    {
        [JsonPropertyName("PhraseImprovements")]
        public List<PhraseImprovement> PhraseImprovements { get; set; } = new();

        [JsonPropertyName("WeakVocabulary")]
        public List<PhraseImprovement> WeakVocabulary { get; set; } = new();

        [JsonPropertyName("BloatedSentences")]
        public List<BloatedSentence> BloatedSentences { get; set; } = new();

        [JsonPropertyName("CriticalMistakes")]
        public List<CriticalMistake> CriticalMistakes { get; set; } = new();
    }

    // ==========================================
    // AGENT 3: Confidence Analyst
    // ==========================================
    public class ConfidenceAnalysisResult
    {
        [JsonPropertyName("FillerWordsDetected")]
        public List<string> FillerWordsDetected { get; set; } = new();

        [JsonPropertyName("RepetitiveWords")]
        public List<RepetitiveWordItem> RepetitiveWords { get; set; } = new();

        [JsonPropertyName("HedgingPhrases")]
        public List<HedgingPhrase> HedgingPhrases { get; set; } = new();
    }

    // ==========================================
    // AGENT 4: Tech Scorer (scores only)
    // ==========================================
    public class TechScoreResult
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
        [JsonPropertyName("DepthOfExplanation")]
        public int DepthOfExplanation { get; set; }
        [JsonPropertyName("TradeoffAnalysis")]
        public int TradeoffAnalysis { get; set; }
    }

    // ==========================================
    // AGENT 5: Tech Reviewer (qualitative feedback)
    // ==========================================
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

    public class TechReviewResult
    {
        [JsonPropertyName("TechnicalInaccuracies")]
        public List<string> TechnicalInaccuracies { get; set; } = new();

        [JsonPropertyName("TechnicalMistakes")]
        public List<TechMistake> TechnicalMistakes { get; set; } = new();

        [JsonPropertyName("AnalogyImprovements")]
        public List<AnalogyImprovement> AnalogyImprovements { get; set; } = new();

        [JsonPropertyName("StrongExplanations")]
        public List<string> StrongExplanations { get; set; } = new();
    }

    // ==========================================
    // COMBINED RESULT (for backward compatibility)
    // ==========================================
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
        [JsonPropertyName("CoherenceStructure")]
        public int CoherenceStructure { get; set; }
        [JsonPropertyName("GrammaticalAccuracy")]
        public int GrammaticalAccuracy { get; set; }
        [JsonPropertyName("ConfidenceLanguage")]
        public int ConfidenceLanguage { get; set; }

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

        [JsonPropertyName("HedgingPhrases")]
        public List<HedgingPhrase> HedgingPhrases { get; set; } = new();
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
        [JsonPropertyName("DepthOfExplanation")]
        public int DepthOfExplanation { get; set; }
        [JsonPropertyName("TradeoffAnalysis")]
        public int TradeoffAnalysis { get; set; }

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
