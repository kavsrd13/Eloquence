using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Eloquence.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Eloquence.Services
{
    public class EvaluationService
    {
        private AzureOpenAIClient? _client;
        
        public bool IsConfigured => _client != null;

        public EvaluationService(string? endpoint, string? apiKey)
        {
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
                return;
            try
            {
                _client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            }
            catch
            {
                _client = null;
            }
        }

        public async Task<(EnglishEvaluationResult English, TechEvaluationResult Tech, List<(string AgentName, int PromptTokens, int CompTokens)> TokenUsage)> EvaluateAsync(string deploymentName, string transcript)
        {
            if (_client == null)
                return (new EnglishEvaluationResult(), new TechEvaluationResult(), new List<(string, int, int)>());
            
            var chatClient = _client.GetChatClient(deploymentName);
            
            var task1 = RunEnglishScorerAsync(chatClient, transcript);
            var task2 = RunEnglishCoachAsync(chatClient, transcript);
            var task3 = RunConfidenceAnalystAsync(chatClient, transcript);
            var task4 = RunTechScorerAsync(chatClient, transcript);
            var task5 = RunTechReviewerAsync(chatClient, transcript);

            await Task.WhenAll(task1, task2, task3, task4, task5);

            var result1 = task1.Result;
            var result2 = task2.Result;
            var result3 = task3.Result;
            var result4 = task4.Result;
            var result5 = task5.Result;

            var englishResult = new EnglishEvaluationResult
            {
                LexicalPrecision = result1.Result?.LexicalPrecision ?? 0,
                DiscourseMarkers = result1.Result?.DiscourseMarkers ?? 0,
                SyntacticVariety = result1.Result?.SyntacticVariety ?? 0,
                Conciseness = result1.Result?.Conciseness ?? 0,
                Fluency = result1.Result?.Fluency ?? 0,
                CoherenceStructure = result1.Result?.CoherenceStructure ?? 0,
                GrammaticalAccuracy = result1.Result?.GrammaticalAccuracy ?? 0,
                ConfidenceLanguage = result1.Result?.ConfidenceLanguage ?? 0,

                PhraseImprovements = result2.Result?.PhraseImprovements,
                WeakVocabulary = result2.Result?.WeakVocabulary,
                BloatedSentences = result2.Result?.BloatedSentences,
                CriticalMistakes = result2.Result?.CriticalMistakes,

                FillerWordsDetected = result3.Result?.FillerWordsDetected,
                RepetitiveWords = result3.Result?.RepetitiveWords,
                HedgingPhrases = result3.Result?.HedgingPhrases
            };

            var techResult = new TechEvaluationResult
            {
                ConceptualAccuracy = result4.Result?.ConceptualAccuracy ?? 0,
                ArchitecturalClarity = result4.Result?.ArchitecturalClarity ?? 0,
                PedagogicalScaffolding = result4.Result?.PedagogicalScaffolding ?? 0,
                RealWorldApplication = result4.Result?.RealWorldApplication ?? 0,
                AnalogyEffectiveness = result4.Result?.AnalogyEffectiveness ?? 0,
                DepthOfExplanation = result4.Result?.DepthOfExplanation ?? 0,
                TradeoffAnalysis = result4.Result?.TradeoffAnalysis ?? 0,

                TechnicalInaccuracies = result5.Result?.TechnicalInaccuracies,
                TechnicalMistakes = result5.Result?.TechnicalMistakes,
                AnalogyImprovements = result5.Result?.AnalogyImprovements,
                StrongExplanations = result5.Result?.StrongExplanations
            };

            var tokenUsage = new List<(string AgentName, int PromptTokens, int CompTokens)>
            {
                ("EnglishScorer", result1.PromptTokens, result1.CompletionTokens),
                ("EnglishCoach", result2.PromptTokens, result2.CompletionTokens),
                ("ConfidenceAnalyst", result3.PromptTokens, result3.CompletionTokens),
                ("TechScorer", result4.PromptTokens, result4.CompletionTokens),
                ("TechReviewer", result5.PromptTokens, result5.CompletionTokens)
            };

            return (englishResult, techResult, tokenUsage);
        }

        private async Task<(EnglishEvaluationResult Result, int PromptTokens, int CompletionTokens)> RunEnglishScorerAsync(ChatClient client, string transcript)
        {
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "english_scorer", 
                    BinaryData.FromObjectAsJson(new {
                        type = "object",
                        properties = new {
                            LexicalPrecision = new { type = "integer" },
                            DiscourseMarkers = new { type = "integer" },
                            SyntacticVariety = new { type = "integer" },
                            Conciseness = new { type = "integer" },
                            Fluency = new { type = "integer" },
                            CoherenceStructure = new { type = "integer" },
                            GrammaticalAccuracy = new { type = "integer" },
                            ConfidenceLanguage = new { type = "integer" }
                        },
                        required = new[] { "LexicalPrecision", "DiscourseMarkers", "SyntacticVariety", "Conciseness", "Fluency", "CoherenceStructure", "GrammaticalAccuracy", "ConfidenceLanguage" },
                        additionalProperties = false
                    }),
                    "English Scorer Schema",
                    true)
            };

            var systemPrompt = @"You are an expert Linguistic Assessor and IELTS/CEFR C2 Senior Examiner evaluating spoken English communication.
Assess the transcript strictly across all 8 metrics on a calibrated 1-100 scale:

SCORING BENCHMARKS:
- 90-100 (Executive / Native Master): Flawless lexical precision, sophisticated discourse structure, dynamic sentence complexity, zero unnecessary words, effortless fluency, impeccable grammar, commanding presence.
- 75-89 (Proficient Professional): Clear, effective vocabulary, sound transitions, good structural variety, minor conversational filler or redundancy, strong overall coherence and grammatical accuracy.
- 60-74 (Developing / Competent): Understandable but relies on repetitive simple syntax, vague words ('stuff', 'things', 'good'), noticeable disfluencies, occasional grammatical lapses, or hesitant delivery.
- Below 60 (Novice / Fragmented): Frequent grammatical breakdowns, severe lack of vocabulary precision, broken flow, highly fragmented coherence.

METRIC DEFINITIONS:
1. LexicalPrecision (1-100): Exactness, richness, and appropriateness of vocabulary; avoids vague, informal, or imprecise words.
2. DiscourseMarkers (1-100): Effective use of transition words, signposts, and connectors (e.g., 'Consequently', 'In contrast', 'Furthermore', 'To illustrate') to guide the listener.
3. SyntacticVariety (1-100): Diversity of sentence structures (compound, complex, conditional, active/passive balance) rather than monotonous repetitive patterns.
4. Conciseness (1-100): High signal-to-noise ratio; elimination of fluff, circumlocution, and rambling preambles.
5. Fluency (1-100): Smooth flow and continuity of thought without disjointed breaks, false starts, or mid-sentence stalls.
6. CoherenceStructure (1-100): Logical organization and progressive narrative flow (Premise -> Development -> Conclusion).
7. GrammaticalAccuracy (1-100): Correctness of tenses, subject-verb agreement, modifiers, articles, and prepositions.
8. ConfidenceLanguage (1-100): Assertive, direct phrasing that avoids apologetic, hesitant, or passive qualifiers.";
            var response = await client.CompleteChatAsync(new ChatMessage[] { new SystemChatMessage(systemPrompt), new UserChatMessage(transcript) }, options);
            
            if (response.Value.Content == null || response.Value.Content.Count == 0)
                return (new EnglishEvaluationResult(), 0, 0);

            var result = JsonSerializer.Deserialize<EnglishEvaluationResult>(response.Value.Content[0].Text);
            return (result ?? new EnglishEvaluationResult(), response.Value.Usage.InputTokenCount, response.Value.Usage.OutputTokenCount);
        }

        private async Task<(EnglishEvaluationResult Result, int PromptTokens, int CompletionTokens)> RunEnglishCoachAsync(ChatClient client, string transcript)
        {
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "english_coach", 
                    BinaryData.FromObjectAsJson(new {
                        type = "object",
                        properties = new {
                            WeakVocabulary = new { 
                                type = "array", 
                                items = new { 
                                     type = "object", 
                                    properties = new {
                                        OriginalPhrase = new { type = "string" },
                                        Score = new { type = "integer" },
                                        ImprovedPhrase = new { type = "string" },
                                        Reasoning = new { type = "string" }
                                    },
                                    required = new[] { "OriginalPhrase", "Score", "ImprovedPhrase", "Reasoning" },
                                    additionalProperties = false
                                } 
                            },
                            PhraseImprovements = new { 
                                type = "array", 
                                items = new { 
                                    type = "object", 
                                    properties = new {
                                        OriginalPhrase = new { type = "string" },
                                        Score = new { type = "integer" },
                                        ImprovedPhrase = new { type = "string" },
                                        Reasoning = new { type = "string" }
                                    },
                                    required = new[] { "OriginalPhrase", "Score", "ImprovedPhrase", "Reasoning" },
                                    additionalProperties = false
                                } 
                            },
                            BloatedSentences = new {
                                type = "array",
                                items = new {
                                    type = "object",
                                    properties = new {
                                        Original = new { type = "string" },
                                        Improved = new { type = "string" }
                                    },
                                    required = new[] { "Original", "Improved" },
                                    additionalProperties = false
                                }
                            },
                            CriticalMistakes = new {
                                type = "array",
                                items = new {
                                    type = "object",
                                    properties = new {
                                        WhatYouSaid = new { type = "string" },
                                        WhatYouShouldSay = new { type = "string" },
                                        WhyItMatters = new { type = "string" },
                                        Severity = new { type = "string" }
                                    },
                                    required = new[] { "WhatYouSaid", "WhatYouShouldSay", "WhyItMatters", "Severity" },
                                    additionalProperties = false
                                }
                            }
                        },
                        required = new[] { "PhraseImprovements", "WeakVocabulary", "BloatedSentences", "CriticalMistakes" },
                        additionalProperties = false
                    }),
                    "English Coach Schema",
                    true)
            };

            var systemPrompt = @"You are an Elite Executive Speech Coach and Communications Director.
