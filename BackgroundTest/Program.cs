using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libheif;
using PhotoSauce.NativeCodecs.Libjxl;
using PhotoSauce.NativeCodecs.Libwebp;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BackgroundTest;

public class MainEntryPoint
{
    private static App? _currentApp;

    [STAThread]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Main(params string[] args)
    {
        // Initialize support for MagicScaler's WebP decoding
        CodecManager.Configure(InitializeMagicScalerCodecs);
        Application.Start(InitializeApp);
    }

    private static void InitializeMagicScalerCodecs(CodecCollection codecs)
    {
        codecs.UseLibwebp();
        codecs.UseLibheif();
        codecs.UseLibjxl();
        codecs.UseWicCodecs(WicCodecPolicy.All);
    }

    private static void InitializeApp(ApplicationInitializationCallbackParams pContext)
    {
        DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        DispatcherQueueSynchronizationContext context = new(dispatcherQueue);
        SynchronizationContext.SetSynchronizationContext(context);

        // ReSharper disable once ObjectCreationAsStatement
        _currentApp ??= new App
        {
            HighContrastAdjustment = ApplicationHighContrastAdjustment.None
        };
    }
}