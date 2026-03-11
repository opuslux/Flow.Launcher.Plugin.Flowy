using System.Collections.ObjectModel;

namespace Flow.Launcher.Plugin.Flowy
{
    public class Settings
    {
        public ObservableCollection<CatalogDirectory> Directories { get; set; } = new ObservableCollection<CatalogDirectory>();
        public int RescanIntervalMinutes { get; set; } = 30;

        // NEU: Die gewünschten General Settings
        public int MaxResults { get; set; } = 1000;
        public bool ShowRuleNumber { get; set; } = true;
    }
}