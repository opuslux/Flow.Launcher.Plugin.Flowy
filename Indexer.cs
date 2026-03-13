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
                
                // HIER IST DER FIX: Wir starten intern bei 0, weil die UI +1 rechnet!
                int currentRule = 0; 

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

                        // Wir übergeben currentRule + 1, damit die Suchergebnisse weiterhin mit #1, #2 anfangen
                        ScanDirectory(expandedPath, dirConfig, 0, newCatalog, ref fileCount, ref folderCount, allowedExtensions, currentRule + 1);

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
                if (allowedExts.Count > 0)
                {
                    foreach (var file in Directory.GetFiles(currentPath))
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        
                        if (allowedExts.Contains(".*") || allowedExts.Contains("") || allowedExts.Contains(ext))
                        {
                            results.Add(new IndexedItem { Path = file, RuleIndex = ruleIndex });
                            fileCount++;
                        }
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
            catch (UnauthorizedAccessException) { /* Systemordner einfach überspringen */ }
            catch (Exception) { /* Sicherheitshalber alles andere auch abfangen */ }
        }
    }
}