Analyze the transcript to provide high-leverage, practical improvements.

CRITICAL CONSTRAINTS:
1. VERBATIM REQUIREMENT: Every 'OriginalPhrase', 'WhatYouSaid', and 'Original' MUST BE AN EXACT CHARACTER-FOR-CHARACTER SUBSTRING from the transcript. Do NOT invent, summarize, or misquote what was said.
2. HIGH-IMPACT ONLY: Provide the top 3-5 most impactful items per category. Avoid trivial conversational nitpicks.
3. EXCELLENCE IN REWRITES: Rewrites must sound natural, authoritative, polished, and executive-ready.

CATEGORIES TO EVALUATE:
- WeakVocabulary: Identify vague, weak, or overly casual words (e.g., 'stuff', 'things', 'good', 'kind of made') and provide elevated, precise alternatives.
- PhraseImprovements: Identify clunky, awkward, or sub-optimal expressions and provide sophisticated, high-clarity alternatives with clear reasoning and a score (1-100) reflecting the quality of the original phrasing.
- BloatedSentences: Identify verbose, run-on, or convoluted sentences and condense them into crisp, punchy, high-impact statements.
- CriticalMistakes: Flag significant grammatical errors, wrong idioms, or broken sentence structures. Categorize severity ('Low', 'Medium', 'High') and explain exactly why it undermines credibility.";
            var response = await client.CompleteChatAsync(new ChatMessage[] { new SystemChatMessage(systemPrompt), new UserChatMessage(transcript) }, options);
            
            if (response.Value.Content == null || response.Value.Content.Count == 0)
                return (new EnglishEvaluationResult(), 0, 0);

            var result = JsonSerializer.Deserialize<EnglishEvaluationResult>(response.Value.Content[0].Text);
            return (result ?? new EnglishEvaluationResult(), response.Value.Usage.InputTokenCount, response.Value.Usage.OutputTokenCount);
        }

        private async Task<(EnglishEvaluationResult Result, int PromptTokens, int CompletionTokens)> RunConfidenceAnalystAsync(ChatClient client, string transcript)
        {
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "confidence_analyst", 
                    BinaryData.FromObjectAsJson(new {
                        type = "object",
                        properties = new {
                            FillerWordsDetected = new { type = "array", items = new { type = "string" } },
                            RepetitiveWords = new { 
                                type = "array", 
                                items = new { 
                                    type = "object", 
                                    properties = new {
                                        Word = new { type = "string" },
                                        Count = new { type = "integer" },
                                        SuggestedAlternatives = new { type = "array", items = new { type = "string" } }
                                    },
                                    required = new[] { "Word", "Count", "SuggestedAlternatives" },
                                    additionalProperties = false
                                } 
                            },
                            HedgingPhrases = new {
                                type = "array",
                                items = new {
                                    type = "object",
                                    properties = new {
                                        Original = new { type = "string" },
                                        Assertive = new { type = "string" }
                                    },
                                    required = new[] { "Original", "Assertive" },
                                    additionalProperties = false
                                }
                            }
                        },
                        required = new[] { "FillerWordsDetected", "RepetitiveWords", "HedgingPhrases" },
                        additionalProperties = false
                    }),
                    "Confidence Analyst Schema",
                    true)
            };

            var systemPrompt = @"You are a Behavioral Communications Analyst specializing in Executive Presence and Assertiveness.
