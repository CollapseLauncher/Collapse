@echo off
echo 	Clearing Collapse cache
rmdir /S /Q CollapseLauncher\bin && rmdir /S /Q CollapseLauncher\obj
echo 	Clearing ColorThief cache
rmdir /S /Q ColorThief\ColorThief\bin && rmdir /S /Q ColorThief\ColorThief\obj
echo 	Clearing Core cache
rmdir /S /Q Hi3Helper.Core\bin && rmdir /S /Q Hi3Helper.Core\obj
echo 	Clearing EncTool cache
rmdir /S /Q Hi3Helper.EncTool\bin && rmdir /S /Q Hi3Helper.EncTool\obj
echo 	Clearing EncTool tester cache
rmdir /S /Q Hi3Helper.EncTool.Test\bin && rmdir /S /Q Hi3Helper.EncTool.Test\obj
echo 	Clearing Http cache
rmdir /S /Q Hi3Helper.Http\bin && rmdir /S /Q Hi3Helper.Http\obj
echo 	Clearing Http tester cache
rmdir /S /Q Hi3Helper.Http\Test\bin && rmdir /S /Q Hi3Helper.Http\Test\obj
echo 	Clearing HDiff cache
rmdir /S /Q Hi3Helper.SharpHDiffPatch\Hi3Helper.SharpHDiffPatch\bin && rmdir /S /Q Hi3Helper.SharpHDiffPatch\Hi3Helper.SharpHDiffPatch\obj
echo 	Clearing 2nd HDiff cache
rmdir /S /Q Hi3Helper.SharpHDiffPatch\SharpHDiffPatch\bin && rmdir /S /Q Hi3Helper.SharpHDiffPatch\SharpHDiffPatch\obj
echo 	Clearing 7z cache
rmdir /S /Q Hi3Helper.Core\Classes\Data\Tools\SevenZipTool\SevenZipExtractor\SevenZipExtractor\bin && rmdir /S /Q Hi3Helper.Core\Classes\Data\Tools\SevenZipTool\SevenZipExtractor\SevenZipExtractor\obj