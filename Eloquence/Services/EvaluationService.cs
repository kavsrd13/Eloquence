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

            var systemPrompt = @"You are a strict English Scorer. Rate the transcript on the 8 metrics strictly from 1-100.";
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

            var systemPrompt = @"You are an English Coach. Only flag truly terrible phrases.";
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

            var systemPrompt = @"You are a Confidence Analyst. Focus on detecting hedging language like 'I think', 'maybe', 'sort of', 'kind of', 'I guess' and suggesting assertive rewrites.";
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

            var systemPrompt = @"You are a Tech Scorer. Rate the transcript strictly on 7 metrics from 1-100.";
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

            var systemPrompt = @"You are a Tech Reviewer. Identify inaccuracies, mistakes, suggest analogies and highlight strong explanations.";
            var response = await client.CompleteChatAsync(new ChatMessage[] { new SystemChatMessage(systemPrompt), new UserChatMessage(transcript) }, options);
            
            if (response.Value.Content == null || response.Value.Content.Count == 0)
                return (new TechEvaluationResult(), 0, 0);

            var result = JsonSerializer.Deserialize<TechEvaluationResult>(response.Value.Content[0].Text);
            return (result ?? new TechEvaluationResult(), response.Value.Usage.InputTokenCount, response.Value.Usage.OutputTokenCount);
        }
    }
}