Analyze the transcript for language patterns that undermine authority, decisiveness, and credibility.

CRITICAL RULES:
1. VERBATIM EXTRACTION: All identified phrases in 'Original' must be exact character-for-character quotes from the transcript.
2. EPISTEMIC NUANCE VS HEDGING:
   - DO NOT penalize legitimate technical uncertainty or scientific nuance (e.g., 'The benchmark indicates a probable 15% latency increase under peak load').
   - DO penalize unassertive, self-doubting, or apologetic hedging (e.g., 'I think maybe we sort of could do this, I guess', 'I'm no expert but...', 'Just my two cents').

CATEGORIES TO EXTRACT:
- FillerWordsDetected: Detect and list vocal crutches and conversational fillers (e.g., 'um', 'uh', 'like', 'you know', 'basically', 'literally', 'actually', 'sort of', 'kind of').
- RepetitiveWords: Identify words or short phrases repeated 3+ times as cognitive crutches, along with count and elevated alternative synonyms.
- HedgingPhrases: Extract non-assertive, hesitant, or apologetic phrases and provide commanding, decisive, executive-ready rewrites that retain the intended core meaning without false humility.";
            var response = await client.CompleteChatAsync(new ChatMessage[] { new SystemChatMessage(systemPrompt), new UserChatMessage(transcript) }, options);
            
            if (response.Value.Content == null || response.Value.Content.Count == 0)
                return (new EnglishEvaluationResult(), 0, 0);

            var result = JsonSerializer.Deserialize<EnglishEvaluationResult>(response.Value.Content[0].Text);
            return (result ?? new EnglishEvaluationResult(), response.Value.Usage.InputTokenCount, response.Value.Usage.OutputTokenCount);
        }

        private async Task<(TechEvaluationResult Result, int PromptTokens, int CompletionTokens)> RunTechScorerAsync(ChatClient client, string transcript)
        {
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "tech_scorer", 
                    BinaryData.FromObjectAsJson(new {
                        type = "object",
                        properties = new {
                            ConceptualAccuracy = new { type = "integer" },
                            ArchitecturalClarity = new { type = "integer" },
                            PedagogicalScaffolding = new { type = "integer" },
                            RealWorldApplication = new { type = "integer" },
                            AnalogyEffectiveness = new { type = "integer" },
                            DepthOfExplanation = new { type = "integer" },
                            TradeoffAnalysis = new { type = "integer" }
                        },
                        required = new[] { "ConceptualAccuracy", "ArchitecturalClarity", "PedagogicalScaffolding", "RealWorldApplication", "AnalogyEffectiveness", "DepthOfExplanation", "TradeoffAnalysis" },
                        additionalProperties = false
                    }),
                    "Tech Scorer Schema",
                    true)
            };

            var systemPrompt = @"You are a Principal Systems Architect and Engineering Director evaluating technical communication and architectural pedagogy.
