using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.Plugin.Core.Management;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using static CollapseLauncher.Helper.LauncherApiLoader.Statics.HttpClientWrapper;

// ReSharper disable IdentifierTypo
// ReSharper disable once CheckNamespace
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

namespace CollapseLauncher.Helper.LauncherApiLoader.Steam
{
    internal sealed partial class SteamApiLoader : LauncherApiBase 
    {
        #region  Constructor
        private SteamApiLoader(PresetConfig presetConfig, string gameName, string gameRegion, int steamGameId)
            : base(presetConfig, gameName, gameRegion, GeneralHttpClientFactory, ResourceHttpClientFactory)
        {
            ArgumentNullException.ThrowIfNull(presetConfig);
        }
        #endregion
        
        #region Loaders

        protected override async Task LoadAsyncInner(ActionOnTimeOutRetry? onTimeOutRoutine,
                                                     CancellationToken     token)
        {
            // TODO: Add all tasks
            await Task.WhenAll();
        }
        #endregion
    }
}