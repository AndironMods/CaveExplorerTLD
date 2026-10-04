using ModSettings;

namespace CaveExplorer
{
    public class CaveExplorerSettings : JsonModSettings
    {
        // stored in Mods\CaveExplorerSettings.json (default would be CaveExplorer.json, named after the assembly)
        public CaveExplorerSettings() : base("CaveExplorerSettings")
        {
        }

        [Name("Enabled")]
        [Description("Record your path in caves and mines and show it as a map")]
        public bool Enabled = true;

        [Name("Sample Interval")]
        [Description("Seconds between two position checks")]
        [Slider(0.1f, 2f, 20)]
        public float SampleSeconds = 0.5f;

        [Name("Path Radius")]
        [Description("Radius in meters around you that is shown as explored on the map. A new point is added after moving this far")]
        [Slider(0.5f, 5f, 10)]
        public float PathRadius = 2f;
    }
}
