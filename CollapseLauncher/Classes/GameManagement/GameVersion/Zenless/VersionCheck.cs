using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Microsoft.UI.Xaml;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CollapseLauncher.GameVersioning
{
    internal sealed class GameTypeZenlessVersion : GameVersionBase
    {
        #region Properties
        private RSA SleepyInstance { get; set; }
        #endregion

        #region Initialize Sleepy
        private void InitializeSleepy(PresetConfig gamePreset)
        {
            // Go YEET
            SleepyInstance = RSA.Create();
            goto StartCheck;

        // Go to the DOOM
        QuitFail:
            DisableRepairAndCacheInstance(gamePreset);
            return;

        StartCheck:
            // Check if the thing does not have thing, then DOOMED
            if (gamePreset.DispatcherKey == null)
                goto QuitFail;

            // We cannot pay the house so rent.
            byte[] keyUtf8Base64 = ArrayPool<byte>.Shared.Rent(gamePreset.DispatcherKey.Length * 2);

            try
            {
                // Check if the data is an impostor, then eject (basically DOOMED)
                if (!Encoding.UTF8.TryGetBytes(gamePreset.DispatcherKey, keyUtf8Base64, out int keyWrittenLen))
                    goto QuitFail;

                // Also if the data is not a crew, then YEET.
                OperationStatus base64DecodeStatus = Base64.DecodeFromUtf8InPlace(keyUtf8Base64.AsSpan(0, keyWrittenLen), out int keyFromBase64Len);
                if (OperationStatus.Done != base64DecodeStatus)
                {
                    Logger.LogWriteLine($"OOF, we cannot go to sleep as the bed is collapsing! :( Operation Status: {base64DecodeStatus}", LogType.Error, true);
                    goto QuitFail;
                }

                // Try serve a dinner and if it fails, then GET OUT!
                bool isServed = DataCooker.IsServeV3Data(keyUtf8Base64);
                if (!isServed)
                    goto QuitFail;

                // Enjoy the meal (i guess?)
                DataCooker.GetServeV3DataSize(keyUtf8Base64, out long servedCompressedSize, out long servedDecompressedSize);
                Span<byte> outServeData = keyUtf8Base64.AsSpan(keyFromBase64Len, (int)servedDecompressedSize);
                DataCooker.ServeV3Data(keyUtf8Base64.AsSpan(0, keyFromBase64Len), outServeData, (int)servedCompressedSize, (int)servedDecompressedSize, out int dataWritten);
                
                // Load the load
                SleepyInstance.ImportRSAPrivateKey(outServeData.Slice(0, dataWritten), out int bytesRead);

                // If you're food poisoned, then go to the hospital
                if (0 == bytesRead)
                    goto QuitFail;

                // Uh, what else? nothing to do? then go to sleep :amimir:
            }
            finally
            {
                // After you wake up, get out from the rent and pay for it.
                ArrayPool<byte>.Shared.Return(keyUtf8Base64, true);
            }

            return;

            // Close the door
            void DisableRepairAndCacheInstance(PresetConfig config)
            {
                config.IsRepairEnabled = false;
                config.IsCacheUpdateEnabled = false;
            }
        }
        #endregion

        public GameTypeZenlessVersion(UIElement parentUIElement, RegionResourceProp gameRegionProp, PresetConfig gamePreset, string gameName, string gameRegion)
            : base(parentUIElement, gameRegionProp, gameName, gameRegion)
        {
            // Try check for reinitializing game version.
            TryReinitializeGameVersion();
            InitializeSleepy(gamePreset);
        }

        public override bool IsGameHasDeltaPatch() => false;

        public override DeltaPatchProperty GetDeltaPatchInfo() => null;
    }
}