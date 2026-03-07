using System.Collections.ObjectModel;

namespace Flow.Launcher.Plugin.Flowy
{
    public class Settings
    {
        public ObservableCollection<CatalogDirectory> Directories { get; set; } = new ObservableCollection<CatalogDirectory>();

        // NEU: Standardmäßig alle 30 Minuten scannen
        public int RescanIntervalMinutes { get; set; } = 30;
    }
}