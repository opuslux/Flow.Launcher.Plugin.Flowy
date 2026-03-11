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

        public void BuildIndex(IEnumerable<CatalogDirectory> directories)
        {
            lock (_scanLock)
            {
                var newCatalog = new List<IndexedItem>();
                int currentRule = 1;

                foreach (var dirConfig in directories)
                {
                    dirConfig.Index = currentRule;
                    string expandedPath = Environment.ExpandEnvironmentVariables(dirConfig.Path);

                    if (Directory.Exists(expandedPath))
                    {
                        int fileCount = 0;
                        int folderCount = 0;

                        // Wir filtern leere Einträge heraus. 
                        // Wenn der User nichts eingibt, bleibt die Liste leer.
                        var allowedExtensions = new HashSet<string>(
                            dirConfig.FileTypes
                                .Where(t => !string.IsNullOrWhiteSpace(t))
                                .Select(t => t.TrimStart('*').ToLower())
                        );

                        ScanDirectory(expandedPath, dirConfig, 0, newCatalog, ref fileCount, ref folderCount, allowedExtensions, currentRule);

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

        private void ScanDirectory(string currentPath, CatalogDirectory config, int currentDepth, List<IndexedItem> results, ref int fileCount, ref int folderCount, HashSet<string> allowedExts, int ruleIndex)
        {
            // Depth = 0 bedeutet: Nur den aktuellen Ordner scannen, keine Unterordner.
            if (currentDepth > config.Depth) return;

            try
            {
                // 1. DATEIEN SCANNEN
                // Nur wenn allowedExts nicht leer ist, scannen wir überhaupt Dateien.
                // Das spart Zeit und erfüllt den Wunsch: "Leer = nichts scannen"
                if (allowedExts.Count > 0)
                {
                    foreach (var file in Directory.GetFiles(currentPath))
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        
                        // Check auf *.* , * oder die spezifische Extension
                        if (allowedExts.Contains(".*") || allowedExts.Contains("") || allowedExts.Contains(ext))
                        {
                            results.Add(new IndexedItem { Path = file, RuleIndex = ruleIndex });
                            fileCount++;
                        }
                    }
                }

                // 2. ORDNER SCANNEN & REKURSION
                foreach (var subDir in Directory.GetDirectories(currentPath))
                {
                    if (config.IncludeDirectories)
                    {
                        results.Add(new IndexedItem { Path = subDir, RuleIndex = ruleIndex });
                        folderCount++;
                    }

                    // Auch wenn wir keine Ordner im Index wollen, müssen wir für die 
                    // Rekursion tiefer gehen, sofern die eingestellte Tiefe noch nicht erreicht ist.
                    ScanDirectory(subDir, config, currentDepth + 1, results, ref fileCount, ref folderCount, allowedExts, ruleIndex);
                }
            }
            catch (UnauthorizedAccessException) { /* Systemordner einfach überspringen */ }
            catch (Exception) { /* Sicherheitshalber alles andere auch abfangen */ }
        }
    }
}