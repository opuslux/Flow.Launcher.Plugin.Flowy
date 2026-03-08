using System.Collections.ObjectModel;

namespace Flow.Launcher.Plugin.Flowy
{
    public class Settings
    {
        public ObservableCollection<CatalogDirectory> Directories { get; set; } = new ObservableCollection<CatalogDirectory>();
        public int RescanIntervalMinutes { get; set; } = 30;
    }
}