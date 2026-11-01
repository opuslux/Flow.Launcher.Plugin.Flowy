using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.Flowy
{
    public class IndexedItem
    {
        public string Path { get; set; }
        public int RuleIndex { get; set; }
    }

    public class Indexer
    {
        private readonly object _scanLock = new object();

        // Der Katalog wird nie verändert, sondern nur als Ganzes ersetzt.
        // Leser (Query) nehmen sich eine Referenz und brauchen keinen Lock.
        private volatile List<IndexedItem> _catalog = new List<IndexedItem>();
        public List<IndexedItem> Catalog
        {
            get => _catalog;
            set => _catalog = value ?? new List<IndexedItem>();
        }

        // Wird von Main gesetzt, damit Fehler im Flow-Log landen
        public Action<string, Exception> LogError { get; set; }

        private class CompiledExclusion
        {
            public string Path;
            public Regex Glob;
            public bool Recursive;
            public HashSet<string> Suffixes;

            public bool AppliesTo(string dir)
            {
                if (Glob == null)
                {
                    return dir.Equals(Path, StringComparison.OrdinalIgnoreCase) ||
                           (Recursive && dir.StartsWith(Path + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                }

                if (Glob.IsMatch(dir)) return true;
                if (!Recursive) return false;

                for (var parent = System.IO.Path.GetDirectoryName(dir); !string.IsNullOrEmpty(parent); parent = System.IO.Path.GetDirectoryName(parent))
                {
                    if (Glob.IsMatch(parent)) return true;
                }
                return false;
            }
        }

        // "*" = beliebige Zeichen (auch "\"), "?" = genau ein Zeichen
        private static Regex GlobToRegex(string glob)
        {
            var sb = new StringBuilder("^");
            foreach (char c in glob)
            {
                if (c == '*') sb.Append(".*");
                else if (c == '?') sb.Append('.');
                else sb.Append(Regex.Escape(c.ToString()));
            }
            sb.Append('$');
            return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        }

        public void BuildIndex(IEnumerable<CatalogDirectory> directories, IEnumerable<ExclusionRule> exclusions)
        {
            lock (_scanLock)
            {
                var newCatalog = new List<IndexedItem>();
                int currentRule = 0;

                // 1. Ausschlüsse für extrem schnellen Abgleich vorbereiten
                var globalExcludedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var compiledPathExclusions = new List<CompiledExclusion>();

                if (exclusions != null)
                {
                    foreach (var ex in exclusions)
                    {
                        if (string.IsNullOrWhiteSpace(ex.Path))
                        {
                            // Globaler Ausschluss (kein Pfad, nur Suffix)
                            foreach (var s in ex.Suffixes) globalExcludedExts.Add(s.TrimStart('*').ToLower());
                        }
                        else
                        {
                            // Pfad-basierter Ausschluss, optional als Glob
                            string expandedPath = Environment.ExpandEnvironmentVariables(ex.Path).Trim().TrimEnd('\\', '/');
                            bool isGlob = expandedPath.IndexOfAny(new[] { '*', '?' }) >= 0;
                            compiledPathExclusions.Add(new CompiledExclusion
                            {
                                Path = expandedPath,
                                Glob = isGlob ? GlobToRegex(expandedPath) : null,
                                Recursive = ex.Recursive,
                                Suffixes = new HashSet<string>(ex.Suffixes.Select(s => s.TrimStart('*').ToLower()), StringComparer.OrdinalIgnoreCase)
                            });
                        }
                    }
                }

                foreach (var dirConfig in directories)
                {
                    dirConfig.Index = currentRule;
                    string expandedPath = Environment.ExpandEnvironmentVariables(dirConfig.Path);

                    if (Directory.Exists(expandedPath))
                    {
                        int fileCount = 0;
                        int folderCount = 0;

                        var allowedExtensions = new HashSet<string>(
                            dirConfig.FileTypes
                                .Where(t => !string.IsNullOrWhiteSpace(t))
                                .Select(t => t.TrimStart('*').ToLower())
                        );

                        ScanDirectory(expandedPath, dirConfig, 0, newCatalog, ref fileCount, ref folderCount, allowedExtensions, currentRule + 1, globalExcludedExts, compiledPathExclusions);

                        dirConfig.FileCount = fileCount;
                        dirConfig.FolderCount = folderCount;
                    }
                    else
                    {
                        dirConfig.FileCount = 0;
                        dirConfig.FolderCount = 0;
                    }
                    currentRule++;
                }

                Catalog = newCatalog;
            }
        }

        private static bool IsFolderExcluded(string dir, List<CompiledExclusion> exclusions)
        {
            // Nur Regeln ohne Suffix blockieren den GANZEN Ordner
            foreach (var ex in exclusions)
            {
                if (ex.Suffixes.Count == 0 && ex.AppliesTo(dir)) return true;
            }
            return false;
        }

        private void ScanDirectory(string currentPath, CatalogDirectory config, int currentDepth, List<IndexedItem> results, ref int fileCount, ref int folderCount, HashSet<string> allowedExts, int ruleIndex, HashSet<string> globalExcludedExts, List<CompiledExclusion> compiledPathExclusions)
        {
            if (currentDepth > config.Depth) return;

            try
            {
                // PRÜFUNG 1: Ist dieser gesamte Ordner ausgeschlossen?
                // Wenn ja, brechen wir hier komplett ab (spart extrem viel Zeit!)
                if (IsFolderExcluded(currentPath, compiledPathExclusions)) return;

                // DATEIEN SCANNEN
                if (allowedExts.Count > 0)
                {
                    // Pfad-spezifische Suffix-Ausschlüsse einmal pro Ordner ermitteln
                    var excludedExts = new HashSet<string>(globalExcludedExts, StringComparer.OrdinalIgnoreCase);
                    foreach (var ex in compiledPathExclusions)
                    {
                        if (ex.Suffixes.Count > 0 && ex.AppliesTo(currentPath)) excludedExts.UnionWith(ex.Suffixes);
                    }
                    bool excludeAll = excludedExts.Contains(".*");

                    foreach (var file in Directory.GetFiles(currentPath))
                    {
                        string ext = Path.GetExtension(file).ToLower();

                        if (excludeAll || excludedExts.Contains(ext)) continue;

                        // Wenn nicht ausgeschlossen, in den Katalog aufnehmen
                        if (allowedExts.Contains(".*") || allowedExts.Contains("") || allowedExts.Contains(ext))
                        {
                            results.Add(new IndexedItem { Path = file, RuleIndex = ruleIndex });
                            fileCount++;
                        }
                    }
                }

                // UNTERORDNER SCANNEN
                foreach (var subDir in Directory.GetDirectories(currentPath))
                {
                    if (config.IncludeDirectories && !IsFolderExcluded(subDir, compiledPathExclusions))
                    {
                        results.Add(new IndexedItem { Path = subDir, RuleIndex = ruleIndex });
                        folderCount++;
                    }

                    ScanDirectory(subDir, config, currentDepth + 1, results, ref fileCount, ref folderCount, allowedExts, ruleIndex, globalExcludedExts, compiledPathExclusions);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception ex)
            {
                LogError?.Invoke($"Failed to scan '{currentPath}'", ex);
            }
        }
    }
}
