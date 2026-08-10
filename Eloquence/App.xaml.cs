using System.Windows;
using System.IO;
using System.Text.Json;
using Eloquence.Services;
using Eloquence.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Eloquence
{
    public partial class App : Application
    {
        public static AudioCaptureService AudioService { get; private set; } = null!;
        public static DatabaseContext DbContext { get; private set; } = null!;
        public static EvaluationService EvalService { get; private set; } = null!;
        public static TeamsDetectorService TeamsService { get; private set; } = null!;

        public static BatchEvaluationService BatchEvalService { get; private set; } = null!;

        private IDisposable? _notifyIcon;

        public App()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Eloquence_InitCrash.txt"), ex.ToString());
            }
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            try 
            {
                AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Eloquence", "Eloquence_AppDomainCrash.txt"), args.ExceptionObject.ToString());
                };
                
                // Global exception handler to prevent silent crashes
                DispatcherUnhandledException += (s, args) =>
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Eloquence", "Eloquence_Crash.txt"), args.Exception.ToString());
                    args.Handled = true; // Prevent app crash
                };
                TaskScheduler.UnobservedTaskException += (s, args) =>
                {
                    args.SetObserved(); // Prevent unobserved task exceptions from crashing the app
                };

                // Load Settings
                string endpoint = "";
                string key = "";
                var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Eloquence", "settings.json");
                if (File.Exists(settingsPath))
                {
                    try
                    {
                        var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
                        if (doc.RootElement.TryGetProperty("AzureOpenAIEndpoint", out var ep)) endpoint = ep.GetString() ?? "";
                        if (doc.RootElement.TryGetProperty("AzureOpenAIKey", out var k)) key = k.GetString() ?? "";
                    }
                    catch { }
                }

                DbContext = new DatabaseContext();
                
                // Auto-cleanup: Delete transcript text older than 3 days, keep scores permanently
                CleanupOldTranscripts();

                EvalService = new EvaluationService(endpoint, key);
                BatchEvalService = new BatchEvaluationService(EvalService);
                
                AudioService = new AudioCaptureService(
                    new VadService(),
                    new TranscriptionService(),
                    EvalService
                );
                AudioService.SetBatchService(BatchEvalService);

                TeamsService = new TeamsDetectorService();
                TeamsService.OnCallStateChanged += (isActive) =>
                {
                    if (isActive)
                    {
                        AudioService.Start();
                    }
                    else
                    {
                        AudioService.Stop();
                        
                        // Wait 5 minutes before evaluating in case they quickly rejoin a call
                        Task.Run(async () => 
                        {
                            await Task.Delay(TimeSpan.FromMinutes(5));
                            if (!TeamsService.IsCallActive)
                            {
                                await BatchEvalService.EvaluatePendingTranscriptsAsync();
                            }
                        });
                    }
                };

                // Start polling for Teams calls instead of always recording
                TeamsService.Start();

                // Auto-evaluate any pending transcripts from previous sessions in the background
                Task.Run(async () => 
                {
                    await Task.Delay(5000); // Wait 5s for UI to load
                    await BatchEvalService.EvaluatePendingTranscriptsAsync();
                });

                try
                {
                    _notifyIcon = (IDisposable)FindResource("NotifyIcon");
                }
                catch { }

                Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Eloquence", "Eloquence_Crash.txt"), ex.ToString());
            }
        }

        /// <summary>
        /// Deletes transcript text from evaluations older than 3 days.
        /// Scores and LLM feedback JSON are kept permanently for trend tracking.
        /// No audio recordings are ever stored.
        /// </summary>
        private void CleanupOldTranscripts()
        {
            try
            {
                using var db = new DatabaseContext();
                var cutoff = DateTime.UtcNow.AddDays(-3);
                
                var oldEvals = db.Evaluations
                    .Where(e => e.Timestamp < cutoff && e.TranscriptChunk != "")
                    .ToList();

                foreach (var eval in oldEvals)
                {
                    eval.TranscriptChunk = ""; // Clear transcript text, keep everything else
                }

                var oldTranscripts = db.TranscriptRecords
                    .Where(t => t.Timestamp < cutoff)
                    .ToList();
                    
                db.TranscriptRecords.RemoveRange(oldTranscripts);

                if (oldEvals.Any() || oldTranscripts.Any())
                {
                    db.SaveChanges();
                }
            }
            catch { }
        }

        private void OnExit(object sender, ExitEventArgs e)
        {
            AudioService?.Stop();
            AudioService?.Dispose();
            TeamsService?.Dispose();
            _notifyIcon?.Dispose();
        }

        private void OpenDashboard_Click(object sender, RoutedEventArgs e)
        {
            var window = Current.MainWindow;
            if (window == null)
            {
                window = new MainWindow();
                Current.MainWindow = window;
            }
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Current.Shutdown();
        }
    }
}

