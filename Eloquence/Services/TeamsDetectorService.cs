using NAudio.CoreAudioApi;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Eloquence.Services
{
    /// <summary>
    /// Polls every 5 seconds to detect if Microsoft Teams has an active audio session.
    /// When Teams joins a call, it creates audio sessions on the OS audio devices.
    /// This service detects that and signals start/stop of recording.
    /// </summary>
    public class TeamsDetectorService : IDisposable
    {
        private Timer? _pollTimer;
        private bool _isCallActive = false;
        private int _consecutiveActiveChecks = 0;
        private int _consecutiveInactiveChecks = 0;

        /// <summary>Fires when Teams call state changes. true = call started, false = call ended.</summary>
        public event Action<bool>? OnCallStateChanged;
        
        /// <summary>Fires with a status string for UI display.</summary>
        public event Action<string>? OnStatusChanged;

        public bool IsCallActive => _isCallActive;

        public void Start()
        {
            _pollTimer = new Timer(CheckForTeamsCall, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            OnStatusChanged?.Invoke("Watching for Teams calls...");
        }

        private void CheckForTeamsCall(object? state)
        {
            try
            {
                bool detected = DetectTeamsAudioSession();

                if (detected)
                {
                    _consecutiveInactiveChecks = 0;
                    _consecutiveActiveChecks++;

                    // Require 2 consecutive active checks (10 seconds) to confirm a call started
                    // This avoids false positives from brief Teams notification sounds
                    if (!_isCallActive && _consecutiveActiveChecks >= 2)
                    {
                        _isCallActive = true;
                        OnStatusChanged?.Invoke("Teams call detected — Recording...");
                        OnCallStateChanged?.Invoke(true);
                    }
                }
                else
                {
                    _consecutiveActiveChecks = 0;
                    _consecutiveInactiveChecks++;

                    // Require 6 consecutive inactive checks (30 seconds) to confirm call ended
                    // This avoids false stops during brief audio gaps or screen sharing transitions
                    if (_isCallActive && _consecutiveInactiveChecks >= 6)
                    {
                        _isCallActive = false;
                        OnStatusChanged?.Invoke("Teams call ended — Stopped recording.");
                        OnCallStateChanged?.Invoke(false);
                    }
                }
            }
            catch
            {
                // Silently handle any audio API exceptions
            }
        }

        private bool DetectTeamsAudioSession()
        {
            try
            {
                var teamsProcesses = Process.GetProcessesByName("ms-teams")
                    .Concat(Process.GetProcessesByName("Teams"))
                    .Concat(Process.GetProcessesByName("msteams"));
                
                var teamsProcessIds = new System.Collections.Generic.HashSet<int>(teamsProcesses.Select(p => p.Id));
                if (teamsProcessIds.Count == 0) return false;

                using var enumerator = new MMDeviceEnumerator();
                bool hasAudio = CheckDeviceForTeams(enumerator, DataFlow.Render, teamsProcessIds) || 
                                CheckDeviceForTeams(enumerator, DataFlow.Capture, teamsProcessIds);
                
                return hasAudio || DetectTeamsCallByWindowTitle();
            }
            catch
            {
                return false;
            }
        }

        private bool CheckDeviceForTeams(MMDeviceEnumerator enumerator, DataFlow flow, System.Collections.Generic.HashSet<int> teamsProcessIds)
        {
            try
            {
                var devices = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
                foreach (var device in devices)
                {
                    try
                    {
                        var sessionManager = device.AudioSessionManager;
                        var sessions = sessionManager.Sessions;

                        for (int i = 0; i < sessions.Count; i++)
                        {
                            var session = sessions[i];
                            try
                            {
                                int processId = (int)session.GetProcessID;
                                if (teamsProcessIds.Contains(processId) &&
                                    session.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateActive)
                                {
                                    return true;
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return false;
        }

        private bool DetectTeamsCallByWindowTitle()
        {
            // Fallback: check Teams window titles for call indicators
            var teamsProcesses = Process.GetProcessesByName("ms-teams")
                .Concat(Process.GetProcessesByName("Teams"))
                .Concat(Process.GetProcessesByName("msteams"));

            foreach (var proc in teamsProcesses)
            {
                try
                {
                    string title = proc.MainWindowTitle ?? "";
                    // Teams window title typically shows meeting name or call indicators
                    if (title.Contains("Meeting", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("Call", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("|", StringComparison.OrdinalIgnoreCase)) // Teams shows "Name | Microsoft Teams" during calls
                    {
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        public void Dispose()
        {
            _pollTimer?.Dispose();
        }
    }
}

