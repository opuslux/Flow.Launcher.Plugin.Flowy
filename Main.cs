using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Timers; // WICHTIG: Das hier importiert den korrekten Timer
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.Flowy
{
    // Die Klasse muss IPlugin (für die Suche) und ISettingProvider (für das Menü) implementieren
    public class Main : IPlugin, ISettingProvider
    {
        private PluginInitContext _context;
        private Settings _settings;
        private Indexer _indexer = new Indexer();
        private string _settingsPath;
        private string _cachePath; // NEU: Pfad für den Cache

        // FEHLERFIX AMBIGUITÄT: Wir nutzen den vollständig qualifizierten Namen
        private System.Timers.Timer _rescanTimer;

        // --- IPlugin Interface (MUSS implementiert sein) ---

        public void Init(PluginInitContext context)
        {
            _context = context;
            _settingsPath = Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "settings.json");
            _cachePath = Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "cache.json"); // NEU

            // Lade die Einstellungen
            _settings = LoadSettings();

            LoadCache(); // NEU: Blitzschnell alte Ergebnisse laden!

            // Initialer Scan beim Start im Hintergrund
            // NEU: Nach dem ersten Hintergrund-Scan den frischen Cache speichern
            Task.Run(() => {
                _indexer.BuildIndex(_settings.Directories);
                SaveCache();
            });

            // Starte den Hintergrund-Timer für Auto-Scan
            StartRescanTimer();
        }

        // FEHLERFIX CS0535: Diese Methode Query MUSS vorhanden sein!
        public List<Result> Query(Query query)
        {
            if (string.IsNullOrWhiteSpace(query.Search)) return new List<Result>();

            var results = new List<Result>();

            foreach (var path in _indexer.Catalog)
            {
                // Explorer-Vibe: Name MIT Endung, Untertitel nur der Ordner
                string fileNameWithExt = Path.GetFileName(path);
                string directory = Path.GetDirectoryName(path);

                // Fuzzy-Suche mit Flow Launcher API
                var matchResult = _context.API.FuzzySearch(query.Search, fileNameWithExt);

                if (matchResult.Score > 0)
                {
                    results.Add(new Result
                    {
                        Title = fileNameWithExt, // Zeigt "sokrates.lnk"
                        SubTitle = directory,    // Zeigt nur den Ordner
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

            return results.OrderByDescending(r => r.Score).ToList();
        }

        // --- ISettingProvider Interface (MUSS implementiert sein) ---

        // FEHLERFIX CS0535: Diese Methode CreateSettingPanel MUSS vorhanden sein!
        public Control CreateSettingPanel()
        {
            return new FlowySettings(_settings, _indexer, this);
        }

        // --- Eigene Hilfsmethoden ---

        // FEHLERFIX CS0103: Diese Methode LoadSettings MUSS vorhanden sein!
        private Settings LoadSettings()
        {
            if (File.Exists(_settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsPath);
                    // Verhindert, dass RescanIntervalMinutes versehentlich 0 wird
                    var loaded = JsonSerializer.Deserialize<Settings>(json);
                    if (loaded != null && loaded.RescanIntervalMinutes <= 0) loaded.RescanIntervalMinutes = 30;
                    return loaded ?? new Settings();
                }
                catch { return new Settings(); }
            }
            return new Settings();
        }

        public void SaveSettings()
        {
            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);

            // Wenn sich die Einstellungen ändern, starten wir den Timer neu (falls das Intervall geändert wurde)
            StartRescanTimer();
        }

        // --- NEU: CACHE METHODEN ---
        public void LoadCache()
        {
            if (File.Exists(_cachePath))
            {
                try
                {
                    string json = File.ReadAllText(_cachePath);
                    var cachedCatalog = JsonSerializer.Deserialize<List<string>>(json);
                    if (cachedCatalog != null) _indexer.Catalog = cachedCatalog;
                }
                catch { /* Falls Datei kaputt, starte einfach mit leerem Katalog */ }
            }
        }

        public void SaveCache()
        {
            try
            {
                string json = JsonSerializer.Serialize(_indexer.Catalog);
                File.WriteAllText(_cachePath, json);
            }
            catch { }
        }

        private void StartRescanTimer()
        {
            // Alten Timer stoppen, falls vorhanden
            _rescanTimer?.Stop();
            _rescanTimer?.Dispose();

            // Wenn das Intervall auf 0 steht, machen wir keinen Auto-Scan
            if (_settings.RescanIntervalMinutes <= 0) return;

            // Timer mit dem eingestellten Intervall erstellen (Minuten -> Millisekunden)
            // FEHLERFIX AMBIGUITÄT: Wir nutzen hier auch System.Timers
            _rescanTimer = new System.Timers.Timer(_settings.RescanIntervalMinutes * 60 * 1000);
            _rescanTimer.Elapsed += OnRescanTimerElapsed;
            _rescanTimer.AutoReset = true; // Timer soll sich wiederholen
            _rescanTimer.Start();
        }

        private void OnRescanTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Den Scan im Hintergrund ausführen
            Task.Run(() => {
                _indexer.BuildIndex(_settings.Directories);
                SaveCache(); // NEU: Auch nach dem Auto-Scan speichern
            });
        }
    }
}