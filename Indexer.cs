using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        public List<IndexedItem> Catalog { get; set; } = new List<IndexedItem>();

        // NEU: Nimmt jetzt auch die exclusions entgegen
        public void BuildIndex(IEnumerable<CatalogDirectory> directories, IEnumerable<ExclusionRule> exclusions)
        {
            lock (_scanLock)
            {
                var newCatalog = new List<IndexedItem>();
                int currentRule = 0; 

                // 1. Ausschlüsse für extrem schnellen Abgleich vorbereiten
                var globalExcludedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var compiledPathExclusions = new List<(string Path, bool Recursive, HashSet<string> Suffixes)>();

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
                            // Pfad-basierter Ausschluss
                            string expandedPath = Environment.ExpandEnvironmentVariables(ex.Path).TrimEnd('\\', '/');
                            var suffixes = new HashSet<string>(ex.Suffixes.Select(s => s.TrimStart('*').ToLower()), StringComparer.OrdinalIgnoreCase);
                            compiledPathExclusions.Add((expandedPath, ex.Recursive, suffixes));
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

        private void ScanDirectory(string currentPath, CatalogDirectory config, int currentDepth, List<IndexedItem> results, ref int fileCount, ref int folderCount, HashSet<string> allowedExts, int ruleIndex, HashSet<string> globalExcludedExts, List<(string Path, bool Recursive, HashSet<string> Suffixes)> compiledPathExclusions)
        {
            if (currentDepth > config.Depth) return;

            try
            {
                // PRÜFUNG 1: Ist dieser gesamte Ordner ausgeschlossen?
                bool isDirExcluded = false;
                foreach (var ex in compiledPathExclusions)
                {
                    if (ex.Suffixes.Count == 0) // Nur wenn Suffix leer ist, wird der GANZE Ordner blockiert
                    {
                        if (currentPath.Equals(ex.Path, StringComparison.OrdinalIgnoreCase)) { isDirExcluded = true; break; }
                        if (ex.Recursive && currentPath.StartsWith(ex.Path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) { isDirExcluded = true; break; }
                    }
                }
                
                // Wenn der Ordner blockiert ist, brechen wir hier komplett ab (spart extrem viel Zeit!)
                if (isDirExcluded) return; 

                // DATEIEN SCANNEN
                if (allowedExts.Count > 0)
                {
                    foreach (var file in Directory.GetFiles(currentPath))
                    {
                        string ext = Path.GetExtension(file).ToLower();

                        // Globale Suffix-Ausschlüsse prüfen
                        if (globalExcludedExts.Contains(ext) || globalExcludedExts.Contains(".*")) continue;

                        // Pfad-spezifische Suffix-Ausschlüsse prüfen
                        bool isFileExcluded = false;
                        foreach (var ex in compiledPathExclusions)
                        {
                            if (ex.Suffixes.Count > 0 && ex.Suffixes.Contains(ext))
                            {
                                if (currentPath.Equals(ex.Path, StringComparison.OrdinalIgnoreCase) || 
                                   (ex.Recursive && currentPath.StartsWith(ex.Path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                                {
                                    isFileExcluded = true;
                                    break;
                                }
                            }
                        }
                        if (isFileExcluded) continue;

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
                    if (config.IncludeDirectories)
                    {
                        // Check: Ist der Ordner als Ergebnis selbst ausgeschlossen?
                        bool isSubDirExcluded = false;
                        foreach (var ex in compiledPathExclusions)
                        {
                            if (ex.Suffixes.Count == 0 && (subDir.Equals(ex.Path, StringComparison.OrdinalIgnoreCase) || (ex.Recursive && subDir.StartsWith(ex.Path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))))
                            {
                                isSubDirExcluded = true; break;
                            }
                        }

                        if (!isSubDirExcluded)
                        {
                            results.Add(new IndexedItem { Path = subDir, RuleIndex = ruleIndex });
                            folderCount++;
                        }
                    }

                    ScanDirectory(subDir, config, currentDepth + 1, results, ref fileCount, ref folderCount, allowedExts, ruleIndex, globalExcludedExts, compiledPathExclusions);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception) { }
        }
    }
}