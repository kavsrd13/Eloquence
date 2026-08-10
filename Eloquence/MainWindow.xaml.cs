using System.Windows;
using Eloquence.ViewModels;

namespace Eloquence
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel(App.AudioService, App.TeamsService, App.BatchEvalService);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = new SettingsViewModel();
            var settingsWindow = new Window
            {
                Title = "Settings",
                Width = 400,
                Height = 380,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Icon = this.Icon,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)),
                Foreground = System.Windows.Media.Brushes.White
            };

            var grid = new System.Windows.Controls.Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            var lblEndpoint = new System.Windows.Controls.TextBlock { Text = "Azure OpenAI Endpoint:", Margin = new Thickness(0, 0, 0, 5) };
            var txtEndpoint = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 0, 0, 15) };
            txtEndpoint.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding("AzureOpenAIEndpoint") { Source = vm, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
            
            var lblKey = new System.Windows.Controls.TextBlock { Text = "Azure OpenAI Key:", Margin = new Thickness(0, 0, 0, 5) };
            var txtKey = new System.Windows.Controls.PasswordBox { Margin = new Thickness(0, 0, 0, 15) };
            txtKey.Password = vm.AzureOpenAIKey; // Initialize manually

            var lblDeploy = new System.Windows.Controls.TextBlock { Text = "Deployment Name:", Margin = new Thickness(0, 0, 0, 5) };
            var txtDeploy = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 0, 0, 20) };
            txtDeploy.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding("DeploymentName") { Source = vm, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });

            var lblTelemetry = new System.Windows.Controls.TextBlock { Text = "Enable Telemetry Logs Tab:", Margin = new Thickness(0, 0, 0, 5) };
            var chkTelemetry = new System.Windows.Controls.CheckBox { Margin = new Thickness(0, 0, 0, 20) };
            chkTelemetry.SetBinding(System.Windows.Controls.CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsTelemetryEnabled") { Source = vm, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });

            var btnSave = new System.Windows.Controls.Button { Content = "Save", HorizontalAlignment = HorizontalAlignment.Right, Width = 80 };
            btnSave.Click += (s, ev) =>
            {
                vm.AzureOpenAIKey = txtKey.Password; // Bind manually back
                vm.SaveSettingsCommand.Execute(null);
                
                // Refresh main view model telemetry property
                if (DataContext is MainViewModel mainVm)
                {
                    mainVm.IsTelemetryEnabled = vm.IsTelemetryEnabled;
                }

                settingsWindow.Close(); // Auto-close window
            };
            
            System.Windows.Controls.Grid.SetRow(lblEndpoint, 0);
            System.Windows.Controls.Grid.SetRow(txtEndpoint, 1);
            System.Windows.Controls.Grid.SetRow(lblKey, 2);
            System.Windows.Controls.Grid.SetRow(txtKey, 3);
            System.Windows.Controls.Grid.SetRow(lblDeploy, 4);
            System.Windows.Controls.Grid.SetRow(txtDeploy, 5);
            System.Windows.Controls.Grid.SetRow(lblTelemetry, 6);
            System.Windows.Controls.Grid.SetRow(chkTelemetry, 7);
            System.Windows.Controls.Grid.SetRow(btnSave, 8);

            grid.Children.Add(lblEndpoint);
            grid.Children.Add(txtEndpoint);
            grid.Children.Add(lblKey);
            grid.Children.Add(txtKey);
            grid.Children.Add(lblDeploy);
            grid.Children.Add(txtDeploy);
            grid.Children.Add(lblTelemetry);
            grid.Children.Add(chkTelemetry);
            grid.Children.Add(btnSave);

            settingsWindow.Content = grid;
            settingsWindow.ShowDialog();
        }

        private bool _isDarkTheme = true;
        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;
            var app = Application.Current;
            var dict = new ResourceDictionary { Source = new Uri(_isDarkTheme ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml", UriKind.Relative) };
            
            // Remove the old theme dictionary (assuming it's the first one, or just clear and add)
            app.Resources.MergedDictionaries.Clear();
            app.Resources.MergedDictionaries.Add(dict);

            if (DataContext is MainViewModel vm)
            {
                vm.UpdateThemeColors(_isDarkTheme);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            App.Current.Shutdown();
        }
    }
}
