using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		/// <summary>
		/// Executes all entries from the source and passes their result to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="TWrite">The input type of the channel.</typeparam>
		/// <typeparam name="TRead">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(this Channel<TWrite, TRead> target,
			IEnumerable<Func<TWrite>> source, bool deferredExecution = false, CancellationToken cancellationToken = default)
		{
			if (target is null) throw new ArgumentNullException(nameof(target));
			if (source is null) throw new ArgumentNullException(nameof(source));
			Contract.EndContractBlock();

			target.Writer.WriteAllAsync(source, true, deferredExecution, cancellationToken).ConfigureAwait(false);
			return target.Reader;
		}

		/// <summary>
		/// Executes all entries from the source and passes their result to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="TWrite">The input type of the channel.</typeparam>
		/// <typeparam name="TRead">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(this Channel<TWrite, TRead> target,
			IEnumerable<Func<TWrite>> source, CancellationToken cancellationToken)
			=> SourceAsync(target, source, false, cancellationToken);

		/// <summary>
		/// Writes all entries from the source to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="TWrite">The input type of the channel.</typeparam>
		/// <typeparam name="TRead">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(this Channel<TWrite, TRead> target,
			IEnumerable<ValueTask<TWrite>> source, bool deferredExecution = false, CancellationToken cancellationToken = default)
		{
			if (target is null) throw new ArgumentNullException(nameof(target));
			if (source is null) throw new ArgumentNullException(nameof(source));
			Contract.EndContractBlock();

			target.Writer.WriteAllAsync(source, true, deferredExecution, cancellationToken).ConfigureAwait(false);
			return target.Reader;
		}

		/// <summary>
		/// Writes all entries from the source to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="TWrite">The input type of the channel.</typeparam>
		/// <typeparam name="TRead">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(this Channel<TWrite, TRead> target,
			IEnumerable<ValueTask<TWrite>> source, CancellationToken cancellationToken)
			=> SourceAsync(target, source, false, cancellationToken);

		/// <summary>
		/// Writes all entries from the source to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="TWrite">The input type of the channel.</typeparam>
		/// <typeparam name="TRead">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(this Channel<TWrite, TRead> target,
			IEnumerable<Task<TWrite>> source, bool deferredExecution = false, CancellationToken cancellationToken = default)
		{
			if (target is null) throw new ArgumentNullException(nameof(target));
			if (source is null) throw new ArgumentNullException(nameof(source));
			Contract.EndContractBlock();

			target.Writer.WriteAllAsync(source, true, deferredExecution, cancellationToken).ConfigureAwait(false);
			return target.Reader;
		}

		/// <summary>
		/// Writes all entries from the source to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="TWrite">The input type of the channel.</typeparam>
		/// <typeparam name="TRead">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(this Channel<TWrite, TRead> target,
			IEnumerable<Task<TWrite>> source, CancellationToken cancellationToken)
			=> SourceAsync(target, source, false, cancellationToken);

		/// <summary>
		/// Writes all entries from the source to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="TWrite">The input type of the channel.</typeparam>
		/// <typeparam name="TRead">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The source data to use.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<TRead> Source<TWrite, TRead>(this Channel<TWrite, TRead> target,
			IEnumerable<TWrite> source, bool deferredExecution = false, CancellationToken cancellationToken = default)
		{
			if (target is null) throw new ArgumentNullException(nameof(target));
			if (source is null) throw new ArgumentNullException(nameof(source));
			Contract.EndContractBlock();

			target.Writer.WriteAll(source, true, deferredExecution, cancellationToken).ConfigureAwait(false);
			return target.Reader;
		}

		/// <summary>
		/// Writes all entries from the source to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="TWrite">The input type of the channel.</typeparam>
		/// <typeparam name="TRead">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The source data to use.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<TRead> Source<TWrite, TRead>(this Channel<TWrite, TRead> target,
			IEnumerable<TWrite> source, CancellationToken cancellationToken)
			=> Source(target, source, false, cancellationToken);

		/// <summary>
		/// Executes all entries from the source and passes their result to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="TWrite">The input type of the channel.</typeparam>
		/// <typeparam name="TRead">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(this Channel<TWrite, TRead> target,
			int maxConcurrency, IEnumerable<Func<TWrite>> source, CancellationToken cancellationToken = default)
		{
			if (target is null) throw new ArgumentNullException(nameof(target));
			Contract.EndContractBlock();

			if (maxConcurrency == 1)
				return SourceAsync(target, source, true, cancellationToken);

			target.Writer.WriteAllConcurrentlyAsync(maxConcurrency, source, true, cancellationToken).ConfigureAwait(false);
			return target.Reader;
		}

		/// <summary>
		/// Writes all entries from the source to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="TWrite">The input type of the channel.</typeparam>
		/// <typeparam name="TRead">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(this Channel<TWrite, TRead> target,
			int maxConcurrency, IEnumerable<ValueTask<TWrite>> source, CancellationToken cancellationToken = default)
		{
			if (target is null) throw new ArgumentNullException(nameof(target));
			Contract.EndContractBlock();

			if (maxConcurrency == 1)
				return SourceAsync(target, source, true, cancellationToken);

			target.Writer
				.WriteAllConcurrentlyAsync(maxConcurrency, source, true, cancellationToken)
				.ConfigureAwait(false);

			return target.Reader;
		}

		/// <summary>
		/// Writes all entries from the source to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="TWrite">The input type of the channel.</typeparam>
		/// <typeparam name="TRead">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(this Channel<TWrite, TRead> target,
			int maxConcurrency, IEnumerable<Task<TWrite>> source, CancellationToken cancellationToken = default)
		{
			if (target is null) throw new ArgumentNullException(nameof(target));
			Contract.EndContractBlock();

			if (maxConcurrency == 1)
				return target.SourceAsync(source, true, cancellationToken);

			target.Writer
				.WriteAllConcurrentlyAsync(maxConcurrency, source, true, cancellationToken)
				.ConfigureAwait(false);

			return target.Reader;
		}

		/// <summary>
		/// Writes all entries from the source to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="T">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The source data to use.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<T> Source<T>(this Channel<string, T> target,
			TextReader source, bool deferredExecution = false, CancellationToken cancellationToken = default)
		{
			if (target is null) throw new ArgumentNullException(nameof(target));
			Contract.EndContractBlock();

			target.Writer
				.WriteAllLines(source, true, deferredExecution, cancellationToken)
				.ConfigureAwait(false);

			return target.Reader;
		}

		/// <summary>
		/// Writes all entries from the source to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="T">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The source data to use.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<T> Source<T>(this Channel<string, T> target,
			TextReader source, CancellationToken cancellationToken)
			=> Source(target, source, false, cancellationToken);

#if NETSTANDARD2_1
		/// <summary>
		/// Executes all entries from the source and passes their result to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="TWrite">The input type of the channel.</typeparam>
		/// <typeparam name="TRead">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<TRead> Source<TWrite, TRead>(this Channel<TWrite, TRead> target,
			IAsyncEnumerable<TWrite> source, bool deferredExecution = false, CancellationToken cancellationToken = default)
		{
			if (target is null) throw new ArgumentNullException(nameof(target));
			Contract.EndContractBlock();

			target.Writer
				.WriteAllAsync(source, true, deferredExecution, cancellationToken)
				.ConfigureAwait(false);

			return target.Reader;
		}

		/// <summary>
		/// Executes all entries from the source and passes their result to the channel.  Calls complete when finished.
		/// </summary>
		/// <typeparam name="TWrite">The input type of the channel.</typeparam>
		/// <typeparam name="TRead">The output type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The channel reader.</returns>
		public static ChannelReader<TRead> Source<TWrite, TRead>(this Channel<TWrite, TRead> target,
			IAsyncEnumerable<TWrite> source, CancellationToken cancellationToken)
			=> Source(target, source, false, cancellationToken);
#endif
	}
}
