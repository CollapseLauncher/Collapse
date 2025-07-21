using Hi3Helper;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Win32.Native.ManagedTools;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable UnusedMember.Global
// ReSharper disable IdentifierTypo

#nullable enable
namespace CollapseLauncher.Extension
{
    public delegate Task<TResult?> ActionTimeoutTaskCallback<TResult>(CancellationToken token);
    public delegate Task ActionTimeoutTaskCallback(CancellationToken token);
    public delegate void ActionOnTimeOutRetry(int retryAttemptCount, int retryAttemptTotal, int timeOutSecond, int timeOutStep);

    internal static class TaskExtensions
    {
        internal const int DefaultTimeoutSec = 10;
        internal const int DefaultRetryAttempt = 5;

        internal static Task
            WaitForRetryAsync(this ActionTimeoutTaskCallback funcCallback,
                              int?                           timeout       = null,
                              int?                           timeoutStep   = null,
                              int?                           retryAttempt  = null,
                              ActionOnTimeOutRetry?          actionOnRetry = null,
                              CancellationToken              fromToken     = default)
            => WaitForRetryAsync(() => funcCallback, timeout, timeoutStep, retryAttempt, actionOnRetry, fromToken);

        internal static async Task
            WaitForRetryAsync(Func<ActionTimeoutTaskCallback> funcCallback,
                              int?                            timeout       = null,
                              int?                            timeoutStep   = null,
                              int?                            retryAttempt  = null,
                              ActionOnTimeOutRetry?           actionOnRetry = null,
                              CancellationToken               fromToken     = default)
        {
            timeout ??= DefaultTimeoutSec;
            timeoutStep ??= 0;
            retryAttempt ??= DefaultRetryAttempt;

            int retryAttemptCurrent = 1;
            Exception? lastException = null;
            while (retryAttemptCurrent < retryAttempt)
            {
                fromToken.ThrowIfCancellationRequested();
                CancellationTokenSource? innerCancellationToken = null;
                CancellationTokenSource? consolidatedToken = null;

                try
                {
                    innerCancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(timeout ?? DefaultTimeoutSec));
                    consolidatedToken = CancellationTokenSource.CreateLinkedTokenSource(innerCancellationToken.Token, fromToken);

                    ActionTimeoutTaskCallback delegateCallback = funcCallback();
                    await delegateCallback(consolidatedToken.Token).ConfigureAwait(false);

                    return;
                }
                catch (OperationCanceledException) when (fromToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    lastException = ex;
                    actionOnRetry?.Invoke(retryAttemptCurrent, (int)retryAttempt, timeout ?? 0, timeoutStep ?? 0);

                    if (ex is TimeoutException)
                    {
                        string msg = $"The operation has timed out! Retrying attempt left: {retryAttemptCurrent}/{retryAttempt}";
                        Logger.LogWriteLine(msg, LogType.Warning, true);
                    }
                    else
                    {
                        string msg = $"The operation has thrown an exception! Retrying attempt left: {retryAttemptCurrent}/{retryAttempt}\r\n{ex}";
                        Logger.LogWriteLine(msg, LogType.Error, true);
                    }

                    retryAttemptCurrent++;
                    timeout += timeoutStep;
                }
                finally
                {
                    innerCancellationToken?.Dispose();
                    consolidatedToken?.Dispose();
                }
            }

            if (lastException is not null
                && !fromToken.IsCancellationRequested)
                throw lastException is TaskCanceledException ?
                    new TimeoutException("The operation has timed out with inner exception!", lastException) :
                    lastException;

            throw new TimeoutException("The operation has timed out!");
        }

        internal static Task<TResult?>
            WaitForRetryAsync<TResult>(this ActionTimeoutTaskCallback<TResult?> funcCallback,
                                       int?                                     timeout       = null,
                                       int?                                     timeoutStep   = null,
                                       int?                                     retryAttempt  = null,
                                       ActionOnTimeOutRetry?                    actionOnRetry = null,
                                       CancellationToken                        fromToken     = default)
            => WaitForRetryAsync(() => funcCallback, timeout, timeoutStep, retryAttempt, actionOnRetry, fromToken);