Evaluate the transcript strictly across all 7 technical metrics on a calibrated 1-100 scale:

SCORING BENCHMARKS:
- 90-100 (Staff / Principal Architect): Deep conceptual precision, crystal-clear architectural boundaries, masterclass scaffolding from fundamentals to edge cases, realistic production tradeoffs (SLAs, failure domains, latency vs throughput), highly effective analogies.
- 75-89 (Senior Engineer): Technically accurate, sound explanation of components and data flow, practical real-world context, good trade-off awareness, minor gaps in depth or edge-case handling.
- 60-74 (Mid-Level / Developing): Basic conceptual understanding, but relies on high-level buzzwords, hand-waving explanations of underlying mechanisms, weak or missing trade-off discussions, or strained analogies.
- Below 60 (Junior / Inaccurate): Factually incorrect technical assertions, confusing architectural relationships, lacks depth, unable to explain how components actually function under the hood.

METRIC DEFINITIONS:
1. ConceptualAccuracy (1-100): Correctness and rigor of technical definitions, algorithms, protocols, and underlying principles.
2. ArchitecturalClarity (1-100): Clear explanation of system components, boundaries, interfaces, dependencies, and end-to-end data flow.
3. PedagogicalScaffolding (1-100): Step-by-step conceptual buildup, guiding the listener logically from basic principles to complex systems.
4. RealWorldApplication (1-100): Grounding concepts in production realities (e.g., deployment patterns, operational failure modes, scalability, reliability).
5. AnalogyEffectiveness (1-100): Accuracy, clarity, and explanatory power of metaphors used to demystify complex technical concepts without breaking down.
6. DepthOfExplanation (1-100): Thoroughness in explaining how and why systems work under the hood (memory layout, execution model, concurrency, network hops) rather than just surface-level APIs.
7. TradeoffAnalysis (1-100): Explicit, balanced evaluation of architectural tradeoffs (e.g., latency vs throughput, consistency vs availability, complexity vs maintainability).";
            var response = await client.CompleteChatAsync(new ChatMessage[] { new SystemChatMessage(systemPrompt), new UserChatMessage(transcript) }, options);
            
            if (response.Value.Content == null || response.Value.Content.Count == 0)
                return (new TechEvaluationResult(), 0, 0);

            var result = JsonSerializer.Deserialize<TechEvaluationResult>(response.Value.Content[0].Text);
            return (result ?? new TechEvaluationResult(), response.Value.Usage.InputTokenCount, response.Value.Usage.OutputTokenCount);
        }

        private async Task<(TechEvaluationResult Result, int PromptTokens, int CompletionTokens)> RunTechReviewerAsync(ChatClient client, string transcript)
        {
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "tech_reviewer", 
                    BinaryData.FromObjectAsJson(new {
                        type = "object",
                        properties = new {
                            TechnicalInaccuracies = new { type = "array", items = new { type = "string" } },
                            TechnicalMistakes = new {
                                type = "array",
                                items = new {
                                    type = "object",
                                    properties = new {
                                        WhatYouSaid = new { type = "string" },
                                        WhatYouShouldSay = new { type = "string" },
                                        WhyItMatters = new { type = "string" }
                                    },
                                    required = new[] { "WhatYouSaid", "WhatYouShouldSay", "WhyItMatters" },
                                    additionalProperties = false
                                }
                            },
                            AnalogyImprovements = new { 
                                type = "array", 
                                items = new { 
                                    type = "object", 
                                    properties = new {
                                        Topic = new { type = "string" },
                                        SuggestedAnalogy = new { type = "string" }
                                    },
                                    required = new[] { "Topic", "SuggestedAnalogy" },
                                    additionalProperties = false
                                } 
                            },
                            StrongExplanations = new { type = "array", items = new { type = "string" } }
                        },
                        required = new[] { "TechnicalInaccuracies", "TechnicalMistakes", "AnalogyImprovements", "StrongExplanations" },
                        additionalProperties = false
                    }),
                    "Tech Reviewer Schema",
                    true)
            };

            var systemPrompt = @"You are a Senior Staff Engineer conducting a rigorous Technical Review and Feedback Session on spoken engineering communication.
