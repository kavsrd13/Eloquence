using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eloquence.Models;
using Eloquence.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Windows.Input;
namespace Eloquence.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private AudioCaptureService _audioService;
        private readonly BatchEvaluationService _batchService;

        [ObservableProperty]
        private ObservableCollection<Evaluation> _evaluations = new();

        [ObservableProperty]
        private ObservableCollection<MergedTranscript> _transcripts = new();

        [ObservableProperty]
        private ObservableCollection<LlmLog> _llmLogs = new();

        [ObservableProperty]
        private bool _isTelemetryEnabled = true;

        [ObservableProperty]
        private ObservableCollection<PhraseImprovement> _phraseImprovements = new();

        [ObservableProperty]
        private ObservableCollection<PhraseImprovement> _weakVocabulary = new();

        [ObservableProperty]
        private ObservableCollection<RepetitiveWordItem> _repetitiveWords = new();

        [ObservableProperty]
        private ObservableCollection<TechMistake> _techMistakes = new();

        [ObservableProperty]
        private ObservableCollection<SkillCardItem> _skillCards = new();

        [ObservableProperty] private string _engLevel = string.Empty;
        [ObservableProperty] private string _techLevel = string.Empty;
        [ObservableProperty] private string _engLevelColor = string.Empty;
        [ObservableProperty] private string _techLevelColor = string.Empty;

        [ObservableProperty]
        private DateTime? _selectedTelemetryDate;

        [ObservableProperty]
        private ObservableCollection<LlmLog> _filteredLlmLogs = new();

        [ObservableProperty] private int _telTotalTokens;
        [ObservableProperty] private int _telTotalCalls;
        [ObservableProperty] private int _telAvgTokens;
        [ObservableProperty] private string _telSuccessRate = string.Empty;

        [ObservableProperty]
        private ISeries[] _engTrendSeries = new ISeries[0];

        [ObservableProperty]
        private ISeries[] _techTrendSeries = new ISeries[0];

        [ObservableProperty]
        private LiveChartsCore.SkiaSharpView.Axis[] _trendXAxes = new LiveChartsCore.SkiaSharpView.Axis[0];

        [ObservableProperty]
        private int _currentEngAvg;

        [ObservableProperty]
        private int _currentTechAvg;

        [ObservableProperty]
        private string _engDelta = string.Empty;

        [ObservableProperty]
        private string _techDelta = string.Empty;

        [ObservableProperty] private int _avgLex; [ObservableProperty] private int _avgDis; [ObservableProperty] private int _avgSyn; [ObservableProperty] private int _avgCon; [ObservableProperty] private int _avgFlu;
        [ObservableProperty] private int _avgCoh; [ObservableProperty] private int _avgGra; [ObservableProperty] private int _avgConf;
        [ObservableProperty] private int _avgAcc; [ObservableProperty] private int _avgArc; [ObservableProperty] private int _avgPed; [ObservableProperty] private int _avgRea; [ObservableProperty] private int _avgAna;
        [ObservableProperty] private int _avgDep; [ObservableProperty] private int _avgTra;

        [ObservableProperty]
        private string _statusText = "Initializing...";

        [ObservableProperty]
        private bool _isRecording = false;

        [ObservableProperty]
        private string _recordingButtonText = "Start Recording";


        public IRelayCommand RunEvaluationCommand { get; }
        public ICommand ToggleRecordingCommand { get; }
        public ICommand ShowAllTelemetryCommand { get; }

        public MainViewModel(AudioCaptureService audioService, TeamsDetectorService teamsService, BatchEvaluationService batchEvalService)
        {
            _audioService = audioService;
            _batchService = batchEvalService;

            ToggleRecordingCommand = new RelayCommand(ToggleRecording);
            RunEvaluationCommand = new RelayCommand(async () => await RunEvaluation());
            ShowAllTelemetryCommand = new RelayCommand(ShowAllTelemetry);

            LoadSettings();
            LoadData();

            _audioService.OnTranscriptAdded += (record) => 
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() => 
                {
                    LoadData();
                });
            };

            _audioService.OnStatusChanged += (status) =>
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    StatusText = status;
                });
            };

            teamsService.OnStatusChanged += (status) =>
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    StatusText = status;
                });
            };

            teamsService.OnCallStateChanged += (isActive) =>
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (isActive && !IsRecording)
                    {
                        ToggleRecording();
                    }
                    else if (!isActive && IsRecording)
                    {
                        ToggleRecording();
                    }
                });
            };

            batchEvalService.OnStatusChanged += (status) =>
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    StatusText = status;
                });
            };

            batchEvalService.OnEvaluationCompleted += (eval) =>
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() => 
                {
                    Evaluations.Insert(0, eval);
                    UpdateCharts();
                    ExtractInsights();
                    LoadData();
                });
            };
        }

        private void LoadSettings()
        {
            var folder = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Eloquence");
            var path = System.IO.Path.Combine(folder, "settings.json");
            if (System.IO.File.Exists(path))
            {
                try
                {
                    var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
                    if (doc.RootElement.TryGetProperty("IsTelemetryEnabled", out var te))
                        IsTelemetryEnabled = te.GetBoolean();
                }
                catch { }
            }
        }

        private void ToggleRecording()
        {
            if (IsRecording)
            {
                _audioService.Stop();
                IsRecording = false;
                RecordingButtonText = "Start Recording";
                StatusText = "Stopped listening.";
            }
            else
            {
                _audioService.Start();
                IsRecording = true;
                RecordingButtonText = "Stop Recording";
            }
        }

        private async Task RunEvaluation()
        {
            if (IsRecording) ToggleRecording();
            await _batchService.EvaluatePendingTranscriptsAsync();
        }

        private void LoadData()
        {
            using var db = new DatabaseContext();
            
            var evalData = db.Evaluations
                         .Include(e => e.Session)
                         .OrderByDescending(e => e.Id)
                         .ToList();
            Evaluations = new ObservableCollection<Evaluation>(evalData);
            UpdateCharts();

            var logsData = db.LlmLogs.OrderByDescending(l => l.Timestamp).ToList();
            LlmLogs = new ObservableCollection<LlmLog>(logsData);
            FilterTelemetryLogs();

            var grouped = db.TranscriptRecords
                                   .OrderBy(t => t.Timestamp)
                                   .ToList()
                                   .GroupBy(t => t.Timestamp.Date);

            var merged = new List<MergedTranscript>();
            foreach (var group in grouped)
            {
                merged.Add(new MergedTranscript
                {
                    Date = group.Key,
                    StartTime = group.Min(t => t.Timestamp),
                    EndTime = group.Max(t => t.Timestamp),
                    Text = string.Join("\n\n", group.Select(t => t.Text)),
                    IsEvaluated = group.All(t => t.IsEvaluated)
                });
            }

            Transcripts = new ObservableCollection<MergedTranscript>(merged.OrderByDescending(m => m.Date));

            ExtractInsights();
        }

        private void ExtractInsights()
        {
            var phrases = new List<PhraseImprovement>();
            var vocab = new List<PhraseImprovement>();
            var mistakes = new List<TechMistake>();
            var repetitiveMap = new Dictionary<string, RepetitiveWordItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var eval in Evaluations)
            {
                if (string.IsNullOrWhiteSpace(eval.LlmFeedbackJson)) continue;
                
                try
                {
                    var root = System.Text.Json.JsonDocument.Parse(eval.LlmFeedbackJson);
                    
                    if (root.RootElement.TryGetProperty("English", out var englishEl))
                    {
                        var engResult = System.Text.Json.JsonSerializer.Deserialize<EnglishEvaluationResult>(englishEl.GetRawText());
                        if (engResult?.PhraseImprovements != null)
                        {
                            phrases.AddRange(engResult.PhraseImprovements);
                        }
                        
                        if (engResult?.WeakVocabulary != null)
                        {
                            vocab.AddRange(engResult.WeakVocabulary);
                        }

                        if (engResult?.RepetitiveWords != null)
                        {
                            foreach (var rw in engResult.RepetitiveWords)
                            {
                                if (string.IsNullOrWhiteSpace(rw.Word)) continue;
                                if (repetitiveMap.TryGetValue(rw.Word, out var existing))
                                {
                                    existing.Count += rw.Count;
                                    existing.SuggestedAlternatives = existing.SuggestedAlternatives
                                        .Concat(rw.SuggestedAlternatives ?? new List<string>())
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .ToList();
                                }
                                else
                                {
                                    repetitiveMap[rw.Word] = new RepetitiveWordItem
                                    {
                                        Word = rw.Word,
                                        Count = rw.Count,
                                        SuggestedAlternatives = rw.SuggestedAlternatives?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>()
                                    };
                                }
                            }
                        }
                    }

                    if (root.RootElement.TryGetProperty("Tech", out var techEl))
                    {
                        var techResult = System.Text.Json.JsonSerializer.Deserialize<TechEvaluationResult>(techEl.GetRawText());
                        if (techResult?.TechnicalMistakes != null)
                        {
                            mistakes.AddRange(techResult.TechnicalMistakes);
                        }
                    }
                }
                catch { }
            }

            PhraseImprovements = new ObservableCollection<PhraseImprovement>(phrases);
            WeakVocabulary = new ObservableCollection<PhraseImprovement>(vocab);
            TechMistakes = new ObservableCollection<TechMistake>(mistakes);
            RepetitiveWords = new ObservableCollection<RepetitiveWordItem>(repetitiveMap.Values.Where(x => x.Count >= 4).OrderByDescending(x => x.Count));
        }

        private void UpdateCharts()
        {
            if (!Evaluations.Any()) return;

            var avgLexical = (int)Evaluations.Average(e => e.LexicalScore);
            var avgDiscourse = (int)Evaluations.Average(e => e.DiscourseScore);
            var avgSyntactic = (int)Evaluations.Average(e => e.SyntacticScore);
            var avgConciseness = (int)Evaluations.Average(e => e.ConcisenessScore);
            var avgFluency = (int)Evaluations.Average(e => e.FluencyScore);
            var avgCoherence = (int)Evaluations.Average(e => e.CoherenceScore);
            var avgGrammar = (int)Evaluations.Average(e => e.GrammarScore);
            var avgConfidence = (int)Evaluations.Average(e => e.ConfidenceScore);

            var avgAccuracy = (int)Evaluations.Average(e => e.AccuracyScore);
            var avgArchitecture = (int)Evaluations.Average(e => e.ArchitectureScore);
            var avgPedagogy = (int)Evaluations.Average(e => e.PedagogyScore);
            var avgRealWorld = (int)Evaluations.Average(e => e.RealWorldScore);
            var avgAnalogy = (int)Evaluations.Average(e => e.AnalogyScore);
            var avgDepth = (int)Evaluations.Average(e => e.DepthScore);
            var avgTradeoff = (int)Evaluations.Average(e => e.TradeoffScore);

            var themeForeground = _isDarkTheme ? new SkiaSharp.SKColor(255, 255, 255) : new SkiaSharp.SKColor(17, 24, 39);
            var themeAxis = _isDarkTheme ? new SkiaSharp.SKColor(212, 212, 216) : new SkiaSharp.SKColor(107, 114, 128);

            AvgLex = avgLexical; AvgDis = avgDiscourse; AvgSyn = avgSyntactic; AvgCon = avgConciseness; AvgFlu = avgFluency;
            AvgCoh = avgCoherence; AvgGra = avgGrammar; AvgConf = avgConfidence;
            AvgAcc = avgAccuracy; AvgArc = avgArchitecture; AvgPed = avgPedagogy; AvgRea = avgRealWorld; AvgAna = avgAnalogy;
            AvgDep = avgDepth; AvgTra = avgTradeoff;

            using var db = new DatabaseContext();
            var allEvals = db.Evaluations.Include(e => e.Session).ToList();
            var sessionGroups = allEvals
                .Where(e => e.Session != null)
                .GroupBy(e => e.Session.SessionDate.Date)
                .OrderBy(g => g.Key)
                .ToList();

            var dates = new List<string>();
            var lexTrend = new List<double>();
            var disTrend = new List<double>();
            var synTrend = new List<double>();
            var conTrend = new List<double>();
            var fluTrend = new List<double>();
            var cohTrend = new List<double>();
            var graTrend = new List<double>();
            var confTrend = new List<double>();
            
            var accTrend = new List<double>();
            var arcTrend = new List<double>();
            var pedTrend = new List<double>();
            var reaTrend = new List<double>();
            var anaTrend = new List<double>();
            var depTrend = new List<double>();
            var traTrend = new List<double>();

            var engTrend = new List<double>();
            var techTrend = new List<double>();

            foreach (var group in sessionGroups)
            {
                dates.Add(group.Key.ToString("MMM dd"));
                
                lexTrend.Add(group.Average(e => e.LexicalScore));
                disTrend.Add(group.Average(e => e.DiscourseScore));
                synTrend.Add(group.Average(e => e.SyntacticScore));
                conTrend.Add(group.Average(e => e.ConcisenessScore));
                fluTrend.Add(group.Average(e => e.FluencyScore));
                cohTrend.Add(group.Average(e => e.CoherenceScore));
                graTrend.Add(group.Average(e => e.GrammarScore));
                confTrend.Add(group.Average(e => e.ConfidenceScore));
                
                accTrend.Add(group.Average(e => e.AccuracyScore));
                arcTrend.Add(group.Average(e => e.ArchitectureScore));
                pedTrend.Add(group.Average(e => e.PedagogyScore));
                reaTrend.Add(group.Average(e => e.RealWorldScore));
                anaTrend.Add(group.Average(e => e.AnalogyScore));
                depTrend.Add(group.Average(e => e.DepthScore));
                traTrend.Add(group.Average(e => e.TradeoffScore));
                
                engTrend.Add(group.Average(e => (e.LexicalScore + e.DiscourseScore + e.SyntacticScore + e.ConcisenessScore + e.FluencyScore + e.CoherenceScore + e.GrammarScore + e.ConfidenceScore) / 8.0));
                techTrend.Add(group.Average(e => (e.AccuracyScore + e.ArchitectureScore + e.PedagogyScore + e.RealWorldScore + e.AnalogyScore + e.DepthScore + e.TradeoffScore) / 7.0));
            }

            int allTimeEngAvg = engTrend.Any() ? (int)engTrend.Average() : 0;
            int allTimeTechAvg = techTrend.Any() ? (int)techTrend.Average() : 0;
            CurrentEngAvg = engTrend.Any() ? (int)engTrend.Last() : 0;
            CurrentTechAvg = techTrend.Any() ? (int)techTrend.Last() : 0;

            EngDelta = GetDelta(CurrentEngAvg, allTimeEngAvg);
            TechDelta = GetDelta(CurrentTechAvg, allTimeTechAvg);

            EngTrendSeries = new ISeries[]
            {
                CreateLineSeries("Lexical", lexTrend, new SkiaSharp.SKColor(59, 130, 246)),
                CreateLineSeries("Discourse", disTrend, new SkiaSharp.SKColor(16, 185, 129)),
                CreateLineSeries("Syntactic", synTrend, new SkiaSharp.SKColor(245, 158, 11)),
                CreateLineSeries("Conciseness", conTrend, new SkiaSharp.SKColor(139, 92, 246)),
                CreateLineSeries("Fluency", fluTrend, new SkiaSharp.SKColor(236, 72, 153))
            };

            TechTrendSeries = new ISeries[]
            {
                CreateLineSeries("Accuracy", accTrend, new SkiaSharp.SKColor(59, 130, 246)),
                CreateLineSeries("Architecture", arcTrend, new SkiaSharp.SKColor(16, 185, 129)),
                CreateLineSeries("Pedagogy", pedTrend, new SkiaSharp.SKColor(245, 158, 11)),
                CreateLineSeries("Real World", reaTrend, new SkiaSharp.SKColor(139, 92, 246)),
                CreateLineSeries("Analogy", anaTrend, new SkiaSharp.SKColor(236, 72, 153))
            };

            TrendXAxes = new[]
            {
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    Labels = dates,
                    LabelsPaint = new SolidColorPaint(themeAxis)
                }
            };
            
            var engLevelInfo = SkillCardItem.GetLevel(CurrentEngAvg);
            EngLevel = engLevelInfo.Level;
            EngLevelColor = engLevelInfo.Color;
            
            var techLevelInfo = SkillCardItem.GetLevel(CurrentTechAvg);
            TechLevel = techLevelInfo.Level;
            TechLevelColor = techLevelInfo.Color;

            var cards = new List<SkillCardItem>
            {
                CreateSkillCard("Lexical Range", "English", avgLexical, lexTrend),
                CreateSkillCard("Discourse", "English", avgDiscourse, disTrend),
                CreateSkillCard("Syntactic", "English", avgSyntactic, synTrend),
                CreateSkillCard("Conciseness", "English", avgConciseness, conTrend),
                CreateSkillCard("Fluency", "English", avgFluency, fluTrend),
                CreateSkillCard("Coherence", "English", avgCoherence, cohTrend),
                CreateSkillCard("Grammar", "English", avgGrammar, graTrend),
                CreateSkillCard("Confidence", "English", avgConfidence, confTrend),
                
                CreateSkillCard("Accuracy", "Tech", avgAccuracy, accTrend),
                CreateSkillCard("Architecture", "Tech", avgArchitecture, arcTrend),
                CreateSkillCard("Pedagogy", "Tech", avgPedagogy, pedTrend),
                CreateSkillCard("Real World", "Tech", avgRealWorld, reaTrend),
                CreateSkillCard("Analogy", "Tech", avgAnalogy, anaTrend),
                CreateSkillCard("Depth", "Tech", avgDepth, depTrend),
                CreateSkillCard("Tradeoff", "Tech", avgTradeoff, traTrend)
            };
            
            SkillCards = new ObservableCollection<SkillCardItem>(cards.OrderBy(c => c.Score));
        }

        private SkillCardItem CreateSkillCard(string name, string category, int currentScore, List<double>? trend)
        {
            int delta = 0;
            if (trend != null && trend.Count >= 2)
            {
                var prev = (int)trend[trend.Count - 2];
                delta = currentScore - prev;
            }
            
            var levelInfo = SkillCardItem.GetLevel(currentScore);
            return new SkillCardItem
            {
                Name = name,
                Category = category,
                Score = currentScore,
                Delta = delta,
                DeltaText = SkillCardItem.GetDeltaText(delta),
                Level = levelInfo.Level,
                LevelColor = levelInfo.Color
            };
        }

        partial void OnSelectedTelemetryDateChanged(DateTime? value)
        {
            FilterTelemetryLogs();
        }

        private void FilterTelemetryLogs()
        {
            if (LlmLogs == null) return;
            
            var filtered = LlmLogs.AsEnumerable();
            if (SelectedTelemetryDate.HasValue)
            {
                var date = SelectedTelemetryDate.Value.Date;
                filtered = filtered.Where(l => l.Timestamp.ToLocalTime().Date == date);
            }

            var result = filtered.ToList();
            FilteredLlmLogs = new ObservableCollection<LlmLog>(result);

            TelTotalCalls = result.Count;
            TelTotalTokens = result.Sum(l => l.TotalTokens);
            TelAvgTokens = TelTotalCalls > 0 ? TelTotalTokens / TelTotalCalls : 0;
            
            var success = result.Count(l => l.IsSuccess);
            TelSuccessRate = TelTotalCalls > 0 ? $"{(success * 100.0 / TelTotalCalls):F1}%" : "0%";
        }

        private void ShowAllTelemetry()
        {
            SelectedTelemetryDate = null;
        }

        private LiveChartsCore.SkiaSharpView.LineSeries<double> CreateLineSeries(string name, List<double> values, SkiaSharp.SKColor color)
        {
            return new LiveChartsCore.SkiaSharpView.LineSeries<double>
            {
                Name = name,
                Values = values,
                Stroke = new SolidColorPaint(color, 2),
                GeometrySize = 6,
                Fill = null // No fill for multiple lines
            };
        }

        private bool _isDarkTheme = true;
        public void UpdateThemeColors(bool isDarkTheme)
        {
            _isDarkTheme = isDarkTheme;
            UpdateCharts();
        }

        private string GetDelta(int current, int average)
        {
            if (average == 0) return "First session — no comparison yet";
            int diff = current - average;
            if (diff > 0) return $"↑ +{diff} vs all-time avg ({average})";
            if (diff < 0) return $"↓ {diff} vs all-time avg ({average})";
            return $"Even with all-time average ({average})";
        }
    }
}

