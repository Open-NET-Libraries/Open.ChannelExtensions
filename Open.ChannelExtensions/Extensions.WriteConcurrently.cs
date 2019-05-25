using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
    public static partial class Extensions
    {
        /// <summary>
        /// Asynchronously writes all entries from the source to the channel.
        /// </summary>
        /// <typeparam name="T">The input type of the channel.</typeparam>
        /// <param name="target">The channel to write to.</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
        /// <param name="source">The asynchronous source data to use.</param>
        /// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is emtpy).</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>A task which completes when all the data has been written to the channel writer.</returns>
        public static Task WriteAllConcurrentlyAsync<T>(this ChannelWriter<T> target,
            int maxConcurrency, IEnumerable<ValueTask<T>> source, bool complete = false, CancellationToken cancellationToken = default)
        {
            if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, "Must be at least 1.");
            Contract.EndContractBlock();

            if (maxConcurrency == 1)
                return target.WriteAllAsync(source, complete, cancellationToken).AsTask();

            var shouldWait = target.WaitToWriteAsync();
            if (shouldWait.IsCompletedSuccessfully && !shouldWait.Result)
                throw new ChannelClosedException("The target channel was closed before writing could begin.");

            var enumerator = source.GetEnumerator();
            var writers = new Task[maxConcurrency];
            for (var w = 0; w < maxConcurrency; w++)
                writers[w] = WriteAllAsyncCore();

            return Task
                .WhenAll(writers)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        return t;

                    if (cancellationToken.IsCancellationRequested)
                        return Task.FromCanceled(cancellationToken);

                    if (complete)
                        target.Complete();

                    return t;
                })
                .Unwrap();

            async Task WriteAllAsyncCore()
            {
                var next = new ValueTask();
                while (!cancellationToken.IsCancellationRequested
                    && TryMoveNextSynchronized(enumerator, out var e))
                {
                    var value = e.IsCompletedSuccessfully ? e.Result : await e.ConfigureAwait(false);
                    if (!next.IsCompletedSuccessfully) await next.ConfigureAwait(false);
                    if (!target.TryWrite(value)) next = target.WriteAsync(value, cancellationToken);
                }
                if (!next.IsCompletedSuccessfully) await next.ConfigureAwait(false);
            }
        }

        static bool TryMoveNextSynchronized<T>(IEnumerator<T> source, out T value)
        {
            lock (source)
            {
                if (source.MoveNext())
                {
                    value = source.Current;
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Asynchronously writes all entries from the source to the channel.
        /// </summary>
        /// <typeparam name="T">The input type of the channel.</typeparam>
        /// <param name="target">The channel to write to.</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
        /// <param name="source">The asynchronous source data to use.</param>
        /// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is emtpy).</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>A task which completes when all the data has been written to the channel writer.</returns>
        public static Task WriteAllConcurrentlyAsync<T>(this ChannelWriter<T> target,
            int maxConcurrency, IEnumerable<Task<T>> source, bool complete = false, CancellationToken cancellationToken = default)
            => WriteAllConcurrentlyAsync(target, maxConcurrency, source.Select(e => new ValueTask<T>(e)), complete, cancellationToken);

        /// <summary>
        /// Asynchronously executes all entries and writes their results to the channel.
        /// </summary>
        /// <typeparam name="T">The input type of the channel.</typeparam>
        /// <param name="target">The channel to write to.</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
        /// <param name="source">The asynchronous source data to use.</param>
        /// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is emtpy).</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>A task which completes when all the data has been written to the channel writer.</returns>
        public static Task WriteAllConcurrentlyAsync<T>(this ChannelWriter<T> target,
            int maxConcurrency, IEnumerable<Func<T>> source, bool complete = false, CancellationToken cancellationToken = default)
            => WriteAllConcurrentlyAsync(target, maxConcurrency, source.Select(e => new ValueTask<T>(e())), complete, cancellationToken);

        /// <summary>
        /// Asynchronously writes all entries from the source to the channel.
        /// </summary>
        /// <typeparam name="T">The input type of the channel.</typeparam>
        /// <param name="target">The channel to write to.</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
        /// <param name="source">The source data to use.</param>
        /// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is emtpy).</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>A task which completes when all the data has been written to the channel writer.</returns>
        public static Task WriteAllConcurrently<T>(this ChannelWriter<T> target,
            int maxConcurrency, IEnumerable<T> source, bool complete = false, CancellationToken cancellationToken = default)
            => WriteAllConcurrentlyAsync(target, maxConcurrency, source.Select(e => new ValueTask<T>(e)), complete, cancellationToken);

    }
}
