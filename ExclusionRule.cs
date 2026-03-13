using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.Flowy
{
    public class ExclusionRule : INotifyPropertyChanged
    {
        private string _path = "";
        private bool _recursive = true;
        private List<string> _suffixes = new List<string>();
        private string _suffixesRaw = null;

        public string Path { get => _path; set { _path = value; OnPropertyChanged(); } }
        public bool Recursive { get => _recursive; set { _recursive = value; OnPropertyChanged(); } }

        public List<string> Suffixes
        {
            get => _suffixes;
            set
            {
                _suffixes = value;
                _suffixesRaw = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SuffixesDisplay));
            }
        }

        [JsonIgnore]
        public string SuffixesDisplay
        {
            get => _suffixesRaw ?? string.Join("; ", Suffixes);
            set
            {
                _suffixesRaw = value;
                // Logik angepasst: Splittet bei Semikolon und Komma, entfernt leere Einträge und trimmt Leerzeichen
                _suffixes = value.Split(new[] { ';', ',' }, System.StringSplitOptions.RemoveEmptyEntries)
                                 .Select(s => s.Trim())
                                 .Where(s => !string.IsNullOrEmpty(s))
                                 .ToList();

                OnPropertyChanged();
                OnPropertyChanged(nameof(Suffixes));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}