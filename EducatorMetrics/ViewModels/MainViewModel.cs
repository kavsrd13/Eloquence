using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EducatorMetrics.Models;
using EducatorMetrics.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Windows.Input;
namespace EducatorMetrics.ViewModels
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
        private ISeries[] _englishSeries = new ISeries[0];

        [ObservableProperty]
        private ISeries[] _techSeries = new ISeries[0];

        [ObservableProperty]
        private LiveChartsCore.SkiaSharpView.VisualElements.LabelVisual _englishAxes = null!;

        [ObservableProperty]
        private LiveChartsCore.SkiaSharpView.VisualElements.LabelVisual _techAxes = null!;

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
        [ObservableProperty] private int _avgAcc; [ObservableProperty] private int _avgArc; [ObservableProperty] private int _avgPed; [ObservableProperty] private int _avgRea; [ObservableProperty] private int _avgAna;

        [ObservableProperty]
        private string _statusText = "Initializing...";

        [ObservableProperty]
        private bool _isRecording = false;

        [ObservableProperty]
        private string _recordingButtonText = "Start Recording";


        public IRelayCommand RunEvaluationCommand { get; }
        public ICommand ToggleRecordingCommand { get; }

        public MainViewModel(AudioCaptureService audioService, TeamsDetectorService teamsService, BatchEvaluationService batchEvalService)
        {
            _audioService = audioService;
            _batchService = batchEvalService;

            ToggleRecordingCommand = new RelayCommand(ToggleRecording);
            RunEvaluationCommand = new RelayCommand(async () => await RunEvaluation());

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

            var avgAccuracy = (int)Evaluations.Average(e => e.AccuracyScore);
            var avgArchitecture = (int)Evaluations.Average(e => e.ArchitectureScore);
            var avgPedagogy = (int)Evaluations.Average(e => e.PedagogyScore);
            var avgRealWorld = (int)Evaluations.Average(e => e.RealWorldScore);
            var avgAnalogy = (int)Evaluations.Average(e => e.AnalogyScore);

            var themeForeground = _isDarkTheme ? new SkiaSharp.SKColor(255, 255, 255) : new SkiaSharp.SKColor(17, 24, 39);
            var themeAxis = _isDarkTheme ? new SkiaSharp.SKColor(212, 212, 216) : new SkiaSharp.SKColor(107, 114, 128);

            EnglishAxes = new LiveChartsCore.SkiaSharpView.VisualElements.LabelVisual
            {
                Text = "Lexical, Discourse, Syntactic, Conciseness, Fluency",
                TextSize = 14,
                Paint = new SolidColorPaint(themeForeground)
            };
            TechAxes = new LiveChartsCore.SkiaSharpView.VisualElements.LabelVisual
            {
                Text = "Accuracy, Architecture, Pedagogy, Real World, Analogy",
                TextSize = 14,
                Paint = new SolidColorPaint(themeForeground)
            };

            var englishValues = new double[]
            {
                avgLexical, avgDiscourse, avgSyntactic, avgConciseness, avgFluency
            };

            var techValues = new double[]
            {
                avgAccuracy, avgArchitecture, avgPedagogy, avgRealWorld, avgAnalogy
            };

            EnglishSeries = new ISeries[]
            {
                new PolarLineSeries<double>
                {
                    Values = englishValues,
                    LineSmoothness = 1,
                    GeometrySize = 10,
                    Fill = new SolidColorPaint(new SkiaSharp.SKColor(96, 165, 250, 90)),
                    Stroke = new SolidColorPaint(new SkiaSharp.SKColor(96, 165, 250), 3)
                }
            };

            TechSeries = new ISeries[]
            {
                new PolarLineSeries<double>
                {
                    Values = techValues,
                    LineSmoothness = 1,
                    GeometrySize = 10,
                    Fill = new SolidColorPaint(new SkiaSharp.SKColor(52, 211, 153, 90)),
                    Stroke = new SolidColorPaint(new SkiaSharp.SKColor(52, 211, 153), 3)
                }
            };

            AvgLex = avgLexical; AvgDis = avgDiscourse; AvgSyn = avgSyntactic; AvgCon = avgConciseness; AvgFlu = avgFluency;
            AvgAcc = avgAccuracy; AvgArc = avgArchitecture; AvgPed = avgPedagogy; AvgRea = avgRealWorld; AvgAna = avgAnalogy;

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
            
            var accTrend = new List<double>();
            var arcTrend = new List<double>();
            var pedTrend = new List<double>();
            var reaTrend = new List<double>();
            var anaTrend = new List<double>();

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
                
                accTrend.Add(group.Average(e => e.AccuracyScore));
                arcTrend.Add(group.Average(e => e.ArchitectureScore));
                pedTrend.Add(group.Average(e => e.PedagogyScore));
                reaTrend.Add(group.Average(e => e.RealWorldScore));
                anaTrend.Add(group.Average(e => e.AnalogyScore));
                
                engTrend.Add(group.Average(e => (e.LexicalScore + e.DiscourseScore + e.SyntacticScore + e.ConcisenessScore + e.FluencyScore) / 5.0));
                techTrend.Add(group.Average(e => (e.AccuracyScore + e.ArchitectureScore + e.PedagogyScore + e.RealWorldScore + e.AnalogyScore) / 5.0));
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
