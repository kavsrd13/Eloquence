using System;
using System.IO;
using System.Linq;
using System.Speech.Recognition;
using System.Threading.Tasks;
using EducatorMetrics.Models;

namespace EducatorMetrics.Services
{
    public class AudioCaptureService : IDisposable
    {
        private SpeechRecognitionEngine? _recognizer;
        private TranscriptionService _transcriptionService;
        private EvaluationService _evaluationService;
        
        public event Action<Evaluation>? OnEvaluationCompleted;
        public event Action<TranscriptRecord>? OnTranscriptAdded;
        public event Action<string>? OnStatusChanged;

        public AudioCaptureService(VadService vadService, TranscriptionService transcriptionService, EvaluationService evaluationService)
        {
            _transcriptionService = transcriptionService;
            _evaluationService = evaluationService;

            _transcriptionService.OnDownloadProgress += (msg) => OnStatusChanged?.Invoke(msg);
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
                    _recognizer.SpeechRecognitionRejected += OnSpeechRejected; // We capture audio even if Windows failed to transcribe it
                    _recognizer.SetInputToDefaultAudioDevice();
                    
                    // Keep listening until long silence
                    _recognizer.InitialSilenceTimeout = TimeSpan.FromSeconds(5);
                    _recognizer.BabbleTimeout = TimeSpan.FromSeconds(3);
                    _recognizer.EndSilenceTimeout = TimeSpan.FromSeconds(1.5);
                }
                
                _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                OnStatusChanged?.Invoke("Listening (Whisper.net)...");
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
                    
                    // Windows gives us the exact audio chunk it captured
                    using var ms = new MemoryStream();
                    audio.WriteToWaveStream(ms);
                    byte[] rawWavData = ms.ToArray();
                    
                    // Since it's already a WAV file (with headers), we can pass it to Whisper directly if we modify TranscriptionService to accept WAV streams.
                    // Wait, our current TranscriptionService TranscribeAsync(byte[] pcm16kHz) expects raw PCM and adds a header!
                    // Let's just pass the rawWavData directly to Whisper, but we need to update TranscriptionService to have a TranscribeWavAsync.
                    
                    var transcript = await _transcriptionService.TranscribeWavAsync(rawWavData);
                    
                    if (string.IsNullOrWhiteSpace(transcript))
                    {
                        OnStatusChanged?.Invoke("Listening (Whisper.net)...");
                        return;
                    }

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
                        Text = transcript,
                        IsEvaluated = false
                    };

                    db.TranscriptRecords.Add(transcriptRecord);
                    await db.SaveChangesAsync();

                    OnStatusChanged?.Invoke("Listening (Whisper.net)...");
                    OnTranscriptAdded?.Invoke(transcriptRecord);
                }
                catch (Exception ex)
                {
                    OnStatusChanged?.Invoke($"Error: {ex.Message}");
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
