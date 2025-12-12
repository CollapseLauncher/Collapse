using System;
using System.IO;
using System.Threading.Tasks;

namespace BackgroundTest.CustomControl.LayeredBackgroundImage;

/// <summary>
/// An interface which implements media cache handler for loading cached media sources.
/// </summary>
public interface IMediaCacheHandler
{
    /// <summary>
    /// Loads the media source from cached local path or URL or Seekable and Readable <see cref="Stream"/>.
    /// </summary>
    /// <param name="sourceObject">The input source to be cached.</param>
    /// <returns>An instance of <see cref="MediaCacheResult"/> which defines the cached source and its properties.</returns>
    Task<MediaCacheResult> LoadCachedSource(object? sourceObject);
}

/// <summary>
/// Contains the result and properties of the cached media source.
/// </summary>
public class MediaCacheResult
{
    /// <summary>
    /// Indicates whether to force using the WIC (Windows Imaging Component) decoder for image source or not.
    /// </summary>
    public bool ForceUseInternalDecoder { get; set; }

    /// <summary>
    /// The source of the media. Only <see cref="string"/> or <see cref="Uri"/> for local path and URL, or Seekable and Readable <see cref="Stream"/> types are supported.
    /// </summary>
    public object? CachedSource { get; set; }

    /// <summary>
    /// Whether to perform Dispose if <see cref="CachedSource"/> is a <see cref="Stream"/> type.
    /// </summary>
    public bool DisposeStream { get; set; }
}
