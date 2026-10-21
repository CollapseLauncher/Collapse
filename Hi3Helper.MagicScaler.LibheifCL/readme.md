PhotoSauce.NativeCodecs.Libheif
===============================

This MagicScaler plugin wraps the [libheif](https://github.com/strukturag/libheif) codec for [HEIC](https://en.wikipedia.org/wiki/High_Efficiency_Image_File_Format) images, such as those created by Apple devices running iOS 11 or later, and [AVIF](https://en.wikipedia.org/wiki/AVIF) images.  This plugin supports only decode, using the [libde265](https://github.com/strukturag/libde265) HEVC decoder and the [dav1d](https://code.videolan.org/videolan/dav1d) AV1 decoder.

Be aware that the HEVC video coding standard used in HEIC images is burdened by [numerous patents](https://en.wikipedia.org/wiki/High_Efficiency_Video_Coding#Patent_licensing) which may vary in validity by country.  

*By using this plugin, you acknowledge that your use of the codec is permitted royalty free or that you have purchased an appropriate license.*

Windows 10 and 11 include a HEIF codec by default, but it may not function for HEIC or AVIF images without purchase/installation of a video codec extension from the Microsoft Store, and it may not function for all users or apps.  This plugin is meant to be used when the Windows codec is not available or does not work.

Requirements
------------

A compatible `libheif` binary must be present for this plugin to function.  For convenience, the NuGet package includes native binaries for Windows (x86, x64, and ARM64) and Linux (glibc x64 and ARM64).

Usage
-----

### Codec Registration

To register the codec, call the `UseLibheif` extension method from your `CodecManager.Configure` action at app startup.  By default, the plugin will remove/replace the Windows HEIF codec if it is present.

```C#
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libheif;

CodecManager.Configure(codecs => {
    codecs.UseLibheif();
});
```

### Using the Codec

Once registered, the codec will automatically detect and decode compatible images.
