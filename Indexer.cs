using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.Flowy
{
    // NEU: Container für Pfad + Info zur Regel
    public class IndexedItem
    {
        public string Path { get; set; }
        public int RuleIndex { get; set; }
    }

    public class Indexer
    {
        private readonly object _scanLock = new object();

        // GEÄNDERT: Liste von Objekten statt Strings
        public List<IndexedItem> Catalog { get; set; } = new List<IndexedItem>();

        public void BuildIndex(IEnumerable<CatalogDirectory> directories)
        {
            lock (_scanLock)
            {
                var newCatalog = new List<IndexedItem>();
                int currentRule = 1;

                foreach (var dirConfig in directories)
                {
                    // NEU: Wir setzen die Nummer für die UI-Anzeige
                    dirConfig.Index = currentRule;

                    string expandedPath = Environment.ExpandEnvironmentVariables(dirConfig.Path);

                    if (Directory.Exists(expandedPath))
                    {
                        int fileCount = 0;
                        int folderCount = 0;

                        var allowedExtensions = new HashSet<string>(
                            dirConfig.FileTypes.Select(t => t.TrimStart('*').ToLower())
                        );

                        // Wir geben die aktuelle Regel-Nummer an den Scan weiter
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
            if (currentDepth > config.Depth) return;

            try
            {
                foreach (var file in Directory.GetFiles(currentPath))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (allowedExts.Contains(".*") || allowedExts.Contains("*") || allowedExts.Contains(ext))
                    {
                        // GEÄNDERT: Wir speichern Pfad UND Regel-Nummer
                        results.Add(new IndexedItem { Path = file, RuleIndex = ruleIndex });
                        fileCount++;
                    }
                }

                foreach (var subDir in Directory.GetDirectories(currentPath))
                {
                    if (config.IncludeDirectories)
                    {
                        results.Add(new IndexedItem { Path = subDir, RuleIndex = ruleIndex });
                        folderCount++;
                    }
                    ScanDirectory(subDir, config, currentDepth + 1, results, ref fileCount, ref folderCount, allowedExts, ruleIndex);
                }
            }
            catch (Exception) { }
        }
    }
}