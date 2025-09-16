using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable UnusedMember.Global
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.Extension
{
    public delegate ConfiguredTaskAwaitable<TResult?> ActionTimeoutTaskAwaitableCallback<TResult>(CancellationToken token);

    internal static partial class TaskExtensions
    {
        internal static async Task<TResult?>
            WaitForRetryAsync<TResult>(this ActionTimeoutTaskAwaitableCallback<TResult?> funcCallback,
                                       int?                                              timeout         = null,
                                       int?                                              timeoutStep     = null,
                                       int?                                              retryAttempt    = null,
                                       ActionOnTimeOutRetry?                             actionOnRetry   = null,
                                       Action<TResult?>?                                 actionOnSuccess = null,
                                       CancellationToken                                 fromToken       = default)
        {
            TResult? result = await funcCallback.Invoke(fromToken);
            actionOnSuccess?.Invoke(result);

            return result;
        }
    }
}
