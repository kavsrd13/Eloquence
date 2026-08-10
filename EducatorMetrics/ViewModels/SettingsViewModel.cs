using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace EducatorMetrics.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _azureOpenAIEndpoint = string.Empty;

        [ObservableProperty]
        private string _azureOpenAIKey = string.Empty;

        [ObservableProperty]
        private string _deploymentName = string.Empty;

        [ObservableProperty]
        private bool _isTelemetryEnabled = true;

        public SettingsViewModel()
        {
            LoadSettings();
        }

        private string GetSettingsPath()
        {
            var folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Eloquence");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "settings.json");
        }

        private void LoadSettings()
        {
            var path = GetSettingsPath();
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("AzureOpenAIEndpoint", out var ep))
                        AzureOpenAIEndpoint = ep.GetString() ?? "";
                    if (doc.RootElement.TryGetProperty("AzureOpenAIKey", out var key))
                        AzureOpenAIKey = key.GetString() ?? "";
                    if (doc.RootElement.TryGetProperty("DeploymentName", out var dn))
                        DeploymentName = dn.GetString() ?? string.Empty;
                    if (doc.RootElement.TryGetProperty("IsTelemetryEnabled", out var te))
                        IsTelemetryEnabled = te.GetBoolean();
                }
                catch { }
            }
        }

        [RelayCommand]
        private void SaveSettings()
        {
            var path = GetSettingsPath();
            var json = JsonSerializer.Serialize(new {
                AzureOpenAIEndpoint = this.AzureOpenAIEndpoint,
                AzureOpenAIKey = this.AzureOpenAIKey,
                DeploymentName = this.DeploymentName,
                IsTelemetryEnabled = this.IsTelemetryEnabled
            });
            File.WriteAllText(path, json);
            MessageBox.Show("Settings saved. Please restart the application to apply changes.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
