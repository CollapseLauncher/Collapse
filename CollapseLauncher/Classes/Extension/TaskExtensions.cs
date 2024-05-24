﻿using Hi3Helper;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Extension
{
    internal delegate ValueTask<TResult?> ActionTimeoutValueTaskCallback<TResult>(CancellationToken token);
    internal delegate void ActionOnTimeOutRetry(int retryAttemptCount, int retryAttemptTotal, int timeOutSecond, int timeOutStep);
    internal static class TaskExtensions
    {
        internal const int DefaultTimeoutSec = 10;
        internal const int DefaultRetryAttempt = 5;

        internal static async ValueTask<TResult?> WaitForRetryAsync<TResult>(this ActionTimeoutValueTaskCallback<TResult?> funcCallback, int? timeout = null,
            int? timeoutStep = null, int? retryAttempt = null, ActionOnTimeOutRetry? actionOnRetry = null, CancellationToken fromToken = default)
            => await WaitForRetryAsync(() => funcCallback, timeout, timeoutStep, retryAttempt, actionOnRetry, fromToken);

        internal static async ValueTask<TResult?> WaitForRetryAsync<TResult>(Func<ActionTimeoutValueTaskCallback<TResult?>> funcCallback, int? timeout = null,
            int? timeoutStep = null, int? retryAttempt = null, ActionOnTimeOutRetry? actionOnRetry = null, CancellationToken fromToken = default)
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

                    ActionTimeoutValueTaskCallback<TResult?> delegateCallback = funcCallback();
                    return await delegateCallback(consolidatedToken.Token);
                }
                catch (OperationCanceledException) when (fromToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    lastException = ex;
                    actionOnRetry?.Invoke(retryAttemptCurrent, retryAttempt ?? 0, timeout ?? 0, timeoutStep ?? 0);

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
                    continue;
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
                    new TimeoutException($"The operation has timed out with inner exception!", lastException) :
                    lastException;

            throw new TimeoutException($"The operation has timed out!");
        }

        internal static async
            ValueTask<T?>
            TimeoutAfter<T>(this Task<T?> task, CancellationToken token = default, int timeout = DefaultTimeoutSec)
        {
            Task<T?> completedTask = await Task.WhenAny(task, ThrowExceptionAfterTimeout<T>(timeout, task, token));
            return await completedTask;
        }

        private static async Task<T?> ThrowExceptionAfterTimeout<T>(int? timeout, Task mainTask, CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
                throw new OperationCanceledException();

            await Task.Delay(TimeSpan.FromSeconds(timeout ?? DefaultTimeoutSec), token);
            if (!(mainTask.IsCompleted ||
                mainTask.IsCompletedSuccessfully ||
                mainTask.IsCanceled || mainTask.IsFaulted || mainTask.Exception != null))
                throw new TimeoutException($"The operation for task has timed out!");

            return default;
        }
    }
}
