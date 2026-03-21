@echo off
echo     Clearing Collapse cache
rmdir /S /Q CollapseLauncher\bin && rmdir /S /Q CollapseLauncher\obj
echo     Clearing ColorThief cache
rmdir /S /Q ColorThief\ColorThief\bin && rmdir /S /Q ColorThief\ColorThief\obj
echo     Clearing CommunityToolkit.ImageCropper cache
rmdir /S /Q Hi3Helper.CommunityToolkit\ImageCropper\bin && rmdir /S /Q Hi3Helper.CommunityToolkit\ImageCropper\obj
echo     Clearing CommunityToolkit.SettingsControls cache
rmdir /S /Q Hi3Helper.CommunityToolkit\SettingsControls\bin && rmdir /S /Q Hi3Helper.CommunityToolkit\SettingsControls\obj
echo     Clearing Core cache
rmdir /S /Q Hi3Helper.Core\bin && rmdir /S /Q Hi3Helper.Core\obj
echo     Clearing EncTool cache
rmdir /S /Q Hi3Helper.EncTool\bin && rmdir /S /Q Hi3Helper.EncTool\obj
echo     Clearing Http cache
rmdir /S /Q Hi3Helper.Http\bin && rmdir /S /Q Hi3Helper.Http\obj
echo     Clearing TaskScheduler cache
rmdir /S /Q Hi3Helper.TaskScheduler\bin && rmdir /S /Q Hi3Helper.TaskScheduler\obj
echo     Clearing InnoSetupHelper cache
rmdir /S /Q InnoSetupHelper\bin && rmdir /S /Q InnoSetupHelper\obj
echo     Clearing ImageEx cache
rmdir /S /Q ImageEx\ImageEx\bin && rmdir /S /Q ImageEx\ImageEx\obj
echo     Clearing 7z cache
rmdir /S /Q SevenZipExtractor\SevenZipExtractor\bin && rmdir /S /Q SevenZipExtractor\SevenZipExtractor\obj
echo     Clearing SharpDiscordRPC cache
rmdir /S /Q Hi3Helper.SharpDiscordRPC\DiscordRPC\bin && rmdir /S /Q Hi3Helper.SharpDiscordRPC\DiscordRPC\obj
echo     Clearing Hi3Helper.SourceGen cache
rmdir /S /Q Hi3Helper.SourceGen\bin && rmdir /S /Q Hi3Helper.SourceGen\obj
echo     Clearing Hi3Helper.LocaleSourceGen cache
rmdir /S /Q Hi3Helper.LocaleSourceGen\bin && rmdir /S /Q Hi3Helper.LocaleSourceGen\obj
echo     Clearing Hi3Helper.Plugin.Core cache
rmdir /S /Q Hi3Helper.Plugin.Core\bin && rmdir /S /Q Hi3Helper.Plugin.Core\obj