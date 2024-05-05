using Hi3Helper;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Extension
{
    internal delegate void ActionOnTimeOutRetry(int retryAttemptCount, int retryAttemptTotal, int timeOutSecond, int timeOutStep);
    internal static class TaskExtensions
    {
        internal const int DefaultTimeoutSec = 10;
        internal const int DefaultRetryAttempt = 5;

        internal static async Task<T?> RetryTimeoutAfter<T>(Func<Task<T?>> taskFunction, int? timeout = null, int? timeoutStep = null, int? retryAttempt = null, ActionOnTimeOutRetry? actionOnRetry = null, CancellationToken token = default)
        {
            timeout ??= DefaultTimeoutSec;
            timeoutStep ??= 0;

            retryAttempt ??= DefaultRetryAttempt;

            int retryAttemptCurrent = 1;
            int lastTaskID = 0;
            Exception? lastException = null;

            while (retryAttemptCurrent < retryAttempt)
            {
                try
                {
                    Task<T?> taskDelegated = taskFunction();
                    lastTaskID = taskDelegated.Id;
                    lastException = null;

                    Task<T?> completedTask = await Task.WhenAny(taskDelegated, ThrowExceptionAfterTimeout<T>(timeout, taskDelegated, token));
                    if (completedTask == taskDelegated)
                        return await taskDelegated;
                }
                catch (TaskCanceledException) { throw; }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (actionOnRetry != null)
                        actionOnRetry(retryAttemptCurrent, retryAttempt ?? 0, timeout ?? 0, timeoutStep ?? 0);

                    if (lastException is TimeoutException)
                    {
                        string msg = $"The operation for task ID: {lastTaskID} has timed out! Retrying attempt left: {retryAttemptCurrent}/{retryAttempt}";
                        Logger.LogWriteLine(msg, LogType.Warning, true);
                    }
                    else
                    {
                        string msg = $"The operation for task ID: {lastTaskID} has thrown an exception! Retrying attempt left: {retryAttemptCurrent}/{retryAttempt}\r\n{ex}";
                        Logger.LogWriteLine(msg, LogType.Error, true);
                    }

                    retryAttemptCurrent++;
                    timeout += timeoutStep;
                    continue;
                }
            }

            if (lastException != null) throw lastException;
            throw new TimeoutException($"The operation for task ID: {lastTaskID} has timed out!");
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
