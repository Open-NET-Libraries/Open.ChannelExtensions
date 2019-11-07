using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	/// <summary>
	/// Extensions for operating with System.Threading.Channels.
	/// </summary>
	public static partial class Extensions
	{
		internal static Channel<T> CreateChannel<T>(int capacity = -1, bool singleReader = false, bool singleWriter = true)
			=> capacity > 0
				? Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
				{
					SingleWriter = singleWriter,
					SingleReader = singleReader,
					AllowSynchronousContinuations = true,
					FullMode = BoundedChannelFullMode.Wait
				})
				: Channel.CreateUnbounded<T>(new UnboundedChannelOptions
				{
					SingleWriter = singleWriter,
					SingleReader = singleReader,
					AllowSynchronousContinuations = true
				});

		static async ValueTask ThrowChannelClosedExceptionIfFalse(ValueTask<bool> write)
		{
			if (!await write.ConfigureAwait(false))
				throw new ChannelClosedException();
		}

		static async ValueTask ThrowChannelClosedExceptionIfFalse(ValueTask<bool> write, string message)
		{
			if (!await write.ConfigureAwait(false))
				throw new ChannelClosedException(message);
		}

		/// <summary>
		/// Waits for opportunity to write to a channel and throws a ChannelClosedException if the channel is closed.  
		/// </summary>
		/// <typeparam name="T">The type being written to the channel</typeparam>
		/// <param name="writer">The channel writer.</param>
		/// <param name="ifClosedMessage">The message to include with the ChannelClosedException if thrown.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		public static ValueTask WaitToWriteAndThrowIfClosedAsync<T>(this ChannelWriter<T> writer, string ifClosedMessage, CancellationToken cancellationToken = default)
		{
			if (cancellationToken.IsCancellationRequested)
				return new ValueTask(Task.FromCanceled(cancellationToken));

			var waitForWrite = writer.WaitToWriteAsync(cancellationToken);
			if (!waitForWrite.IsCompletedSuccessfully)
				return ThrowChannelClosedExceptionIfFalse(waitForWrite, ifClosedMessage);

			if (waitForWrite.Result)
				return new ValueTask();

			throw new ChannelClosedException(ifClosedMessage);
		}

		/// <summary>
		/// Waits for opportunity to write to a channel and throws a ChannelClosedException if the channel is closed.  
		/// </summary>
		/// <typeparam name="T">The type being written to the channel</typeparam>
		/// <param name="writer">The channel writer.</param>
		/// <param name="ifClosedMessage">The message to include with the ChannelClosedException if thrown.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before continuing.</param>
		public static async ValueTask WaitToWriteAndThrowIfClosedAsync<T>(this ChannelWriter<T> writer, string ifClosedMessage, bool deferredExecution, CancellationToken cancellationToken = default)
		{
			var wait = writer.WaitToWriteAndThrowIfClosedAsync(ifClosedMessage, cancellationToken);

			if (deferredExecution)
			{
				await Task.Yield();
				if (wait.IsCompletedSuccessfully)
					wait = writer.WaitToWriteAndThrowIfClosedAsync(ifClosedMessage, cancellationToken);
			}

			await wait.ConfigureAwait(false);
		}

		/// <summary>
		/// Waits for opportunity to write to a channel and throws a ChannelClosedException if the channel is closed.  
		/// </summary>
		/// <typeparam name="T">The type being written to the channel</typeparam>
		/// <param name="writer">The channel writer.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		public static ValueTask WaitToWriteAndThrowIfClosedAsync<T>(this ChannelWriter<T> writer, CancellationToken cancellationToken = default)
		{
			if (cancellationToken.IsCancellationRequested)
				return new ValueTask(Task.FromCanceled(cancellationToken));

			var waitForWrite = writer.WaitToWriteAsync(cancellationToken);
			if (!waitForWrite.IsCompletedSuccessfully)
				return ThrowChannelClosedExceptionIfFalse(waitForWrite);

			if (waitForWrite.Result)
				return new ValueTask();

			throw new ChannelClosedException();
		}

		/// <summary>
		/// Waits for opportunity to write to a channel and throws a ChannelClosedException if the channel is closed.  
		/// </summary>
		/// <typeparam name="T">The type being written to the channel</typeparam>
		/// <param name="writer">The channel writer.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before continuing.</param>
		public static async ValueTask WaitToWriteAndThrowIfClosedAsync<T>(this ChannelWriter<T> writer, bool deferredExecution, CancellationToken cancellationToken = default)
		{
			var wait = writer.WaitToWriteAndThrowIfClosedAsync(cancellationToken);

			if (deferredExecution)
			{
				await Task.Yield();
				if (wait.IsCompletedSuccessfully)
					wait = writer.WaitToWriteAndThrowIfClosedAsync(cancellationToken);
			}

			await wait.ConfigureAwait(false);
		}

		/// <summary>
		/// Calls complete on the writer and returns the completion from the reader.
		/// </summary>
		/// <typeparam name="TWrite">The type being received by the writer.</typeparam>
		/// <typeparam name="TRead">The type being read from the reader.</typeparam>
		/// <param name="channel">The channel to complete asynchronously.</param>
		/// <param name="exception">The optional exception to include with completion.</param>
		/// <returns>The reader's completion task.</returns>
		public static Task CompleteAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel, Exception? exception = null)
		{
			channel.Writer.Complete(exception);
			return channel.Reader.Completion;
		}

		/// <summary>
		/// Writes all lines from the source to a channel and calls complete when finished.
		/// </summary>
		/// <param name="source">The source data to use.</param>
		/// <param name="capacity">The optional bounded capacity of the channel. Default is unbound.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
		/// <returns>The channel reader containing the results.</returns>
		public static ChannelReader<string> ToChannel(this TextReader source,
			int capacity = -1, bool singleReader = false,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
			=> CreateChannel<string>(capacity, singleReader)
				.Source(source, cancellationToken, deferredExecution);

		/// <summary>
		/// Writes all entries from the source to a channel and calls complete when finished.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="source">The source data to use.</param>
		/// <param name="capacity">The optional bounded capacity of the channel. Default is unbound.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
		/// <returns>The channel reader containing the results.</returns>
		public static ChannelReader<T> ToChannel<T>(this IEnumerable<T> source,
			int capacity = -1, bool singleReader = false,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
			=> CreateChannel<T>(capacity, singleReader)
				.Source(source, cancellationToken, deferredExecution);

#if NETSTANDARD2_1
		/// <summary>
		/// Writes all entries from the source to a channel and calls complete when finished.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="source">The source data to use.</param>
		/// <param name="capacity">The optional bounded capacity of the channel. Default is unbound.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
		/// <returns>The channel reader containing the results.</returns>
		public static ChannelReader<T> ToChannel<T>(this IAsyncEnumerable<T> source,
			int capacity = -1, bool singleReader = false,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
			=> CreateChannel<T>(capacity, singleReader)
				.Source(source, cancellationToken, deferredExecution);
#endif

		/// <summary>
		/// Asynchronously executes all entries and writes their results to a channel.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="capacity">The optional bounded capacity of the channel. Default is unbound.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader containing the results.</returns>
		public static ChannelReader<T> ToChannelAsync<T>(this IEnumerable<Func<T>> source,
			int capacity = -1, bool singleReader = false, int maxConcurrency = 1,
			CancellationToken cancellationToken = default)
			=> CreateChannel<T>(capacity, singleReader)
				.SourceAsync(maxConcurrency, source, cancellationToken);

		/// <summary>
		/// Writes all entries from the source to a channel and calls complete when finished.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="capacity">The optional bounded capacity of the channel. Default is unbound.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader containing the results.</returns>
		public static ChannelReader<T> ToChannelAsync<T>(this IEnumerable<ValueTask<T>> source,
			int capacity = -1, bool singleReader = false, int maxConcurrency = 1,
			CancellationToken cancellationToken = default)
			=> CreateChannel<T>(capacity, singleReader)
				.SourceAsync(maxConcurrency, source, cancellationToken);

		/// <summary>
		/// Writes all entries from the source to a channel and calls complete when finished.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="capacity">The optional bounded capacity of the channel. Default is unbound.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader containing the results.</returns>
		public static ChannelReader<T> ToChannelAsync<T>(this IEnumerable<Task<T>> source,
			int capacity = -1, bool singleReader = false, int maxConcurrency = 1,
			CancellationToken cancellationToken = default)
			=> CreateChannel<T>(capacity, singleReader)
				.SourceAsync(maxConcurrency, source, cancellationToken);

	}
}
