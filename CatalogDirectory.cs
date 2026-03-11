using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.Flowy
{
    public class CatalogDirectory : INotifyPropertyChanged
    {
        private string _path = "";
        private int _depth = 0; // Standardmäßig 0 erlauben
        private List<string> _fileTypes = new List<string> { "*.lnk" };
        private bool _includeDirectories = false;
        private int _fileCount = 0;
        private int _folderCount = 0;
        private string _comment = "";
        private int _index;

        // NEU: Zwischenspeicher, damit das UI beim Tippen nicht gestört wird
        private string _fileTypesRaw = null;

        public string Path { get => _path; set { _path = value; OnPropertyChanged(); } }
        public int Depth
        {
            get => _depth;
            set
            {
                // Erlaube Werte ab 0. Blockiere nur negative Zahlen.
                if (value >= 0)
                {
                    _depth = value;
                    OnPropertyChanged();
                }
            }
        }

        public List<string> FileTypes
        {
            get => _fileTypes;
            set
            {
                _fileTypes = value;
                _fileTypesRaw = null; // Reset, wenn Daten z.B. aus der JSON geladen werden
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileTypesDisplay));
            }
        }

        public bool IncludeDirectories { get => _includeDirectories; set { _includeDirectories = value; OnPropertyChanged(); } }
        public string Comment { get => _comment; set { _comment = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public int FileCount { get => _fileCount; set { _fileCount = value; OnPropertyChanged(); } }

        [JsonIgnore] // Die Nummer ist dynamisch und muss nicht gespeichert werden
        public int Index { get => _index; set { _index = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public int FolderCount { get => _folderCount; set { _folderCount = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public string FileTypesDisplay
        {
            // Zeigt den rohen Text an (falls gerade getippt wird), ansonsten den formatierten
            get => _fileTypesRaw ?? string.Join("; ", FileTypes);
            set
            {
                _fileTypesRaw = value;
                _fileTypes = value.Split(new[] { ';', ',' })
                                 .Select(s => s.Trim())
                                 .Where(s => !string.IsNullOrEmpty(s))
                                 .ToList();

                OnPropertyChanged();
                OnPropertyChanged(nameof(FileTypes)); // Informiert das System über die neue Liste
            }
        }

        [JsonIgnore]
        public string DepthDisplay
        {
            get => Depth.ToString();
            set
            {
                if (int.TryParse(value, out int result))
                {
                    Depth = result; // Setzt die 0 im int-Feld
                }
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}