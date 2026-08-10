using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using EducatorMetrics.Models;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace EducatorMetrics.Services
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

        public async Task<(EnglishEvaluationResult English, TechEvaluationResult Tech, int EngPromptTokens, int EngCompTokens, int TechPromptTokens, int TechCompTokens)> EvaluateAsync(string deploymentName, string transcript)
        {
            if (_client == null)
                return (new EnglishEvaluationResult(), new TechEvaluationResult(), 0, 0, 0, 0);
            
            var chatClient = _client.GetChatClient(deploymentName);
            
            var englishTask = EvaluateEnglishAsync(chatClient, transcript);
            var techTask = EvaluateTechAsync(chatClient, transcript);

            await Task.WhenAll(englishTask, techTask);
            var engResult = englishTask.Result;
            var techResult = techTask.Result;
            return (engResult.Result, techResult.Result, engResult.PromptTokens, engResult.CompletionTokens, techResult.PromptTokens, techResult.CompletionTokens);
        }

        private async Task<(EnglishEvaluationResult Result, int PromptTokens, int CompletionTokens)> EvaluateEnglishAsync(ChatClient client, string transcript)
        {
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "english_eval", 
                    BinaryData.FromObjectAsJson(new {
                        type = "object",
                        properties = new {
                            LexicalPrecision = new { type = "integer" },
                            DiscourseMarkers = new { type = "integer" },
                            SyntacticVariety = new { type = "integer" },
                            Conciseness = new { type = "integer" },
                            Fluency = new { type = "integer" },
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
                        required = new[] { "LexicalPrecision", "DiscourseMarkers", "SyntacticVariety", "Conciseness", "Fluency", "FillerWordsDetected", "RepetitiveWords", "WeakVocabulary", "PhraseImprovements", "BloatedSentences", "CriticalMistakes" },
                        additionalProperties = false
                    }),
                    "English Evaluation Schema",
                    true)
            };

            var systemPrompt = @"You are an EXTREMELY STRICT English communication coach evaluating a technical trainer.
You are NOT kind. You are NOT forgiving. You are a perfectionist.

SCORING RULES (integers 1-100):
- 1-20: Terrible. Constant mistakes, incoherent.
- 21-40: Below average. Many filler words, poor vocabulary, rambling.
- 41-60: Average. Acceptable but clearly needs improvement.
- 61-80: Good. Minor issues. Most speakers fall here.
- 81-100: Exceptional. ONLY if the speech is genuinely polished, precise, and professional. Do NOT give 80+ unless truly deserved.

YOUR TASKS:
1. Score each metric STRICTLY. Do NOT be generous. An average speaker should get 40-55.
2. Extract EVERY SINGLE filler word (um, uh, like, basically, actually, you know, so, right, I mean, kind of, sort of, literally, honestly, obviously). Even one 'um' must be logged.
3. Track 'RepetitiveWords'. Identify any substantive words the speaker overuses in this chunk. Provide the word, the number of times it was used, and a list of powerful alternatives.
4. Extract 'WeakVocabulary'. Find at least 10 to 15 weak, average, or uninspiring words/phrases used in the text. Suggest sophisticated, high-end vocabulary alternatives. Rate the OriginalPhrase and ImprovedPhrase.
5. ONLY provide 'PhraseImprovements' for phrases that are TRULY TERRIBLE — meaning they are fundamentally grammatically incorrect, highly unprofessional, or extremely repetitive. Do NOT suggest improvements for sentences that are just 'okay' or 'average'. We only want to highlight the worst offenders. Score the phrase (1-100), provide the improved phrase, and explain your reasoning.
6. Identify the most bloated, rambling sentences and rewrite them to be 50% shorter while keeping meaning.
7. MOST IMPORTANTLY: Find the speaker's CRITICAL MISTAKES — the exact phrases where they made a grammar error, used completely wrong words, said something confusing, or communicated so poorly that a student would be misled. Quote the exact problematic phrase in 'WhatYouSaid', provide the correct version in 'WhatYouShouldSay', explain why it matters, and rate severity as 'Critical', 'High', or 'Medium'.

Return strict JSON.";

            var messages = new ChatMessage[]
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(transcript)
            };

            var response = await client.CompleteChatAsync(messages, options);
            if (response.Value.Content == null || response.Value.Content.Count == 0)
                return (new EnglishEvaluationResult(), 0, 0);

            var content = response.Value.Content[0].Text;
            var result = JsonSerializer.Deserialize<EnglishEvaluationResult>(content);
            return (result ?? new EnglishEvaluationResult(), response.Value.Usage.InputTokenCount, response.Value.Usage.OutputTokenCount);
        }

        private async Task<(TechEvaluationResult Result, int PromptTokens, int CompletionTokens)> EvaluateTechAsync(ChatClient client, string transcript)
        {
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "tech_eval", 
                    BinaryData.FromObjectAsJson(new {
                        type = "object",
                        properties = new {
                            ConceptualAccuracy = new { type = "integer" },
                            ArchitecturalClarity = new { type = "integer" },
                            PedagogicalScaffolding = new { type = "integer" },
                            RealWorldApplication = new { type = "integer" },
                            AnalogyEffectiveness = new { type = "integer" },
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
                        required = new[] { "ConceptualAccuracy", "ArchitecturalClarity", "PedagogicalScaffolding", "RealWorldApplication", "AnalogyEffectiveness", "TechnicalInaccuracies", "TechnicalMistakes", "AnalogyImprovements", "StrongExplanations" },
                        additionalProperties = false
                    }),
                    "Tech Evaluation Schema",
                    true)
            };

            var systemPrompt = @"You are an EXTREMELY STRICT senior technical reviewer evaluating an AI and software engineering trainer's spoken explanations.
You have 20+ years of industry experience. You do NOT tolerate sloppy explanations.

SCORING RULES (integers 1-100):
- 1-20: Dangerously wrong. Would mislead students.
- 21-40: Weak. Vague explanations, missing key concepts.
- 41-60: Average. Correct but surface-level.
- 61-80: Good. Clear and mostly accurate. Minor improvements possible.
- 81-100: Expert-level. ONLY if the explanation is precise, complete, and uses real-world grounding. Rarely deserved.

YOUR TASKS:
1. Score each metric STRICTLY. Average trainers should get 45-55.
2. List any technical inaccuracies, misconceptions, or oversimplifications that could mislead students.
3. For each technical mistake, quote the EXACT problematic phrase in 'WhatYouSaid', provide the technically correct statement in 'WhatYouShouldSay', and explain why the distinction matters for students.
4. Suggest better analogies for complex topics that were poorly explained.
5. Highlight what was explained exceptionally well (be specific, quote the good explanation).

Return strict JSON.";

            var messages = new ChatMessage[]
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(transcript)
            };

            var response = await client.CompleteChatAsync(messages, options);
            if (response.Value.Content == null || response.Value.Content.Count == 0)
                return (new TechEvaluationResult(), 0, 0);

            var content = response.Value.Content[0].Text;
            var result = JsonSerializer.Deserialize<TechEvaluationResult>(content);
            return (result ?? new TechEvaluationResult(), response.Value.Usage.InputTokenCount, response.Value.Usage.OutputTokenCount);
        }
    }
}