Analyze the technical substance of the transcript and provide actionable, high-value engineering feedback.

CRITICAL RULES:
1. VERBATIM CITATIONS: In 'TechnicalMistakes', the 'WhatYouSaid' field must be an exact verbatim substring from the transcript.
2. FACTUAL RIGOR: Flag only genuine technical errors, incorrect architecture assumptions, or flawed mental models. Do not nitpick valid stylistic differences.
3. ENGINEERING VALUE: Feedback must focus on real-world engineering impact (production bugs, performance bottlenecks, system failure modes).

CATEGORIES TO EVALUATE:
- TechnicalInaccuracies: List any factually incorrect statements, false architectural assumptions, or inaccurate technical claims made in the transcript.
- TechnicalMistakes: Detail specific conceptual errors with:
  * WhatYouSaid: Exact quote from transcript.
  * WhatYouShouldSay: Technically precise, correct statement.
  * WhyItMatters: The underlying architectural, operational, or algorithmic reason this distinction is critical.
- AnalogyImprovements: Review any analogies or mental models used. If an analogy breaks down under scale or misleads the listener, provide a robust, technically sound analogy.
- StrongExplanations: Quote or summarize moments of exceptional technical clarity, brilliant pedagogical scaffolding, or thorough trade-off analysis to reinforce outstanding engineering communication habits.";
            var response = await client.CompleteChatAsync(new ChatMessage[] { new SystemChatMessage(systemPrompt), new UserChatMessage(transcript) }, options);
            
            if (response.Value.Content == null || response.Value.Content.Count == 0)
                return (new TechEvaluationResult(), 0, 0);

            var result = JsonSerializer.Deserialize<TechEvaluationResult>(response.Value.Content[0].Text);
            return (result ?? new TechEvaluationResult(), response.Value.Usage.InputTokenCount, response.Value.Usage.OutputTokenCount);
        }
    }
}
