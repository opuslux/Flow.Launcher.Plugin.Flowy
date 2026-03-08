using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Timers;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.Flowy
{
    public class Main : IPlugin, ISettingProvider
    {
        private PluginInitContext _context;
        private Settings _settings;
        private Indexer _indexer = new Indexer();

        // _settingsPath wurde entfernt, das macht jetzt die API!
        private string _cachePath;
        private System.Timers.Timer _rescanTimer;

        // Ein Schloss für die Thread-Sicherheit
        private readonly object _catalogLock = new object();

        public void Init(PluginInitContext context)
        {
            _context = context;

            // Globaler Exception Handler
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = (Exception)args.ExceptionObject;
                MessageBox.Show($"Kritischer Flowy-Fehler:\n{ex.Message}\n\n{ex.StackTrace}",
                                "Flowy Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            // NEU: Flow Launcher lädt die Settings automatisch vom korrekten Ort (Portable oder %APPDATA%)
            _settings = _context.API.LoadSettingJsonStorage<Settings>();

            // Sicherheits-Check: Falls die Datei komplett neu ist
            if (_settings.RescanIntervalMinutes <= 0)
            {
                _settings.RescanIntervalMinutes = 30;
            }

            // Der Cache bleibt lokal im Plugin-Ordner (Darf bei einem Update ruhig gelöscht werden)
            var dataDirectory = _context.CurrentPluginMetadata.PluginDirectory;
            _cachePath = Path.Combine(dataDirectory, "cache.json");

            LoadCache();

            // Erster Scan über die zentrale Methode
            TriggerScan();

            StartRescanTimer();
        }

        public List<Result> Query(Query query)
        {
            if (string.IsNullOrWhiteSpace(query.Search)) return new List<Result>();

            var results = new List<Result>();

            // Sperren, damit der Indexer nicht während der Suche reinschreibt
            lock (_catalogLock)
            {
                foreach (var path in _indexer.Catalog)
                {
                    string fileNameWithExt = Path.GetFileName(path);
                    var matchResult = _context.API.FuzzySearch(query.Search, fileNameWithExt);

                    if (matchResult.Score > 0)
                    {
                        results.Add(new Result
                        {
                            Title = fileNameWithExt,
                            SubTitle = Path.GetDirectoryName(path),
                            IcoPath = path,
                            Score = matchResult.Score,
                            Action = _ => {
                                try
                                {
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                                }
                                catch (Exception ex)
                                {
                                    _context.API.ShowMsg("Fehler beim Öffnen", ex.Message);
                                }
                                return true;
                            }
                        });
                    }
                }
            }

            return results.OrderByDescending(r => r.Score).ToList();
        }

        public Control CreateSettingPanel()
        {
            try
            {
                return new FlowySettings(_settings, _indexer, this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Einstellungen:\n{ex.Message}", "Flowy Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new Label { Content = "Fehler beim Laden der UI." };
            }
        }

        // --- Zentrale Scan-Logik ---

        public void TriggerScan()
        {
            Task.Run(() => {
                // Auch beim Schreiben sperren!
                lock (_catalogLock)
                {
                    _indexer.BuildIndex(_settings.Directories);
                }
                SaveCache();
            });
        }

        private void OnRescanTimerElapsed(object sender, ElapsedEventArgs e)
        {
            TriggerScan();
        }

        // --- Speicher-Logik ---

        public void SaveSettings()
        {
            // NEU: API speichert am perfekten, update-sicheren Ort
            _context.API.SaveSettingJsonStorage<Settings>();

            StartRescanTimer(); // Timer aktualisieren
        }

        private void LoadCache()
        {
            if (!File.Exists(_cachePath)) return;
            try
            {
                string json = File.ReadAllText(_cachePath);
                var cachedCatalog = JsonSerializer.Deserialize<List<string>>(json);
                if (cachedCatalog != null)
                {
                    lock (_catalogLock) _indexer.Catalog = cachedCatalog;
                }
            }
            catch { }
        }

        public void SaveCache()
        {
            try
            {
                string json;
                lock (_catalogLock) json = JsonSerializer.Serialize(_indexer.Catalog);
                File.WriteAllText(_cachePath, json);
            }
            catch { }
        }

        private void StartRescanTimer()
        {
            _rescanTimer?.Stop();
            _rescanTimer?.Dispose();

            if (_settings.RescanIntervalMinutes <= 0) return;

            _rescanTimer = new System.Timers.Timer(_settings.RescanIntervalMinutes * 60 * 1000);
            _rescanTimer.Elapsed += OnRescanTimerElapsed;
            _rescanTimer.AutoReset = true;
            _rescanTimer.Start();
        }
    }
}