using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.Flowy
{
    public class Indexer
    {
        // Ein interner Lock, damit niemals zwei Scans gleichzeitig laufen
        private readonly object _scanLock = new object();

        public List<string> Catalog { get; set; } = new List<string>();

        public void BuildIndex(IEnumerable<CatalogDirectory> directories)
        {
            // Verhindert, dass zwei Hintergrund-Tasks gleichzeitig scannen
            lock (_scanLock)
            {
                var newCatalog = new List<string>();

                foreach (var dirConfig in directories)
                {
                    string expandedPath = Environment.ExpandEnvironmentVariables(dirConfig.Path);

                    if (Directory.Exists(expandedPath))
                    {
                        int fileCount = 0;
                        int folderCount = 0;

                        // Wir erstellen ein Set für schnellere Dateityp-Prüfung
                        var allowedExtensions = new HashSet<string>(
                            dirConfig.FileTypes.Select(t => t.TrimStart('*').ToLower())
                        );

                        ScanDirectory(expandedPath, dirConfig, 0, newCatalog, ref fileCount, ref folderCount, allowedExtensions);

                        // Erst am Ende des Verzeichnis-Scans die Werte setzen
                        dirConfig.FileCount = fileCount;
                        dirConfig.FolderCount = folderCount;
                    }
                    else
                    {
                        dirConfig.FileCount = 0;
                        dirConfig.FolderCount = 0;
                    }
                }

                // Referenz-Swap: Der Katalog ist ab jetzt sofort für die Query verfügbar
                Catalog = newCatalog;
            }
        }

        private void ScanDirectory(string currentPath, CatalogDirectory config, int currentDepth, List<string> results, ref int fileCount, ref int folderCount, HashSet<string> allowedExts)
        {
            if (currentDepth > config.Depth) return;

            try
            {
                // Dateien verarbeiten
                foreach (var file in Directory.GetFiles(currentPath))
                {
                    string ext = Path.GetExtension(file).ToLower();

                    // Optimierte Prüfung: Ist die Endung im Set oder ist Wildcard erlaubt?
                    if (allowedExts.Contains(".*") || allowedExts.Contains("*") || allowedExts.Contains(ext))
                    {
                        results.Add(file);
                        fileCount++;
                    }
                }

                // Ordner verarbeiten
                foreach (var subDir in Directory.GetDirectories(currentPath))
                {
                    if (config.IncludeDirectories)
                    {
                        results.Add(subDir);
                        folderCount++;
                    }
                    // Rekursion
                    ScanDirectory(subDir, config, currentDepth + 1, results, ref fileCount, ref folderCount, allowedExts);
                }
            }
            catch (Exception)
            {
                // Ob UnauthorizedAccess oder verschwundener USB-Stick: 
                // Wir fangen alles ab, damit der gesamte Scan nicht abbricht.
            }
        }
    }
}