﻿using CollapseLauncher.Interfaces;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Proto.StarRail;
using Microsoft.UI.Xaml;
using System.Text;

namespace CollapseLauncher.GameVersioning
{
    internal class GameTypeStarRailVersion : GameVersionBase, IGameVersionCheck
    {
        #region Properties
        public SRMetadata StarRailMetadataTool { get; set; }
        #endregion

        public GameTypeStarRailVersion(UIElement parentUIElement, RegionResourceProp gameRegionProp, string gameName, string gameRegion)
            : base(parentUIElement, gameRegionProp, gameName, gameRegion)
        {
            // Try check for reinitializing game version.
            TryReinitializeGameVersion();

            // Initialize Star Rail metadata tool
            if (GamePreset.ProtoDispatchKey != null)
            {
                StarRailMetadataTool = new SRMetadata(
                    GamePreset.GameDispatchArrayURL[0],
                    GamePreset.ProtoDispatchKey,
                    GamePreset.GameDispatchURLTemplate,
                    GamePreset.GameGatewayURLTemplate,
                    GamePreset.GameDispatchChannelName,
                    GameVersionAPI.VersionString);
            }

            // Initialize Proto ID for static StarRailGateway
            InitializeProtoId();
        }

        ~GameTypeStarRailVersion() => StarRailMetadataTool?.Dispose();

        public override bool IsGameHasDeltaPatch() => GameDeltaPatchProp != null;

        public override DeltaPatchProperty GetDeltaPatchInfo() => GameDeltaPatchProp == null ? null : GameDeltaPatchProp;

#nullable enable
        private void InitializeProtoId()
        {
            if (base.GamePreset.GameDataTemplates != null && base.GamePreset.GameDataTemplates.Count != 0)
            {
                byte[]? data = base.GamePreset.GetGameDataTemplate("MagicSpell", new byte[] { 2, 1, 0, 0 });
                if (data == null) return;

                string jsonResponse = Encoding.UTF8.GetString(data);
                StarRailDispatchGatewayProps.Deserialize(jsonResponse);
            }
        }
    }
}
