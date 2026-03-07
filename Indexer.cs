using System;
using System.Collections.Generic;
using System.IO;

namespace Flow.Launcher.Plugin.Flowy
{
    public class Indexer
    {
        public List<string> Catalog { get; set; } = new List<string>();

        public void BuildIndex(IEnumerable<CatalogDirectory> directories)
        {
            var newCatalog = new List<string>();
            foreach (var dirConfig in directories)
            {
                // NEU: Übersetzt Windows-Variablen (wie %UserProfile%) in echte Pfade
                string expandedPath = Environment.ExpandEnvironmentVariables(dirConfig.Path);

                // WICHTIG: Ab hier nutzen wir "expandedPath" statt "dirConfig.Path"
                if (Directory.Exists(expandedPath))
                {
                    int fileCount = 0;
                    int folderCount = 0;

                    ScanDirectory(expandedPath, dirConfig, 0, newCatalog, ref fileCount, ref folderCount);

                    dirConfig.FileCount = fileCount;
                    dirConfig.FolderCount = folderCount;
                }
                else
                {
                    dirConfig.FileCount = 0;
                    dirConfig.FolderCount = 0;
                }
            }
            Catalog = newCatalog;
        }

        private void ScanDirectory(string currentPath, CatalogDirectory config, int currentDepth, List<string> results, ref int fileCount, ref int folderCount)
        {
            if (currentDepth > config.Depth) return;

            try
            {
                foreach (var file in Directory.GetFiles(currentPath))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (config.FileTypes.Exists(ft => ft.EndsWith(ext) || ft == "*.*" || ft == "*"))
                    {
                        results.Add(file);
                        fileCount++;
                    }
                }

                foreach (var subDir in Directory.GetDirectories(currentPath))
                {
                    if (config.IncludeDirectories)
                    {
                        results.Add(subDir);
                        folderCount++;
                    }
                    ScanDirectory(subDir, config, currentDepth + 1, results, ref fileCount, ref folderCount);
                }
            }
            catch (UnauthorizedAccessException) { /* Ignorieren */ }
        }
    }
}