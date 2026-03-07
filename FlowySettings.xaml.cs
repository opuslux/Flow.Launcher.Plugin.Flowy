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
            DirectoryGrid.ItemsSource = _settings.Directories;
        }

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
                    Depth = 2,
                    FileTypes = new System.Collections.Generic.List<string> { "*.lnk", "*.exe" }
                });
                _plugin.SaveSettings();
            }
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (DirectoryGrid.SelectedItem is CatalogDirectory selected)
            {
                _settings.Directories.Remove(selected);
                _plugin.SaveSettings();
            }
        }

        private void DirectoryGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
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