        internal static async Task<TResult?>
            WaitForRetryAsync<TResult>(Func<ActionTimeoutTaskCallback<TResult?>> funcCallback,
                                       int?                                      timeout       = null,
                                       int?                                      timeoutStep   = null,
                                       int?                                      retryAttempt  = null,
                                       ActionOnTimeOutRetry?                     actionOnRetry = null,
                                       CancellationToken                         fromToken     = default)
        {
            timeout      ??= DefaultTimeoutSec;
            timeoutStep  ??= 0;
            retryAttempt ??= DefaultRetryAttempt;

            int retryAttemptCurrent = 1;
            Exception? lastException = null;
            while (retryAttemptCurrent < retryAttempt)
            {
                fromToken.ThrowIfCancellationRequested();
                CancellationTokenSource? innerCancellationToken = null;
                CancellationTokenSource? consolidatedToken = null;

                try
                {
                    innerCancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(timeout ?? DefaultTimeoutSec));
                    consolidatedToken = CancellationTokenSource.CreateLinkedTokenSource(innerCancellationToken.Token, fromToken);

                    ActionTimeoutTaskCallback<TResult?> delegateCallback = funcCallback();
                    return await delegateCallback(consolidatedToken.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (fromToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    lastException = ex;
                    actionOnRetry?.Invoke(retryAttemptCurrent, (int)retryAttempt, timeout ?? 0, timeoutStep ?? 0);

                    if (ex is TimeoutException)
                    {
                        string msg = $"The operation has timed out! Retrying attempt left: {retryAttemptCurrent}/{retryAttempt}";
                        Logger.LogWriteLine(msg, LogType.Warning, true);
                    }
                    else
                    {
                        string msg = $"The operation has thrown an exception! Retrying attempt left: {retryAttemptCurrent}/{retryAttempt}\r\n{ex}";
                        Logger.LogWriteLine(msg, LogType.Error, true);
                    }

                    retryAttemptCurrent++;
                    timeout += timeoutStep;
                }
                finally
                {
                    innerCancellationToken?.Dispose();
                    consolidatedToken?.Dispose();
                }
            }

            if (lastException is not null
                && !fromToken.IsCancellationRequested)
                throw lastException is TaskCanceledException ? 
                    new TimeoutException("The operation has timed out with inner exception!", lastException) :
                    lastException;

            throw new TimeoutException("The operation has timed out!");
        }

        internal static Guid RegisterCancelToken(this IPlugin pluginInstance, CancellationToken token)
        {
            Guid guid = Guid.CreateVersion7();
            token.Register(() =>
            {
                pluginInstance.CancelAsync(in guid);
            });

            return guid;
        }

        internal static async Task InitPluginComAsync<T>(this T initializableInterface, IPlugin pluginInstance, CancellationToken token)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(pluginInstance, nameof(pluginInstance));

            IInitializableTask initiableTask = GetInitializableTask(initializableInterface);

            initiableTask.InitAsync(pluginInstance.RegisterCancelToken(token), out nint asyncResult);
            Task<int> initTask   = asyncResult.AsTask<int>();
            int       returnCode = await initTask;

            if (initTask.IsCompletedSuccessfully)
            {
                return;
            }

            if (returnCode == 69420)
            {
                throw new NotImplementedException("InitAsync isn't overriden!");
            }

            throw new COMException($"Initialization for {nameof(T)} has failed with return code: {returnCode}", initTask.Exception);
        }

        private static IInitializableTask GetInitializableTask<T>(T instance)
            where T : class
        {
            Guid iInitGuid = new Guid(ComInterfaceId.ExInitializable);
            IInitializableTask? task = instance.CastComInterfaceAs<T, IInitializableTask>(in iInitGuid);
            if (task == null)
            {
                throw new InvalidComObjectException($"Interface cannot be marshalled! Guid: {iInitGuid}");
            }

            return task;
        }
    }
}
