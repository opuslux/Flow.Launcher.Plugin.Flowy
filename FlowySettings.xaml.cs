using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Flowy
{
    public partial class FlowySettings : UserControl
    {
        private bool _isDragging = false;
        private CatalogDirectory _draggedItem = null;

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
            
            // Falls das Plugin geupdatet wird und Exclusions noch nicht initialisiert wurden
            if (_settings.Exclusions == null) _settings.Exclusions = new ObservableCollection<ExclusionRule>();
            
            UpdateIndices();
        }

        private void UpdateIndices()
        {
            for (int i = 0; i < _settings.Directories.Count; i++)
            {
                _settings.Directories[i].Index = i;
            }
        }

        // --- Drag and Drop Handlers (für DirectoryList) ---

        private void Handle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var handleGrid = sender as StackPanel; 
            if (handleGrid?.DataContext is CatalogDirectory directory)
            {
                _draggedItem = directory;
                _isDragging = true;
                
                DataObject data = new DataObject(typeof(CatalogDirectory), directory);
                DragDropEffects effects = DragDrop.DoDragDrop(DirectoryList, data, DragDropEffects.Move);
                
                if (effects == DragDropEffects.Move || effects == DragDropEffects.None)
                {
                    _draggedItem = null;
                    _isDragging = false;
                }
            }
        }

        private void DirectoryList_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;

            var listBox = sender as ListBox;
            if (listBox == null) return;

            foreach (var item in _settings.Directories)
            {
                item.ShowDropTop = false;
                item.ShowDropBottom = false;
            }

            var hitTest = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
            if (hitTest != null)
            {
                var listBoxItem = FindAncestor<ListBoxItem>(hitTest.VisualHit);
                if (listBoxItem != null && listBoxItem.DataContext is CatalogDirectory targetItem && targetItem != _draggedItem)
                {
                    Point position = e.GetPosition(listBoxItem);
                    int targetIndex = _settings.Directories.IndexOf(targetItem);
                    
                    if (position.Y < listBoxItem.ActualHeight / 2)
                    {
                        targetItem.ShowDropTop = true;
                    }
                    else
                    {
                        if (targetIndex + 1 < _settings.Directories.Count)
                        {
                            var nextItem = _settings.Directories[targetIndex + 1];
                            if (nextItem != _draggedItem)
                            {
                                nextItem.ShowDropTop = true;
                            }
                        }
                        else
                        {
                            targetItem.ShowDropBottom = true;
                        }
                    }
                }
            }
        }

        private void DirectoryList_DragLeave(object sender, DragEventArgs e)
        {
            foreach (var item in _settings.Directories)
            {
                item.ShowDropTop = false;
                item.ShowDropBottom = false;
            }
        }

        private void DirectoryList_Drop(object sender, DragEventArgs e)
        {
            foreach (var item in _settings.Directories)
            {
                item.ShowDropTop = false;
                item.ShowDropBottom = false;
            }

            if (e.Data.GetData(typeof(CatalogDirectory)) is CatalogDirectory droppedItem &&
                _draggedItem != null && _isDragging)
            {
                var directories = _settings.Directories;
                var listBox = sender as ListBox;
                if (listBox == null) return;

                var hitTest = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
                if (hitTest != null)
                {
                    var listBoxItem = FindAncestor<ListBoxItem>(hitTest.VisualHit);
                    if (listBoxItem?.DataContext is CatalogDirectory targetItem && targetItem != _draggedItem)
                    {
                        Point position = e.GetPosition(listBoxItem);
                        bool dropAtBottom = position.Y >= listBoxItem.ActualHeight / 2;

                        directories.Remove(_draggedItem);
                        
                        int newTargetIndex = directories.IndexOf(targetItem);
                        if (dropAtBottom) newTargetIndex++;

                        if (newTargetIndex < 0) newTargetIndex = 0;
                        if (newTargetIndex > directories.Count) newTargetIndex = directories.Count;

                        directories.Insert(newTargetIndex, _draggedItem);
                        
                        _plugin.SaveSettings();
                        UpdateIndices();
                    }
                }
            }
            
            _draggedItem = null;
            _isDragging = false;
        }

        private static T FindAncestor<T>(DependencyObject dependencyObject) where T : class
        {
            var parent = VisualTreeHelper.GetParent(dependencyObject);
            while (parent != null)
            {
                if (parent is T ancestor)
                {
                    return ancestor;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        // --- Buttons: Bottom Bar (Catalog) ---

        private async void Rescan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                btn.Content = "Scanning...";
                btn.IsEnabled = false;
                ScanProgressBar.Visibility = Visibility.Visible;

                // NEU: Exclusions an Indexer übergeben
                await Task.Run(() => _indexer.BuildIndex(_settings.Directories, _settings.Exclusions));
                _plugin.SaveCache(); 

                int totalFiles = _settings.Directories.Sum(d => d.FileCount);
                int totalFolders = _settings.Directories.Sum(d => d.FolderCount);

                btn.Content = "Rescan Catalogue";
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
                    Depth = 0, 
                    FileTypes = new System.Collections.Generic.List<string>(), 
                    Comment = "" 
                });
                
                UpdateIndices();
                _plugin.SaveSettings();
            }
        }

        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CatalogDirectory directory)
            {
                _settings.Directories.Remove(directory);
                UpdateIndices();
                _plugin.SaveSettings();
            }
        }

        private void SortPaths_Click(object sender, RoutedEventArgs e)
        {
            if (_settings.Directories.Count <= 1) return;

            var sortedList = _settings.Directories.OrderBy(d => d.Path).ToList();
            
            _settings.Directories.Clear();
            foreach (var item in sortedList)
            {
                _settings.Directories.Add(item);
            }

            UpdateIndices();
            _plugin.SaveSettings();
        }

        private void DeleteAll_Click(object sender, RoutedEventArgs e)
        {
            if (_settings.Directories.Count == 0) return;

            var result = MessageBox.Show(
                "Are you sure you want to permanently delete ALL catalogue rules?\n\nTip: You might want to export your settings first.", 
                "Delete All Rules", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _settings.Directories.Clear();
                UpdateIndices();
                _plugin.SaveSettings();
            }
        }

        // --- Buttons: Exclusion Rules ---
        
        private void AddExclusion_Click(object sender, RoutedEventArgs e)
        {
            _settings.Exclusions.Add(new ExclusionRule
            {
                Path = "",
                Recursive = true,
                Suffixes = new System.Collections.Generic.List<string>()
            });
            _plugin.SaveSettings();
        }

        private void RemoveExclusion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ExclusionRule rule)
            {
                _settings.Exclusions.Remove(rule);
                _plugin.SaveSettings();
            }
        }

        // --- Import / Export ---

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = $"Flowy Settings Backup - {DateTime.Now:yyyyMMdd}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Hinweis: Wenn wir das so exportieren, speichert es Directories und General Settings, aber nicht Exclusions!
                    // Wir sollten _settings komplett exportieren (für zukünftige Features)
                    var json = System.Text.Json.JsonSerializer.Serialize(_settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dialog.FileName, json);

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
                    
                    // Wir versuchen das alte Format (List<CatalogDirectory>) oder das neue Format (Settings-Objekt) zu laden.
                    try 
                    {
                        var importedSettings = System.Text.Json.JsonSerializer.Deserialize<Settings>(json);
                        if (importedSettings != null && importedSettings.Directories != null)
                        {
                            _plugin.ImportDirectories(importedSettings.Directories.ToList());
                            
                            _settings.Exclusions.Clear();
                            if (importedSettings.Exclusions != null)
                            {
                                foreach(var ex in importedSettings.Exclusions) _settings.Exclusions.Add(ex);
                            }
                            _plugin.SaveSettings();
                            MessageBox.Show("Settings imported! The catalog is now being rescanned.", "Import Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    } 
                    catch { /* Fallback */ }

                    // Fallback für alte Backups
                    var imported = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<CatalogDirectory>>(json);
                    if (imported != null)
                    {
                        _plugin.ImportDirectories(imported);
                        MessageBox.Show("Legacy settings imported! The catalog is now being rescanned.", "Import Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Import failed. Make sure it's a valid Flowy backup file.\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- Auto-Save Handler ---

        private void UIField_Changed(object sender, RoutedEventArgs e)
        {
            SaveWithDelay();
        }

        private void IntervalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveWithDelay();
        }

        private void NumberValidationTextBox(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var regex = new System.Text.RegularExpressions.Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void SaveWithDelay()
        {
            Task.Run(async () =>
            {
                await Task.Delay(100);
                _plugin.SaveSettings();
            });
        }
    }
}