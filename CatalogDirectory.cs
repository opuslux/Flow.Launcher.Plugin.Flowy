using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.Flowy
{
    // INotifyPropertyChanged sorgt dafür, dass die Tabelle sich live updatet
    public class CatalogDirectory : INotifyPropertyChanged
    {
        private string _path = "";
        private int _depth = 2;
        private List<string> _fileTypes = new List<string> { "*.lnk" };
        private bool _includeDirectories = false;
        private int _fileCount = 0;
        private int _folderCount = 0;

        public string Path { get => _path; set { _path = value; OnPropertyChanged(); } }
        public int Depth { get => _depth; set { _depth = value; OnPropertyChanged(); } }
        public List<string> FileTypes { get => _fileTypes; set { _fileTypes = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileTypesDisplay)); } }
        public bool IncludeDirectories { get => _includeDirectories; set { _includeDirectories = value; OnPropertyChanged(); } }

        // NEU: Die Statistiken (JsonIgnore verhindert, dass sie in der settings.json landen)
        [JsonIgnore]
        public int FileCount { get => _fileCount; set { _fileCount = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public int FolderCount { get => _folderCount; set { _folderCount = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public string FileTypesDisplay
        {
            get => string.Join("; ", FileTypes);
            set => FileTypes = value.Split(';')
                                    .Select(s => s.Trim())
                                    .Where(s => !string.IsNullOrEmpty(s))
                                    .ToList();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}