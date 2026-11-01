using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Timers;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.Flowy
{
    public class Main : IPlugin, ISettingProvider, IContextMenu, IDisposable
    {
        // Mit vorhandenem Cache startet der erste Scan erst später, um den Windows-Login zu entlasten
        private static readonly TimeSpan StartupScanDelay = TimeSpan.FromSeconds(30);
        private const int WatcherDebounceMs = 5000;
        private const int UsageSaveDelayMs = 2000;
        private const string RescanCommand = "flowy rescan";

        private PluginInitContext _context;
        private Settings _settings;
        private Indexer _indexer = new Indexer();
        private string _cachePath;
        private string _legacyCachePath;
        private string _usagePath;
        private string _cacheDir;
        private string _settingsDir;
        private System.Timers.Timer _rescanTimer;
        private readonly object _saveLock = new object();

        // Scan-Steuerung: höchstens ein Scan gleichzeitig, weitere Anfragen werden zu einem Folge-Scan zusammengefasst
        private readonly object _scanStateLock = new object();
        private bool _scanning;
        private bool _rescanRequested;
        private Task _scanTask = Task.CompletedTask;

        private readonly object _watcherLock = new object();
        private readonly List<(FileSystemWatcher Watcher, int Depth)> _watchers = new List<(FileSystemWatcher, int)>();
        private System.Threading.Timer _watchDebounce;

        private readonly object _usageLock = new object();
        private Dictionary<string, int> _usage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private System.Threading.Timer _usageSaveTimer;

        private string _pwshPath;
        private bool _pwshResolved;

        public void Init(PluginInitContext context)
        {
            _context = context;

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = (Exception)args.ExceptionObject;
                _context.API.LogException(nameof(Main), "Unhandled exception", ex);
                MessageBox.Show($"Critical Flowy error:\n{ex.Message}\n\n{ex.StackTrace}",
                                "Flowy Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            var initWatch = Stopwatch.StartNew();
            _settings = _context.API.LoadSettingJsonStorage<Settings>();
            _context.API.LogInfo(nameof(Main), $"Settings loaded in {initWatch.ElapsedMilliseconds} ms");

            if (_settings.RescanIntervalMinutes <= 0)
            {
                _settings.RescanIntervalMinutes = 30;
            }

            _indexer.LogError = (message, ex) => _context.API.LogException(nameof(Indexer), message, ex);

            var metadata = _context.CurrentPluginMetadata;
            _cacheDir = metadata.PluginCacheDirectoryPath ?? metadata.PluginDirectory;
            _settingsDir = metadata.PluginSettingsDirectoryPath ?? metadata.PluginDirectory;
            _cachePath = Path.Combine(_cacheDir, "cache.json");
            _legacyCachePath = Path.Combine(metadata.PluginDirectory, "cache.json");
            _usagePath = Path.Combine(_settingsDir, "usage.json");

            _watchDebounce = new System.Threading.Timer(_ => TriggerScan("watcher"));
            _usageSaveTimer = new System.Threading.Timer(_ => SaveUsage());

            StartRescanTimer();

            // Cache, Watcher und Start-Scan laufen im Hintergrund, damit Init sofort zurückkehrt
            Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                bool hasCache = LoadCache();
                LoadUsage();
                SetupWatchers();
                _context.API.LogInfo(nameof(Main), $"Background init finished in {sw.ElapsedMilliseconds} ms");
                if (hasCache) await Task.Delay(StartupScanDelay);
                await RunScanAsync("startup");
            });
        }

        public List<Result> LoadContextMenus(Result selected)
        {
            var results = new List<Result>();

            if (selected?.ContextData is not string path || string.IsNullOrEmpty(path))
                return results;

            string dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path);

            results.Add(new Result
            {
                Title = "Show in File Explorer",
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue838"),
                Action = e =>
                {
                    try { Process.Start("explorer.exe", $"/select,\"{path}\""); }
                    catch (Exception ex) { _context.API.ShowMsg("Error opening Explorer", ex.Message); }
                    return true;
                }
            });

            results.Add(new Result
            {
                Title = "Copy path",
                SubTitle = path,
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue8c8"),
                Action = e =>
                {
                    _context.API.CopyToClipboard(path, showDefaultNotification: false);
                    return true;
                }
            });

            if (!string.IsNullOrEmpty(dir))
            {
                results.Add(TerminalResult("Open cmd here", dir, admin: false, powershell: false));
                results.Add(TerminalResult("Open cmd here (Admin)", dir, admin: true, powershell: false));
                results.Add(TerminalResult("Open PowerShell here", dir, admin: false, powershell: true));
                results.Add(TerminalResult("Open PowerShell here (Admin)", dir, admin: true, powershell: true));
            }

            // Flow sortiert auch das Kontextmenü nach Score, deshalb hier ganz nach unten
            results.Add(RescanResult(score: -1000));

            return results;
        }

        private Result TerminalResult(string title, string dir, bool admin, bool powershell)
        {
            return new Result
            {
                Title = title,
                SubTitle = dir,
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: admin ? "\uea18" : "\ue756"),
                Action = e =>
                {
                    if (powershell)
                    {
                        string pwsh = FindPwsh();
                        if (pwsh != null)
                            StartProcess(pwsh, $"-NoExit -WorkingDirectory {PathHelper.QuoteArg(dir)}", dir, admin);
                        else
                            StartProcess("powershell.exe", $"-NoExit -Command \"Set-Location -LiteralPath '{dir.Replace("'", "''")}'\"", dir, admin);
                    }
                    else
                    {
                        StartProcess("cmd.exe", $"/K cd /d \"{dir}\"", dir, admin);
                    }
                    return true;
                }
            };
        }

        private void StartProcess(string fileName, string arguments, string workingDirectory, bool admin)
        {
            try
            {
                // Bei Elevation wird WorkingDirectory ignoriert, deshalb steckt der Ordner auch in den Argumenten
                Process.Start(new ProcessStartInfo(fileName, arguments)
                {
                    UseShellExecute = true,
                    WorkingDirectory = workingDirectory,
                    Verb = admin ? "runas" : ""
                });
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // UAC-Abfrage abgebrochen
            }
            catch (Exception ex)
            {
                _context.API.ShowMsg($"Failed to start {Path.GetFileName(fileName)}", ex.Message);
            }
        }

        private string FindPwsh()
        {
            if (_pwshResolved) return _pwshPath;

            var candidates = (Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => Path.Combine(d.Trim(), "pwsh.exe"))
                .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe"));

            _pwshPath = candidates.FirstOrDefault(File.Exists);
            _pwshResolved = true;
            return _pwshPath;
        }

        public List<Result> Query(Query query)
        {
            if (string.IsNullOrWhiteSpace(query.Search)) return new List<Result>();

            var pathResults = QueryPath(query.Search);
            if (pathResults != null) return pathResults;

            var results = new List<Result>();

            string trimmed = query.Search.Trim();
            if (trimmed.StartsWith("flowy", StringComparison.OrdinalIgnoreCase) &&
                RescanCommand.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(RescanResult());
            }

            // Snapshot: der Katalog wird beim Scan nur als Ganzes ersetzt
            var catalog = _indexer.Catalog;

            foreach (var item in catalog)
            {
                string fileNameWithExt = Path.GetFileName(item.Path);
                var matchResult = _context.API.FuzzySearch(query.Search, fileNameWithExt);

                if (matchResult.Score > 0)
                {
                    string prefix = _settings.ShowRuleNumber ? $"#{item.RuleIndex} | " : "";

                    results.Add(new Result
                    {
                        Title = fileNameWithExt,
                        SubTitle = $"{prefix}{Path.GetDirectoryName(item.Path)}",
                        IcoPath = item.Path,
                        ContextData = item.Path,
                        Score = matchResult.Score + UsageBoost(item.Path),
                        Action = e =>
                        {
                            if (e.SpecialKeyState.CtrlPressed)
                            {
                                try { Process.Start("explorer.exe", $"/select,\"{item.Path}\""); }
                                catch (Exception ex) { _context.API.ShowMsg("Error locating file", ex.Message); }
                            }
                            else
                            {
                                IncrementUsage(item.Path);
                                try { Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true }); }
                                catch (Exception ex) { _context.API.ShowMsg("Error opening", ex.Message); }
                            }
                            return true;
                        }
                    });
                }
            }

            return results.OrderByDescending(r => r.Score).Take(_settings.MaxResults).ToList();
        }

        private Result RescanResult(int score = 10000)
        {
            return new Result
            {
                Title = "Flowy: Rescan catalogue",
                SubTitle = "Rebuild the Flowy catalogue now",
                IcoPath = "Images\\app.png",
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\ue72c"),
                Score = score,
                Action = e =>
                {
                    RunScanAsync("manual").ContinueWith(_ =>
                    {
                        int files = _settings.Directories.Sum(d => d.FileCount);
                        int folders = _settings.Directories.Sum(d => d.FolderCount);
                        _context.API.ShowMsg("Flowy", $"Scan complete: {files} files, {folders} folders");
                    });
                    return true;
                }
            };
        }

        // Liefert Unterordner, wenn die Eingabe ein Pfad ist (z.B. "c:\" oder "c:\win"), sonst null.
        private List<Result> QueryPath(string search)
        {
            if (!PathHelper.TryParsePathInput(search, out string dir, out string filter)) return null;

            var results = new List<Result>();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return results;

            try
            {
                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    string name = Path.GetFileName(sub);
                    if (filter.Length > 0 && !name.StartsWith(filter, StringComparison.OrdinalIgnoreCase)
                        && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string full = sub;
                    results.Add(new Result
                    {
                        Title = name,
                        SubTitle = full,
                        IcoPath = full,
                        ContextData = full,
                        // Tab/Autovervollständigung: Pfad mit abschließendem Backslash einsetzen
                        AutoCompleteText = full.TrimEnd('\\') + "\\",
                        // Startet-mit-Treffer vor reinen Teilstring-Treffern
                        Score = name.StartsWith(filter, StringComparison.OrdinalIgnoreCase) ? 100 : 50,
                        Action = e =>
                        {
                            try { Process.Start(new ProcessStartInfo(full) { UseShellExecute = true }); }
                            catch (Exception ex) { _context.API.ShowMsg("Error opening", ex.Message); }
                            return true;
                        }
                    });
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception ex)
            {
                _context.API.LogException(nameof(Main), $"Failed to list '{dir}'", ex);
            }

            return results.OrderByDescending(r => r.Score)
                          .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                          .Take(_settings.MaxResults)
                          .ToList();
        }

        public Control CreateSettingPanel()
        {
            try
            {
                return new FlowySettings(_settings, this);
            }
            catch (Exception ex)
            {
                _context.API.LogException(nameof(Main), "Failed to load settings panel", ex);
                MessageBox.Show($"Failed to load Flowy settings:\n{ex.Message}", "Flowy Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new Label { Content = "Failed to load settings UI." };
            }
        }

        // --- Scan ---

        public void TriggerScan(string trigger = "manual")
        {
            _ = RunScanAsync(trigger);
        }

        public Task RunScanAsync(string trigger)
        {
            lock (_scanStateLock)
            {
                if (_scanning)
                {
                    // Der laufende Scan hängt danach noch einen Durchlauf an
                    _rescanRequested = true;
                    return _scanTask;
                }
                _scanning = true;
                _scanTask = Task.Run(() => ScanLoop(trigger));
                return _scanTask;
            }
        }

        private void ScanLoop(string trigger)
        {
            while (true)
            {
                ScanOnce(trigger);
                lock (_scanStateLock)
                {
                    if (!_rescanRequested)
                    {
                        _scanning = false;
                        return;
                    }
                    _rescanRequested = false;
                    trigger = "queued";
                }
            }
        }

        private void ScanOnce(string trigger)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                _indexer.BuildIndex(_settings.Directories.ToList(), _settings.Exclusions?.ToList());
            }
            catch (Exception ex)
            {
                _context.API.LogException(nameof(Main), $"Scan ({trigger}) failed", ex);
                return;
            }
            long scanMs = sw.ElapsedMilliseconds;
            SaveCache();
            _context.API.LogInfo(nameof(Main), $"Scan ({trigger}) finished in {scanMs} ms, cache saved after {sw.ElapsedMilliseconds} ms: {_indexer.Catalog.Count} entries");
        }

        private void OnRescanTimerElapsed(object sender, ElapsedEventArgs e)
        {
            TriggerScan("timer");
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

        // --- FileSystemWatcher ---

        private void SetupWatchers()
        {
            lock (_watcherLock)
            {
                DisposeWatchers();
                if (!_settings.WatchForChanges) return;

                // Gleiche Pfade nur einmal beobachten, mit der größten Tiefe
                var targets = _settings.Directories.ToList()
                    .Select(d => (Path: Environment.ExpandEnvironmentVariables(d.Path ?? "").TrimEnd('\\', '/'), d.Depth))
                    .Where(t => t.Path.Length > 0)
                    .GroupBy(t => t.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(g => (Path: g.Key, Depth: g.Max(t => t.Depth)));

                foreach (var (path, depth) in targets)
                {
                    string root = path.EndsWith(":") ? path + "\\" : path;
                    if (!Directory.Exists(root)) continue;

                    try
                    {
                        var watcher = new FileSystemWatcher(root)
                        {
                            IncludeSubdirectories = depth > 0,
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                        };
                        watcher.Created += OnWatchedChange;
                        watcher.Deleted += OnWatchedChange;
                        watcher.Renamed += OnWatchedChange;
                        watcher.Error += OnWatcherError;
                        watcher.EnableRaisingEvents = true;
                        _watchers.Add((watcher, depth));
                    }
                    catch (Exception ex)
                    {
                        _context.API.LogException(nameof(Main), $"Failed to watch '{root}'", ex);
                    }
                }
            }
        }

        private void DisposeWatchers()
        {
            foreach (var (watcher, _) in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
        }

        private void OnWatchedChange(object sender, FileSystemEventArgs e)
        {
            // Eigene Dateien (Cache, Settings) lösen keinen Scan aus
            if (e.FullPath.StartsWith(_cacheDir, StringComparison.OrdinalIgnoreCase) ||
                e.FullPath.StartsWith(_settingsDir, StringComparison.OrdinalIgnoreCase))
                return;

            if (sender is FileSystemWatcher watcher)
            {
                int depth;
                lock (_watcherLock) depth = _watchers.FirstOrDefault(w => w.Watcher == watcher).Depth;

                // Änderungen unterhalb der eingestellten Tiefe ignorieren
                string relative = Path.GetRelativePath(watcher.Path, e.FullPath);
                int level = relative.Count(c => c == Path.DirectorySeparatorChar);
                if (level > depth) return;
            }

            _watchDebounce.Change(WatcherDebounceMs, Timeout.Infinite);
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            _context.API.LogException(nameof(Main), "FileSystemWatcher error, recreating watchers", e.GetException());
            Task.Run(() =>
            {
                SetupWatchers();
                TriggerScan("watcher");
            });
        }

        // --- Settings ---

        public void SaveSettings()
        {
            lock (_saveLock)
            {
                _context.API.SaveSettingJsonStorage<Settings>();
                StartRescanTimer();
            }
            SetupWatchers();
        }

        public void ImportDirectories(List<CatalogDirectory> importedDirs)
        {
            _settings.Directories.Clear();
            foreach (var d in importedDirs)
            {
                _settings.Directories.Add(d);
            }
            SaveSettings();
            TriggerScan("import");
        }

        // --- Cache ---

        private bool LoadCache()
        {
            string path = File.Exists(_cachePath) ? _cachePath : File.Exists(_legacyCachePath) ? _legacyCachePath : null;
            if (path == null) return false;

            var sw = Stopwatch.StartNew();
            try
            {
                string json = File.ReadAllText(path);
                var cachedCatalog = JsonSerializer.Deserialize<List<IndexedItem>>(json);
                if (cachedCatalog == null) return false;

                _indexer.Catalog = cachedCatalog;
                _context.API.LogInfo(nameof(Main), $"Cache loaded from '{path}' in {sw.ElapsedMilliseconds} ms: {cachedCatalog.Count} entries");
                return true;
            }
            catch (Exception ex)
            {
                _context.API.LogException(nameof(Main), $"Failed to load cache '{path}'", ex);
                return false;
            }
        }

        public void SaveCache()
        {
            try
            {
                Directory.CreateDirectory(_cacheDir);
                string json = JsonSerializer.Serialize(_indexer.Catalog);
                File.WriteAllText(_cachePath, json);
            }
            catch (Exception ex)
            {
                _context.API.LogException(nameof(Main), $"Failed to save cache '{_cachePath}'", ex);
            }
        }

        // --- Usage ---

        private int UsageBoost(string path)
        {
            lock (_usageLock)
            {
                return _usage.TryGetValue(path, out int count) ? Math.Min(count, 20) * 5 : 0;
            }
        }

        private void IncrementUsage(string path)
        {
            lock (_usageLock)
            {
                _usage[path] = _usage.TryGetValue(path, out int count) ? count + 1 : 1;
            }
            _usageSaveTimer.Change(UsageSaveDelayMs, Timeout.Infinite);
        }

        private void LoadUsage()
        {
            if (!File.Exists(_usagePath)) return;
            try
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(_usagePath));
                if (loaded == null) return;
                lock (_usageLock)
                {
                    // Bereits gezählte Starts seit Init nicht überschreiben
                    foreach (var (path, count) in loaded)
                        _usage[path] = _usage.TryGetValue(path, out int current) ? current + count : count;
                }
            }
            catch (Exception ex)
            {
                _context.API.LogException(nameof(Main), $"Failed to load usage '{_usagePath}'", ex);
            }
        }

        private void SaveUsage()
        {
            try
            {
                string json;
                lock (_usageLock) json = JsonSerializer.Serialize(_usage);
                Directory.CreateDirectory(_settingsDir);
                File.WriteAllText(_usagePath, json);
            }
            catch (Exception ex)
            {
                _context.API.LogException(nameof(Main), $"Failed to save usage '{_usagePath}'", ex);
            }
        }

        public void Dispose()
        {
            _rescanTimer?.Dispose();
            _watchDebounce?.Dispose();
            _usageSaveTimer?.Dispose();
            SaveUsage();
            lock (_watcherLock) DisposeWatchers();
        }
    }
}
