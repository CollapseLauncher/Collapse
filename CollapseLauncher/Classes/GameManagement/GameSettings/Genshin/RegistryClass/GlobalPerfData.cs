using CollapseLauncher.GameSettings.Genshin.Context;
using System.Collections.Generic;
using System.Text.Json;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.Genshin
{
    internal class PerfDataItem
    {
        #region Properties
        public int entryType { get; set; }
        public int index { get; set; }
        public string itemVersion { get; set; }
        #endregion

        #region Methods
        public PerfDataItem(int entryType, int index, string itemVersion)
        {
            this.entryType = entryType;
            this.index = index;
            this.itemVersion = itemVersion;
        }
        #endregion
    }

    internal class GenshinKeyValuePair
    {
        #region Properties
        public int key { get; set; }
        public int value { get; set; }
        #endregion

        #region Methods
        public GenshinKeyValuePair(int Key, int Value)
        {
            key = Key;
            value = Value;
        }
        #endregion
    }

    internal class GlobalPerfData
    {
        #region Properties
        public List<PerfDataItem> saveItems { get; set; } = new();
        public bool truePortedFromGraphicData { get; set; } = true;
        public string portedVersion { get; set; } = "OSRELWin3.8.0";
        public bool portedFromGraphicsData { get; set; } = false;
        #endregion

        #region Methods
        public string Create(GraphicsData graphics, string version)
        {
            saveItems = new()
            {
                new PerfDataItem (1, (int)graphics.FPS - 1, version),
                new PerfDataItem (2, (int)graphics.RenderResolution - 1, version),
                new PerfDataItem (3,(int) graphics.ShadowQuality - 1, version),
                new PerfDataItem (4,(int) graphics.VisualEffects - 1, version),
                new PerfDataItem (5,(int) graphics.SFXQuality - 1, version),
                new PerfDataItem (6,(int) graphics.EnvironmentDetail - 1, version),
                new PerfDataItem (7,(int) graphics.VerticalSync - 1, version),
                new PerfDataItem (8,(int) graphics.Antialiasing - 1, version),
                new PerfDataItem (9,(int) graphics.VolumetricFog - 1, version),
                new PerfDataItem (10,(int) graphics.Reflections - 1, version),
                new PerfDataItem (11,(int) graphics.MotionBlur - 1, version),
                new PerfDataItem (12,(int) graphics.Bloom - 1, version),
                new PerfDataItem (13,(int) graphics.CrowdDensity - 1, version),
                new PerfDataItem (16,(int) graphics.CoOpTeammateEffects - 1, version),
                new PerfDataItem (15,(int) graphics.SubsurfaceScattering - 1, version),
                new PerfDataItem (17,(int) graphics.AnisotropicFiltering - 1, version),
                new PerfDataItem (19,(int) graphics.GlobalIllumination - 1, version)
            };
            string data = JsonSerializer.Serialize(this, typeof(GlobalPerfData), GenshinSettingsJSONContext.Default);
#if DEBUG
            LogWriteLine($"Saved Genshin GlobalPerfData\r\n{data}", Hi3Helper.LogType.Debug, true);
#endif
            return data;
        }
        #endregion
    }
}
