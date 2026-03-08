using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Flowy
{
    public partial class FlowySettings : UserControl
    {
        private readonly Settings _settings;
        private readonly Indexer _indexer;
        private readonly Main _plugin;

        public FlowySettings(Settings settings, Indexer indexer, Main plugin)
        {
            InitializeComponent();
            _settings = settings;
            _indexer = indexer;
            _plugin = plugin;

            DataContext = _settings;
            DirectoryList.ItemsSource = _settings.Directories;
        }

        // --- Buttons: Bottom Bar ---

        private async void Rescan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                btn.Content = "Scanning...";
                btn.IsEnabled = false;
                ScanProgressBar.Visibility = Visibility.Visible;

                await Task.Run(() => _indexer.BuildIndex(_settings.Directories));
                _plugin.SaveCache(); // Cache aktualisieren!

                int totalFiles = _settings.Directories.Sum(d => d.FileCount);
                int totalFolders = _settings.Directories.Sum(d => d.FolderCount);

                btn.Content = "Rescan Catalog";
                btn.IsEnabled = true;
                ScanProgressBar.Visibility = Visibility.Collapsed;

                MessageBox.Show($"Total {totalFiles} files and {totalFolders} folders found.", "Scan Complete");
            }
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                _settings.Directories.Add(new CatalogDirectory
                {
                    Path = dialog.FolderName,
                    Depth = 0, // Wird in der UI jetzt als leerer Text angezeigt
                    FileTypes = new System.Collections.Generic.List<string>(), // Leer für Placeholder
                    Comment = "" // Leer für Placeholder
                });
                _plugin.SaveSettings();
            }
        }

        // Row-Level Delete (Das rote ❌ in jeder Zeile)
        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CatalogDirectory directory)
            {
                _settings.Directories.Remove(directory);
                _plugin.SaveSettings();
            }
        }

        // --- Import / Export ---

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = "FlowySettingsBackup.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(_settings.Directories, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dialog.FileName, json);

                    // NEU: Bessere Erfolgsmeldung
                    MessageBox.Show("Settings successfully exported!", "Export Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.IO.File.ReadAllText(dialog.FileName);
                    var imported = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<CatalogDirectory>>(json);

                    if (imported != null)
                    {
                        _plugin.ImportDirectories(imported);

                        // NEU: Bessere Erfolgsmeldung
                        MessageBox.Show("Settings imported! The catalog is now being rescanned.", "Import Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Import failed. Make sure it's a valid Flowy backup file.\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- Auto-Save Handler ---

        // Fängt Änderungen in allen TextBoxes und CheckBoxes in der Liste automatisch ab
        private void UIField_Changed(object sender, RoutedEventArgs e)
        {
            SaveWithDelay();
        }

        private void IntervalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveWithDelay();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void SaveWithDelay()
        {
            Task.Run(async () => {
                await Task.Delay(100);
                _plugin.SaveSettings();
            });
        }
    }
}