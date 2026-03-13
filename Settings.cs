using System.Collections.ObjectModel;

namespace Flow.Launcher.Plugin.Flowy
{
    public class Settings
    {
        public ObservableCollection<CatalogDirectory> Directories { get; set; } = new ObservableCollection<CatalogDirectory>();
        
        // NEU: Die Liste für die Ausschlüsse
        public ObservableCollection<ExclusionRule> Exclusions { get; set; } = new ObservableCollection<ExclusionRule>();
        
        public int RescanIntervalMinutes { get; set; } = 30;
        public int MaxResults { get; set; } = 1000;
        public bool ShowRuleNumber { get; set; } = true;
    }
}