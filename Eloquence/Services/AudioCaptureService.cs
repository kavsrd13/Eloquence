using System;
using System.IO;
using System.Linq;
using System.Speech.Recognition;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eloquence.Models;

namespace Eloquence.Services
{
    public class AudioCaptureService : IDisposable
    {
        private SpeechRecognitionEngine? _recognizer;
        private TranscriptionService _transcriptionService;
        private EvaluationService _evaluationService;
        private BatchEvaluationService? _batchService;
        
        // Buffer for interval-based transcription
        private readonly StringBuilder _transcriptBuffer = new();
        private Timer? _flushTimer;
        private int _flushIntervalMinutes = 10;
        private bool _autoEvaluate = true;
        private readonly object _bufferLock = new();

        public event Action<Evaluation>? OnEvaluationCompleted;
        public event Action<TranscriptRecord>? OnTranscriptAdded;
        public event Action<string>? OnStatusChanged;

        public AudioCaptureService(VadService vadService, TranscriptionService transcriptionService, EvaluationService evaluationService)
        {
            _transcriptionService = transcriptionService;
            _evaluationService = evaluationService;

            _transcriptionService.OnDownloadProgress += (msg) => OnStatusChanged?.Invoke(msg);

            LoadTranscriptionSettings();
        }

        /// <summary>
        /// Set the BatchEvaluationService reference for auto-evaluation after buffer flush.
        /// </summary>
        public void SetBatchService(BatchEvaluationService batchService)
        {
            _batchService = batchService;
        }

        private void LoadTranscriptionSettings()
        {
            try
            {
                var settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Eloquence", "settings.json");
                if (File.Exists(settingsPath))
                {
                    var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(settingsPath));
                    if (doc.RootElement.TryGetProperty("TranscriptionIntervalMinutes", out var interval))
                        _flushIntervalMinutes = Math.Clamp(interval.GetInt32(), 5, 30);
                    if (doc.RootElement.TryGetProperty("AutoEvaluate", out var autoEval))
                        _autoEvaluate = autoEval.GetBoolean();
                }
            }
            catch { }
        }

        public void Start()
        {
            try
            {
                if (_recognizer == null)
                {
                    _recognizer = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-US"));
                    _recognizer.LoadGrammar(new DictationGrammar());
                    _recognizer.SpeechRecognized += OnSpeechRecognized;
                    _recognizer.SpeechRecognitionRejected += OnSpeechRejected;
                    _recognizer.SetInputToDefaultAudioDevice();
                    
                    _recognizer.InitialSilenceTimeout = TimeSpan.FromSeconds(5);
                    _recognizer.BabbleTimeout = TimeSpan.FromSeconds(3);
                    _recognizer.EndSilenceTimeout = TimeSpan.FromSeconds(1.5);
                }
                
                // Start the flush timer
                _flushTimer = new Timer(
                    FlushBuffer,
                    null,
                    TimeSpan.FromMinutes(_flushIntervalMinutes),
                    TimeSpan.FromMinutes(_flushIntervalMinutes));

                _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                OnStatusChanged?.Invoke($"Listening (Whisper.net) — buffering {_flushIntervalMinutes}min intervals...");
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"Mic Error: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                _recognizer?.RecognizeAsyncCancel();
                _flushTimer?.Dispose();
                _flushTimer = null;

                // Flush any remaining buffer content on stop
                FlushBuffer(null);

                OnStatusChanged?.Invoke("Stopped listening.");
            }
            catch { }
        }

        private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            ProcessCapturedAudio(e.Result?.Audio);
        }

        private void OnSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
        {
            ProcessCapturedAudio(e.Result?.Audio);
        }

        private void ProcessCapturedAudio(RecognizedAudio? audio)
        {
            if (audio == null) return;
            
            _ = Task.Run(async () => 
            {
                try
                {
                    OnStatusChanged?.Invoke("Transcribing via Whisper...");
                    
                    using var ms = new MemoryStream();
                    audio.WriteToWaveStream(ms);
                    byte[] rawWavData = ms.ToArray();
                    
                    var transcript = await _transcriptionService.TranscribeWavAsync(rawWavData);
                    
                    if (string.IsNullOrWhiteSpace(transcript))
                    {
                        OnStatusChanged?.Invoke($"Listening (Whisper.net) — buffering {_flushIntervalMinutes}min intervals...");
                        return;
                    }

                    // Append to buffer instead of saving immediately
                    lock (_bufferLock)
                    {
                        if (_transcriptBuffer.Length > 0)
                            _transcriptBuffer.Append("\n\n");
                        _transcriptBuffer.Append(transcript);
                    }

                    OnStatusChanged?.Invoke($"Listening (Whisper.net) — buffering {_flushIntervalMinutes}min intervals...");
                }
                catch (Exception ex)
                {
                    OnStatusChanged?.Invoke($"Error: {ex.Message}");
                }
            });
        }

        private void FlushBuffer(object? state)
        {
            string bufferedText;
            lock (_bufferLock)
            {
                if (_transcriptBuffer.Length == 0) return;
                bufferedText = _transcriptBuffer.ToString();
                _transcriptBuffer.Clear();
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    OnStatusChanged?.Invoke("Flushing transcript buffer...");

                    using var db = new DatabaseContext();
                    
                    var session = db.Sessions.FirstOrDefault(s => s.SessionDate.Date == DateTime.Today);
                    if (session == null)
                    {
                        session = new Session { SessionDate = DateTime.Today };
                        db.Sessions.Add(session);
                        await db.SaveChangesAsync();
                    }

                    var transcriptRecord = new TranscriptRecord
                    {
                        SessionId = session.Id,
                        Timestamp = DateTime.UtcNow,
                        Text = bufferedText,
                        IsEvaluated = false
                    };

                    db.TranscriptRecords.Add(transcriptRecord);
                    await db.SaveChangesAsync();

                    OnTranscriptAdded?.Invoke(transcriptRecord);
                    OnStatusChanged?.Invoke($"Transcript saved ({bufferedText.Split(' ').Length} words).");

                    // Auto-trigger evaluation if enabled
                    if (_autoEvaluate && _batchService != null)
                    {
                        await _batchService.EvaluatePendingTranscriptsAsync(force: false);
                    }

                    OnStatusChanged?.Invoke($"Listening (Whisper.net) — buffering {_flushIntervalMinutes}min intervals...");
                }
                catch (Exception ex)
                {
                    OnStatusChanged?.Invoke($"Flush error: {ex.Message}");
                }
            });
        }

        public void Dispose()
        {
            Stop();
            _recognizer?.Dispose();
        }
    }
}
