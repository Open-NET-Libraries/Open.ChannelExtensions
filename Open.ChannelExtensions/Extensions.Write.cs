using System;
using System.Collections.Generic;
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
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is emtpy).</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task which completes when all the data has been written to the channel writer.</returns>
		public static async ValueTask WriteAllAsync<T>(this ChannelWriter<T> target,
			IEnumerable<ValueTask<T>> source, bool complete = false, CancellationToken cancellationToken = default)
		{
            cancellationToken.ThrowIfCancellationRequested();
            var shouldWait = target.WaitToWriteAsync();
            if (shouldWait.IsCompletedSuccessfully ? !shouldWait.Result : !await shouldWait)
                throw new ChannelClosedException("The target channel was closed before writing could begin.");
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var e in source)
			{
				var value = e.IsCompletedSuccessfully ? e.Result : await e;
				var test = target.WriteAsync(value, cancellationToken);
				if (!test.IsCompletedSuccessfully) await test;
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (complete)
				target.Complete();
		}

		/// <summary>
		/// Asynchronously writes all entries from the source to the channel.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is emtpy).</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task which completes when all the data has been written to the channel writer.</returns>
		public static ValueTask WriteAllAsync<T>(this ChannelWriter<T> target,
			IEnumerable<Task<T>> source, bool complete = false, CancellationToken cancellationToken = default)
			=> WriteAllAsync(target, source.Select(e => new ValueTask<T>(e)), complete, cancellationToken);

        /// <summary>
        /// Asynchronously executes all entries and writes their results to the channel.
        /// </summary>
        /// <typeparam name="T">The input type of the channel.</typeparam>
        /// <param name="target">The channel to write to.</param>
        /// <param name="source">The asynchronous source data to use.</param>
        /// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is emtpy).</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>A task which completes when all the data has been written to the channel writer.</returns>
        public static ValueTask WriteAllAsync<T>(this ChannelWriter<T> target,
            IEnumerable<Func<T>> source, bool complete = false, CancellationToken cancellationToken = default)
            => WriteAllAsync(target, source.Select(e => new ValueTask<T>(e())), complete, cancellationToken);

        /// <summary>
        /// Asynchronously writes all entries from the source to the channel.
        /// </summary>
        /// <typeparam name="T">The input type of the channel.</typeparam>
        /// <param name="target">The channel to write to.</param>
        /// <param name="source">The source data to use.</param>
        /// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is emtpy).</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>A task which completes when all the data has been written to the channel writer.</returns>
        public static ValueTask WriteAll<T>(this ChannelWriter<T> target,
			IEnumerable<T> source, bool complete = false, CancellationToken cancellationToken = default)
			=> WriteAllAsync(target, source.Select(e => new ValueTask<T>(e)), complete, cancellationToken);

        /// <summary>
        /// Executes all entries from the source and passes their result to the channel.  Calls complete when finished.
        /// </summary>
        /// <typeparam name="T">The input type of the channel.</typeparam>
        /// <param name="target">The channel to write to.</param>
        /// <param name="source">The asynchronous source data to use.</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>The channel reader.</returns>
        public static ChannelReader<T> SourceAsync<T>(this Channel<T> target,
            IEnumerable<Func<T>> source, CancellationToken cancellationToken = default)
        {
            target.Writer.WriteAllAsync(source, true, cancellationToken).ConfigureAwait(false);
            return target.Reader;
        }

        /// <summary>
        /// Writes all entries from the source to the channel.  Calls complete when finished.
        /// </summary>
        /// <typeparam name="T">The input type of the channel.</typeparam>
        /// <param name="target">The channel to write to.</param>
        /// <param name="source">The asynchronous source data to use.</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>The channel reader.</returns>
        public static ChannelReader<T> SourceAsync<T>(this Channel<T> target,
			IEnumerable<ValueTask<T>> source, CancellationToken cancellationToken = default)
		{
			target.Writer.WriteAllAsync(source, true, cancellationToken).ConfigureAwait(false);
			return target.Reader;
		}

        /// <summary>
        /// Writes all entries from the source to the channel.  Calls complete when finished.
        /// </summary>
        /// <typeparam name="T">The input type of the channel.</typeparam>
        /// <param name="target">The channel to write to.</param>
        /// <param name="source">The asynchronous source data to use.</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>The channel reader.</returns>
        public static ChannelReader<T> SourceAsync<T>(this Channel<T> target,
			IEnumerable<Task<T>> source, CancellationToken cancellationToken = default)
		{
			target.Writer.WriteAllAsync(source, true, cancellationToken).ConfigureAwait(false);
			return target.Reader;
		}

        /// <summary>
        /// Writes all entries from the source to the channel.  Calls complete when finished.
        /// </summary>
        /// <typeparam name="T">The input type of the channel.</typeparam>
        /// <param name="target">The channel to write to.</param>
        /// <param name="source">The source data to use.</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>The channel reader.</returns>
        public static ChannelReader<T> Source<T>(this Channel<T> target,
			IEnumerable<T> source, CancellationToken cancellationToken = default)
		{
			target.Writer.WriteAll(source, true, cancellationToken).ConfigureAwait(false);
			return target.Reader;
		}
	}
}
